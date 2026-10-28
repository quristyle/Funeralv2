-- ============================================================
-- 일정표 — projmng.dev_wbs 에 요청자(req_user) 칸 추가 (2026-10-28)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor
--       (/projmng/proj/scheduler — 일정표 관리 항목에 요청자, 담당자 관리)
-- 요청자와 담당자는 시스템 사용자가 아니어도 되는 자유 성명 문자열로 보관한다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-requester-2026-10-28.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS).

ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS req_user character varying(1000);

COMMENT ON COLUMN projmng.dev_wbs.req_user IS '요청자 성명';
