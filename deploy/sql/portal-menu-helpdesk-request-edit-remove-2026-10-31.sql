-- ============================================================
-- 「요청 수정」 메뉴를 지운다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- `/helpdesk/request/edit/:id`(`HD_REQ_EDIT`)는 화면(`RequestEdit.razor`)을
-- 이미 걷어내고 메뉴만 `status = 0` 으로 재워 두었던 것이다
-- (`helpdesk-request-close-2026-09-28.sql`). 같은 일은 요청 상세가
-- 「접수」·「완료」·「종료」 단추로 한다.
--
-- 지우기 전 실측: 그 경로를 `@page` 로 가진 화면 없음 · 코드에서 그 메뉴 아이디나
-- 경로를 부르는 곳 없음 · `menu_usage_logs` 0건 · `blocked_access_logs` 0건 ·
-- 하위 메뉴 0 · 즐겨찾기 0. 권한 줄 일곱 중 여섯은 이미 `is_deleted` 였고
-- 남은 하나(SERVER_ADMIN)도 꺼진 메뉴를 가리키고 있었다.
--
-- 포털 DB(jsiniportal)에 돌린다. 두 번 돌려도 안전하다.

-- `role_menus` · `menu_favorites` 가 외래키로 `system_menus.id` 를 본다 — 먼저 지운다.
DELETE FROM scom.role_menus     WHERE menu_id = 'HD_REQ_EDIT';
DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_EDIT';

DELETE FROM scom.system_menus   WHERE id = 'HD_REQ_EDIT';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id      = 'HD_REQ_EDIT') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id = 'HD_REQ_EDIT') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_EDIT') AS fav;
