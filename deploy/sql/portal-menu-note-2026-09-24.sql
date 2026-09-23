-- ============================================================
-- 「쪽지 쓰기」·「쪽지함」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   쪽지 쓰기  HD_NOTE_WRITE  /admin/note/write  admin.note.write
--   쪽지함     HD_NOTE_BOX    /admin/note/box    admin.note.box
--
-- 둘 다 「알림 관리」(HD_PUSH) 아래에 붙인다. 그 묶음이 이미 「메시지 발송」·
-- 「내 알림함」·「알림 설정」을 들고 있어서, 사람이 「누구에게 무엇을 보내고
-- 무엇을 받았나」를 찾을 때 먼저 여는 자리다.
--
-- ── path 를 새 경로로 바로 적는다 ──────────────────────────
--
-- 옛 메뉴 69건은 Vue 시절 경로(/system/push/...)를 들고 있고 `RouteAliases`
-- 가 그것을 새 경로로 옮겨 준다. **새로 만드는 메뉴는 그 표를 탈 이유가 없다** —
-- 「메시지 발송」(HD_PUSH_SEND)이 이미 /admin/push/send 로 들어와 있다.
--
-- path 는 권한표와 즐겨찾기의 열쇠이므로(web/CLAUDE.md) 한 번 정하면 안 바꾼다.
-- 화면 주소를 옮길 일이 생기면 `route_key` 만 따라간다.
--
-- ── 권한은 「내 알림함」의 것을 그대로 옮긴다 ───────────────
--
-- 역할 아이디를 짐작해 적지 않는다. 쪽지는 **자기 것을 자기가 읽고 쓰는**
-- 일이라 성격이 가장 가까운 것이 「내 알림함」(HD_PUSH_HISTORY)이고,
-- 그 줄을 그대로 베끼면 지금 켜 둔 역할과 꺼 둔 역할이 함께 따라온다.
--
-- 두 번 돌려도 안전하다.

BEGIN;

-- ── 메뉴 두 줄 ───────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
VALUES
    ('HD_NOTE_WRITE', 'NoteWrite', '/admin/note/write', 'HD_PUSH', 'MENU',
     '쪽지 쓰기', 'lucide:mail-plus', 4,
     false, 1, now(), 'note-menu', false,
     true, true, true, false, false, false, false,
     'admin.note.write'),

    ('HD_NOTE_BOX', 'NoteBox', '/admin/note/box', 'HD_PUSH', 'MENU',
     '쪽지함', 'lucide:mails', 5,
     false, 1, now(), 'note-menu', false,
     true, true, true, true, false, true, false,
     'admin.note.box')
ON CONFLICT (id) DO NOTHING;

-- ── 권한은 「내 알림함」을 그대로 베낀다 ────────────────────
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT r.role_id, m.menu_id,
       r.can_view, r.can_search, r.can_create, r.can_delete, r.can_update,
       r.can_print, r.can_excel,
       r.can_cust1, r.can_cust2, r.can_cust3, r.can_cust4,
       r.can_cust5, r.can_cust6, r.can_cust7, r.can_cust8,
       r.is_deleted, now(), 'note-menu'
  FROM scom.role_menus r
 CROSS JOIN (VALUES ('HD_NOTE_WRITE'), ('HD_NOTE_BOX')) AS m(menu_id)
 WHERE r.menu_id = 'HD_PUSH_HISTORY'
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = r.role_id AND x.menu_id = m.menu_id);

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, pid, order_no
--   FROM scom.system_menus WHERE id IN ('HD_NOTE_WRITE', 'HD_NOTE_BOX');
-- SELECT menu_id, role_id, can_view
--   FROM scom.role_menus WHERE menu_id IN ('HD_NOTE_WRITE', 'HD_NOTE_BOX') ORDER BY menu_id, role_id;
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id IN ('HD_NOTE_WRITE', 'HD_NOTE_BOX');
--   DELETE FROM scom.role_menus     WHERE menu_id IN ('HD_NOTE_WRITE', 'HD_NOTE_BOX');
--   DELETE FROM scom.system_menus   WHERE id      IN ('HD_NOTE_WRITE', 'HD_NOTE_BOX');
