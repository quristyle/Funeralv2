-- ============================================================
-- 헬프데스크 **처리 상태 알림**을 알림 서비스로 옮긴다 (2026-10-12)
--
-- 대상 DB: jsiniportal (스키마 scom)
--
-- ── 무엇이 잘못돼 있었나 ────────────────────────────────────
--
-- 요청글을 「완료」로 넘겨도 글을 쓴 사람의 휴대폰이 울리지 않았다. 코드에는
-- 보내는 줄이 있었지만(`RequestEndpoints` 의 `PushUtil.SendPushMsg`) 그 길은
-- **헬프데스크 제 구독표**(`helpdesk.pushsubscriptions`)로 보낸다. 그 표를
-- 채우던 것은 옛 Vue 포털이고 그 화면은 2026-09-05 에 걷어냈다 — 지금 포털
-- (:5557)에서 알림을 켜면 구독은 `scom.push_subscriptions` 에 들어간다.
--
--   helpdesk.pushsubscriptions   0 줄   ← 보내던 곳
--   scom.push_subscriptions      6 줄   ← 실제 기기가 있는 곳 (2026-10-12)
--
-- 그래서 완료 알림은 **받을 기기가 0 대인 채로 조용히 성공**했다. 접수·완료
-- 메일도 같은 꼴이었다 — 받는 사람을 고르는 조건이 `helpdesk.userproperty` 의
-- `receiveEmail = true` 인데 그 표가 0 줄이라 언제나 빈 목록이었다.
--
-- ── 무엇을 넣는가 ───────────────────────────────────────────
--
-- 알림 이벤트 `HELPDESK_STATUS` 하나와, 그 알림구분 공통코드 하나.
-- 보내는 쪽이 적는 글자는 `JSini.Shared.DTOs.NotificationEvents.HelpDeskStatus`
-- (`HelpDeskServer/Services/RequestStatusNotifier.cs`).
--
-- * `target_kind = USER` — 기다리던 **당사자**에게 간다(작성자, 종료는 접수자).
--   그래서 정책의 역할은 **거름막**이다(docs/notify-policy.md 의 「대상의 성격」).
-- * `email_from_push = false` — 메일은 헬프데스크가 제 틀로 따로 낸다.
--   켜면 한 번 완료할 때 메일이 **두 통** 간다.
--
-- ── 정책 줄은 하나도 넣지 않는다 ────────────────────────────
--
-- 줄이 없는 이벤트는 **제한 없음**이라 지금까지와 똑같이 나간다. 기본을
-- 「아무도 못 받음」으로 두면 이 스크립트를 넣은 날 알림이 통째로 멎는데,
-- 그 증상은 「알림이 안 온다」 하나라서 원인이 이 표로 보이지 않는다.
--
-- ── HELPDESK_LEGACY 는 남는다 ───────────────────────────────
--
-- 이름이 말하던 넷 중 **접수·완료·종료가 빠지고 서비스 점검만 남는다.**
-- 그 둘(`HealthCheckWorker` · `AutoCheckWorker`)은 아직 알림 서버를 안 거친다.
-- 설명을 그대로 두면 관리자가 「완료 알림을 여기서 끈다」고 읽는다.
--
-- 되돌리려면 맨 아래 두 줄.
-- ============================================================

BEGIN;

-- ── 1. 알림 이벤트 ──────────────────────────────────────────

INSERT INTO scom.notification_events
    (id, name, description, category, source, target_kind,
     supports_push, supports_email, email_from_push, governed, is_active,
     order_no, created_at, created_by)
VALUES
('HELPDESK_STATUS', '내 요청글 접수 · 완료 · 종료',
 '내가 올린 요청글이 접수·완료 되었을 때 글쓴이에게, 사용자가 종료했을 때 접수자에게. 앱 푸시와 이메일을 헬프데스크가 각각 낸다.',
 'HELPDESK_STATUS', 'HelpDeskServer', 'USER', true, true, false, true, true,
 125, now(), 'helpdesk-status-notify')

ON CONFLICT (id) DO UPDATE SET
    name            = EXCLUDED.name,
    description     = EXCLUDED.description,
    category        = EXCLUDED.category,
    source          = EXCLUDED.source,
    target_kind     = EXCLUDED.target_kind,
    supports_push   = EXCLUDED.supports_push,
    supports_email  = EXCLUDED.supports_email,
    email_from_push = EXCLUDED.email_from_push,
    governed        = EXCLUDED.governed,
    order_no        = EXCLUDED.order_no,
    is_deleted      = false,
    updated_at      = now(),
    updated_by      = 'helpdesk-status-notify';

-- ── 2. 알림구분을 공통코드에 넣는다 ─────────────────────────
--
-- 이 표에는 유일 제약이 없어 ON CONFLICT 를 걸 곳이 없다. 있는지 보고 넣는다.
-- 차례는 36 — 헬프데스크 댓글(35) 바로 뒤다.
--
-- 넣지 않으면 알림함·발송 이력의 갈래 조건에 이름이 안 붙고 코드값 그대로
-- 보인다(`PushCategories` 머리말).

INSERT INTO scom.common_codes
    (id, group_id, parent_id, code_value, code_name, sort_order, level, is_leaf,
     status, remark, created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, g.id, NULL, 'HELPDESK_STATUS', '헬프데스크 처리',
       36, 1, true, 1,
       '내가 올린 요청글이 접수·완료·종료 되었다는 알림',
       now(), 'helpdesk-status-notify', false
  FROM scom.common_code_groups g
 WHERE g.group_code = 'NOTI_CATEGORY'
   AND g.is_deleted = false
   AND NOT EXISTS (
        SELECT 1 FROM scom.common_codes c
         WHERE c.group_id = g.id
           AND c.code_value = 'HELPDESK_STATUS'
           AND c.is_deleted = false
   );

-- ── 3. 옛 이벤트의 이름과 설명을 좁힌다 ─────────────────────

UPDATE scom.notification_events
   SET name        = '헬프데스크 서비스 점검',
       description = '헬프데스크가 알림 서버를 거치지 않고 직접 보내는 몫(PushUtil · EMailUtil) — 서비스 상태 점검과 자동 점검 알림이다. 접수·완료·종료는 2026-10-12 에 HELPDESK_STATUS 로 옮겼다. 정책이 아직 안 걸린다.',
       updated_at  = now(),
       updated_by  = 'helpdesk-status-notify'
 WHERE id = 'HELPDESK_LEGACY'
   AND NOT is_deleted;

COMMIT;

-- 넣은 결과 확인
--
-- SELECT id, name, category, target_kind, supports_push, supports_email,
--        email_from_push, governed, order_no
--   FROM scom.notification_events
--  WHERE id IN ('HELPDESK_STATUS', 'HELPDESK_LEGACY');
--
-- SELECT c.code_value, c.code_name, c.sort_order
--   FROM scom.common_codes c
--   JOIN scom.common_code_groups g ON g.id = c.group_id
--  WHERE g.group_code = 'NOTI_CATEGORY' AND NOT c.is_deleted
--  ORDER BY c.sort_order;
--
-- 이 스크립트 직후 HELPDESK_STATUS 정책 줄은 0 건이 정상이다(= 제한 없음).
--
-- SELECT event_code, role_id, push_enabled, email_enabled
--   FROM scom.notification_policies
--  WHERE event_code = 'HELPDESK_STATUS';

-- 되돌리기
--
-- DELETE FROM scom.notification_events WHERE id = 'HELPDESK_STATUS';
-- DELETE FROM scom.common_codes
--  WHERE code_value = 'HELPDESK_STATUS'
--    AND group_id = (SELECT id FROM scom.common_code_groups WHERE group_code = 'NOTI_CATEGORY');
