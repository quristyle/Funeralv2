-- ============================================================
-- 알림관리 — 「이메일」 체크를 잠가 두었던 이벤트 다섯을 연다 (2026-10-11)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/NotifyPolicyPage.razor
-- 코드: microservices/NotificationServer/Services/PushSender.FanOutEmailAsync
-- 문서: docs/notify-policy.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f notify-policy-email-2026-10-11.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS · WHERE 로 좁힌 UPDATE).
--
-- ── 무엇이 잠겨 있었나 ──────────────────────────────────────
--
-- 알림관리 화면의 역할 표에는 길이 둘이다 — 「앱 푸시」와 「이메일」. 그런데
-- 이벤트 여섯에서 이메일 칸이 **회색으로 잠겨** 있었다:
--
--   DEPLOY · HELPDESK · BIRTHDAY · WEATHER · SUBSCRIPTION · TEST
--
-- 까닭은 화면이 아니라 **발송 경로**에 있었다. 그 이벤트들을 보내는 쪽은
-- 알림 서버의 `/notifications/push` 하나만 부르고 `/emails/send` 는 부르지
-- 않는다. 체크를 열어 두면 켜도 아무 일이 없고, 그것은 「껐는데 간다」의
-- 반대편인 **「켰는데 안 온다」**다. 그래서 `supports_email = false` 로 두어
-- 칸 자체를 잠갔다 — 할 수 없는 일을 할 수 있는 것처럼 보이게 두지 않는다.
--
-- ── 무엇을 바꾸나 ───────────────────────────────────────────
--
-- 발송 경로에 **메일 곁가지**를 냈다. `PushSender` 가 푸시를 보내면서 같은
-- 내용을 메일로도 낸다. 그 곁가지를 타는 이벤트를 표가 정한다 —
-- 새 칸 `email_from_push` 다.
--
-- **둘을 가르는 칸이 왜 필요한가.** 메일이 나가는 길이 두 갈래다:
--
--   (가) 보내는 쪽이 `/emails/send` 를 따로 부른다
--        SIGNUP · AI_TASK · NOTE · HELPDESK_COMMENT · SITE_INQUIRY · REPORT_MAIL
--        → 그 길은 이미 정책을 묻는다. 체크가 벌써 듣는다.
--
--   (나) 푸시만 보낸다 — 메일 경로가 아예 없다
--        DEPLOY · HELPDESK · BIRTHDAY · WEATHER · SUBSCRIPTION
--        → 이번에 `PushSender` 가 함께 낸다. 이 다섯만 email_from_push = true.
--
-- 가르지 않고 전부 켜면 (가)의 이벤트가 **같은 알림을 두 통** 보낸다.
--
-- ── TEST 는 그대로 둔다 ─────────────────────────────────────
--
-- 시험 발송은 `governed = false` 라 정책이 아예 안 걸린다(막으면 시험의 뜻이
-- 없어진다). 곁가지는 정책이 고른 역할로만 나가므로 TEST 에서는 영영 안 돈다 —
-- 칸만 열면 또 「켰는데 안 온다」가 된다.
--
-- ── 기본값이 푸시와 **반대**다 ──────────────────────────────
--
-- 정책 줄이 없는 이벤트를 푸시는 「제한 없음」으로 본다(지금까지와 똑같이
-- 보낸다). 메일 곁가지는 그 반대다 — **「이메일」을 켠 역할이 하나도 없으면
-- 한 통도 안 낸다.** 같은 기본값을 쓰면 이 변경이 올라가는 날 배포·생일·기상
-- 알림이 전 직원 메일함으로 쏟아지고, 그것은 되돌릴 수가 없다.
--
-- 그래서 **이 스크립트는 정책 줄을 하나도 넣지 않는다.** 칸만 열고, 역할을
-- 매는 것은 화면에서 한다.
--
-- 되돌리려면 맨 아래 두 줄.
-- ============================================================

BEGIN;

-- ── 새 칸 ───────────────────────────────────────────────────
--
-- 기본값이 거짓이라, 넣는 것만으로는 아무 알림도 늘지 않는다.

ALTER TABLE scom.notification_events
    ADD COLUMN IF NOT EXISTS email_from_push boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN scom.notification_events.email_from_push IS
    '참이면 PushSender 가 앱 푸시와 같은 내용을 메일로도 낸다. 거짓이면 보내는 쪽이 /emails/send 로 따로 낸다 — 둘 다 참이면 두 통 간다';

-- ── 잠가 두었던 다섯을 연다 ─────────────────────────────────
--
-- **아이디를 짚어 고친다.** `supports_email = false` 전부를 켜면 TEST 까지
-- 걸리고, 그쪽은 정책이 안 걸려 영영 안 나간다.

UPDATE scom.notification_events
   SET supports_email  = true,
       email_from_push = true,
       updated_at      = now(),
       updated_by      = 'notify-policy-email'
 WHERE id IN ('DEPLOY', 'HELPDESK', 'BIRTHDAY', 'WEATHER', 'SUBSCRIPTION')
   AND NOT is_deleted
   AND (supports_email IS DISTINCT FROM true OR email_from_push IS DISTINCT FROM true);

-- ── 설명을 고쳐 적는다 ──────────────────────────────────────
--
-- 「메일은 옛 발송 경로라 HELPDESK_LEGACY 에 있다」가 이제 틀린 말이 되었다.

UPDATE scom.notification_events
   SET description = '새 요청이 올라왔을 때 처리할 사람에게. 이메일을 켜면 같은 내용이 메일로도 간다 — 헬프데스크가 제 관리자 목록으로 따로 보내는 메일(HELPDESK_LEGACY)과 겹칠 수 있다.',
       updated_at  = now(),
       updated_by  = 'notify-policy-email'
 WHERE id = 'HELPDESK' AND NOT is_deleted;

COMMIT;

-- 넣은 결과 확인
SELECT id, name, target_kind,
       supports_push, supports_email, email_from_push, governed, is_active
  FROM scom.notification_events
 WHERE NOT is_deleted
 ORDER BY order_no;

-- 「이메일」을 켠 정책 줄. 이 스크립트 직후에는 0 건이 정상이다.
SELECT event_code, role_id, push_enabled, email_enabled, is_active
  FROM scom.notification_policies
 WHERE email_enabled AND NOT is_deleted
 ORDER BY event_code, role_id;

-- 되돌리기 (칸을 지우면 코드가 못 읽으므로, 서비스를 되돌린 뒤에 한다)
-- UPDATE scom.notification_events SET supports_email = false, email_from_push = false
--  WHERE id IN ('DEPLOY','HELPDESK','BIRTHDAY','WEATHER','SUBSCRIPTION');
-- ALTER TABLE scom.notification_events DROP COLUMN IF EXISTS email_from_push;
