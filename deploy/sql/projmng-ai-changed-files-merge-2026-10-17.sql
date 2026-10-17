-- ============================================================
-- AI 작업 「처리 파일」 합치기 도장 — projmng.ai_task_run.files_merged_at (2026-10-17)
-- ============================================================
--
-- [무엇이 문제였나]
--
-- `changed_files` 를 채우는 자리는 **실행기 하나뿐**이었다
-- (`AiTaskRunner.Workspace.ChangedFilesAsync`). 실행기가 그 값을 안 보내면 끝이고
-- 아무도 다시 채워 주지 않는다 — 운영에서 **최근 2주 276건 전부**가 그랬다
-- (`select count(*), count(changed_files) from projmng.ai_task_run` 로 확인).
-- 운영 실행기 바이너리가 그 칸이 생기기 전(2026-10-12) 것이라 보낼 줄을 몰랐고,
-- 그 바이너리는 GHCR 배포와 다른 길로 가므로 main 에 올려도 바뀌지 않는다.
--
-- 화면의 「처리 파일」 탭은 그동안 늘 「없음」이었고, 그것이 **정말 안 고친 건**인지
-- **값이 안 온 건**인지 가릴 길이 없었다.
--
-- [어떻게 고치나]
--
-- 채우는 길을 둘로 둔다.
--
--   ① 실행기가 CLI 가 끝나자마자 `git diff --name-status` 로 물어 보낸 목록 — **정본**
--   ② 결과문을 LLM 서버에게 읽혀 뽑은 목록 — **보강**
--      (`AiResultSummarizer.ExtractFilesAsync`)
--
-- 둘을 합쳐 다시 `changed_files` 에 적는다(`AiChangedFiles.Merge`).
-- **git 이 본 것이 언제나 이기고**, git 이 못 본 줄만 상태 글자 뒤에 `?` 를 달고
-- 뒤에 붙는다 — `M?` 는 「결과문에서 뽑은 것, git 으로 확인되지 않음」이다.
-- 화면과 메일은 그 줄에 「추정」을 적어 **둘을 섞지 않는다**.
--
-- 이 칸은 그 합치기를 **이미 했다는 도장**이다. 없으면 완료 뒤처리와 따라잡기
-- (`AiSummaryCatchUp`)가 같은 건에 모델을 몇 번씩 다시 부르고, 부를 때마다 조금씩
-- 다른 답이 와서 목록이 이유 없이 흔들린다.
--
-- **널인 채로 두는 것이 「다시 보라」는 뜻**이다. 합칠 것이 양쪽 다 없었던 건은
-- 도장을 안 찍어 따라잡기가 다시 집는다(`AiRunFileWriter.EnsureAsync`).
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-ai-changed-files-merge-2026-10-17.sql
--

ALTER TABLE projmng.ai_task_run
    ADD COLUMN IF NOT EXISTS files_merged_at timestamptz;

COMMENT ON COLUMN projmng.ai_task_run.files_merged_at IS
    '「처리 파일」 목록(changed_files)을 git 목록과 결과문에서 뽑은 목록으로 합친 시각. 널이면 아직 안 합친 것이라 AiSummaryCatchUp 이 집어 간다.';

-- 따라잡기가 「아직 안 합친 건」을 끝에서부터 집는다. 그 질의가 보는 것이
-- 이 셋뿐이라 함께 묶어 둔다 — 이 표는 실행마다 한 줄씩 늘어난다.
CREATE INDEX IF NOT EXISTS ix_ai_task_run_files_pending
    ON projmng.ai_task_run (finished_at DESC)
 WHERE files_merged_at IS NULL AND finished_at IS NOT NULL;
