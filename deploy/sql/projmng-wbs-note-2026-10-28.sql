-- ============================================================
-- 일정 메모 — projmng.dev_wbs 에 요약·작성 본문 칸 (2026-10-28)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/ScheduleNote.razor
--       (/projmng/proj/schedule-note — 오른쪽에 적으면 AI 가 날짜·제목·요약을 채운다)
-- 저장은 일정표(/projmng/proj/scheduler)와 같은 표에 schedule_type 으로 들어간다.
--
-- 제목은 proc_nm, 날짜는 plan_sdt · plan_edt 를 그대로 쓰고, 없던 둘만 더한다.
-- comm(메모)은 varchar(1000) 이라 요약·본문을 담기에 짧다 — 따로 둔다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-note-2026-10-28.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS).

ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS summary   text;
ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS body_text text;

COMMENT ON COLUMN projmng.dev_wbs.summary   IS '일정 요약. 일정 메모 화면에서 AI 가 채우고 사람이 고친다.';
COMMENT ON COLUMN projmng.dev_wbs.body_text IS '일정 메모 화면에서 사람이 적은 본문 원문.';
