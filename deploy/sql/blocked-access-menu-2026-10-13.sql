-- ============================================================
-- 메뉴 등록 — 포털관리 › 상태관리 › 차단 접근기록 (2026-10-13)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/BlockedAccessList.razor
-- 표:   deploy/sql/blocked-access-2026-10-13.sql (scom.blocked_access_logs)
--
--   psql -h … -U funeralv2 -d jsiniportal -f blocked-access-menu-2026-10-13.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
--
-- ── 왜 상태관리 묶음인가 ────────────────────────────────────
--
-- 바로 위가 「메뉴 사용기록」(SYS_MENU_USAGE)이다. 그쪽이 **열린** 화면을 적고
-- 이쪽이 **막힌** 화면을 적는다 — 한 짝이라 한 자리에 있어야 나란히 읽힌다.
-- order_no 는 그 다음(9)이다.
--
-- ── 이 메뉴가 **반드시** 있어야 한다 ────────────────────────
--
-- 2026-10-13 부터 권한관리 밖의 주소는 기본으로 막힌다. 이 화면은 무엇이
-- 막혔는지 보는 **유일한** 자리이고, 그런데 자기도 메뉴가 없으면 자기가
-- 막힌다 — 그 차단은 아무 데서도 볼 수 없다. 표를 만드는 SQL 과 **함께**
-- 돌린다.
--
-- ── 시스템관리자만 본다 ─────────────────────────────────────
--
-- 「어느 화면이 어떻게 뚫려 있나」는 이 표에서 사실상 공격 지도다. 그래서
-- SYSTEM_ADMINISTRATOR 하나만 넣는다 — 더 줄 역할이 생기면 메뉴 권한 화면
-- (/admin/auth/menu-role)에서 더한다. **서버의 조회도 같은 표를 본다**
-- (AuthServer 의 MenuViewAccess) — 역할을 더하면 조회도 그 순간부터 따라가고,
-- 코드를 고칠 일이 없다.
--
-- 조회만 하는 화면이라 권한 항목은 셋만 켠다(열람 · 조회 · 엑셀).
-- 등록·수정·삭제 칸을 켜 두면 역할 권한 화면에 눌러도 아무 일 없는
-- 체크칸이 생긴다.
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
    'SYS_BLOCKED_ACCESS',
    '차단 접근기록',
    '/admin/status/blocked-access',
    'admin.status.blocked-access',
    'fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5',   -- 상태관리(CATALOG)
    'MENU',
    '차단 접근기록',
    'lucide:shield-alert',
    9,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, false, false, false, false, true,
    false, false, false, false, false, false, false, false,
    now(), 'blocked-access', '-infinity'
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
    updated_at = now(),
    updated_by = 'blocked-access';

-- 시스템관리자에게만 열람·조회·엑셀을 준다(머리말).
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'SYS_BLOCKED_ACCESS',
    true, true, false, false, false, false, true,
    false, false, false, false, false, false, false, false,
    now(), 'blocked-access', false
)
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view   = true,
    can_search = true,
    can_excel  = true,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'blocked-access';

COMMIT;

-- 넣은 결과 확인
SELECT m.id, m.title, m.path, m.route_key, m.order_no, m.status,
       r.role_id, r.can_view, r.can_search, r.can_excel
  FROM scom.system_menus m
  LEFT JOIN scom.role_menus r ON r.menu_id = m.id AND NOT r.is_deleted
 WHERE m.id = 'SYS_BLOCKED_ACCESS';
