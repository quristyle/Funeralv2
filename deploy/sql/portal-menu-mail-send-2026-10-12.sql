-- ============================================================
-- 「메일 보내기」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   메일 보내기  SYS_MAIL_SEND  /admin/mail/send  admin.mail.send
--
-- 「알림 관리」(HD_PUSH) 아래에 붙인다. 그 묶음이 이미 「메시지 발송」·
-- 「쪽지 쓰기」·「쪽지함」을 들고 있어서, 사람이 「누구에게 무엇을 보내나」를
-- 찾을 때 먼저 여는 자리다.
--
-- ── 권한은 **시스템관리자에게만** 준다 ─────────────────────
--
-- 옆 메뉴의 권한을 베끼지 않는다. 쪽지가 「내 알림함」을 베낀 것은 그것이
-- 누구나 쓰는 기능이라서였는데, 이 화면은 **회사 메일 계정으로 바깥에 메일을
-- 내보내는 자리**다. 받는 사람에 주소를 직접 적을 수 있으므로 계정 목록
-- 바깥으로도 나간다 — 역할을 넓게 열면 그만큼 회사 이름으로 나가는 메일을
-- 아무나 쓸 수 있게 된다.
--
-- 그래서 SYSTEM_ADMINISTRATOR(시스템관리자) 한 줄만 넣는다. 더 열어야 하면
-- 포털관리 > 권한관리 > 메뉴-역할에서 화면으로 더한다 — SQL 을 다시 돌리지
-- 않는다.
--
-- 지우기(can_delete)까지 주는 까닭은 **보낸 메일함에서 치우는 것**이 그
-- 권한이기 때문이다. 발송 기록은 그래도 남는다(서버가 표시만 한다).
-- 엑셀·인쇄는 안 준다 — 보낸함을 통째로 내려받는 일은 아무도 안 하고,
-- 메일 주소 목록이 파일로 나가는 길을 굳이 열 까닭이 없다.
--
-- ── path 를 새 경로로 바로 적는다 ──────────────────────────
--
-- 옛 메뉴 69건은 Vue 시절 경로를 들고 있고 RouteAliases 가 옮겨 주지만,
-- 새로 만드는 메뉴는 그 표를 탈 이유가 없다(web/CLAUDE.md).
--
-- 두 번 돌려도 안전하다.

BEGIN;

-- ── 메뉴 한 줄 ───────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
VALUES
    ('SYS_MAIL_SEND', 'MailSend', '/admin/mail/send', 'HD_PUSH', 'MENU',
     '메일 보내기', 'lucide:mail-plus', 8,
     false, 1, now(), 'mail-send-menu', false,
     true, true, true, false, true, false, false,
     'admin.mail.send')
ON CONFLICT (id) DO NOTHING;

-- ── 권한은 시스템관리자에게만 ───────────────────────────────
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT 'SYSTEM_ADMINISTRATOR', 'SYS_MAIL_SEND',
       true, true, true, true, false,
       false, false,
       false, false, false, false, false, false, false, false,
       false, now(), 'mail-send-menu'
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = 'SYSTEM_ADMINISTRATOR' AND x.menu_id = 'SYS_MAIL_SEND');

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, pid, order_no
--   FROM scom.system_menus WHERE id = 'SYS_MAIL_SEND';
-- SELECT role_id, can_view, can_create, can_delete
--   FROM scom.role_menus WHERE menu_id = 'SYS_MAIL_SEND';
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id = 'SYS_MAIL_SEND';
--   DELETE FROM scom.role_menus     WHERE menu_id = 'SYS_MAIL_SEND';
--   DELETE FROM scom.system_menus   WHERE id      = 'SYS_MAIL_SEND';
