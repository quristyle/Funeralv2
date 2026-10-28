-- ============================================================
-- 메뉴 및 알림 등록 — 포털관리 › 상태관리 › 내부 서버 Ping 모니터링 (2026-10-28)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/ServerPingMonitor.razor
-- 대상 DB: jsiniportal (스키마 scom)
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/portal-menu-server-ping-2026-10-28.sql
--
-- 두 번 돌려도 안전하다 (ON CONFLICT DO UPDATE).
-- ============================================================

BEGIN;

-- 1. 메뉴 등록 (상태관리 카탈로그 fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5 아래)
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
    'SYS_INTERNAL_SERVER_PING',
    'ServerPingMonitor',
    '/admin/status/server-ping',
    'admin.status.server-ping',
    'fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5',
    'MENU',
    '내부 서버 Ping 모니터링',
    'lucide:network',
    10,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'admin', '-infinity'
)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    path = EXCLUDED.path,
    route_key = EXCLUDED.route_key,
    pid = EXCLUDED.pid,
    title = EXCLUDED.title,
    icon = EXCLUDED.icon,
    order_no = EXCLUDED.order_no,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'admin';

-- 2. 시스템관리자(SYSTEM_ADMINISTRATOR) 역할에 권한 부여
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'SYS_INTERNAL_SERVER_PING',
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'admin', false
)
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view = true,
    can_search = true,
    can_create = true,
    can_delete = true,
    can_update = true,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'admin';

-- 3. 알림 이벤트 등록 (scom.notification_events)
INSERT INTO scom.notification_events (
    id, name, description, category, source, target_kind,
    supports_push, supports_email, email_from_push, policy_required,
    governed, is_active, order_no, created_at, created_by, is_deleted
)
VALUES (
    'SERVER_PING',
    '내부 서버 Ping (192.168.219.229)',
    '192.168.219.229 장비 통신 불가(서버 다운) 시 알림',
    'SERVER_PING',
    'NotificationServer',
    'ROLE',
    true, true, true, false,
    true, true, 55, now(), 'admin', false
)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    supports_push = EXCLUDED.supports_push,
    supports_email = EXCLUDED.supports_email,
    email_from_push = EXCLUDED.email_from_push,
    is_active = true,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'admin';

-- 4. 시스템관리자(SYSTEM_ADMINISTRATOR)에 기본 수신 정책 등록 (scom.notification_policies)
INSERT INTO scom.notification_policies (
    id, event_code, role_id, push_enabled, email_enabled, is_active,
    created_at, created_by, is_deleted
)
VALUES (
    'pol-server-ping-sysadmin',
    'SERVER_PING',
    'SYSTEM_ADMINISTRATOR',
    true, true, true,
    now(), 'admin', false
)
ON CONFLICT (event_code, role_id) DO UPDATE SET
    push_enabled = true,
    email_enabled = true,
    is_active = true,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'admin';

COMMIT;
