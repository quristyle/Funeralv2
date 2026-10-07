-- ============================================================
-- 「내 차량」을 설정 묶음의 「나의 차량등록」으로 옮긴다 (scom · 포털 DB)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.CargoTrust/Components/Pages/MyVehiclePage.razor
--       @page "/cargotrust/vehicles" · [RouteKey("cargotrust.vehicles")]
--
-- 앞선 일: deploy/sql/portal-menu-toll-vehicle-2026-10-06.sql 이 이 줄을
--          운송관리 묶음(CARGOTRUST)에 세웠다. 여기서 자리와 이름만 옮긴다.
--
-- [왜 설정으로 옮기나]
--
-- 차량을 등록하는 자리가 둘이다 — 관리자가 남의 차를 매다는 **차량 관리**
-- (포털관리 › 차량 관리)와, 본인이 자기 차를 건사하는 **나의 차량등록**.
-- 뒤엣것은 거래·결제처럼 날마다 하는 업무가 아니라 **한 번 맞춰 두고 마는 것**이라,
-- 운송관리 업무 메뉴 사이에 끼어 있는 것보다 설정 쪽이 찾기 쉽다.
-- 환경설정·업무 설정과 같은 결이다.
--
-- [화면은 그대로 둔다]
--
-- 메뉴가 어느 묶음에 서 있든 화면의 주소(`@page`)는 제 모듈 접두사로 시작해야
-- 한다(web/CLAUDE.md 의존 규칙 3). 그래서 주소는 `/cargotrust/vehicles` 그대로이고
-- 바뀌는 것은 **메뉴의 자리와 제목**뿐이다. 사이드바는 `route_key` 로 주소를 푼다.
--
-- [path 는 안 바꾼다]
--
-- `path` 는 역할-메뉴 권한표와 즐겨찾기의 열쇠다. 바꾸면 그 메뉴의 권한이
-- 조용히 끊기고, 끊기는 방향이 *권한이 없는데 메뉴가 보이는* 쪽이라 특히 나쁘다.
-- 묶음(pid)과 제목만 옮긴다.
--
--   · 멱등하다. 여러 번 돌려도 같은 자리에 선다.
--   · 되돌리기는 맨 아래.

BEGIN;

-- 설정 묶음의 맨 뒤로. 형제(환경설정 0 · 업무 설정 1)의 번호와 겹치지 않게
-- 가장 큰 번호 다음을 쓴다 — 겹치면 둘의 순서가 조회마다 달라진다.
WITH spot AS (
    SELECT COALESCE(MAX(order_no), -1) + 1 AS order_no
      FROM scom.system_menus
     WHERE pid = 'SETTING'
       AND is_deleted = false
       AND id <> 'CT_VEHICLES'
)
UPDATE scom.system_menus m
   SET pid        = 'SETTING',
       title      = '나의 차량등록',
       icon       = 'lucide:truck',
       order_no   = spot.order_no,
       status     = 1,
       hide_in_menu = false,
       updated_at = now(),
       updated_by = 'my-vehicle'
  FROM spot
 WHERE m.id = 'CT_VEHICLES';

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus WHERE pid = 'SETTING' ORDER BY order_no;
--
-- 되돌리기 (운송관리 묶음의 제자리로)
--
--   UPDATE scom.system_menus
--      SET pid = 'CARGOTRUST', title = '내 차량', order_no = 64,
--          updated_at = now(), updated_by = 'my-vehicle-revert'
--    WHERE id = 'CT_VEHICLES';
