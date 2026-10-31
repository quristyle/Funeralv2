-- ============================================================
-- 메뉴: 설정 › 내 알림 설정 (2026-10-31)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Funeral/Components/Pages/MyNotificationSettingPage.razor
-- 열쇠: funeral.setting.notification
--
-- 「환경설정」 바로 아래에 둔다. 누구나 자기 알림을 고칠 수 있어야 하므로 권한은
-- 환경설정(ENV_SETTING)의 역할 줄을 그대로 베낀다.
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
-- ============================================================

BEGIN;

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
    'MY_NOTIFY_SETTING',
    '내 알림 설정',
    '/funeral/setting/notification',
    'funeral.setting.notification',
    'SETTING',
    'MENU',
    '내 알림 설정',
    'lucide:bell',
    1,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, false, false, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'my-notify-setting', '-infinity'
)
ON CONFLICT (id) DO UPDATE SET
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    pid        = EXCLUDED.pid,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = 1,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'my-notify-setting';

-- 업무 설정을 한 칸 뒤로 민다(환경설정 0 · 내 알림 설정 1 · 업무 설정 2).
UPDATE scom.system_menus
   SET order_no = 2, updated_at = now(), updated_by = 'my-notify-setting'
 WHERE id = 'WORK_OPTIONS' AND order_no < 2;

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
SELECT role_id, 'MY_NOTIFY_SETTING',
       can_view, can_search, false, false, can_update, false, false,
       false, false, false, false, false, false, false, false,
       now(), 'my-notify-setting', false
  FROM scom.role_menus
 WHERE menu_id = 'ENV_SETTING' AND NOT is_deleted
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view   = EXCLUDED.can_view,
    can_search = EXCLUDED.can_search,
    can_update = EXCLUDED.can_update,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'my-notify-setting';

COMMIT;
