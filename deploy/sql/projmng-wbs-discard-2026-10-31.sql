-- ============================================================
-- 일정표 — projmng.dev_wbs 에 폐기 시각(discard_dt) 칸 추가 (2026-10-31)
-- ============================================================
--
-- 화면: /projmng/proj/scheduler 의 진행 상태 「폐기」.
-- 값이 있으면 개발 시작·종료일과 무관하게 진행 상태가 DISCARD 다(WbsService.Columns).
-- 지우지 않고 남겨 두는 까닭은 메일·표에서 「폐기된 일정」으로 보여 주기 위해서다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-discard-2026-10-31.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS).

ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS discard_dt timestamptz;

COMMENT ON COLUMN projmng.dev_wbs.discard_dt IS '폐기한 때 — 있으면 진행 상태가 폐기(DISCARD)';
