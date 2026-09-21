-- ============================================================
-- AI 모델 한도 — CLI 하나에 칸이 여럿 (2026-09-21)
-- ============================================================
--
-- 앞선 것: projmng-ai-usage-2026-09-20.sql (표를 만든다). **그것을 먼저 돌린다.**
-- 설계: docs/ai-task-runner.md 11.6
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiDashboard.razor
--
--   psql -h … -U projmng -d projmng -f projmng-ai-usage-bucket-2026-09-21.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── **먼저 돌리면 안 된다** ──────────────────────────────────
--
-- 앞의 것들과 달리 이것은 **옛 코드와 함께 설 수 없다.** 아래에서 열쇠를
-- (장비, 종류) 에서 (장비, 종류, 칸) 으로 바꾸는데, 옛 `AiUsageService` 의
-- 덮어쓰기는 `ON CONFLICT (runner_nm, runner_kind)` 로 적혀 있어 그 열쇠에
-- 맞는 제약을 못 찾고 **통째로 끊긴다** —
--
--   42P10: there is no unique or exclusion constraint matching
--          the ON CONFLICT specification
--
-- 한도 한 줄만 빠지는 것이 아니다. 같은 트랜잭션에 `ai_runner` 의
-- `last_seen_at` 이 함께 있어서 **화면이 실행기를 죽은 것으로 본다.**
-- 그래서 이 파일은 코드를 올리는 그 배포에서 같이 돌린다. 먼저 돌렸다면
-- 새 코드가 뜰 때까지 사용량 보고가 전부 실패한다(작업 실행에는 지장 없다).
--
-- ── 왜 고치나 ────────────────────────────────────────────────
--
-- 표를 만들 때는 `claude` 하나만 한도를 말해 줬고, 그 CLI 는 계정 하나에
-- 줄 하나면 맞았다. **나머지 둘을 붙이면서 그 전제가 깨졌다.**
--
--   agy     — 모델군마다 따로 센다 (Gemini · Claude and GPT)
--   copilot — 한도 종류마다 따로 센다 (chat · completions · premium_interactions)
--
-- 장비 × 종류로 한 줄만 두면 **둘 중 하나가 다른 하나를 덮는다.** 15분마다
-- 번갈아 덮으므로 화면의 숫자가 주기마다 바뀌는데, 둘 다 그럴듯한 값이라
-- 아무도 고장으로 읽지 못한다 — 표가 비는 것보다 나쁜 모양이다.
--
-- 그래서 `bucket_nm` 을 열쇠에 넣는다. 하나뿐인 CLI(claude)는 빈 글자라
-- 지금 있는 줄이 그대로 산다.
ALTER TABLE projmng.ai_usage_snapshot
    ADD COLUMN IF NOT EXISTS bucket_nm varchar(60) NOT NULL DEFAULT '';

COMMENT ON COLUMN projmng.ai_usage_snapshot.bucket_nm IS
    '같은 CLI 안에서 무엇의 한도인가 — agy 는 모델군, copilot 은 한도 종류. 하나뿐인 CLI 는 빈 글자.';

-- ── 달로 끊는 한도를 위한 칸 ─────────────────────────────────
--
-- **주간 칸에 밀어 넣지 않는다.** copilot 은 달로 끊고 claude 는 주로 끊는데,
-- 한 칸에 담으면 화면의 「주간」이 CLI 마다 다른 기간을 뜻하게 된다 —
-- 그 화면을 보고 「이번 주에 얼마나 남았나」를 판단하는 사람이 틀린다.
ALTER TABLE projmng.ai_usage_snapshot
    ADD COLUMN IF NOT EXISTS month_pct      numeric(5,2),
    ADD COLUMN IF NOT EXISTS month_reset_at timestamp;

COMMENT ON COLUMN projmng.ai_usage_snapshot.month_pct IS
    '월간 한도 사용률(%). 쓴 비율이다 — 남은 비율이 아니다.';

-- ── 열쇠를 바꾼다 ────────────────────────────────────────────
--
-- 덮어쓰기(upsert)가 이 제약을 탄다. **옛 인덱스를 먼저 지워야 한다** —
-- 남겨 두면 (장비, 종류)가 여전히 하나뿐이라 새 칸들이 서로 밀어낸다.
DROP INDEX IF EXISTS projmng.ux_ai_usage_snapshot;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ai_usage_snapshot
    ON projmng.ai_usage_snapshot (runner_nm, runner_kind, bucket_nm);

COMMENT ON TABLE projmng.ai_usage_snapshot IS
    'AI CLI 의 한도를 실행기가 읽어 올려 둔 마지막 값. 장비 × 종류 × 칸으로 한 줄.';

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT runner_nm, runner_kind, bucket_nm, ok,
--        session_pct, week_pct, month_pct, observed_at
--   FROM projmng.ai_usage_snapshot
--  ORDER BY runner_kind, bucket_nm, runner_nm;
