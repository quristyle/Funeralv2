-- ============================================================
-- dev_wbs 일정 일시(시분초) 관리 — date → timestamptz (2026-10-28)
-- ============================================================
--
-- 계획 시작(plan_sdt), 계획 종료(plan_edt), 개발 시작(dev_sdt), 개발 종료(dev_edt)
-- 4개 항목을 일자뿐만 아니라 시간까지 관리되도록 컬럼 타입을 date 에서 timestamptz 로 변경한다.
--
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-datetime-2026-10-28.sql

BEGIN;

ALTER TABLE projmng.dev_wbs
    ALTER COLUMN plan_sdt TYPE timestamptz USING plan_sdt::timestamptz,
    ALTER COLUMN plan_edt TYPE timestamptz USING plan_edt::timestamptz,
    ALTER COLUMN dev_sdt  TYPE timestamptz USING dev_sdt::timestamptz,
    ALTER COLUMN dev_edt  TYPE timestamptz USING dev_edt::timestamptz;

COMMENT ON COLUMN projmng.dev_wbs.plan_sdt IS '계획 시작 일시(timestamptz)';
COMMENT ON COLUMN projmng.dev_wbs.plan_edt IS '계획 종료 일시(timestamptz)';
COMMENT ON COLUMN projmng.dev_wbs.dev_sdt  IS '개발 시작 일시(timestamptz)';
COMMENT ON COLUMN projmng.dev_wbs.dev_edt  IS '개발 종료 일시(timestamptz)';

COMMIT;
