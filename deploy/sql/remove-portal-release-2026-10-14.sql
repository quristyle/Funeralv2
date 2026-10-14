-- ============================================================
-- 배포 도구(/admin/release, /portal/release) 및 관련 데이터 제거
-- ============================================================
--
-- /admin/release 화면 미사용에 따라 화면, 백엔드 엔드포인트 및 서비스를 제거함.
-- 이에 따라 DB 메뉴 및 권한, 사용 기록, release 관련 테이블/데이터를 삭제한다.
--
-- 대상 DB: jsiniportal (스키마 scom)
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/remove-portal-release-2026-10-14.sql
--

BEGIN;

-- 1. 메뉴 사용 로그에서 release 관련 내역 삭제
DELETE FROM scom.menu_usage_logs 
WHERE menu_id = 'PORTAL_RELEASE' 
   OR menu_path IN ('/admin/release', '/portal/release') 
   OR route_key = 'admin.release';

-- 2. 메뉴 권한 매핑 삭제
DELETE FROM scom.role_menus WHERE menu_id = 'PORTAL_RELEASE';

-- 3. 메뉴 즐겨찾기 삭제
DELETE FROM scom.menu_favorites WHERE menu_id = 'PORTAL_RELEASE';

-- 4. 시스템 메뉴 삭제
DELETE FROM scom.system_menus WHERE id = 'PORTAL_RELEASE';

-- 5. release_run_events 및 release_runs 테이블 및 데이터 삭제
DROP TABLE IF EXISTS scom.release_run_events CASCADE;
DROP TABLE IF EXISTS scom.release_runs CASCADE;

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- 모두 0 이어야 한다.
--
-- SELECT (SELECT count(*) FROM scom.system_menus WHERE id = 'PORTAL_RELEASE' OR path = '/admin/release' OR route_key = 'admin.release') AS menu_count,
--        (SELECT count(*) FROM scom.role_menus WHERE menu_id = 'PORTAL_RELEASE') AS role_menu_count,
--        (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'PORTAL_RELEASE') AS favorite_count,
--        (SELECT count(*) FROM scom.menu_usage_logs WHERE menu_id = 'PORTAL_RELEASE' OR menu_path IN ('/admin/release', '/portal/release') OR route_key = 'admin.release') AS usage_log_count,
--        (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'scom' AND table_name IN ('release_runs', 'release_run_events')) AS table_count;
