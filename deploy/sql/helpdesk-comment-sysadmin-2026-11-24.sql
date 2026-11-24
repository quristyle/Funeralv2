-- 헬프데스크 댓글 알림: 시스템 관리자는 당사자가 아니어도 모든 댓글을 앱푸시로 받는다(CommentNotifier).
-- 알림관리 화면의 설명만 고친다. 정책 줄(HELPDESK_COMMENT × SYSTEM_ADMINISTRATOR)은 이미 있고 푸시가 켜져 있어야 나간다.
UPDATE scom.notification_events
   SET description = '내가 쓴 요청글에 댓글이, 내 댓글에 답글이 달렸을 때 그 주인에게. 시스템 관리자(SYSTEM_ADMINISTRATOR)는 모든 댓글을 앱푸시로 받는다 — 그 역할의 푸시를 끄면 멎는다.',
       updated_at = now(),
       updated_by = 'helpdesk-comment-sysadmin-2026-11-24'
 WHERE id = 'HELPDESK_COMMENT';
