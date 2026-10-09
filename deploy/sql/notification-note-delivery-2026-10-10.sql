-- ============================================================
-- 쪽지가 **기기에 닿았나** · **메일로 돌렸나** — scom.notes 칸 둘 (2026-10-10)
-- ============================================================
--
-- 코드: microservices/NotificationServer/Entities/Note.cs
--       microservices/NotificationServer/Services/NoteFallbackMailer.cs
--       web/src/Shell/JSini.Web.Shell/wwwroot/push-sw.js
--
--   psql -h … -U funeralv2 -d jsiniportal -f notification-note-delivery-2026-10-10.sql
--
-- ── 왜 push_sent 로는 모자라나 ───────────────────────────────
--
-- 그 칸은 「우리가 푸시 서비스(FCM 등)에 넘겼다」까지다. 넘긴 것이 도착한다는
-- 보장이 없다 — 기기가 꺼져 있으면 푸시 서비스가 **수명(TTL, 기본 6시간)까지
-- 들고 있다가 조용히 버린다**(docs/push-delivery.md). 그래서 보낸 사람 화면에는
-- 「앱 알림 1대」라고 적혀 있는데 받는 사람은 영영 모르는 일이 생긴다.
--
--   delivered_at       기기의 서비스워커가 **받아서 되알려 준** 때.
--                      그 보고는 토큰 없이 오므로(BFF — 서비스워커에는 로그인
--                      토큰이 없다) 게이트웨이에서 그 길 하나만 익명이고,
--                      신원 대신 **쪽지 아이디(GUID)** 가 열쇠다.
--
--   fallback_email_at  푸시가 안 닿아 **대신 보낸 메일**이 나간 때.
--                      email_sent 와 칸을 나눈다 — 그쪽은 보낼 때 받는 사람이
--                      「쪽지 메일받기」를 켜 두어 함께 나간 것이고, 이쪽은
--                      나중에 배치가 대신 보낸 것이다. 한 칸에 담으면 전환
--                      메일이 나간 뒤에도 배치가 그 줄을 다시 집어 **5분마다
--                      같은 메일**을 보낸다.
--
-- ── 색인이 부분 색인인 까닭 ──────────────────────────────────
--
-- 전환 메일 배치가 5분마다 「아직 안 닿고 안 읽고 안 보낸」 쪽지를 훑는다.
-- 조건이 전부 NULL 비교라 보통 색인으로는 안 걸린다. 부분 색인으로 두면
-- **그 셋이 채워지는 순간 줄이 색인에서 빠져서**, 표가 아무리 커져도 색인은
-- 「아직 처리 안 된 몇 줄」만 든다.
--
-- 멱등하다. 되돌리려면 맨 아래 세 줄.

BEGIN;

ALTER TABLE scom.notes
    ADD COLUMN IF NOT EXISTS delivered_at timestamptz,
    ADD COLUMN IF NOT EXISTS fallback_email_at timestamptz;

COMMENT ON COLUMN scom.notes.delivered_at
    IS '앱 푸시가 기기에 실제로 닿은 때(UTC). 서비스워커가 되알려 준다. push_sent(넘겼다)와 다르다.';

COMMENT ON COLUMN scom.notes.fallback_email_at
    IS '푸시가 안 닿아 대신 보낸 메일이 나간 때(UTC). email_sent(보낼 때 함께 나간 메일)와 다르다.';

CREATE INDEX IF NOT EXISTS "IX_notes_fallback"
    ON scom.notes (sent_at)
    WHERE read_at IS NULL AND delivered_at IS NULL AND fallback_email_at IS NULL;

COMMIT;

-- 확인
--   \d scom.notes
--   SELECT count(*) FILTER (WHERE delivered_at IS NOT NULL)      AS 기기도착,
--          count(*) FILTER (WHERE fallback_email_at IS NOT NULL) AS 메일전환,
--          count(*)                                              AS 전체
--     FROM scom.notes;
--
-- 되돌리기
--   DROP INDEX IF EXISTS scom."IX_notes_fallback";
--   ALTER TABLE scom.notes DROP COLUMN IF EXISTS delivered_at;
--   ALTER TABLE scom.notes DROP COLUMN IF EXISTS fallback_email_at;
