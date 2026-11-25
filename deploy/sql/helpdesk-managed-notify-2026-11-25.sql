-- ============================================================
-- 맡은 회사에 새 헬프데스크 요청이 올라오면 그 회사 담당자에게 알린다 (2026-11-25)
--
-- 대상 DB: jsiniportal (스키마 scom)
--
-- 등록 알림(HELPDESK)은 역할 단위(시스템관리자 · 관리자 · 서버관리자)라 맡은 회사를
-- 모른다. 그래서 SK가스를 「사용처 담당자」(HELP_DESK)로 맡은 헬프데스크
-- 관리자(HELPDESK_MANAGER)는 SK가스 사용자가 올린 글을 앱푸시로도 메일로도
-- 받지 못했다.
--
-- 보내는 쪽: HelpDeskServer/Services/ManagedRequestNotifier.cs
--   (JSini.Shared.DTOs.NotificationEvents.HelpDeskManaged)
--
-- * target_kind = USER — 받는 사람은 역할이 아니라 그 회사의 담당자다.
-- * email_from_push = false — 메일은 헬프데스크가 제 틀로 따로 낸다(두 통 방지).
-- * 정책 줄은 넣지 않는다 — 줄이 없으면 제한 없음이고, 끄는 자리는 본인 환경설정이다.
-- ============================================================

BEGIN;

INSERT INTO scom.notification_events
    (id, name, description, category, source, target_kind,
     supports_push, supports_email, email_from_push, governed, is_active,
     order_no, created_at, created_by)
VALUES
('HELPDESK_MANAGED', '맡은 회사의 헬프데스크 요청 등록',
 '포털관리 「사용처 담당자」(헬프데스크)로 맡은 회사에 새 요청이 올라왔을 때, 그 회사 담당자 중 헬프데스크 관리자(HELPDESK_MANAGER)에게. 글쓴이 본인은 뺀다. 앱 푸시와 이메일을 헬프데스크가 각각 낸다.',
 'HELPDESK', 'HelpDeskServer', 'USER', true, true, false, true, true,
 35, now(), 'helpdesk-managed-notify')
ON CONFLICT (id) DO NOTHING;

COMMIT;

-- 확인
-- SELECT id, name, category, target_kind, email_from_push, governed, is_active
--   FROM scom.notification_events WHERE id = 'HELPDESK_MANAGED';

-- 되돌리기
-- DELETE FROM scom.notification_events WHERE id = 'HELPDESK_MANAGED';
