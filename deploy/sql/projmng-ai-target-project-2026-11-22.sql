-- ============================================================
-- AI 작업 대상 - 프로젝트 대상 연결 (projmng 스키마)
-- ============================================================
--
-- AI 작업대상(projmng.ai_target)을 프로젝트(/projmng/proj/manage, projmng.dev_proj)와 연결한다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-ai-target-project-2026-11-22.sql
--

BEGIN;

-- dev_proj 테이블에 PK 제약조건 추가 (없는 경우)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'pk_dev_proj'
    ) THEN
        ALTER TABLE projmng.dev_proj ADD CONSTRAINT pk_dev_proj PRIMARY KEY (prj_rid);
    END IF;
END $$;

-- ai_target 에 prj_rid 컬럼 추가 (dev_proj 참조)
ALTER TABLE projmng.ai_target
    ADD COLUMN IF NOT EXISTS prj_rid int REFERENCES projmng.dev_proj (prj_rid);

CREATE INDEX IF NOT EXISTS ix_ai_target_prj_rid
    ON projmng.ai_target (prj_rid) WHERE is_deleted = false;

-- 기본 연결 데이터 반영
UPDATE projmng.ai_target
   SET prj_rid = 1
 WHERE prj_rid IS NULL
   AND (target_nm ILIKE '%portal%' OR target_path ILIKE '%funeral%');

UPDATE projmng.ai_target
   SET prj_rid = 9
 WHERE prj_rid IS NULL
   AND (target_nm ILIKE '%tdms%' OR target_path ILIKE '%tdms%');

UPDATE projmng.ai_target
   SET prj_rid = 10
 WHERE prj_rid IS NULL
   AND (target_nm = 'ghub' OR target_path ILIKE '%/ghub%');

COMMIT;
