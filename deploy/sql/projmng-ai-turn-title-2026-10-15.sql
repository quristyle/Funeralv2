-- ============================================================
-- 차수마다 제 제목을 갖는다 — projmng.ai_task_run.turn_title (2026-10-15)
-- ============================================================
--
-- 「지시와 결과」 탭은 차수마다 한 묶음을 접어 둔다(2026-10-15). 그런데 접힌
-- 머리줄이 말하는 것은 **몇 차인가 · 어떻게 끝났나 · 언제 얼마나 돌았나** 셋뿐이라,
-- 세 차수가 쌓이면 **어느 것을 펴야 하는지 알 수 없다** — 차례로 다 펴 보게 된다.
-- 그 셋 가운데 「무엇을 시킨 차수였나」를 말해 주는 것이 하나도 없기 때문이다.
--
-- 그래서 차수마다 **그 차수의 지시를 한 줄로 줄인 제목**을 AI 에게 짓게 해 적어 둔다.
--
-- [왜 ai_task.title 로는 안 되나]
--
-- 그 칸은 **건 하나에 하나**다. 「이어서 지시」는 새 건을 만들지 않고 같은 건에
-- 실행을 하나 더 붙이므로(`AiTaskService.ContinueAsync`), 3차까지 간 건의 제목은
-- 늘 **마지막 차수의 것 한 줄**이다. 1차에 무엇을 시켰는지는 어디에도 안 남는다.
--
-- [두 번 지어진다 — 시킬 때와 끝났을 때]
--
--   · **시킬 때**(`AiTaskService.RequestAsync` → `AiTurnTitler.PrepareAsync`)
--     그 차수에 사람이 적은 글만 보고 짓는다. 이때는 결과가 없으므로
--     「무엇을 시켰나」다. 아직 실행 줄이 없으므로 `ai_task.next_turn_title` 에
--     잠깐 얹어 두고, 실행기가 집어 갈 때(`AiRunService.ClaimAsync`) 실행 줄로
--     옮겨 담으면서 그 칸을 비운다.
--   · **끝났을 때**(`AiRunSummaryWriter`) 처리 요약을 받아 오는 그 **한 번의
--     모델 호출**이 짧은 제목까지 함께 들고 온다. 이때는 결과까지 본 제목이라
--     더 정확하므로 덮어쓴다.
--
-- 화면 최상단의 제목(`ai_task.title`)도 같은 값으로 따라간다 — **마지막 차수의
-- 제목이 곧 그 건의 제목**이다. 다만 **사람이 손으로 적은 제목은 손대지 않는다**
-- (`title_auto = false`). 그 규칙은 이 칸이 생기기 전과 똑같다.
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-ai-turn-title-2026-10-15.sql
--

ALTER TABLE projmng.ai_task_run
    ADD COLUMN IF NOT EXISTS turn_title varchar(200);

COMMENT ON COLUMN projmng.ai_task_run.turn_title IS
    '이 차수의 제목. 시킬 때 지시문으로 한 번, 끝났을 때 결과까지 보고 한 번 더 AI 가 짓는다. 「지시와 결과」 탭의 접힌 머리줄이 읽는다.';

ALTER TABLE projmng.ai_task
    ADD COLUMN IF NOT EXISTS next_turn_title varchar(200);

COMMENT ON COLUMN projmng.ai_task.next_turn_title IS
    '아직 실행 줄이 없는 차수의 제목을 잠깐 얹어 두는 자리. 실행기가 집어 갈 때 ai_task_run.turn_title 로 옮기고 비운다.';
