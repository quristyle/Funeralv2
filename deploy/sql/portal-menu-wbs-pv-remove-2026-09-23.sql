-- ============================================================
-- ProjectView 화면 둘을 메뉴에서 걷어낸다 (scom · 포털 DB)
-- ============================================================
--
--   PM_WBS_PVSYNC  ProjectView 동기화  /projmng/wbs/pv-sync
--   PM_WBS_PVFLOW  워크플로 채우기     /projmng/wbs/pv-flow
--
-- 쓰지 않기로 했다(사용자 결정, 2026-09-23). 화면·API·콘솔 스크립트 2,595줄을
-- 같은 변경에서 함께 지웠다 — 메뉴만 남기면 **누르면 「준비 중」이 뜬다.**
--
-- 두 번 돌려도 안전하다.
--
-- ── 함께 죽은 것 ────────────────────────────────────────────
--
-- 상세 목록(`/projmng/wbs/rows`)의 PV 칸 둘(진척·단계)도 뺐다. 그 캐시를
-- 채울 곳이 없어졌으므로 남기면 **늘 비어 있는 칸**이 되고, 빈 값을
-- 「아직 안 걷었나 보다」로 읽게 된다.
--
-- ── 표 셋은 그대로 둔다 ─────────────────────────────────────
--
--   projmng.wbs_pv · wbs_pv_task · wbs_pv_node
--
-- 읽는 코드가 없어졌지만 **지우지 않았다.** 운영 DDL 이고, 되돌리려면
-- 자료까지 되살려야 하는 쪽이라 사람이 정할 일이다. 지울 것이면 한 줄이다 —
--
--   DROP TABLE projmng.wbs_pv_node, projmng.wbs_pv_task, projmng.wbs_pv;
--
-- (`wbs_pv_node` 가 `wbs_pv_task` 를 외래키로 물고 있어 순서가 저렇다.)

BEGIN;

DELETE FROM scom.menu_favorites WHERE menu_id IN ('PM_WBS_PVSYNC', 'PM_WBS_PVFLOW');
DELETE FROM scom.role_menus     WHERE menu_id IN ('PM_WBS_PVSYNC', 'PM_WBS_PVFLOW');
DELETE FROM scom.system_menus   WHERE id      IN ('PM_WBS_PVSYNC', 'PM_WBS_PVFLOW');

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path FROM scom.system_menus WHERE pid = 'PM_WBS' ORDER BY order_no;
