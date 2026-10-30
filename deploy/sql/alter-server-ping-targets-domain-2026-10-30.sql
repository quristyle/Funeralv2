-- ============================================================
-- Ping 감시 대상 장비 ip 컬럼 도메인 지원을 위한 길이 확장 (2026-10-30)
-- ============================================================
-- 대상 DB: jsiniportal (스키마 scom)
--   psql -h 127.0.0.1 -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/alter-server-ping-targets-domain-2026-10-30.sql
-- ============================================================

BEGIN;

-- ip 컬럼 길이를 50자에서 255자로 확장하여 긴 도메인(FQDN) 입력 지원
ALTER TABLE scom.server_ping_targets 
ALTER COLUMN ip TYPE VARCHAR(255);

COMMIT;
