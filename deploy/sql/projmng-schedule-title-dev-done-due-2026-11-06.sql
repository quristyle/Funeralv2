-- 일정표(/projmng/proj/scheduler) 일정 제목 끝의 「개발완료」를 「개발완료예정일」로 바꾼다.
-- 반영 전 90 줄(전부 prj_rid 8 · Public · wbs_id 796~891). 이미 「…예정일」인 줄은 무늬에 안 걸려 다시 돌려도 안전하다.
-- 되돌리기: regexp_replace(proc_nm, '개발완료예정일(\s*)$', '개발완료\1')
BEGIN;

UPDATE projmng.dev_wbs
   SET proc_nm = regexp_replace(proc_nm, '개발완료(\s*)$', '개발완료예정일\1')
 WHERE proc_nm ~ '개발완료\s*$';

COMMIT;
