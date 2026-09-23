-- ============================================================
-- 개발자 명부를 지운다 — projmng.wbs_user (projmng DB)
-- ============================================================
--
-- 사람을 **포털 계정 하나**로 다룬다. 사번 · 직급 · 장비 대장 · 계정 발급
-- 현황 · 옷 치수는 `scom.account_profile_details` 의 `Dev.*` 로 들어가고
-- 화면은 `/admin/system/account` 다. 경위는 `docs/projmng-account-merge.md`.
--
-- 읽는 코드는 앞선 변경에서 모두 없앴다(조인 11곳 · 명부 API · 명부 화면).
-- **표만 남아 있었다.**
--
-- ── 지우기 전에 본 것 ───────────────────────────────────────
--
--   줄 수            0
--   이 표를 물고 있는 외래키   없음
--   이 표를 보는 뷰            없음
--
-- 자료가 들어 있는 곳에서 돌릴 것이면 **먼저 같은 것을 확인한다.**
-- 아래 조회가 그대로 있다.
--
-- ── 함께 사라지는 길 ────────────────────────────────────────
--
-- 사내 자료를 들여올 때 쓰던 **사번 → 계정 대조**가 이 표를 거쳐 갔다.
-- 이제 대조는 계정관리에 적은 사번(`Dev.BpId`)에서 나온다 — 적재 스크립트
-- (`projmng-wbs-data-load.sql`)의 「사번을 계정으로 바꾼다」 절이 그 대조표를
-- 받아 원장의 담당자 칸을 옮긴다. **데이터베이스가 달라 조인이 아예
-- 불가능해서** 그 표를 손으로 옮겨 붙이는 모양이 되었다.
--
-- 두 번 돌려도 안전하다.
--
-- ── 되돌리려면 ──────────────────────────────────────────────
--
-- 표 정의는 git 이력에 있다 —
--   git show 48a40502:deploy/sql/projmng-wbs-2026-09-23.sql
-- 다만 **자료는 되돌아오지 않는다.** 그래서 지우기 전에 줄 수를 본다.

-- ── 지우기 전 확인 (먼저 이것만 돌려 본다) ───────────────────
-- SELECT count(*) AS 줄수, count(login_id) AS 계정이어둔것 FROM projmng.wbs_user;
--
-- SELECT c.conname, c.conrelid::regclass AS 물고있는표
--   FROM pg_constraint c WHERE c.confrelid = 'projmng.wbs_user'::regclass;

BEGIN;

DROP TABLE IF EXISTS projmng.wbs_user;

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT tablename FROM pg_tables
--  WHERE schemaname = 'projmng' AND tablename LIKE 'wbs_%' ORDER BY 1;
-- (`wbs_user_pref` 는 남는다 — 이름만 비슷하고 화면 설정 표다.)
