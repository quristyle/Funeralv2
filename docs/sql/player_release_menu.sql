-- ============================================================
-- 플레이어 릴리스 메뉴 (/system/player-release)
-- ============================================================
--
-- DB: jsiniportal (스키마 scom)
--
-- funeralv2_player 설치 파일을 만들어 GitHub Release 로 내보내는 화면.
-- 발행하면 바로 옆 [플레이어 다운로드] 화면이 그 파일을 찾아 보여 준다.
--
-- 자리는 [플레이어 다운로드] 바로 아래다(같은 부모 = 장례식장 관리시스템).
-- 만드는 쪽과 받는 쪽이 붙어 있어야 찾기 쉽다.
--
-- ── 권한 ─────────────────────────────────────────────────
--
-- 릴리스 발행은 **can_create** 다. 화면의 [릴리스 발행] 단추가 `v-perm:create` 로
-- 걸려 있고, **서버도 같은 값을 본다**(PlayerReleaseService.CanReleaseAsync).
-- 화면에서 숨기는 것은 통제가 아니다 — 서버 판정이 실제 통제다.
--
-- **use_view 는 반드시 켠다.** 화면이 '열람' 단추를 쓰지 않더라도, `can_view` 는
-- 사이드바 노출과 화면 진입을 가르는 값이다(menu-visibility.ts · setupViewPermissionGuard).
-- 서버가 내려주는 canView 는 `역할의 can_view AND 메뉴의 use_view` 라서,
-- use_view 를 끄면 권한을 줘도 메뉴가 사라지고 들어가면 403 이 된다.
--
-- 나머지(조회·수정·삭제·출력·엑셀)는 이 화면에 없으므로 꺼 둔다.
-- 그래야 역할 권한 화면에 쓸데없는 체크박스가 안 나온다(menu_permission_items.sql).
--
-- 처음에는 **SYSTEM_ADMINISTRATOR 에게만** 열람·발행을 준다.
-- 릴리스는 되돌리기 어려운 동작이라 기본을 좁게 잡는다. 필요한 역할은
-- 역할 권한 화면에서 더 켠다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- ── 메뉴 ────────────────────────────────────────────────
INSERT INTO scom.system_menus (
    id, pid, name, path, component, type, title, icon, order_no,
    status, hide_in_menu, keep_alive, affix_tab, dom_cached,
    menu_visible_with_forbidden,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel,
    use_mobile, use_tablet,
    created_at, created_by, is_deleted
)
SELECT
    'menu-player-release',
    p.id,
    '플레이어 릴리스',
    '/system/player-release',
    '#/views/portal/system/player-release/index.vue',
    'MENU',
    '플레이어 릴리스',
    'lucide:rocket',
    8,                      -- 플레이어 다운로드(7) 바로 아래
    1, false, true, false, false,
    false,
    -- 열람(화면 진입·사이드바 노출)과 발행(create) 둘만 쓴다.
    true, false, true, false, false, false, false,
    -- 작은 화면에서는 쓸 일이 없다. 진행 상황 표가 좁은 화면에 맞지 않는다.
    false, false,
    now(), 'System', false
FROM scom.system_menus p
WHERE p.path = '/funerals'
ON CONFLICT (id) DO UPDATE
SET pid        = EXCLUDED.pid,
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    component  = EXCLUDED.component,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = EXCLUDED.status,
    use_view   = EXCLUDED.use_view,
    use_search = EXCLUDED.use_search,
    use_create = EXCLUDED.use_create,
    use_delete = EXCLUDED.use_delete,
    use_update = EXCLUDED.use_update,
    use_print  = EXCLUDED.use_print,
    use_excel  = EXCLUDED.use_excel,
    use_mobile = EXCLUDED.use_mobile,
    use_tablet = EXCLUDED.use_tablet,
    updated_at = now(),
    updated_by = 'System',
    is_deleted = false;

-- ── 권한 ────────────────────────────────────────────────
--
-- 열람(can_view)이 있어야 사이드바에 나오고 화면에 들어갈 수 있다.
-- 발행(can_create)이 있어야 단추가 보이고 서버가 요청을 받는다.
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
SELECT
    'SYSTEM_ADMINISTRATOR', 'menu-player-release',
    true, false, true, false, false, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'System', false
WHERE EXISTS (SELECT 1 FROM scom.roles WHERE id = 'SYSTEM_ADMINISTRATOR')
ON CONFLICT (role_id, menu_id) DO UPDATE
SET can_view   = true,
    can_create = true,
    updated_at = now(),
    updated_by = 'System',
    is_deleted = false;

COMMIT;

-- 확인
SELECT m.path, m.title, m.order_no, m.status, m.use_create, m.use_mobile
FROM scom.system_menus m
WHERE m.path IN ('/system/player-download', '/system/player-release')
ORDER BY m.order_no;

SELECT rm.role_id, rm.can_view AS 열람, rm.can_create AS 발행
FROM scom.role_menus rm
WHERE rm.menu_id = 'menu-player-release' AND rm.is_deleted = false;
