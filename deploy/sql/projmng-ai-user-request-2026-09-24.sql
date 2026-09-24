-- ============================================================
-- 「AI 작업 요청」 — 일반 사용자가 적어 두는 지시 (projmng 스키마)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md · 화면
--   web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiRequestList.razor
--
-- **EF 마이그레이션이 아니다.** ProjMngServer 는 Dapper + Npgsql 이라
-- 스키마를 SQL 파일이 만든다. 운영 반영은 손으로 한 번 돌린다.
--
--   psql -h … -U projmng -d projmng -f projmng-ai-user-request-2026-09-24.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── 왜 칸이 하나 더 필요한가 ────────────────────────────────
--
-- 일반 사용자가 적어 둔 지시는 저장만 되고 **아무 데서도 안 돈다** —
-- 요청여부 `none`, 상태 `idle`, 작업 대상 없음이다. 그런데 그 세 값은
-- **관리자가 「AI 작업」 화면에서 쓰다 만 건**과 글자 하나 다르지 않다.
--
-- 둘을 못 가르면 관리자 쪽에 「누가 시켜 달라고 올린 것」이 모이는 자리가
-- 생기지 않는다. 쓰다 만 제 글 사이에 섞여 **영영 안 돌아가는 지시**가 된다.
-- 그래서 출처를 한 칸으로 남긴다.
ALTER TABLE projmng.ai_task
    ADD COLUMN IF NOT EXISTS is_user_request boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN projmng.ai_task.is_user_request IS
    '일반 사용자가 「AI 작업 요청」 화면에서 올린 건인가. 관리자가 대상·AI 를 채워 시킨다.';

-- 「내가 올린 것」만 읽는 조회는 언제나 cre_id 로 좁혀 들어온다. 그 길은
-- **이미 나 있다** — `projmng-ai-usage-2026-09-20.sql` 의 `ix_ai_task_cre`
-- (cre_id, cre_dt) 가 같은 부분 조건(is_deleted = false)으로 서 있으므로
-- 여기서 인덱스를 또 만들지 않는다. 앞자리가 같은 인덱스를 둘 두면 쓰기마다
-- 두 벌을 갱신하면서 조회는 한 벌만 탄다.

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT column_name, data_type, column_default
--   FROM information_schema.columns
--  WHERE table_schema = 'projmng' AND table_name = 'ai_task'
--    AND column_name = 'is_user_request';
