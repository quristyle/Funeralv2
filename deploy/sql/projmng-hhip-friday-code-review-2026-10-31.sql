-- ============================================================
-- HHIP_MM 프로젝트 일정표 — 금요일 코드리뷰 일정 보충 (2026-10-31)
-- ============================================================
--
-- 대상: HHIP_MM 프로젝트 (prj_rid = 8), 2026-07-28 ~ 2026-10-30 의 금요일
-- 규칙: 그 금요일(한국 달력)에 제목에 「리뷰」가 든 일정이 하나도 없을 때만
--       「코드리뷰」 공유일정을 넣는다 — 이미 있는 날(09-18 · 10-30)은 건너뛴다.
--       시간은 wbs 665 의 메모대로 14시부터 1시간(한국 시각), 담당자 이순열.
--       진행 상태는 완료 — dev_sdt·dev_edt 를 계획과 같은 때로 채운다.
-- 표지: comm LIKE '금요일 코드리뷰 보충 %' — 다시 돌리면 그 줄을 갈아 끼운다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -v ON_ERROR_STOP=1 -f deploy/sql/projmng-hhip-friday-code-review-2026-10-31.sql
-- ============================================================

BEGIN;

DELETE FROM projmng.dev_wbs
 WHERE prj_rid = 8
   AND comm LIKE '금요일 코드리뷰 보충 %';

WITH fridays AS (
    SELECT d::date AS d
      FROM generate_series(date '2026-07-28', date '2026-10-30', interval '1 day') d
     WHERE extract(isodow FROM d) = 5
),
missing AS (
    SELECT f.d,
           ROW_NUMBER() OVER (ORDER BY f.d) AS ord
      FROM fridays f
     WHERE NOT EXISTS (
               SELECT 1
                 FROM projmng.dev_wbs w
                WHERE w.prj_rid = 8
                  AND w.discard_dt IS NULL
                  AND replace(w.proc_nm, ' ', '') LIKE '%리뷰%'
                  AND (COALESCE(w.plan_sdt, w.dev_edt) AT TIME ZONE 'Asia/Seoul')::date = f.d)
),
base AS (
    SELECT COALESCE(MAX(wbs_id), 0) AS max_id FROM projmng.dev_wbs
)
INSERT INTO projmng.dev_wbs
       (prj_rid, wbs_id, proc_nm, dev_user,
        plan_sdt, plan_edt, dev_sdt, dev_edt,
        cre_user, cre_dt, mod_user, mod_dt,
        comm, summary, body_text, schedule_type)
SELECT 8,
       b.max_id + m.ord,
       '코드리뷰',
       '이순열',
       (m.d + time '14:00') AT TIME ZONE 'Asia/Seoul',
       (m.d + time '15:00') AT TIME ZONE 'Asia/Seoul',
       (m.d + time '14:00') AT TIME ZONE 'Asia/Seoul',
       (m.d + time '15:00') AT TIME ZONE 'Asia/Seoul',
       'quristyle', projmng.today_kst(), 'quristyle', projmng.today_kst(),
       '금요일 코드리뷰 보충 (' || to_char(m.d, 'YYYY-MM-DD') || ')' || E'\n' || '매주 금요일 14시부터 1시간 동안 코드리뷰 수행',
       '매주 금요일 14시부터 1시간 동안 코드리뷰 수행',
       '금요일 코드리뷰 ' || to_char(m.d, 'YYYY-MM-DD') || ' 14:00~15:00',
       'Public'
  FROM missing m
 CROSS JOIN base b;

COMMIT;
