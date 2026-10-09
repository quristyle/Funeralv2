-- ============================================================
-- 「내 이동경로」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   내 이동경로  HD_MY_TRACK  /admin/location/my-track  admin.location.my-track
--
-- 「알림 관리」(HD_PUSH) 아래, 「위치 지도」 바로 다음에 붙인다. 좌표를 다루는
-- 화면이 그 둘뿐이라 나란히 있는 편이 찾기 쉽다.
--
-- ── 「위치 지도」와 권한이 다르다 ───────────────────────────
--
-- 저쪽은 **직원 전체가 지금 어디 있는지**를 한 장에 보여 주므로 관리자와
-- 헬프데스크 담당에게만 준다. 이쪽은 **자기가 지나온 길**이고 주소에 `/me`
-- 가 박혀 있어 남의 것을 물을 길이 아예 없다 — 자기 것을 자기가 보는 화면이다.
--
-- 그래서 「내 알림함」(HD_PUSH_HISTORY)의 권한을 **그대로 베낀다.** 그쪽도
-- 같은 갈래라(자기 것을 자기가 읽는다) 역할 구성이 이미 그 뜻으로 맞춰져
-- 있고, 베껴 두면 역할이 늘거나 줄 때 두 화면이 함께 따라간다 — 여기에
-- 역할 이름을 손으로 적어 두면 그날 이 화면만 남는다.
--
-- 읽기만 하는 화면이라 켜는 권한은 베낀 것 중 보기·조회·엑셀뿐이다.
-- (엑셀은 왼쪽 목록의 내려받기다. 지도에는 내려받을 것이 없다.)
--
-- ── path 는 새 경로로 바로 적는다 ───────────────────────────
--
-- 옛 메뉴 69건은 Vue 시절 경로를 들고 있고 `RouteAliases` 가 그것을 옮겨
-- 준다. 새로 만드는 메뉴는 그 표를 탈 이유가 없다 — 옆의 「위치 지도」가
-- 이미 /admin/... 으로 들어와 있다.
--
-- path 는 권한표와 즐겨찾기의 열쇠이므로(web/CLAUDE.md) 한 번 정하면 안 바꾼다.
-- 화면 주소를 옮길 일이 생기면 `route_key` 만 따라간다.
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
    ('HD_MY_TRACK', 'MyTrack', '/admin/location/my-track', 'HD_PUSH', 'MENU',
     '내 이동경로', 'lucide:route', 7,
     false, 1, now(), 'my-track-menu', false,
     true, true, false, false, false, true, false,
     true, true,
     'admin.location.my-track')
ON CONFLICT (id) DO NOTHING;

-- ── 권한은 「내 알림함」에서 베낀다 ─────────────────────────
--
-- 역할 이름을 손으로 적지 않는다(머리말). 베낀 줄에서 켜는 것은 보기·조회·
-- 엑셀뿐이고, 그 셋도 저쪽이 꺼 둔 역할에는 켜 주지 않는다 — 저쪽에서
-- `can_view = false` 인 역할(FUNERAL_OPERATOR)이 여기서만 열리면 안 된다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT s.role_id, 'HD_MY_TRACK',
       s.can_view, s.can_search, false, false, false,
       false, s.can_view,
       false, false, false, false, false, false, false, false,
       false, now(), 'my-track-menu'
  FROM scom.role_menus s
 WHERE s.menu_id = 'HD_PUSH_HISTORY'
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = s.role_id AND x.menu_id = 'HD_MY_TRACK');

COMMIT;

-- 확인
--   SELECT id, title, path, route_key, pid, order_no, icon
--     FROM scom.system_menus WHERE id = 'HD_MY_TRACK';
--   SELECT menu_id, role_id, can_view, can_search, can_excel
--     FROM scom.role_menus WHERE menu_id = 'HD_MY_TRACK' ORDER BY role_id;
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_MY_TRACK';
--   DELETE FROM scom.role_menus     WHERE menu_id = 'HD_MY_TRACK';
--   DELETE FROM scom.system_menus   WHERE id      = 'HD_MY_TRACK';
