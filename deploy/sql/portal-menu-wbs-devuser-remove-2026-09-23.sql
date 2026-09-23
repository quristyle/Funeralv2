-- ============================================================
-- 개발자 관리 화면을 메뉴에서 걷어낸다 (scom · 포털 DB)
-- ============================================================
--
--   PM_WBS_DEVUSER  개발자 관리  /projmng/wbs/dev-users
--
-- 그 화면이 다루던 속성(사번 · 직급 · 장비 대장 · 계정 발급 현황 · 옷 치수)은
-- **포털 계정관리**(`/admin/system/account`)로 옮겼다 —
-- `scom.account_profile_details` 의 `Dev.*`. 경위와 설계는
-- `docs/projmng-account-merge.md`.
--
-- 화면·API·명부 서비스를 같은 변경에서 함께 지웠다 — 메뉴만 남기면
-- **누르면 「준비 중」이 뜬다.**
--
-- 두 번 돌려도 안전하다.
--
-- ── 같은 변경에서 함께 바뀐 것 ──────────────────────────────
--
-- 대시보드가 사람 이름을 얻는 길이 바뀌었다. 서버가 `wbs_user` 를 조인해
-- 실어 보내던 것을(11곳) 걷어내고 **화면이 포털 계정 목록에서 붙인다** —
-- 원장은 `projmng` DB, 계정은 `jsiniportal` DB 라 SQL 조인이 아예 불가능하다.
--
-- 계정과 안 이어진 사람(퇴사자 · 외부 인력 · 오타)은 **적힌 값 그대로**
-- 보인다. 「미할당」으로 덮으면 오타인지 퇴사자인지 가려낼 수 없다.
--
-- ── 화면 설정의 주인도 바뀌었다 ─────────────────────────────
--
-- `projmng.wbs_user_pref` 의 주인을 `login_id → bp_id` 로 풀던 단계가
-- 없어지고 **로그인 아이디가 곧 열쇠**다. 이미 사번으로 담긴 설정은 그
-- 사람에게 안 보인다 — 화면 설정이라 다시 고르면 그만이다.
--
-- ── 표도 지웠다 ────────────────────────────────────────────
--
--   projmng.wbs_user
--
-- 같은 날 지웠다(`projmng-wbs-user-drop-2026-09-23.sql`). 0건이었고 물고 있는
-- 외래키도, 보는 뷰도 없었다. 사번 → 계정 대조는 이제 계정관리에 적은 사번
-- (`Dev.BpId`)에서 나온다.
--
-- (`wbs_user_pref` 는 남는다 — 이름만 비슷하고 화면 설정 표다.)

BEGIN;

DELETE FROM scom.menu_favorites WHERE menu_id = 'PM_WBS_DEVUSER';
DELETE FROM scom.role_menus     WHERE menu_id = 'PM_WBS_DEVUSER';
DELETE FROM scom.system_menus   WHERE id      = 'PM_WBS_DEVUSER';

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path FROM scom.system_menus WHERE pid = 'PM_WBS' ORDER BY order_no;
