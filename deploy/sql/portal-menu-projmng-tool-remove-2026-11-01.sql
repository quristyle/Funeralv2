-- ============================================================
-- 프로젝트관리 「개발 도구」 묶음과 그 아래 화면 넷을 지운다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 쓰지 않는 화면이라 걷어냈다 — 화면(`SheetView` · `FastTest` · `ComTest` ·
-- `ComponentView`)과 그것들만 쓰던 `ProjectGrid` 를 함께 지웠다.
--
--   PM_TOOL_SHEET      /projmng/tool/sheet
--   PM_TOOL_FAST       /projmng/tool/fast-test
--   PM_TOOL_COMTEST    /projmng/tool/com-test
--   PM_TOOL_COMPONENT  /projmng/tool/component
--   PM_TOOL            /projmng/tool   (묶음 — 자식이 위 넷뿐이라 비면 빈 묶음만 남는다)
--
-- 지우기 전 실측: 하위 메뉴는 위 넷뿐 · 권한 줄 각 5 · 즐겨찾기 0.
-- `menu_usage_logs` 는 외래키가 없고 지난 기록이라 그대로 둔다.
--
-- 포털 DB(jsiniportal)에 돌린다. 두 번 돌려도 안전하다.

-- `role_menus` · `menu_favorites` 가 외래키로 `system_menus.id` 를 본다 — 먼저 지운다.
DELETE FROM scom.role_menus     WHERE menu_id IN ('PM_TOOL', 'PM_TOOL_SHEET', 'PM_TOOL_FAST', 'PM_TOOL_COMTEST', 'PM_TOOL_COMPONENT');
DELETE FROM scom.menu_favorites WHERE menu_id IN ('PM_TOOL', 'PM_TOOL_SHEET', 'PM_TOOL_FAST', 'PM_TOOL_COMTEST', 'PM_TOOL_COMPONENT');

DELETE FROM scom.system_menus   WHERE id IN ('PM_TOOL_SHEET', 'PM_TOOL_FAST', 'PM_TOOL_COMTEST', 'PM_TOOL_COMPONENT');
DELETE FROM scom.system_menus   WHERE id = 'PM_TOOL';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id LIKE 'PM_TOOL%' OR path LIKE '/projmng/tool%') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id LIKE 'PM_TOOL%') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id LIKE 'PM_TOOL%') AS fav;
