-- ============================================================
-- 일정표 — 추가대상 공통코드에 회식 · 휴가를 더한다 (2026-10-31)
-- ============================================================
--
-- 앞 파일: deploy/sql/projmng-add-target-issue-notice-request-2026-10-31.sql (ISSUE · NOTICE · REQUEST)
-- 표 보기·달력 일정 목록의 추가대상 단추가 cm_srt 차례로 돈다 —
-- 비움 → 추가 → 변경 → 이동 → 이슈 → 공지 → 요구 → 회식 → 휴가 → 비움.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-add-target-dinner-vacation-2026-10-31.sql
--
-- 두 번 돌려도 안전하다.

COMMENT ON COLUMN projmng.dev_wbs.add_target IS '추가대상 — 공통코드 ADD_TARGET (ADD 추가 · CHANGE 변경 · MOVE 이동 · ISSUE 이슈 · NOTICE 공지 · REQUEST 요구 · DINNER 회식 · VACATION 휴가)';

-- devcomm.cm_rid 에는 기본값이 없어 MAX + n 으로 매긴다.
INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_prop, cm_pcd, cm_val, cm_type, cm_val2, cm_val3, cm_srt, cm_rmk)
SELECT (SELECT MAX(cm_rid) FROM projmng.devcomm) + v.n, v.cd, v.nm, '', 'ADD_TARGET', '', '', '', '', v.srt, NULL
  FROM (VALUES (1, 'DINNER',   '회식', 7),
               (2, 'VACATION', '휴가', 8)) AS v(n, cd, nm, srt)
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm c WHERE c.cm_cd = v.cd AND c.cm_pcd = 'ADD_TARGET');
