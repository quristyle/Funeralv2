-- ============================================================
-- 도움말 '문의' 화면 제거
-- ============================================================
--
-- 지시: "/help/inquiry 는 문의를 위해 준비한 화면이다. QnA 화면이 그 역활을 하니
--        /help/inquiry 의 화면은 삭제하라. 메뉴에서도 제거하라."
--
-- Q&A(`/help/qna`)가 같은 일을 한다 — 사용자가 묻고 관리자가 답한다.
-- 둘을 함께 두면 사용자가 어디에 물어야 할지 고르게 되고, 관리자는 두 곳을 봐야 한다.
--
-- 화면 파일도 함께 지웠으므로(`views/funeral/help/inquiry`, `inquiry-custom`)
-- 메뉴 행을 남겨 두면 **없는 파일을 가리키는 경로**가 된다. 그래서 행까지 지운다.
-- 공지 때와 같은 방식이다(`notice_menu.sql` 의 헬프데스크 공지 제거).
--
-- 참고: 헬프데스크의 '문의하기'(`/helpdesk/contact-us`)는 다른 화면이다. 건드리지 않는다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- 즐겨찾기에 담아 둔 사람이 있으면 함께 정리한다.
-- (없어진 메뉴를 가리키는 행이 남으면 사이드바 조회에서 매번 걸러야 한다)
DELETE FROM scom.menu_favorites
WHERE menu_id IN (
  SELECT id FROM scom.system_menus WHERE path = '/help/inquiry'
);

-- 역할 권한
DELETE FROM scom.role_menus
WHERE menu_id IN (
  SELECT id FROM scom.system_menus WHERE path = '/help/inquiry'
);

-- 메뉴
DELETE FROM scom.system_menus WHERE path = '/help/inquiry';

COMMIT;

-- 확인 — 도움말 아래에 Q&A · F.A.Q · 자료실만 남아야 한다
SELECT id, path, title, order_no, status
FROM scom.system_menus
WHERE pid = 'HELP' AND is_deleted = false
ORDER BY order_no;


-- ============================================================
-- 되돌리기
-- ============================================================
-- 다시 쓰기로 하면 화면 파일을 되살린 뒤 아래를 실행한다.
-- (지우기 전 값 그대로다 — 순서 1, 아이콘 lucide:message-circle)
--
-- BEGIN;
-- INSERT INTO scom.system_menus (
--   id, pid, name, path, component, type, title, icon,
--   order_no, hide_in_menu, keep_alive, status,
--   created_at, created_by,
--   affix_tab, dom_cached, menu_visible_with_forbidden, is_deleted
-- ) VALUES (
--   'INQUIRY', 'HELP', 'Inquiry', '/help/inquiry',
--   '#/views/funeral/help/inquiry/index.vue', 'MENU', '문의', 'lucide:message-circle',
--   1, false, true, 1,
--   now(), 'help-inquiry-restore',
--   false, false, false, false
-- ) ON CONFLICT (id) DO NOTHING;
--
-- INSERT INTO scom.role_menus (
--   role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
--   can_print, can_excel, created_at, created_by, is_deleted
-- )
-- SELECT r.id, 'INQUIRY', true, true, true, true, true, false, false,
--        now(), 'help-inquiry-restore', false
-- FROM scom.roles r WHERE r.is_deleted = false
--   AND NOT EXISTS (
--     SELECT 1 FROM scom.role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = 'INQUIRY'
--   );
-- COMMIT;
