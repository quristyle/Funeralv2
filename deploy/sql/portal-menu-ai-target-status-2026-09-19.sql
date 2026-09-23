-- ============================================================
-- 「대상 git 상태」 화면을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md 11.5
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiTargetStatusList.razor
--
-- 앞선 `portal-menu-ai-tasks-2026-09-17.sql` 이 만든 `PM_AI` 묶음 아래에
-- 한 줄을 더한다. 그 파일을 아직 안 돌렸으면 **그것부터 돌린다** —
-- 여기서는 묶음을 만들지 않는다(부모가 없으면 안 넣고 조용히 넘어간다).
--
-- **표를 만드는 SQL(projmng-ai-target-status-…)과 DB 가 다르다.** 이쪽은
-- 포털 DB(jsiniportal/scom)고 저쪽은 프로젝트관리 DB(projmng/projmng)다.
-- 둘 다 돌려야 화면이 자료를 본다.
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
--   projmng.ai.target-status → AiTargetStatusList.razor
--                              (`@page "/projmng/ai/target-status"`)
--
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이
-- **「준비 중」이 뜬다.**

-- ── 화면 ─────────────────────────────────────────────────────
--
-- 「AI 작업 대상」 바로 뒤에 둔다(order_no = 3). 같은 자료를 보는 짝이라
-- 붙여 놓는 것이 맞다 — 한쪽은 등록하는 자리고 한쪽은 들여다보는 자리다.
--
-- 권한은 **조회와 등록만** 준다. 「등록」은 새 대상을 만드는 권한이 아니라
-- 이 화면의 「지금 확인」이 그 자리에 걸려 있기 때문이다(`MenuAction.Create`).
-- 고치고 지우는 일이 없으므로 나머지는 주지 않는다 — 없는 기능에 권한을
-- 주면 「왜 안 나오나」를 나중에 뒤지게 된다.
--
-- 엑셀은 켠다. 대상이 늘면 이 표를 그대로 떠서 쓰는 일이 생긴다.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_TARGET_STATUS', 'PmAiTargetStatus', '/projmng/ai/target-status', 'PM_AI',
       'MENU', '대상 git 상태',
       'lucide:git-branch', 3, false, 1, now(), 'ai-target-status-setup', true,
       true, true, true, false, false, true, false,
       'projmng.ai.target-status'
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_TARGET_STATUS')
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI');

-- ── 권한 ─────────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- 「AI 작업 대상」을 보는 역할과 같게 준다. 대상 등록만큼 조심할 화면은
-- 아니지만(여기서는 아무것도 고치지 않는다) 같은 자료라 같이 묶어 둔다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다(실제로 밟음).
     is_deleted, created_at, created_by)
SELECT r.role_id, 'PM_AI_TARGET_STATUS',
       true, true, true, false, false,
       false, true,
       false, false, false, false, false, false, false, false,
       false, now(), 'ai-target-status-setup'
  FROM (VALUES ('PROJMNG_ADMIN'), ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS r(role_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = r.role_id AND x.menu_id = 'PM_AI_TARGET_STATUS'
 );

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, order_no
--   FROM scom.system_menus WHERE pid = 'PM_AI' ORDER BY order_no;
