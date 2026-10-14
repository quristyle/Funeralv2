-- ============================================================
-- 결과 메일을 보낸 시각 — projmng.ai_task_run.mail_sent_at (2026-10-14)
-- ============================================================
--
-- [무엇을 고치려고 생겼나]
--
-- 「빠른 지시」로 시킨 일이 끝나면 세 가지가 잇따라 나간다 — 앱푸시 · 처리 요약
-- (AI 가 지시문과 결과를 종합해 쓴다) · 결과 메일. 그런데 요약은 모델을 부르는
-- 일이라 **빈손으로 돌아오는 날이 있다**(붐빔 · 한도 · 시간 초과). 그때 메일은
-- 그대로 나갔고, 10분 뒤 따라잡기(AiSummaryCatchUp)가 요약을 채워 넣어도
-- **이미 나간 메일에는 그것이 없었다.** 받는 사람 눈에는 「요약이 만들어지기
-- 전에 메일이 온다」이다.
--
-- 이제 메일은 **요약이 손에 들어온 뒤에만** 나간다. 요약이 늦으면 메일도 미루고,
-- 따라잡기가 요약을 채우는 그 자리에서 밀린 메일을 보낸다. 그러자면 「이 실행의
-- 메일은 보냈나」를 어딘가에 적어 두어야 한다 — 그 칸이다.
--
-- [왜 플래그가 아니라 시각인가]
--
-- 「언제 나갔나」가 곧 이 기능의 품질 지표다(끝난 시각과의 거리). 참/거짓으로
-- 두면 그것을 다시 잴 길이 없다.
--
-- [되메움을 반드시 함께 한다]
--
-- 이 칸이 비어 있는 것이 「아직 안 보냈다」는 뜻이므로, 넣기만 하고 두면
-- **지난 모든 실행이 한꺼번에 안 보낸 것으로 보인다** — 따라잡기가 수백 통을
-- 쏟아낸다. 그래서 기존 줄은 전부 끝난 시각으로 채워 둔다.
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-ai-mail-sent-at-2026-10-14.sql
--

ALTER TABLE projmng.ai_task_run
    ADD COLUMN IF NOT EXISTS mail_sent_at timestamp with time zone;

COMMENT ON COLUMN projmng.ai_task_run.mail_sent_at IS
    '결과 메일을 보낸(또는 보낼 이유가 없다고 판정한) 시각. 비어 있으면 아직 안 보낸 것이고, 따라잡기가 주워 간다.';

-- 이미 있던 실행은 전부 「처리가 끝난 것」으로 본다.
UPDATE projmng.ai_task_run
   SET mail_sent_at = COALESCE(finished_at, cre_dt, now())
 WHERE mail_sent_at IS NULL
   AND (finished_at IS NOT NULL OR cre_dt IS NOT NULL);

-- 따라잡기가 보는 것은 「끝났는데 아직 안 보낸 것」뿐이다. 부분 인덱스로 두면
-- 평소에는 거의 빈 인덱스라 쓰기에 짐이 되지 않는다.
CREATE INDEX IF NOT EXISTS ix_ai_task_run_mail_pending
    ON projmng.ai_task_run (finished_at)
 WHERE mail_sent_at IS NULL;
