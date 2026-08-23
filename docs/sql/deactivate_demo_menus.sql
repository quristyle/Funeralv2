-- ============================================================
-- vben 템플릿 예제 메뉴 비활성화  (실행 전 확인 필요 — D4)
-- ============================================================
--
-- 활성 메뉴 231개 중 102개가 vben 템플릿 예제다(/demos, /examples, /vben-admin).
-- 운영 사용자의 메뉴 트리에 그대로 노출된다.
--
-- 지우지 않고 status = 0 으로 내린다. 개발자가 참고할 때 되돌리면 된다.
-- 되돌리는 SQL 은 파일 맨 아래에 있다.
--
-- 반복 실행해도 안전하다.

BEGIN;

UPDATE scom.system_menus
SET status     = 0,
    updated_at = now(),
    updated_by = 'demo-menu-cleanup'
WHERE is_deleted = false
  AND status = 1
  AND (path LIKE '/demos%' OR path LIKE '/examples%' OR path LIKE '/vben-admin%');

COMMIT;

-- 확인
SELECT
  count(*) FILTER (WHERE status = 1) AS 활성,
  count(*) FILTER (WHERE status = 0) AS 비활성
FROM scom.system_menus
WHERE is_deleted = false;


-- ============================================================
-- 되돌리기
-- ============================================================
-- BEGIN;
-- UPDATE scom.system_menus
-- SET status = 1, updated_at = now(), updated_by = 'demo-menu-restore'
-- WHERE is_deleted = false
--   AND updated_by = 'demo-menu-cleanup'
--   AND (path LIKE '/demos%' OR path LIKE '/examples%' OR path LIKE '/vben-admin%');
-- COMMIT;
