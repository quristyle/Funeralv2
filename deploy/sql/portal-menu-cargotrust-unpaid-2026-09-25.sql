-- ============================================================
-- 운송관리 메뉴에 「미지급 거래」 한 줄을 더한다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.CargoTrust/Components/Pages/UnpaidPage.razor
--       @page "/cargotrust/unpaid" · [RouteKey("cargotrust.unpaid")]
--
-- 묶음(CARGOTRUST)과 권한표의 생김새는
-- deploy/sql/portal-menu-cargotrust-2026-09-24.sql 이 정본이다. 이 파일은
-- 거기에 줄 하나를 보태는 것뿐이라 같은 규칙을 그대로 따른다 —
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이 「준비 중」이 뜨고,
-- `path` 는 권한표와 즐겨찾기의 열쇠라 한 번 정하면 안 바꾼다.
--
-- 자리는 미수금(50)과 내 거래(60) 사이다. 미수금이 「내가 못 받은 돈」이고
-- 이 화면이 「누가 적었든 미지급으로 남은 거래」라, 둘을 나란히 두어야
-- 무엇이 다른지가 메뉴에서 읽힌다.
--
--   · 멱등하다. 메뉴는 ON CONFLICT 로 덮고, 권한은 없을 때만 넣는다.
--   · 배포 순서를 안 탄다. 화면이 아직 안 떠 있으면 「준비 중」이 뜬다.
--   · 되돌리기는 맨 아래.

BEGIN;

-- ── 메뉴 ────────────────────────────────────────────────────

WITH m (id, pid, name, path, route_key, type, title, icon, order_no, hide,
        v, s, c, u, d, x) AS (
    VALUES
    ('CT_UNPAID', 'CARGOTRUST', 'CtUnpaid', '/cargotrust/unpaid', 'cargotrust.unpaid',
     'MENU', '미지급 거래', 'lucide:file-warning', 55, false,
     true, true, false, true, false, true)
)
INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
SELECT id, pid, name, path, route_key, type, title, icon,
       order_no, 1, hide, now(), 'cargotrust-unpaid',
       v, s, c, u, d, false, x
  FROM m
ON CONFLICT (id) DO UPDATE SET
    pid        = EXCLUDED.pid,
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = EXCLUDED.status,
    use_view   = EXCLUDED.use_view,
    use_search = EXCLUDED.use_search,
    use_create = EXCLUDED.use_create,
    use_update = EXCLUDED.use_update,
    use_delete = EXCLUDED.use_delete,
    use_print  = EXCLUDED.use_print,
    use_excel  = EXCLUDED.use_excel,
    updated_at = now(),
    updated_by = 'cargotrust-unpaid';

-- ── 권한 ────────────────────────────────────────────────────
--
-- 역할마다 줄을 만들고 두 관리자 역할만 켠다(시험 서비스 단계). 나머지는 줄만
-- 두어 권한 화면(/admin/auth)에서 켤 자리를 마련한다.

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete,
    can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4,
    can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted)
SELECT r.role_id, sm.id,
       on_ AND sm.use_view, on_ AND sm.use_search, on_ AND sm.use_create,
       on_ AND sm.use_update, on_ AND sm.use_delete,
       false, on_ AND sm.use_excel,
       false, false, false, false,
       false, false, false, false,
       now(), 'cargotrust-unpaid', false
  FROM (SELECT DISTINCT role_id,
               role_id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR') AS on_
          FROM scom.role_menus) r
 CROSS JOIN scom.system_menus sm
 WHERE sm.id = 'CT_UNPAID'
ON CONFLICT (role_id, menu_id) DO NOTHING;

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus WHERE pid = 'CARGOTRUST' ORDER BY order_no;
--
--   SELECT role_id, can_view FROM scom.role_menus
--    WHERE menu_id = 'CT_UNPAID' ORDER BY role_id;
--
-- 되돌리기
--
--   DELETE FROM scom.role_menus  WHERE menu_id = 'CT_UNPAID';
--   DELETE FROM scom.system_menus WHERE id     = 'CT_UNPAID';
