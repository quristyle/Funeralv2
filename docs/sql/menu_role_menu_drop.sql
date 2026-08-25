-- ============================================================
-- '롤메뉴'(/auth/role-menu) 메뉴 삭제
-- ============================================================
--
-- '롤사람' 과 같은 이유다 → menu_role_user_drop.sql
-- /auth/role-menu 는 '역할 관리'(/system/role-map) 의 '역할-메뉴 권한' 탭과
-- 같은 일을 하는 중복 메뉴였다.
--   /system/role-map  → views/portal/system/role/index.vue
--                       └ modules/RoleMenuTab.vue  (역할에 메뉴 권한 부여)
--                       API: /auth/system/role-permission/roles/{roleId}/menus
--
-- 남기는 쪽이 더 나은 구현이다. RoleMenuTab 은 메뉴가 실제로 쓰는 권한 항목
-- (`system_menus.use_*`, `cust*_name`) 만 켤 수 있게 하고 안 쓰는 항목은 잠근다.
-- 지운 role-menu-custom 은 15개 체크박스를 모든 메뉴에 똑같이 띄우던 쪽이다.
--
-- /auth/role-menu 가 가리키던 화면은 백엔드 연동 전의 임시 화면이었고,
-- 같은 기능을 구현해 둔 role-menu-custom 은 어느 메뉴도 가리키지 않는
-- 고아 파일이었다. 둘 다 지웠다.
--   삭제: views/portal/auth/role-menu/index.vue
--   삭제: views/portal/auth/role-menu-custom/index.vue
--   삭제: api/portal/system/role-mapping.ts 의 롤메뉴 함수 2개
--         (getRoleMenus · saveRoleMenus) 와 RoleMenuMapping 타입
--         — 호출하던 백엔드 엔드포인트는 애초에 없었다.
--
-- 메뉴 삭제는 앱과 같은 방식(하드 삭제)으로 한다.
-- 반복 실행해도 안전하다.

BEGIN;

-- 역할별 메뉴 권한 (4개 역할에 걸려 있었다)
DELETE FROM scom.role_menus WHERE menu_id = 'ROLE_MENU';

-- 즐겨찾기에 담아 둔 사용자가 있으면 함께 정리
DELETE FROM scom.menu_favorites WHERE menu_id = 'ROLE_MENU';

-- 메뉴 본체
DELETE FROM scom.system_menus WHERE id = 'ROLE_MENU';

COMMIT;

-- 확인: 남아 있는 /auth 하위 메뉴
SELECT id, name, path, component
FROM scom.system_menus
WHERE pid = 'AUTH' AND is_deleted = false
ORDER BY order_no, path;
