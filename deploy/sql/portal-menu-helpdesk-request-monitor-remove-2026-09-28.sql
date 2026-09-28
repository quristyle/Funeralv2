-- ============================================================
-- 「요청 모니터」 화면을 메뉴에서 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- `/helpdesk/request/monitor` 가 보여 주던 것은 셋이다 —
-- 담당자별 처리 현황 · 고객사별 처리 현황 · 최근 접수 목록.
-- 지금은 **헬프데스크 현황(`/helpdesk/dashboard`)이 그 셋을 모두 보여 준다**
-- (고객사별·담당자별은 2026-09-28 에 카드로 바뀌었고 최근 접수는 두 번 누르면
-- 상세로 간다). 같은 자료를 두 자리에서 보는 셈이라 화면을 지웠다.
--
-- 화면(`RequestMonitor.razor`)을 없앴으므로 메뉴만 남으면 눌렀을 때
-- 「준비 중」이 뜬다 — 그래서 여기서 메뉴도 지운다.
--
-- 엔드포인트는 **지우지 않는다.** `dashboard/all-admin-stats` ·
-- `dashboard/company-stats` · `dashboard/requests/recent` 는 현황 화면이 그대로 쓴다.
--
-- 포털 DB(jsiniportal)에 돌린다. 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다. 권한 줄이 남아 있으면
-- 메뉴 줄이 외래키에 걸려 안 지워진다 — 순서를 바꾸면 안 된다.
DELETE FROM scom.role_menus  WHERE menu_id = 'HD_REQ_MONITOR';

-- 즐겨찾기도 같은 열쇠를 본다.
DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_MONITOR';

-- ── 메뉴 ─────────────────────────────────────────────────────
DELETE FROM scom.system_menus WHERE id = 'HD_REQ_MONITOR';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id      = 'HD_REQ_MONITOR') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id = 'HD_REQ_MONITOR') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_MONITOR') AS fav;
