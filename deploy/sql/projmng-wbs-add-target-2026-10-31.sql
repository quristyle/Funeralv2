-- ============================================================
-- 일정표 — projmng.dev_wbs 에 추가대상(add_target) 칸과 그 공통코드 (2026-10-31)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor (편집 창 · 표 보기)
--       web/src/Apps/JSini.Web.ProjMng/Components/Pages/ScheduleMailPage.razor (메일 본문의 딱지)
-- 값은 공통코드 ADD_TARGET 의 코드(ADD · CHANGE · MOVE)다. 비어 있으면 추가대상이 아니다.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-wbs-add-target-2026-10-31.sql
--
-- 두 번 돌려도 안전하다.

ALTER TABLE projmng.dev_wbs ADD COLUMN IF NOT EXISTS add_target character varying(20);

COMMENT ON COLUMN projmng.dev_wbs.add_target IS '추가대상 — 공통코드 ADD_TARGET (ADD 추가 · CHANGE 변경 · MOVE 이동)';

-- devcomm.cm_rid 에는 기본값이 없어 MAX + n 으로 매긴다.
INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_prop, cm_pcd, cm_val, cm_type, cm_val2, cm_val3, cm_srt, cm_rmk)
SELECT (SELECT MAX(cm_rid) FROM projmng.devcomm) + v.n, v.cd, v.nm, '', v.pcd, '', v.tp, '', '', v.srt, NULL
  FROM (VALUES (1, 'ADD_TARGET', '추가대상', '',           'Project', NULL),
               (2, 'ADD',        '추가',     'ADD_TARGET', '',        1),
               (3, 'CHANGE',     '변경',     'ADD_TARGET', '',        2),
               (4, 'MOVE',       '이동',     'ADD_TARGET', '',        3)) AS v(n, cd, nm, pcd, tp, srt)
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm c WHERE c.cm_cd = v.cd AND c.cm_pcd = v.pcd);
