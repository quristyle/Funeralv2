-- ============================================================
-- 메뉴 등록 — 회사관리 › 사용처 담당자 (2026-11-23)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/CompanyUsageManagerBoard.razor
-- 표:   deploy/sql/company-usage-manager-2026-11-23.sql (scom.company_usage_managers)
--
--   psql -h … -U funeralv2 -d jsiniportal -f company-usage-manager-menu-2026-11-23.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
--
-- 회사 → 사용처 → 처리 담당자 세 단 화면이다. 「회사 사용자」 다음(order_no 4)에 둔다.
-- 시스템관리자에게만 준다 — 서버의 조회·저장도 이 메뉴의 권한(MenuViewAccess)을 본다.
-- 더 줄 역할이 생기면 메뉴 권한 화면에서 더한다.
-- ============================================================

BEGIN;

INSERT INTO scom.system_menus (
    id, name, path, route_key, pid, type, title, icon, order_no,
    status, hide_in_menu, is_deleted, keep_alive, affix_tab, dom_cached,
    menu_visible_with_forbidden, hide_children_in_menu, hide_in_breadcrumb, hide_in_tab,
    use_mobile, use_tablet,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel,
    use_cust1, use_cust2, use_cust3, use_cust4, use_cust5, use_cust6, use_cust7, use_cust8,
    created_at, created_by, updated_at
)
VALUES (
    'COMPANY_USAGE_MANAGER',
    '사용처 담당자',
    '/admin/company/usage-manager',
    'admin.company.usage-manager',
    '0f2843e6-4def-4cd4-9742-f814ce230269',   -- 회사관리(CATALOG)
    'MENU',
    '사용처 담당자',
    'lucide:user-cog',
    4,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'usage-manager', '-infinity'
)
ON CONFLICT (id) DO UPDATE SET
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    pid        = EXCLUDED.pid,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = 1,
    is_deleted = false,
    use_print  = false,
    use_excel  = false,
    updated_at = now(),
    updated_by = 'usage-manager';

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'COMPANY_USAGE_MANAGER',
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'usage-manager', false
)
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view   = true,
    can_search = true,
    can_create = true,
    can_update = true,
    can_delete = true,
    can_print  = false,
    can_excel  = false,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'usage-manager';

COMMIT;

-- 넣은 결과 확인
SELECT m.id, m.title, m.path, m.route_key, m.order_no, m.status,
       r.role_id, r.can_view, r.can_create, r.can_update, r.can_delete
  FROM scom.system_menus m
  LEFT JOIN scom.role_menus r ON r.menu_id = m.id AND NOT r.is_deleted
 WHERE m.id = 'COMPANY_USAGE_MANAGER';
