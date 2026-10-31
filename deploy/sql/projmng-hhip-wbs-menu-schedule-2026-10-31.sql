-- ============================================================
-- HHIP_MM 프로젝트 일정표 — WBS 상세 목록(/projmng/wbs/rows)의 메뉴 개발일정 반영 (2026-10-31)
-- ============================================================
--
-- 대상: HHIP_MM 프로젝트 (prj_rid = 8)
-- 도출: /projmng/wbs/rows (projmng.wbs_work) 의
--       메뉴명(menu_nm)       → 일정 제목(proc_nm)
--       개발종료일(plan_edt)  → 일정 시작일(plan_sdt) 및 종료일(plan_edt)
--       개발자(user_real_id)  → 담당자(dev_user: 성명)
--       요청자(req_user)      → '이순열'
--
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -v ON_ERROR_STOP=1 -f deploy/sql/projmng-hhip-wbs-menu-schedule-2026-10-31.sql
-- ============================================================

BEGIN;

-- 기존 반영분이 있으면 정리 (재실행 안전)
DELETE FROM projmng.dev_wbs
 WHERE prj_rid = 8
   AND schedule_type = 'Public'
   AND comm LIKE 'WBS 메뉴 개발일정 (%';

WITH dev_map(user_id, user_nm) AS (
    VALUES
        ('BP33969', '주우인'),
        ('BP33971', '정상준'),
        ('BP33972', '정일인'),
        ('BP34173', '이선우')
),
src AS (
    SELECT w.activity_id,
           w.menu_nm,
           w.plan_edt,
           w.plan_sdt_c,
           w.plan_edt_c,
           w.complate_real_yn,
           COALESCE(m.user_nm, w.user_real_id, '') AS dev_name,
           ROW_NUMBER() OVER (ORDER BY w.plan_edt, w.activity_id) AS ord
      FROM projmng.wbs_work w
      LEFT JOIN dev_map m ON m.user_id = w.user_real_id
     WHERE w.prj_rid = 8
       AND w.plan_edt IS NOT NULL
),
max_id AS (
    SELECT COALESCE(MAX(wbs_id), 0) AS cur_max FROM projmng.dev_wbs
)
INSERT INTO projmng.dev_wbs
     ( prj_rid, wbs_id, proc_id, proc_nm, dev_user, req_user,
       plan_sdt, plan_edt, dev_sdt, dev_edt, dev_chk,
       schedule_type, comm, cre_user, cre_dt, mod_user, mod_dt )
SELECT 8,
       m.cur_max + s.ord,
       s.activity_id,
       s.menu_nm,
       s.dev_name,
       '이순열',
       s.plan_edt::timestamp with time zone,
       s.plan_edt::timestamp with time zone,
       CASE WHEN s.complate_real_yn = 'o' THEN COALESCE(s.plan_sdt_c, s.plan_edt)::timestamp with time zone ELSE NULL END,
       CASE WHEN s.complate_real_yn = 'o' THEN COALESCE(s.plan_edt_c, s.plan_edt)::timestamp with time zone ELSE NULL END,
       CASE WHEN s.complate_real_yn = 'o' THEN '완료' ELSE NULL END,
       'Public',
       'WBS 메뉴 개발일정 (' || s.activity_id || ')',
       '이순열',
       projmng.today_kst(),
       '이순열',
       projmng.today_kst()
  FROM src s
 CROSS JOIN max_id m;

COMMIT;
