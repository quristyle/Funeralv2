-- ============================================================
-- 프로젝트 일정표(dev_wbs) 일정구분(SCHEDULE_TYPE)에 이벤트(Event) 코드 추가
-- ============================================================
--
-- /projmng/proj/scheduler 화면의 왼쪽 조회영역 구분 항목(web, 개인, 공유)에
-- 이벤트(Event) 항목을 추가한다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-schedule-type-event-2026-10-28.sql

BEGIN;

UPDATE projmng.devcomm SET cm_srt = 1 WHERE cm_cd = 'WBS' AND cm_pcd = 'SCHEDULE_TYPE';
UPDATE projmng.devcomm SET cm_srt = 2 WHERE cm_cd = 'Private' AND cm_pcd = 'SCHEDULE_TYPE';
UPDATE projmng.devcomm SET cm_srt = 3 WHERE cm_cd = 'Public' AND cm_pcd = 'SCHEDULE_TYPE';

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_prop, cm_pcd, cm_val, cm_type, cm_val2, cm_val3, cm_srt, cm_rmk)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'Event', '이벤트', '', 'SCHEDULE_TYPE', '', '', '', '', 4, '이벤트'
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_pcd = 'SCHEDULE_TYPE' AND cm_cd = 'Event');

UPDATE projmng.devcomm SET cm_srt = 4, cm_nm = '이벤트' WHERE cm_cd = 'Event' AND cm_pcd = 'SCHEDULE_TYPE';

COMMENT ON COLUMN projmng.dev_wbs.schedule_type IS '일정구분WBS,Private,Public,Event';

COMMIT;
