-- ============================================================
-- 메뉴 등록 — 프로젝트관리 › 프로젝트 › AI 일정 메모 (2026-10-28)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/ScheduleNote.razor
-- 표:   deploy/sql/projmng-wbs-note-2026-10-28.sql (dev_wbs.summary · body_text)
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f portal-menu-schedule-note-2026-10-28.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT · order_no 밀기는 아직 4 자리에 이 메뉴가 없을 때만).
--
-- 「프로젝트 일정표」(PM_PROJ_SCHED, 3) 바로 아래 4 자리에 둔다 — 저장한 것이 그 일정표에
-- 뜬다. 권한은 일정표를 그대로 베낀다: 같은 표를 쓰므로 일정표를 고칠 수 있는 사람이
-- 여기서도 적을 수 있어야 하고, 못 보는 사람이 여기서 넣을 수 있으면 안 된다.
-- ============================================================

BEGIN;

UPDATE scom.system_menus
   SET order_no = order_no + 1, updated_at = now(), updated_by = 'schedule-note'
 WHERE pid = 'PM_PROJ' AND order_no >= 4 AND id <> 'PM_PROJ_SNOTE'
   AND NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_PROJ_SNOTE');

INSERT INTO scom.system_menus (
    id, name, path, route_key, pid, type, title, icon, order_no,
    status, hide_in_menu, is_deleted, keep_alive, affix_tab, dom_cached,
    menu_visible_with_forbidden, hide_children_in_menu, hide_in_breadcrumb, hide_in_tab,
    use_mobile, use_tablet,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel,
    use_cust1, use_cust2, use_cust3, use_cust4, use_cust5, use_cust6, use_cust7, use_cust8,
    created_at, created_by, updated_at
)
VALUES (
    'PM_PROJ_SNOTE',
    'PmProjectScheduleNote',
    '/projmng/proj/schedule-note',
    'projmng.proj.schedule-note',
    'PM_PROJ',
    'MENU',
    'AI 일정 메모',
    'lucide:file-pen',
    4,
    1, false, false, true, false, false,
    false, false, false, false,
    true, true,
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'schedule-note', '-infinity'
)
ON CONFLICT (id) DO UPDATE SET
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    pid        = EXCLUDED.pid,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    keep_alive = true,
    status     = 1,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'schedule-note';

-- 일정표의 역할 권한을 그대로 베낀다(머리말).
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
SELECT role_id, 'PM_PROJ_SNOTE',
       can_view, can_search, can_create, can_delete, can_update, false, false,
       false, false, false, false, false, false, false, false,
       now(), 'schedule-note', false
  FROM scom.role_menus
 WHERE menu_id = 'PM_PROJ_SCHED' AND NOT is_deleted
ON CONFLICT (role_id, menu_id) DO NOTHING;

COMMIT;

-- 넣은 결과 확인
SELECT m.id, m.title, m.path, m.route_key, m.order_no,
       r.role_id, r.can_view, r.can_create, r.can_update, r.can_delete
  FROM scom.system_menus m
  LEFT JOIN scom.role_menus r ON r.menu_id = m.id AND NOT r.is_deleted
 WHERE m.id = 'PM_PROJ_SNOTE'
 ORDER BY r.role_id;
