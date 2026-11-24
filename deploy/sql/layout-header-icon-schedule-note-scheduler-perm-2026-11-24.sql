-- ============================================================
-- AI 일정 메모 상단 아이콘의 권한 열쇠를 일정표 메뉴로 (2026-11-24)
-- ============================================================
--
-- AI 일정 메모는 프로젝트 일정표(dev_wbs)에 일정을 넣으므로, 아이콘도
-- /projmng/proj/scheduler 메뉴를 쓸 수 있는 사람에게만 보여야 한다.
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/layout-header-icon-schedule-note-scheduler-perm-2026-11-24.sql
--
-- 두 번 돌려도 안전하다.
-- ============================================================

BEGIN;

UPDATE scom.layout_header_icons
   SET route_key = 'projmng.proj.scheduler',
       path = '/projmng/proj/scheduler',
       updated_at = now()
 WHERE icon_key = 'schedule_note';

COMMIT;

SELECT icon_key, route_key, path, default_enabled, order_no
  FROM scom.layout_header_icons
 WHERE icon_key = 'schedule_note';
