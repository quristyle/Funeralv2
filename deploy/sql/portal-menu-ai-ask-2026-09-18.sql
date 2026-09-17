-- ============================================================
-- 「빠른 지시」 화면을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md 11장
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiAsk.razor
--
-- 앞선 `portal-menu-ai-tasks-2026-09-17.sql` 이 만든 `PM_AI` 묶음 아래에
-- 한 줄을 더한다. 그 파일을 아직 안 돌렸으면 **그것부터 돌린다** —
-- 여기서는 묶음을 만들지 않는다(부모가 없으면 안 넣고 조용히 넘어간다).
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
--   projmng.ai.ask → Components/Pages/AiAsk.razor  (`@page "/projmng/ai/ask"`)
--
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이
-- **「준비 중」이 뜬다.**

-- ── 화면 ─────────────────────────────────────────────────────
--
-- **맨 앞에 둔다**(order_no = 0). 이 화면의 요점이 「빠르게 닿는 것」이라
-- 목록에서 세 번째에 있으면 그 요점이 반쯤 사라진다.
--
-- 권한은 조회와 등록만 준다. 이 화면은 고치지도 지우지도 않고, 엑셀도
-- 인쇄도 없다 — 없는 기능에 권한을 주면 「왜 안 나오나」를 나중에 뒤지게 된다.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_ASK', 'PmAiAsk', '/projmng/ai/ask', 'PM_AI', 'MENU', '빠른 지시',
       'lucide:message-square-plus', 0, false, 1, now(), 'ai-ask-setup', true,
       true, true, true, false, false, false, false,
       'projmng.ai.ask'
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_ASK')
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI');

-- ── 권한 ─────────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- 「AI 작업 지시」를 보는 역할과 같게 준다 — 같은 자료를 다른 자리에서
-- 만드는 화면이라, 한쪽만 보이면 그게 더 헷갈린다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다(실제로 밟음).
     is_deleted, created_at, created_by)
SELECT r.role_id, 'PM_AI_ASK',
       true, true, true, false, false,
       false, false,
       false, false, false, false, false, false, false, false,
       false, now(), 'ai-ask-setup'
  FROM (VALUES ('PROJMNG_ADMIN'), ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS r(role_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = r.role_id AND x.menu_id = 'PM_AI_ASK'
 );

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, order_no
--   FROM scom.system_menus WHERE pid = 'PM_AI' ORDER BY order_no;
