-- ============================================================
-- 프로젝트관리 공통코드 — 프로젝트 DB(스키마)별로 나눈다 (2026-11-01)
-- ============================================================
--
-- 화면 /projmng/comm/common-code 가 프로젝트 → DB(스키마)를 골라 그 DB 의 공통코드만 본다.
-- db_rid 가 비어 있는 줄은 「공용」— 이 도구 자신이 드롭다운·할 일에 쓰는 코드다.
-- 공통코드를 읽는 다른 자리(ProjCodeService · HomeTodoService)는 공용만 본다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-devcomm-db-scope-2026-11-01.sql
--
-- 두 번 돌려도 안전하다.

ALTER TABLE projmng.devcomm ADD COLUMN IF NOT EXISTS db_rid integer;

COMMENT ON COLUMN projmng.devcomm.db_rid IS '프로젝트 DB(devdbinfo.db_rid). 비면 공용 — 프로젝트관리 자체가 쓰는 코드';

CREATE INDEX IF NOT EXISTS devcomm_db_rid_pcd ON projmng.devcomm (db_rid, cm_pcd);
