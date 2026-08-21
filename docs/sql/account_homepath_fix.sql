-- ============================================================
-- 사용자별 첫 화면(HomePath) 경로 교정
-- ============================================================
--
-- 로그인 후 첫 화면은 계정별로 관리한다.
--   scom.account_profile_details 의 detail_type = 'HomePath' 행
--   값이 없으면 AuthServer 가 '/workspace' 를 기본으로 준다 (UserService.cs)
--
-- 그런데 4개 계정 전부 '/dashboard/workspace' 로 저장되어 있었다.
-- 실제 라우트는 '/workspace' 다 —
--   scom.system_menus: Workspace, path='/workspace',
--                      component='#/views/portal/dashboard/workspace/index.vue'
-- '/dashboard' 는 화면이 없는 디렉터리(CATALOG)이므로
-- '/dashboard/workspace' 라는 경로는 존재하지 않아 404 가 떴다.
-- (component 경로에 dashboard 가 들어가는 것과 라우트 경로는 별개다.)
--
-- 반복 실행해도 안전하다.

BEGIN;

UPDATE scom.account_profile_details d
SET content    = '/workspace',
    updated_at = now(),
    updated_by = 'homepath-fix'
WHERE d.detail_type = 'HomePath'
  AND d.is_deleted = false
  AND d.content = '/dashboard/workspace';

COMMIT;

-- 확인: 저장된 홈 경로가 실제 활성 메뉴 경로와 맞는지
SELECT a.user_id                       AS 계정,
       d.content                       AS 홈경로,
       CASE WHEN m.id IS NULL THEN '없는 경로!' ELSE m.name END AS 대상메뉴
FROM scom.accounts a
JOIN scom.account_profile_details d
  ON d.account_id = a.id AND d.detail_type = 'HomePath' AND d.is_deleted = false
LEFT JOIN scom.system_menus m
  ON m.path = d.content AND m.status = 1 AND m.is_deleted = false
WHERE a.is_deleted = false
ORDER BY a.user_id;
