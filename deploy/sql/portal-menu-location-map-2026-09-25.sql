-- ============================================================
-- 「위치 지도」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   위치 지도  HD_LOCATION_MAP  /admin/location/map  admin.location.map
--
-- 「알림 관리」(HD_PUSH) 아래에 붙인다. 그 묶음이 「메시지 발송」·「쪽지 쓰기」·
-- 「쪽지함」을 이미 들고 있어서, 사람이 **누구에게 연락할지**를 찾을 때 먼저
-- 여는 자리다. 이 화면도 점을 눌러 그 자리에서 쪽지를 보내는 화면이다.
--
-- ── path 를 새 경로로 바로 적는다 ──────────────────────────
--
-- 옛 메뉴 69건은 Vue 시절 경로를 들고 있고 `RouteAliases` 가 그것을 옮겨
-- 준다. **새로 만드는 메뉴는 그 표를 탈 이유가 없다** — 「메시지 발송」·
-- 「쪽지함」이 이미 /admin/... 으로 들어와 있다.
--
-- path 는 권한표와 즐겨찾기의 열쇠이므로(web/CLAUDE.md) 한 번 정하면 안 바꾼다.
-- 화면 주소를 옮길 일이 생기면 `route_key` 만 따라간다.
--
-- ── 권한을 옆 메뉴에서 베끼지 않는다 ───────────────────────
--
-- 쪽지 메뉴들은 「내 알림함」의 권한을 그대로 베꼈다. 그것은 **자기 것을
-- 자기가 읽고 쓰는** 일이라 파트너 역할까지 켜 두는 것이 맞았다.
--
-- 이 화면은 다르다. **직원 전체가 지금 어디 있는지**를 한 장에 보여 준다 —
-- 협력사 역할에 줄 것이 아니다. 그래서 역할을 베끼지 않고 관리자 둘에게만
-- 준다. 역할 아이디는 짐작한 것이 아니라 scom.roles 에서 읽은 값이다.
--
-- 읽기만 하는 화면이라 켜는 권한도 셋뿐이다 — 보기·조회·엑셀.
-- (엑셀은 왼쪽 목록의 내려받기다. 지도에는 내려받을 것이 없다.)
--
-- 두 번 돌려도 안전하다.

BEGIN;

-- ── 메뉴 한 줄 ───────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     use_mobile, use_tablet,
     route_key)
VALUES
    ('HD_LOCATION_MAP', 'LocationMap', '/admin/location/map', 'HD_PUSH', 'MENU',
     '위치 지도', 'lucide:map-pin', 6,
     false, 1, now(), 'location-map-menu', false,
     true, true, false, false, false, true, false,
     true, true,
     'admin.location.map')
ON CONFLICT (id) DO NOTHING;

-- ── 권한은 관리자 둘에게만 ──────────────────────────────────
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT r.role_id, 'HD_LOCATION_MAP',
       true, true, false, false, false,
       false, true,
       false, false, false, false, false, false, false, false,
       false, now(), 'location-map-menu'
  FROM (VALUES ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS r(role_id)
 WHERE NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = r.role_id AND x.menu_id = 'HD_LOCATION_MAP');

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, pid, order_no
--   FROM scom.system_menus WHERE id = 'HD_LOCATION_MAP';
-- SELECT menu_id, role_id, can_view, can_search, can_excel
--   FROM scom.role_menus WHERE menu_id = 'HD_LOCATION_MAP' ORDER BY role_id;
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_LOCATION_MAP';
--   DELETE FROM scom.role_menus     WHERE menu_id = 'HD_LOCATION_MAP';
--   DELETE FROM scom.system_menus   WHERE id      = 'HD_LOCATION_MAP';
