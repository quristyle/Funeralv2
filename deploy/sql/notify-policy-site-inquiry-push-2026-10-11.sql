-- ============================================================
-- 알림관리 — 「소개 사이트 문의 접수」에 앱 푸시를 연다 (2026-10-11)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/NotifyPolicyPage.razor
-- 코드: microservices/SiteServer/Services/InquiryPushNotifier.cs
-- 문서: docs/notify-policy.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f notify-policy-site-inquiry-push-2026-10-11.sql
--
-- 두 번 돌려도 안전하다(WHERE 로 좁힌 UPDATE · NOT EXISTS 로 막은 INSERT).
-- notify-policy-2026-10-11.sql 을 먼저 돌려 표가 있어야 한다.
--
-- ── 무엇이 잠겨 있었나 ──────────────────────────────────────
--
-- `SITE_INQUIRY` 는 `supports_push = false` 였다. 까닭은 화면이 아니라
-- **발송 경로**에 있었다 — SiteServer 는 문의가 들어오면 알림 서버의
-- `/emails/send` 만 불렀고 `/notifications/push` 는 부른 적이 없다. 체크를
-- 열어 두면 켜도 아무 일이 없고, 그것이 「껐는데 간다」의 반대편인
-- **「켰는데 안 온다」**다. 그래서 칸 자체를 잠가 두었다.
--
-- ── 무엇을 바꾸나 ───────────────────────────────────────────
--
-- 발송 경로에 앱 푸시를 냈다(`InquiryPushNotifier`). 담당 역할로
-- `/notifications/push` 를 부르고 `eventCode = SITE_INQUIRY` 를 실어 보내므로,
-- 이제 이 화면의 「앱 푸시」 체크가 실제로 듣는다. 그래서 칸을 연다.
--
-- ── `email_from_push` 는 **거짓으로 둔다** ──────────────────
--
-- 푸시가 생겼다고 메일 곁가지(`PushSender.FanOutEmailAsync`)를 켜면 안 된다.
-- 문의 메일은 `InquiryMailNotifier` 가 **제 틀로 이미 따로 낸다**(접수번호·
-- 본문·답장 안내가 들어간 그 메일이다). 둘 다 참이면 담당자가 문의 하나에
-- **메일 두 통**을 받고, 그중 한 통은 내용이 더 적다.
--
-- 이 이벤트는 notify-policy-email-2026-10-11.sql 이 「(가) 보내는 쪽이
-- /emails/send 를 따로 부른다」로 갈라 둔 쪽이고, 그 갈래는 그대로다.
--
-- ── 구분(category)을 이제야 붙인다 ──────────────────────────
--
-- 한동안 `category` 가 비어 있었다 — 메일로만 나가 **알림함에 쌓이지 않아서**
-- 붙일 갈래가 없었다(보고서 메일과 같은 까닭). 푸시가 생기면 발송 이력과 내
-- 알림함에 줄이 쌓이기 시작하고, 구분이 없으면 그 줄들이 「구분 없음」으로
-- 모인다 — 거기는 **구분을 빠뜨린 발송 자리를 찾는 자리**라 섞이면 안 된다.
--
-- 그래서 공통코드 `NOTI_CATEGORY` 에 `SITE_INQUIRY` 를 넣고 이벤트에도 맨다.
-- 보내는 쪽이 적는 글자는 `JSini.Shared.DTOs.PushCategories.SiteInquiry` 다.
--
-- ── 정책 줄은 하나도 넣지 않는다 ────────────────────────────
--
-- 「앱 푸시」를 켠 역할이 하나도 없으면 이 이벤트의 푸시는 **제한 없음**이다 —
-- SiteServer 가 적어 보낸 기본 역할(`InquiryMail:ToRole`, 지금은
-- SYSTEM_ADMINISTRATOR)로 그냥 나간다. 역할을 한 줄이라도 켜는 순간부터
-- 그것이 기본값을 대신한다(docs/notify-policy.md 의 「조용히 막지 않는다」).
--
-- **이미 깔려 있는 SITE_INQUIRY 정책 줄은 건드리지 않는다.** 지금 그 줄들은
-- `push_enabled = false`(칸이 잠겨 있던 때 저장된 값)이고, 그것을 여기서
-- 켜 주면 **관리자가 고른 적 없는 사람에게 알림이 늘어난다.** 틀리는 방향을
-- 고를 수 있을 때는 되돌릴 수 있는 쪽을 고른다.
--
-- 되돌리려면 맨 아래 두 줄.
-- ============================================================

BEGIN;

-- ── 1. 「앱 푸시」 칸을 연다 ────────────────────────────────
--
-- `email_from_push` 는 일부러 안 건드린다(머리말). `supports_email` 도
-- 그대로 참이다 — 메일 길이 없어지는 것이 아니라 푸시가 더해지는 것이다.

UPDATE scom.notification_events
   SET supports_push = true,
       description   = '회사 소개 사이트에서 문의가 들어왔을 때 담당자에게. 앱 푸시를 누르면 [사이트 문의내역]이 열리고, 이메일은 접수번호와 본문이 담긴 제 틀의 메일이 따로 나간다.',
       updated_at    = now(),
       updated_by    = 'notify-policy-site-inquiry-push'
 WHERE id = 'SITE_INQUIRY'
   AND NOT is_deleted
   AND supports_push IS DISTINCT FROM true;

-- ── 2. 알림구분을 공통코드에 넣는다 ─────────────────────────
--
-- 이 표에는 유일 제약이 없어 ON CONFLICT 를 걸 곳이 없다. 있는지 보고 넣는다
-- (notification-push-category-2026-09-27.sql 과 같은 모양).
--
-- 차례는 95 — 알림구독(90)과 시험발송(100) 사이다. 시험발송은 늘 맨 끝에
-- 두는 것이 목록에서 읽기 쉽다.

INSERT INTO scom.common_codes
    (id, group_id, parent_id, code_value, code_name, sort_order, level, is_leaf,
     status, remark, created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, g.id, NULL, 'SITE_INQUIRY', '사이트 문의',
       95, 1, true, 1,
       '회사 소개 사이트에서 문의가 접수됐다는 알림',
       now(), 'notify-policy-site-inquiry-push', false
  FROM scom.common_code_groups g
 WHERE g.group_code = 'NOTI_CATEGORY'
   AND g.is_deleted = false
   AND NOT EXISTS (
        SELECT 1 FROM scom.common_codes c
         WHERE c.group_id = g.id
           AND c.code_value = 'SITE_INQUIRY'
           AND c.is_deleted = false
   );

-- ── 3. 이벤트에 그 구분을 맨다 ──────────────────────────────
--
-- 화면의 「알림구분」 칸이 이 값으로 공통코드의 이름을 찾아 그린다
-- (NotificationPolicyService.CategoryNamesAsync). 발송 경로가 이벤트를 찾는
-- 열쇠는 `id` 라, 이 값을 채워도 정책이 걸리는 자리는 달라지지 않는다.

UPDATE scom.notification_events
   SET category   = 'SITE_INQUIRY',
       updated_at = now(),
       updated_by = 'notify-policy-site-inquiry-push'
 WHERE id = 'SITE_INQUIRY'
   AND NOT is_deleted
   AND category IS DISTINCT FROM 'SITE_INQUIRY';

COMMIT;

-- 넣은 결과 확인
SELECT id, name, target_kind,
       supports_push, supports_email, email_from_push, governed, is_active, category
  FROM scom.notification_events
 WHERE id = 'SITE_INQUIRY' AND NOT is_deleted;

SELECT c.code_value, c.code_name, c.sort_order
  FROM scom.common_codes c
  JOIN scom.common_code_groups g ON g.id = c.group_id
 WHERE g.group_code = 'NOTI_CATEGORY' AND NOT c.is_deleted
 ORDER BY c.sort_order;

-- 「앱 푸시」를 켠 SITE_INQUIRY 정책 줄. 이 스크립트 직후에는 0 건이 정상이다
-- (0 건이면 기본 역할로 그냥 나간다 — 머리말).
SELECT event_code, role_id, push_enabled, email_enabled, is_active
  FROM scom.notification_policies
 WHERE event_code = 'SITE_INQUIRY' AND NOT is_deleted
 ORDER BY role_id;

-- 되돌리기 (SiteServer 를 되돌린 뒤에 한다 — 칸을 먼저 잠그면 푸시는 계속
-- 나가는데 화면에서 끌 수가 없다)
-- UPDATE scom.notification_events SET supports_push = false, category = NULL
--  WHERE id = 'SITE_INQUIRY';
-- UPDATE scom.common_codes SET is_deleted = true
--  WHERE code_value = 'SITE_INQUIRY'
--    AND group_id = (SELECT id FROM scom.common_code_groups WHERE group_code = 'NOTI_CATEGORY');
