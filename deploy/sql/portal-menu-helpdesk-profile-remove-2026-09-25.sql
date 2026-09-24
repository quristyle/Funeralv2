-- ============================================================
-- 「내 프로필」 화면을 메뉴에서 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- `/helpdesk/org/profile` 은 안 쓰는 화면이라 2026-09-25 에 지웠다.
-- 화면(`OrgProfile.razor`)을 없앴으므로 메뉴만 남으면 눌렀을 때
-- 「준비 중」이 뜬다 — 그래서 지운다.
--
-- 백엔드는 건드리지 않았다. 이 화면이 부르던 `auth-links/me` 와
-- `admins/{id}` 는 계정 연결·담당자 화면이 함께 쓰는 것이라 그대로 둔다.
--
-- 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다(ON DELETE CASCADE 라
-- 메뉴만 지워도 따라 지워지지만, 무엇이 사라지는지 눈에 보이도록 먼저 지운다).
-- 2026-09-25 실측으로 다섯 줄이 걸려 있었다.
DELETE FROM scom.role_menus  WHERE menu_id = 'HD_PROFILE';

-- 즐겨찾기도 같은 열쇠를 본다. 지금은 비어 있지만 뒤늦게 눌러 둔 사람이
-- 있으면 여기서 걸린다.
DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_PROFILE';

-- ── 메뉴 ─────────────────────────────────────────────────────
--
-- 「조직 관리」(HD_ORG) 아래 order_no 6 이었다. 형제 셋(팀·팀-고객사·담당자)은
-- 그대로 두므로 묶음은 비지 않는다.
DELETE FROM scom.system_menus WHERE id = 'HD_PROFILE';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id      = 'HD_PROFILE') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id = 'HD_PROFILE') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'HD_PROFILE') AS fav;
