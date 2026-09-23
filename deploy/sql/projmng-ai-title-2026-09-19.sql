-- AI 작업 완료 후 자동 제목 생성용 컬럼 추가
-- 2026-09-19

ALTER TABLE projmng.ai_task
    ADD COLUMN IF NOT EXISTS title_auto BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS title_run_key BIGINT NULL;

COMMENT ON COLUMN projmng.ai_task.title_auto IS '저장할 때 제목이 비어 있어 서버가 자동 생성했는지 여부. false이면 사람이 직접 적은 제목이므로 기계가 손대지 않는다.';
COMMENT ON COLUMN projmng.ai_task.title_run_key IS '어느 실행을 보고 제목을 다시 지었는지(또는 도장을 찍었는지) 나타내는 run_key. 화면이 TitlePending 여부를 아는 근거.';
