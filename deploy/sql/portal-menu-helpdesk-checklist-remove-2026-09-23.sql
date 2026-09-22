-- ============================================================
-- 「체크리스트」 화면을 메뉴에서 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- `/helpdesk/system/checklist` 는 안 쓰는 화면이라 2026-09-23 에 지웠다.
-- 화면(`ChecklistList.razor`)·엔드포인트·모델·표(`helpdesk.checklist`)를
-- 전부 없앴으므로 메뉴만 남으면 눌렀을 때 「준비 중」이 뜬다 — 그래서 지운다.
--
-- 표를 지우는 쪽은 EF 마이그레이션 `20260922222726_RemoveChecklist` 다.
-- 그것은 **헬프데스크 DB** 에 돌리고, 이 파일은 **포털 DB(jsiniportal)** 에 돌린다.
-- 둘은 서로 다른 DB 라 한 번에 돌아가지 않는다.
--
-- 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다. 권한 줄이 남아 있으면
-- 메뉴 줄이 외래키에 걸려 안 지워진다 — 순서를 바꾸면 안 된다.
DELETE FROM scom.role_menus  WHERE menu_id = 'HD_SYS_CHECKLIST';

-- 즐겨찾기도 같은 열쇠를 본다. 지금은 비어 있지만 뒤늦게 눌러 둔 사람이
-- 있으면 여기서 걸린다.
DELETE FROM scom.menu_favorites WHERE menu_id = 'HD_SYS_CHECKLIST';

-- ── 메뉴 ─────────────────────────────────────────────────────
DELETE FROM scom.system_menus WHERE id = 'HD_SYS_CHECKLIST';

-- ── 확인 ─────────────────────────────────────────────────────
-- 셋 다 0 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus   WHERE id      = 'HD_SYS_CHECKLIST') AS menu,
--          (SELECT count(*) FROM scom.role_menus     WHERE menu_id = 'HD_SYS_CHECKLIST') AS role,
--          (SELECT count(*) FROM scom.menu_favorites WHERE menu_id = 'HD_SYS_CHECKLIST') AS fav;
