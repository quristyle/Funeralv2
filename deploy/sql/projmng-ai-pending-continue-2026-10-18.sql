-- ============================================================
-- 작업 진행 중 이어서 지시 사전 예약 — projmng.ai_task (2026-10-18)
-- ============================================================
--
-- 작업이 진행 중(IsBusy)일 때 사용자가 다음 지시 내용을 미리 작성하고
-- 예약해 두면, 현재 작업이 성공(succeeded)으로 끝나는 즉시 서버가
-- 자동으로 이어서 지시(ContinueAsync -> RequestAsync)를 처리한다.
--
-- 브라우저를 닫거나 다른 화면으로 이동해도 서버에서 직접 완료 시점에
-- 예약된 지시를 이어받아 실행한다.
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-ai-pending-continue-2026-10-18.sql
--

ALTER TABLE projmng.ai_task
    ADD COLUMN IF NOT EXISTS pending_continue_addition text,
    ADD COLUMN IF NOT EXISTS pending_continue_runner_kind varchar(20),
    ADD COLUMN IF NOT EXISTS pending_continue_user_id varchar(50),
    ADD COLUMN IF NOT EXISTS pending_continue_at timestamp;

COMMENT ON COLUMN projmng.ai_task.pending_continue_addition IS
    '현재 작업이 진행 중일 때 미리 작성해 둔 이어서 지시 내용. 작업 완료 시 서버가 이 내용으로 자동 계속 요청한다.';

COMMENT ON COLUMN projmng.ai_task.pending_continue_runner_kind IS
    '예약된 이어서 지시의 실행기 종류 (claude, antigravity, copilot 등).';

COMMENT ON COLUMN projmng.ai_task.pending_continue_user_id IS
    '예약된 이어서 지시를 작성한 사용자 ID.';

COMMENT ON COLUMN projmng.ai_task.pending_continue_at IS
    '이어서 지시가 예약된 시각.';
