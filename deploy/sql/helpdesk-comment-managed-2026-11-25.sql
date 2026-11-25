-- ============================================================
-- 댓글 알림(HELPDESK_COMMENT)이 그 글의 회사를 맡은 헬프데스크 관리자에게도 간다 (2026-11-25)
--
-- 대상 DB: jsiniportal (스키마 scom)
-- 보내는 쪽: HelpDeskServer/Services/CommentNotifier.cs (3번 길)
--
-- 이벤트·정책은 그대로다(HELPDESK_MANAGER 는 이미 HELPDESK_COMMENT 정책에 있다).
-- 알림관리 화면의 설명만 받는 사람에 맞게 고친다.
-- ============================================================

BEGIN;

UPDATE scom.notification_events
   SET description = '내가 쓴 요청글에 댓글이, 내 댓글에 답글이 달렸을 때 그 주인에게. 그 글의 회사를 「사용처 담당자」(헬프데스크)로 맡은 헬프데스크 관리자(HELPDESK_MANAGER)에게도 — 맡지 않은 회사의 글은 안 가고, 댓글을 단 본인은 뺀다. 시스템 관리자(SYSTEM_ADMINISTRATOR)는 모든 댓글을 앱푸시로 받는다 — 그 역할의 푸시를 끄면 멎는다.'
 WHERE id = 'HELPDESK_COMMENT';

COMMIT;

-- 확인
-- SELECT id, description FROM scom.notification_events WHERE id = 'HELPDESK_COMMENT';
