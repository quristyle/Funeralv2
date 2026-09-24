-- ============================================================
-- 헬프데스크 「조직 관리」 묶음을 메뉴에서 통째로 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 조직(팀·담당자)과 계정은 JSini 관리 포털(AuthServer)이 단독으로 맡는다.
-- 헬프데스크가 같은 것을 또 보여 줄 까닭이 없어 2026-09-25 에 지웠다.
--
-- 지우는 것은 묶음 하나와 그 아래 셋이다.
--
--   HD_ORG          조직 관리   (CATALOG, /helpdesk/org)
--   HD_ORG_TEAM     팀          (/helpdesk/org/team)
--   HD_ORG_TEAM_COM 팀-고객사   (/helpdesk/org/team-company)
--   HD_ORG_ADMIN    담당자      (/helpdesk/org/admin)
--
-- 같은 묶음에 있던 「내 프로필」(HD_PROFILE)은 앞서
-- `portal-menu-helpdesk-profile-remove-2026-09-25.sql` 로 지웠다.
-- 그것이 마지막 형제였으므로 이번에 묶음까지 비워 없앤다.
--
-- 화면(`TeamList.razor`·`TeamCompany.razor`·`AdminList.razor`)을 함께
-- 지웠으므로 메뉴만 남기면 눌렀을 때 「준비 중」이 뜬다 — 그래서 지운다.
--
-- 백엔드도 같은 커밋에서 걷어냈다(`/api/admins` 묶음 전부,
-- `/api/teams` 의 등록·수정·삭제·검색·팀고객사, `dashboard/teams/workload`).
-- `GET /api/teams` 는 남겼다 — 프로젝트 관리 화면이 팀을 고르는 데 쓴다.
-- 담당자·팀 **레코드**는 그대로다. 요청 배정이 그 열쇠를 참조한다.
--
-- 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다(ON DELETE CASCADE 라
-- 메뉴만 지워도 따라 지워지지만, 무엇이 사라지는지 눈에 보이도록 먼저 지운다).
-- 2026-09-25 실측으로 넷에 각각 다섯 줄, 모두 스무 줄이 걸려 있었다.
DELETE FROM scom.role_menus
 WHERE menu_id IN ('HD_ORG', 'HD_ORG_TEAM', 'HD_ORG_TEAM_COM', 'HD_ORG_ADMIN');

-- 즐겨찾기도 같은 열쇠를 본다. 2026-09-25 에는 비어 있었지만 뒤늦게 눌러 둔
-- 사람이 있으면 여기서 걸린다.
DELETE FROM scom.menu_favorites
 WHERE menu_id IN ('HD_ORG', 'HD_ORG_TEAM', 'HD_ORG_TEAM_COM', 'HD_ORG_ADMIN');

-- ── 메뉴 ─────────────────────────────────────────────────────
--
-- 자식을 먼저 지운다. `pid` 는 외래키가 아니라 글자라 순서를 안 지켜도
-- 에러는 안 나지만, 중간에 멈췄을 때 부모 없는 자식이 남지 않게 한다.
DELETE FROM scom.system_menus
 WHERE id IN ('HD_ORG_TEAM', 'HD_ORG_TEAM_COM', 'HD_ORG_ADMIN');

DELETE FROM scom.system_menus WHERE id = 'HD_ORG';

-- 혹시 나중에 같은 자리에 다른 줄이 생겼다면 경로로도 한 번 훑는다.
DELETE FROM scom.system_menus WHERE path LIKE '/helpdesk/org%';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus
--             WHERE id LIKE 'HD_ORG%' OR path LIKE '/helpdesk/org%')        AS menu,
--          (SELECT count(*) FROM scom.role_menus
--             WHERE menu_id LIKE 'HD_ORG%')                                 AS role,
--          (SELECT count(*) FROM scom.menu_favorites
--             WHERE menu_id LIKE 'HD_ORG%')                                 AS fav;
