-- 권한 제어 샘플 화면을 메뉴에 등록한다.
--
-- 이 화면은 `scom.role_menus` 의 15개 권한을 화면에서 어떻게 쓰는지 보여 주는
-- 참고용이다. 그래서 15개 항목을 전부 '사용한다'로 켜고, 사용자 정의 1~8 에는
-- 화면이 실제로 쓰는 이름을 붙인다 — 그 이름이 역할 관리 화면의 열 제목이 된다.
--
-- 반복 실행 안전.

-- ============================================================
-- 1. 메뉴
--    /system 아래 '상태관리'·'공통' 과 같은 층에 둔다.
--    order_no 9 — 관리 화면들보다 뒤로 보낸다 (참고용이므로).
-- ============================================================
INSERT INTO scom.system_menus (
    id, pid, name, title, path, component, type,
    order_no, status, icon, hide_in_menu, keep_alive, is_deleted,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel,
    use_cust1, use_cust2, use_cust3, use_cust4, use_cust5, use_cust6, use_cust7, use_cust8,
    cust1_name, cust2_name, cust3_name, cust4_name,
    cust5_name, cust6_name, cust7_name, cust8_name,
    created_at, created_by, updated_at, updated_by
)
VALUES (
    'PERM_SAMPLE',
    'b24f9105-0dbf-4d30-a13d-b7997def1d5d',   -- System 카탈로그
    'SystemPermSample', '권한 제어 샘플', '/system/perm-sample',
    '#/views/portal/system/perm-sample/index.vue', 'MENU',
    9, 1, 'mdi:shield-key-outline', false, true, false,
    true, true, true, true, true, true, true,
    true, true, true, true, true, true, true, true,
    '승인', '반려', '마감', '재계산', '이력조회', '알림발송', '일괄변경', '잠금해제',
    NOW(), 'System', NOW(), 'System'
)
ON CONFLICT (id) DO UPDATE
   SET pid          = EXCLUDED.pid,
       name         = EXCLUDED.name,
       title        = EXCLUDED.title,
       path         = EXCLUDED.path,
       component    = EXCLUDED.component,
       type         = EXCLUDED.type,
       order_no     = EXCLUDED.order_no,
       status       = EXCLUDED.status,
       icon         = EXCLUDED.icon,
       hide_in_menu = EXCLUDED.hide_in_menu,
       keep_alive   = EXCLUDED.keep_alive,
       is_deleted   = false,
       use_view     = EXCLUDED.use_view,
       use_search   = EXCLUDED.use_search,
       use_create   = EXCLUDED.use_create,
       use_delete   = EXCLUDED.use_delete,
       use_update   = EXCLUDED.use_update,
       use_print    = EXCLUDED.use_print,
       use_excel    = EXCLUDED.use_excel,
       use_cust1    = EXCLUDED.use_cust1,
       use_cust2    = EXCLUDED.use_cust2,
       use_cust3    = EXCLUDED.use_cust3,
       use_cust4    = EXCLUDED.use_cust4,
       use_cust5    = EXCLUDED.use_cust5,
       use_cust6    = EXCLUDED.use_cust6,
       use_cust7    = EXCLUDED.use_cust7,
       use_cust8    = EXCLUDED.use_cust8,
       cust1_name   = EXCLUDED.cust1_name,
       cust2_name   = EXCLUDED.cust2_name,
       cust3_name   = EXCLUDED.cust3_name,
       cust4_name   = EXCLUDED.cust4_name,
       cust5_name   = EXCLUDED.cust5_name,
       cust6_name   = EXCLUDED.cust6_name,
       cust7_name   = EXCLUDED.cust7_name,
       cust8_name   = EXCLUDED.cust8_name,
       updated_at   = NOW(),
       updated_by   = 'System';

-- ============================================================
-- 2. 역할 권한 행
--
--    행이 없으면 그 역할에게는 이 메뉴가 권한 표에 아예 없는 상태가 된다.
--    그러면 화면은 상위 메뉴의 권한을 물려받아 버려서 체크박스를 켜고 끄는
--    실험을 할 수가 없다. 그래서 모든 역할에 행을 만들어 둔다.
--
--    관리자·시스템관리자는 전부 켜고(샘플을 보려면 열려 있어야 한다),
--    파트너 계열은 열람·조회만 켠다 — 권한이 실제로 막히는 모습을
--    두 역할을 번갈아 보며 확인할 수 있게 한다.
--
--    이미 있는 행은 건드리지 않는다. 역할 관리 화면에서 손으로 조정한 값을
--    이 스크립트를 다시 돌렸다고 되돌려 놓으면 안 된다.
-- ============================================================
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
SELECT
    r.id, 'PERM_SAMPLE',
    true,
    true,
    full_access, full_access, full_access, full_access, full_access,
    full_access, full_access, full_access, full_access,
    full_access, full_access, full_access, full_access,
    NOW(), 'perm-sample', false
FROM scom.roles r
CROSS JOIN LATERAL (
    SELECT r.id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR') AS full_access
) f
WHERE r.is_deleted = false
  AND NOT EXISTS (
      SELECT 1 FROM scom.role_menus rm
      WHERE rm.role_id = r.id AND rm.menu_id = 'PERM_SAMPLE'
  );
