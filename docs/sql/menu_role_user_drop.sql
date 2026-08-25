-- ============================================================
-- '롤사람'(/auth/role-user) 메뉴 삭제
-- ============================================================
--
-- /auth/role-user 는 '역할 관리'(/system/role-map) 의 '역할-사용자' 탭과
-- 같은 일을 하는 중복 메뉴였다.
--   /system/role-map  → views/portal/system/role/index.vue
--                       └ modules/RoleUserTab.vue  (역할에 사용자 배정)
--                       API: /auth/system/role-permission/roles/{roleId}/users
--
-- /auth/role-user 가 가리키던 화면은 백엔드 연동 전의 임시 화면이었고,
-- 같은 기능을 구현해 둔 role-user-custom 은 어느 메뉴도 가리키지 않는
-- 고아 파일이었다. 둘 다 지웠다.
--   삭제: views/portal/auth/role-user/index.vue
--   삭제: views/portal/auth/role-user-custom/index.vue
--   삭제: api/portal/system/role-mapping.ts 의 롤사람 함수 3개
--         (getRoleUsers · assignRoleToUsers · removeRoleFromUsers)
--         — 호출하던 백엔드 엔드포인트는 애초에 없었다.
--
-- 메뉴 삭제는 앱과 같은 방식(하드 삭제)으로 한다.
-- 반복 실행해도 안전하다.

BEGIN;

-- 역할별 메뉴 권한 (4개 역할에 걸려 있었다)
DELETE FROM scom.role_menus WHERE menu_id = 'ROLE_USER';

-- 즐겨찾기에 담아 둔 사용자가 있으면 함께 정리
DELETE FROM scom.menu_favorites WHERE menu_id = 'ROLE_USER';

-- 메뉴 본체
DELETE FROM scom.system_menus WHERE id = 'ROLE_USER';

COMMIT;

-- 확인: 남아 있는 /auth 하위 메뉴
SELECT id, name, path, component
FROM scom.system_menus
WHERE pid = 'AUTH' AND is_deleted = false
ORDER BY order_no, path;
