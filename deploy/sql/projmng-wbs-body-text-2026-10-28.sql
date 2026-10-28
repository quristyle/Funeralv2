-- ============================================================
-- 일정표 — dev_wbs 원문(body_text) 및 메모(comm) 컬럼 확장 (2026-10-28)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor
--       web/src/Apps/JSini.Web.ProjMng/Components/Shared/WbsEditForm.razor
--       web/src/Apps/JSini.Web.ProjMng/Components/Shared/ScheduleNotePopup.razor
--
-- 1. dev_wbs 에 원문(body_text text) 컬럼 보장
-- 2. 메모(comm) 컬럼을 text 로 확장하여 요약 내용 전체를 안전하게 보관
--
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-body-text-2026-10-28.sql

BEGIN;

ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS body_text text;
ALTER TABLE projmng.dev_wbs ALTER COLUMN comm TYPE text;

COMMENT ON COLUMN projmng.dev_wbs.body_text IS '일정 원문(body_text)';
COMMENT ON COLUMN projmng.dev_wbs.comm IS '일정 메모(comm) - LLM 요약 내용 포함';

COMMIT;
