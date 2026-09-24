-- ============================================================
-- 「AI 작업 요청」 화면을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiRequestList.razor
--
-- 앞선 `portal-menu-ai-tasks-2026-09-17.sql` 이 만든 `PM_AI` 묶음 아래에
-- 한 줄을 더한다. 그 파일을 아직 안 돌렸으면 **그것부터 돌린다** —
-- 여기서는 묶음을 만들지 않는다(부모가 없으면 안 넣고 조용히 넘어간다).
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
--   projmng.ai.request → Components/Pages/AiRequestList.razor
--                        (`@page "/projmng/ai/request"`)
--
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이
-- **「준비 중」이 뜬다.**

-- ── 화면 ─────────────────────────────────────────────────────
--
-- 「빠른 지시」(order_no 0) 바로 앞에 둔다(-1 은 대시보드가 쓰고 있으므로
-- 그 사이를 쓴다). 이 묶음에서 **가장 많은 사람이 여는 화면**이 이것이라,
-- 맨 아래에 두면 관리자 전용 화면 넷을 지나야 닿는다.
--
-- 권한은 조회·등록·수정·삭제를 준다. 「빠른 지시」와 달리 **올린 뒤에도
-- 고치고 거둬들일 수 있는** 화면이라, 없는 권한을 주는 것이 아니다 —
-- 다만 서버가 그 셋을 「내가 올렸고 관리자가 아직 안 건드린 건」으로 한 번 더
-- 좁힌다(`AiTaskService.UpdateUserRequestAsync`).
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_AI_REQ', 'PmAiRequest', '/projmng/ai/request', 'PM_AI', 'MENU', 'AI 작업 요청',
       'lucide:clipboard-pen', 0, false, 1, now(), 'ai-request-setup', true,
       true, true, true, true, true, false, false,
       'projmng.ai.request'
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI_REQ')
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_AI');

-- 「빠른 지시」를 한 칸 뒤로 민다. 둘 다 0 이면 순서가 사실상 무작위다.
UPDATE scom.system_menus SET order_no = 1 WHERE id = 'PM_AI_ASK' AND order_no = 0;
UPDATE scom.system_menus SET order_no = 2 WHERE id = 'PM_AI_TASKS' AND order_no = 1;
UPDATE scom.system_menus SET order_no = 3 WHERE id = 'PM_AI_TARGETS' AND order_no = 2;
UPDATE scom.system_menus SET order_no = 4 WHERE id = 'PM_AI_TARGET_STATUS' AND order_no = 3;

-- ── 권한 ─────────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- **관리자 셋만 주면 이 화면을 만든 뜻이 없어진다.** 「AI 작업」 묶음의
-- 다른 화면 넷은 전부 관리자 전용이고, 이 화면만 **일반 사용자가 쓰는
-- 자리**다. 그래서 프로젝트관리 업무 역할 둘
-- (`PROJMNG_JSINITEAM` · `PROJMNG_MNM_SMG`)에도 함께 준다.
--
-- 묶음(`PM_AI`)에는 권한을 따로 주지 않는다 — 사이드바는 자식이 하나라도
-- 남으면 부모를 남긴다(`MenuFilter.Filter`).
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다.
     is_deleted, created_at, created_by)
SELECT r.role_id, 'PM_AI_REQ',
       true, true, true, true, true,
       false, false,
       false, false, false, false, false, false, false, false,
       false, now(), 'ai-request-setup'
  FROM (VALUES ('PROJMNG_ADMIN'), ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR'),
               ('PROJMNG_JSINITEAM'), ('PROJMNG_MNM_SMG')) AS r(role_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus x
      WHERE x.role_id = r.role_id AND x.menu_id = 'PM_AI_REQ'
 );

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, order_no
--   FROM scom.system_menus WHERE pid = 'PM_AI' ORDER BY order_no;
-- SELECT role_id, can_view, can_create, can_update, can_delete
--   FROM scom.role_menus WHERE menu_id = 'PM_AI_REQ' ORDER BY role_id;
