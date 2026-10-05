-- ============================================================
-- 「내 요청」 화면을 메뉴에서 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- `/helpdesk/request/list` 가 보여 주던 것은 **요청자 자신이 올린 요청**이다.
-- 「요청 처리」(`/helpdesk/request/manage`)가 같은 자료를 조건 칸만 더 달고
-- 보여 주므로 — 요청자를 나로 두고 기간을 비우면 처음 올린 것까지 다 나온다 —
-- 화면을 지웠다. 고객은 그 화면에 들어설 때 요청자가 나로 서 있고 「처리 중인
-- 것만」이 꺼져 있으며, **요청자 칸은 바꿀 수 없다**(`RequestManage`) — 걷어낸
-- 화면이 조회에 `customerId` 를 못 박던 것을 그대로 잇는다.
--
-- 화면(`RequestList.razor`)을 없앴으므로 메뉴만 남으면 눌렀을 때 「준비 중」이
-- 뜬다 — 그래서 여기서 메뉴도 지운다.
--
-- **권한이 좁아지는 사람은 없다.** 지우기 전 실측으로 `HD_REQ_MNG` 의 역할이
-- `HD_REQ_LIST` 와 같은 여섯이었다 — ADMINISTRATOR · FUNERAL_OPERATOR ·
-- HELPDESK_USER · PARTNER · PARTNER_ADMINISTRATOR · SYSTEM_ADMINISTRATOR.
--
-- 엔드포인트는 **지우지 않는다.** `requests/srch` 는 요청 처리 화면이 그대로 쓴다.
--
-- 포털 DB(jsiniportal)에 돌린다. 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다. 권한 줄이 남아 있으면
-- 메뉴 줄이 외래키에 걸려 안 지워진다 — 순서를 바꾸면 안 된다.
DELETE FROM scom.role_menus  WHERE menu_id = 'HD_REQ_LIST';

-- 즐겨찾기도 같은 열쇠를 본다(지우기 전 실측 0건이지만 두고 간다).
DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_LIST';

-- ── 메뉴 ─────────────────────────────────────────────────────
DELETE FROM scom.system_menus WHERE id = 'HD_REQ_LIST';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id      = 'HD_REQ_LIST') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id = 'HD_REQ_LIST') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'HD_REQ_LIST') AS fav;
