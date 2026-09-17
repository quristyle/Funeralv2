-- ============================================================
-- AI 작업 화면 둘을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md 11장
--
-- **표를 만드는 SQL(projmng-ai-tasks-…)과 DB 가 다르다.** 이쪽은 포털 DB
-- (jsiniportal/scom)고 저쪽은 프로젝트관리 DB(projmng/projmng)다.
--
-- 두 번 돌려도 안전하다 — 전부 「없을 때만」 넣는다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
-- 라우팅은 화면의 `@page` 가 갖는다. DB 는 URL 을 모른다. 사이드바가 링크를
-- 걸 때 보는 것은 `route_key` 하나이고, 그 값이 화면의 `[RouteKey(...)]` 와
-- 같아야 한다. 다르면 오류 없이 **「준비 중」이 뜬다.**
--
--   projmng.ai.tasks   → Components/Pages/AiTaskList.razor
--   projmng.ai.targets → Components/Pages/AiTargetList.razor
--
-- `component` 칸은 Vue 시절 잔재라 더 이상 읽지 않는다. 비워 둔다.

-- ── 묶음 ─────────────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive)
SELECT 'PM_AI', 'PmAi', '/projmng/ai', 'PROJMNG', 'CATALOG', 'AI 작업',
       'lucide:bot', 90, false, 1, now(), 'ai-task-setup', true
WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI');

-- ── 화면 둘 ──────────────────────────────────────────────────
--
-- 「작업 지시」가 먼저다. 대상 관리는 처음 한 번 등록하고 나면 거의 안 여는
-- 화면이라 뒤에 둔다.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_TASKS', 'PmAiTasks', '/projmng/ai/tasks', 'PM_AI', 'MENU', 'AI 작업 지시',
       'lucide:sparkles', 1, false, 1, now(), 'ai-task-setup', true,
       true, true, true, true, true, true, false,
       'projmng.ai.tasks'
WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_TASKS');

-- 대상 등록은 **경로를 적는 유일한 자리**다(설계 9.5). 권한을 따로 줄 수
-- 있게 메뉴를 나눠 두었다 — 작업은 여럿이 쓰고 대상은 소수만 고치는 그림.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_TARGETS', 'PmAiTargets', '/projmng/ai/targets', 'PM_AI', 'MENU', 'AI 작업 대상',
       'lucide:folder-cog', 2, false, 1, now(), 'ai-task-setup', true,
       true, true, true, true, true, true, false,
       'projmng.ai.targets'
WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_TARGETS');

-- ── 권한 ─────────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- 지금 프로젝트관리 메뉴를 보는 세 역할에 같은 모양으로 준다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다(실제로 밟음).
     is_deleted, created_at, created_by)
SELECT r.role_id, m.menu_id,
       true, true, true, true, true,
       false, true,
       false, false, false, false, false, false, false, false,
       false, now(), 'ai-task-setup'
  FROM (VALUES ('PROJMNG_ADMIN'), ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS r(role_id)
 CROSS JOIN (VALUES ('PM_AI'), ('PM_AI_TASKS'), ('PM_AI_TARGETS')) AS m(menu_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = r.role_id AND x.menu_id = m.menu_id
 );
