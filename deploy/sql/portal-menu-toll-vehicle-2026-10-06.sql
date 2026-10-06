-- ============================================================
-- 심야할인 계산 · 차량 메뉴 세 줄 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 화면:
--   web/src/Apps/JSini.Web.CargoTrust/Components/Pages/TollDiscountPage.razor
--     @page "/cargotrust/toll"      · [RouteKey("cargotrust.toll")]
--   web/src/Apps/JSini.Web.CargoTrust/Components/Pages/MyVehiclePage.razor
--     @page "/cargotrust/vehicles"  · [RouteKey("cargotrust.vehicles")]
--   web/src/Apps/JSini.Web.Admin/Components/Pages/VehicleList.razor
--     @page "/admin/system/vehicle" · [RouteKey("admin.system.vehicle")]
--
-- 설계: docs/cargotrust/06-toll-night-discount.md
--
-- 묶음(CARGOTRUST)과 권한표의 생김새는
-- deploy/sql/portal-menu-cargotrust-2026-09-24.sql 이 정본이다.
--
--   · 멱등하다. 메뉴는 ON CONFLICT 로 덮고, 권한은 없을 때만 넣는다.
--   · 배포 순서를 안 탄다. 화면이 아직 안 떠 있으면 「준비 중」이 뜬다.
--   · `route_key` 가 화면의 [RouteKey(...)] 와 다르면 오류 없이 「준비 중」이 뜬다.
--   · `path` 는 권한표와 즐겨찾기의 열쇠라 한 번 정하면 안 바꾼다.
--   · 되돌리기는 맨 아래.
--
-- [차량 관리가 포털관리에 있는 까닭]
--
-- 운송관리의 사용자는 **포털 계정 그 사람**이다. 그래서 「이 사람 차가 뭐냐」를
-- 묻는 자리는 계정이 모여 있는 쪽이다. 자료는 cargotrust DB 에 있고 화면만
-- 거기 선다 — 계정 표(scom.accounts)는 전사 공용이라 운송관리를 안 쓰는
-- 계정까지 축수를 지게 할 수 없다.
--
-- 계정 관리 화면에 **탭으로 붙이지 않았다.** 그 화면은 이미 알림 상태를 따로
-- 읽어 붙이고 있고, 까닭이 거기 적혀 있다 — 다른 서비스의 표를 계정 조회에
-- 엮으면 그 서비스가 죽었을 때 계정 관리가 통째로 안 열린다.
-- 운송관리를 안 쓰는 곳에서는 이 메뉴 하나만 끄면 된다.

BEGIN;

-- ── 운송관리 메뉴 둘 ────────────────────────────────────────
--
-- 자리는 내 거래(60) 다음, 내 신고(70) 앞이다. 심야할인은 거래를 적는 일이
-- 아니라 **거래를 잡기 전에** 보는 것이라 위쪽이 맞지만, 메뉴를 재배치하면
-- 쓰던 사람이 길을 잃는다. 새 줄은 뒤에 붙인다.

WITH m (id, pid, name, path, route_key, type, title, icon, order_no, hide,
        v, s, c, u, d, x) AS (
    VALUES
    ('CT_TOLL', 'CARGOTRUST', 'CtToll', '/cargotrust/toll', 'cargotrust.toll',
     'MENU', '심야할인 계산', 'lucide:moon-star', 62, false,
     true, false, false, false, false, false),
    ('CT_VEHICLES', 'CARGOTRUST', 'CtVehicles', '/cargotrust/vehicles', 'cargotrust.vehicles',
     'MENU', '내 차량', 'lucide:truck', 64, false,
     true, false, true, true, true, true)
)
INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
SELECT id, pid, name, path, route_key, type, title, icon,
       order_no, 1, hide, now(), 'toll-vehicle',
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
    updated_by = 'toll-vehicle';

-- ── 포털관리 메뉴 하나 ──────────────────────────────────────
--
-- 상위 묶음은 **계정 관리가 든 묶음**이다. 그 id 를 여기 적지 않고 계정 관리
-- 줄에서 읽어 온다 — 포털관리 묶음 id 는 환경마다 다르고(운영은 UUID 다),
-- 틀리게 적으면 메뉴가 뿌리에 혼자 떠서 아무도 못 찾는다.
--
-- 자리는 그 묶음의 **맨 뒤**다. 계정 관리 바로 다음 번호를 쓰면 이미 그 번호를
-- 가진 형제(운영에서는 「메뉴 관리」)와 겹쳐, 둘의 순서가 조회마다 달라진다.
-- 사이에 끼우려면 형제들을 밀어야 하는데 그것은 이미 선 줄을 고치는 일이다.
--
-- 계정 관리 줄이 없으면(= 아직 안 선 환경) 아무것도 안 넣고 지나간다.

WITH anchor AS (
    SELECT pid, order_no
      FROM scom.system_menus
     WHERE path = '/system/account'
        OR route_key = 'admin.system.account'
     ORDER BY order_no
     LIMIT 1
), spot AS (
    SELECT a.pid, COALESCE(MAX(sib.order_no), a.order_no) + 1 AS order_no
      FROM anchor a
      LEFT JOIN scom.system_menus sib
             ON sib.pid IS NOT DISTINCT FROM a.pid
           AND sib.is_deleted = false
     GROUP BY a.pid, a.order_no
)
INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
SELECT 'ACC_VEHICLE', a.pid, 'AccVehicle', '/admin/system/vehicle', 'admin.system.vehicle',
       'MENU', '차량 관리', 'lucide:truck',
       a.order_no, 1, false, now(), 'toll-vehicle',
       true, true, true, true, true, false, true
  FROM spot a
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
    updated_by = 'toll-vehicle';

-- ── 권한 ────────────────────────────────────────────────────
--
-- 역할마다 줄을 만들고 두 관리자 역할만 켠다. 나머지는 줄만 두어 권한 화면
-- (/admin/auth)에서 켤 자리를 마련한다 — 차량과 심야할인을 실제로 쓸 사람은
-- 기사인데, 어느 역할이 기사인지는 설치본마다 다르다.

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
       now(), 'toll-vehicle', false
  FROM (SELECT DISTINCT role_id,
               role_id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR') AS on_
          FROM scom.role_menus) r
 CROSS JOIN scom.system_menus sm
 WHERE sm.id IN ('CT_TOLL', 'CT_VEHICLES', 'ACC_VEHICLE')
ON CONFLICT (role_id, menu_id) DO NOTHING;

-- ── 시스템 관리자는 전부 켠다 ───────────────────────────────
--
-- 위 INSERT 는 화면이 실제로 가진 단추(use_*)만 켜 준다. 시스템 관리자에게는
-- 그와 무관하게 전부 켠다 — 뒤에 화면에 단추가 생겼을 때(use_* 를 켰을 때)
-- 권한을 다시 손보지 않아도 되게.
--
-- can_* 가 켜져 있어도 use_* 가 꺼진 단추는 화면에 나오지 않는다. 그래서
-- 전부 켜는 것이 「없는 단추가 생기는」 일을 만들지는 않는다.

UPDATE scom.role_menus
   SET can_view = true, can_search = true, can_create = true,
       can_update = true, can_delete = true, can_print = true, can_excel = true,
       can_cust1 = true, can_cust2 = true, can_cust3 = true, can_cust4 = true,
       can_cust5 = true, can_cust6 = true, can_cust7 = true, can_cust8 = true,
       is_deleted = false,
       updated_at = now(), updated_by = 'toll-vehicle'
 WHERE role_id = 'SYSTEM_ADMINISTRATOR'
   AND menu_id IN ('CT_TOLL', 'CT_VEHICLES', 'ACC_VEHICLE');

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus
--    WHERE id IN ('CT_TOLL','CT_VEHICLES','ACC_VEHICLE')
--    ORDER BY pid, order_no;
--
--   SELECT role_id, menu_id, can_view FROM scom.role_menus
--    WHERE menu_id IN ('CT_TOLL','CT_VEHICLES','ACC_VEHICLE') ORDER BY menu_id, role_id;
--
-- 되돌리기
--
--   DELETE FROM scom.role_menus   WHERE menu_id IN ('CT_TOLL','CT_VEHICLES','ACC_VEHICLE');
--   DELETE FROM scom.system_menus WHERE id      IN ('CT_TOLL','CT_VEHICLES','ACC_VEHICLE');
