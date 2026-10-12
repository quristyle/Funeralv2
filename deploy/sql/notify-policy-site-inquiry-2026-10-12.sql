-- ============================================================
-- 알림관리 — 「소개 사이트 문의 접수」의 받는 사람을 **표로 옮긴다** (2026-10-12)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/NotifyPolicyPage.razor
-- 코드: microservices/SiteServer/Services/InquiryPushNotifier.cs
--       microservices/SiteServer/Services/InquiryMailNotifier.cs
--       microservices/NotificationServer/Entities/NotificationEventRow.cs
-- 문서: docs/notify-policy.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f notify-policy-site-inquiry-2026-10-12.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS · WHERE 로 좁힌 UPDATE).
-- notify-policy-2026-10-11.sql · …-site-inquiry-push-2026-10-11.sql 뒤에 돌린다.
--
-- ── 무엇이 화면 밖에 남아 있었나 ────────────────────────────
--
-- 문의 접수 알림의 「누가 받나」가 **두 자리**에 적혀 있었다 —
--
--   ① 이 표(`notification_policies`)의 SITE_INQUIRY 줄
--   ② SiteServer 설정 파일의 `InquiryMail:ToRole`(= SYSTEM_ADMINISTRATOR)
--
-- 그런데 정책은 **줄이 하나도 없으면 「제한 없음」**이고, 제한이 없으면 발송
-- 경로가 ②로 되돌아간다(docs/notify-policy.md 의 「조용히 막지 않는다」).
-- 그래서 관리자가 화면에서 체크를 **다 풀어도 알림이 계속 갔다.** 끄려다 못 끄는
-- 쪽이고, 화면만 보면 까닭이 안 보인다.
--
-- 실제로 지금 그 꼴이다 — 이 표의 SITE_INQUIRY 두 줄은 `push_enabled = false`
-- 인데(앱 푸시 칸이 잠겨 있던 때 저장된 값), 앱 푸시는 ②의 역할로 **나가고
-- 있다.** 화면은 「앱 푸시 켠 역할 0」이라고 말한다.
--
-- ── 무엇을 바꾸나 ───────────────────────────────────────────
--
-- 1. SiteServer 가 역할을 안 적어 보낸다(설정 파일의 ToRole 을 비웠다).
--    이제 받는 사람의 정본은 이 표 하나다.
-- 2. 그러면 **「체크 0 개」의 뜻이 이 이벤트에서만 반대가 된다** — 다른
--    이벤트는 「지금 그대로 나간다」이고 여기서는 「아무에게도 안 간다」다.
--    화면이 그 갈림을 말해야 해서 칸을 하나 더한다: `policy_required`.
-- 3. 지금 앱 푸시를 받고 있는 역할을 **표에 적는다** — 설정 파일이 보내던
--    SYSTEM_ADMINISTRATOR 그대로다. 받는 사람이 늘지도 줄지도 않는다.
--
-- **ADMINISTRATOR 에는 앱 푸시를 안 켠다.** 지금 그 역할은 이메일만 받고
-- 있다(설정 파일의 역할이 SYSTEM_ADMINISTRATOR 하나였다). 여기서 켜 주면
-- 관리자가 고른 적 없는 사람에게 알림이 느는 쪽이라, 화면에서 체크 한 번으로
-- 켜도록 둔다.
--
-- 되돌리려면 맨 아래 주석 두 줄.
-- ============================================================

BEGIN;

-- ── 1. 「받는 사람의 정본이 이 표인가」 칸 ──────────────────
--
-- 기본값은 **거짓**이다. 참으로 두면 기존 이벤트 열다섯이 전부 「역할을 안
-- 걸면 안 나간다」가 되어, 이 칸이 생긴 날 포털의 알림이 통째로 멎는다.

ALTER TABLE scom.notification_events
    ADD COLUMN IF NOT EXISTS policy_required boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN scom.notification_events.policy_required IS
    '받는 사람의 정본이 정책 표인가. 참이면 역할을 안 걸었을 때 제한 없음이 아니라 아무에게도 안 간다.';

-- ── 2. 소개 사이트 문의 접수를 그 쪽으로 옮긴다 ─────────────

UPDATE scom.notification_events
   SET policy_required = true,
       description     = '회사 소개 사이트에서 문의가 들어왔을 때 담당자에게. 받는 역할의 정본이 이 화면이라, 역할을 하나도 안 걸면 아무에게도 가지 않는다. 앱 푸시를 누르면 [사이트 문의내역]이 열리고, 이메일은 접수번호와 본문이 담긴 제 틀의 메일이 따로 나간다.',
       updated_at      = now(),
       updated_by      = 'notify-policy-site-inquiry-2026-10-12'
 WHERE id = 'SITE_INQUIRY'
   AND NOT is_deleted
   AND policy_required IS DISTINCT FROM true;

-- ── 3. 지금 푸시를 받고 있는 역할을 표에 적는다 ─────────────
--
-- 설정 파일이 보내던 역할 그대로다(SYSTEM_ADMINISTRATOR). 이 줄이 없으면
-- 설정 파일을 비운 순간 **앱 푸시가 한 통도 안 나간다.**

UPDATE scom.notification_policies
   SET push_enabled = true,
       updated_at   = now(),
       updated_by   = 'notify-policy-site-inquiry-2026-10-12'
 WHERE event_code = 'SITE_INQUIRY'
   AND role_id    = 'SYSTEM_ADMINISTRATOR'
   AND NOT is_deleted
   AND push_enabled IS DISTINCT FROM true;

-- 줄 자체가 없는 설치(정책을 한 줄도 안 넣은 서버)에서는 만들어 준다.
INSERT INTO scom.notification_policies
       (id, event_code, role_id, push_enabled, email_enabled, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, 'SITE_INQUIRY', 'SYSTEM_ADMINISTRATOR', true, true, true,
       now(), 'notify-policy-site-inquiry-2026-10-12', false
 WHERE EXISTS (SELECT 1 FROM scom.roles WHERE id = 'SYSTEM_ADMINISTRATOR' AND NOT is_deleted)
   AND NOT EXISTS (
       SELECT 1 FROM scom.notification_policies
        WHERE event_code = 'SITE_INQUIRY' AND role_id = 'SYSTEM_ADMINISTRATOR');

COMMIT;

-- ── 확인 ────────────────────────────────────────────────────
--
-- SELECT id, policy_required, supports_push, supports_email, email_from_push
--   FROM scom.notification_events WHERE id = 'SITE_INQUIRY';
--
-- SELECT role_id, push_enabled, email_enabled
--   FROM scom.notification_policies WHERE event_code = 'SITE_INQUIRY' ORDER BY role_id;
--
-- ── 되돌리기 ────────────────────────────────────────────────
--
-- UPDATE scom.notification_events SET policy_required = false WHERE id = 'SITE_INQUIRY';
-- UPDATE scom.notification_policies SET push_enabled = false
--  WHERE event_code = 'SITE_INQUIRY' AND role_id = 'SYSTEM_ADMINISTRATOR';
-- (그리고 SiteServer 의 InquiryMail:ToRole 에 SYSTEM_ADMINISTRATOR 를 되적는다)
