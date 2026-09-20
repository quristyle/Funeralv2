-- ============================================================
-- 「AI 작업 현황」 화면을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiDashboard.razor
-- 집계: microservices/ProjMngServer/Services/AiDashboardService.cs
--
-- 앞선 `portal-menu-ai-tasks-2026-09-17.sql` 이 만든 `PM_AI` 묶음 아래에
-- 한 줄을 더한다. 그 파일을 아직 안 돌렸으면 **그것부터 돌린다** —
-- 여기서는 묶음을 만들지 않는다(부모가 없으면 안 넣고 조용히 넘어간다).
--
-- 한도 스냅샷 표는 `projmng-ai-usage-2026-09-20.sql` 이 만든다. 그것을
-- 안 돌려도 화면은 뜨지만 **「작업지시 CLI 한도」 칸이 통째로 비고**
-- 집계 조회가 그 표를 못 찾아 끊긴다 — 둘을 같이 돌린다.
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
--   projmng.ai.dashboard → Components/Pages/AiDashboard.razor
--                          (`@page "/projmng/ai/dashboard"`)
--
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이
-- **「준비 중」이 뜬다.**

-- ── 화면 ─────────────────────────────────────────────────────
--
-- **맨 앞에 둔다**(order_no = -1). 현황은 「무엇을 할까」를 정하기 전에
-- 보는 화면이라, 지시 화면들보다 뒤에 있으면 늘 지나치고 나서 돌아오게 된다.
-- 「빠른 지시」가 0 이므로 그보다 앞이 되려면 음수여야 한다.
--
-- 권한은 조회·검색·엑셀만 준다. 이 화면은 아무것도 만들지 않고 고치지도
-- 지우지도 않는다 — 없는 기능에 권한을 주면 「왜 안 나오나」를 나중에 뒤진다.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_DASH', 'PmAiDashboard', '/projmng/ai/dashboard', 'PM_AI', 'MENU', 'AI 작업 현황',
       'lucide:bar-chart-3', -1, false, 1, now(), 'ai-dashboard-setup', true,
       true, true, false, false, false, true, false,
       'projmng.ai.dashboard'
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_DASH')
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI');

-- ── 권한 ─────────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- 「AI 작업 지시」를 보는 역할과 같게 준다 — 같은 자료를 세는 화면이라
-- 한쪽만 보이면 그게 더 헷갈린다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다(실제로 밟음).
     is_deleted, created_at, created_by)
SELECT r.role_id, 'PM_AI_DASH',
       true, true, false, false, false,
       false, true,
       false, false, false, false, false, false, false, false,
       false, now(), 'ai-dashboard-setup'
  FROM (VALUES ('PROJMNG_ADMIN'), ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS r(role_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = r.role_id AND x.menu_id = 'PM_AI_DASH'
 );

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, order_no
--   FROM scom.system_menus WHERE pid = 'PM_AI' ORDER BY order_no;
