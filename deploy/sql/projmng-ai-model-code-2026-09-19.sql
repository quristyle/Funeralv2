-- AI 작업지시 화면에서 고를 실행기 목록을 프로젝트관리 공통코드로 관리한다.
-- 화면에 실행기 이름을 하드코딩하지 않기 위한 정본이다.

BEGIN;

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_type, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'AI_MODEL', 'AI 모델', '', 'AI', 1
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_cd = 'AI_MODEL' AND COALESCE(cm_pcd, '') = '');

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'claude', 'Claude CLI', 'AI_MODEL', 'claude', 1
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_pcd = 'AI_MODEL' AND cm_cd = 'claude');

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'antigravity', '안티그래비티 (agy)', 'AI_MODEL',
       'antigravity', 2
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_pcd = 'AI_MODEL' AND cm_cd = 'antigravity');

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'copilot', 'Copilot CLI', 'AI_MODEL', 'copilot', 3
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_pcd = 'AI_MODEL' AND cm_cd = 'copilot');

-- 기존 대상도 공통코드에 등록된 Copilot을 선택할 수 있게 한다.
UPDATE projmng.ai_target
   SET runner_kinds = runner_kinds || ',copilot',
       mod_dt = now()
 WHERE is_deleted = false
   AND position(',copilot,' IN ',' || replace(runner_kinds, ' ', '') || ',') = 0;

COMMIT;
