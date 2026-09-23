-- ============================================================
-- 「GitLab 모니터링」을 「Git 모니터링」으로 옮긴다 (scom · 포털 DB)
-- ============================================================
--
-- 사내 GitLab 을 쓰던 화면이 GitHub 로 바뀌었다. 이름에 공급자가 박혀 있으면
-- 다음에 또 갈아탈 때 같은 일을 한다 — 중립적인 `git` 으로 바꾼다.
--
--   주소   /projmng/wbs/gitlab  →  /projmng/wbs/git
--   열쇠   projmng.wbs.gitlab   →  projmng.wbs.git
--   메뉴   PM_WBS_GITLAB        →  PM_WBS_GIT
--   제목   GitLab 모니터링      →  Git 모니터링
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠를 바꾸는 것은 원래 하지 않는 일이다 ────────────────
--
-- web/CLAUDE.md 는 `route_key` 를 「한 번 정하면 안 바꾼다」고 적어 두었다.
-- DB 가 그것을 가리키므로 한쪽만 고치면 **오류 없이 「준비 중」이 뜬다.**
--
-- 여기서 바꾸는 까닭은 그 열쇠가 **하루짜리**여서다(2026-09-23 에 만들었다).
-- 화면과 DB 를 같은 변경에 함께 옮기고, 가리키는 곳이 이 한 줄뿐인 것을
-- 확인했다. 굳어진 뒤였다면 이름이 어긋난 채로 두는 편이 맞다.
--
-- ── 메뉴 번호를 바꾸므로 권한을 먼저 옮긴다 ─────────────────
--
-- `role_menus` 가 메뉴 번호로 걸려 있다. 메뉴를 먼저 지우면 권한이 딸려
-- 사라지고, 새 번호로 다시 넣어야 한다. 순서를 바꿔 **옮기고 지운다.**

BEGIN;

-- ── 새 메뉴 줄 ───────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT 'PM_WBS_GIT', 'PmWbsGit', '/projmng/wbs/git', pid, 'MENU', 'Git 모니터링',
       'lucide:git-branch', order_no,
       hide_in_menu, status, now(), 'wbs-git-rename', keep_alive,
       use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
       'projmng.wbs.git'
  FROM scom.system_menus
 WHERE id = 'PM_WBS_GITLAB'
   AND NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_WBS_GIT');

-- ── 권한을 새 번호로 옮긴다 ─────────────────────────────────
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT role_id, 'PM_WBS_GIT', can_view, can_search, can_create, can_delete, can_update,
       can_print, can_excel,
       can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
       is_deleted, now(), 'wbs-git-rename'
  FROM scom.role_menus r
 WHERE r.menu_id = 'PM_WBS_GITLAB'
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = r.role_id AND x.menu_id = 'PM_WBS_GIT');

-- ── 즐겨찾기도 따라간다 ─────────────────────────────────────
--
-- 만든 지 하루라 담아 둔 사람이 없을 것이다. 그래도 옮겨 둔다 — 없으면
-- 아무 일도 안 일어나고, 있는데 안 옮기면 **즐겨찾기가 빈 곳을 가리킨다.**
UPDATE scom.menu_favorites
   SET menu_id = 'PM_WBS_GIT'
 WHERE menu_id = 'PM_WBS_GITLAB'
   AND NOT EXISTS (
       SELECT 1 FROM scom.menu_favorites x
        WHERE x.menu_id = 'PM_WBS_GIT'
          AND x.account_id = scom.menu_favorites.account_id);

-- ── 옛 줄을 걷어낸다 ────────────────────────────────────────
DELETE FROM scom.menu_favorites WHERE menu_id = 'PM_WBS_GITLAB';
DELETE FROM scom.role_menus     WHERE menu_id = 'PM_WBS_GITLAB';
DELETE FROM scom.system_menus   WHERE id      = 'PM_WBS_GITLAB';

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key FROM scom.system_menus WHERE id LIKE 'PM_WBS_GIT%';
-- SELECT count(*) FROM scom.role_menus WHERE menu_id = 'PM_WBS_GIT';
