-- ============================================================
-- AI 모델 한도 — 로그인이 언제 끊기는가 (2026-10-18)
-- ============================================================
--
-- 앞선 것: projmng-ai-usage-2026-09-20.sql · projmng-ai-usage-bucket-2026-09-21.sql
-- 설계: docs/ai-task-runner.md 11.6
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/AiDashboard.razor
--
--   psql -h … -U projmng -d projmng -f projmng-ai-usage-auth-2026-10-18.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── 먼저 돌려도 된다 ────────────────────────────────────────
--
-- 칸을 더하기만 한다. 옛 코드는 이 칸을 안 쓰고, 새 코드는 칸이 없으면
-- 조회가 42703 으로 끊긴다 — **코드보다 먼저 돌리는 쪽이 안전하다.**
--
-- ── 왜 두나 ──────────────────────────────────────────────────
--
-- 2026-10-17 에 운영 실행기의 claude 갱신 토큰이 만료됐다. CLI 는 오류를
-- 내지 않는다 — `/usage` 는 비용 요약으로 내려앉고, 시킨 작업은 빈손으로
-- 끝난다. **끊기고 나서야 알았고, 그때는 이미 반나절이 지나 있었다.**
--
-- 그 날짜는 끊기기 전부터 로그인 정보에 적혀 있다
-- (`~/.claude/.credentials.json` 의 `refreshTokenExpiresAt`). 그것을 함께
-- 받아 적으면 화면이 **「언제 끊길 것인가」를 미리 말할 수 있다.**
--
-- 한도의 갱신 시각(`week_reset_at` 따위)과 섞지 않는다 — 그것은 저절로
-- 다시 차는 시각이고 이것은 **사람이 손으로 풀어야 끝나는 시각**이다.
--
-- 모든 CLI 가 말해 주지는 않는다. `agy` 가 들고 있는 것은 한 시간짜리
-- 접근 토큰의 만료라 적어 봐야 시간마다 바뀌고, `copilot` 은 요금제 이름만
-- 준다. 비어 있는 것이 정상인 칸이다.
-- ============================================================

ALTER TABLE projmng.ai_usage_snapshot
  ADD COLUMN IF NOT EXISTS auth_expires_at timestamptz;

COMMENT ON COLUMN projmng.ai_usage_snapshot.auth_expires_at IS
  '로그인(갱신 토큰)이 끊기는 시각. 한도 갱신 시각과 다르다 — 사람이 다시 로그인해야 풀린다.';

ALTER TABLE projmng.ai_usage_history
  ADD COLUMN IF NOT EXISTS auth_expires_at timestamptz;

COMMENT ON COLUMN projmng.ai_usage_history.auth_expires_at IS
  '로그인(갱신 토큰)이 끊기는 시각. 추이에는 쓰지 않고 「언제부터 알 수 있었나」를 되짚는 데 쓴다.';
