-- ============================================================
-- 내부 Ping 감시 장비 관리 테이블 및 메뉴 등록 (2026-10-30)
-- ============================================================
-- 대상 DB: jsiniportal (스키마 scom)
--   psql -h 127.0.0.1 -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/portal-menu-server-ping-manage-2026-10-30.sql
-- ============================================================

BEGIN;

-- 1. 장비 목록 테이블 생성 (scom.server_ping_targets)
CREATE TABLE IF NOT EXISTS scom.server_ping_targets (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    ip VARCHAR(50) NOT NULL,
    timeout_ms INT NOT NULL DEFAULT 2000,
    is_active BOOLEAN NOT NULL DEFAULT true,
    sort_order INT NOT NULL DEFAULT 0,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by VARCHAR(50) NOT NULL DEFAULT 'admin',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by VARCHAR(50) NOT NULL DEFAULT 'admin',
    is_deleted BOOLEAN NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_server_ping_targets_active 
ON scom.server_ping_targets (is_active, is_deleted, sort_order);

-- 초기 기본 장비 2건 적재 (기존 그리드 중계 장비, jin sm 장비)
INSERT INTO scom.server_ping_targets (name, ip, timeout_ms, is_active, sort_order, description, created_by, updated_by)
SELECT '그리드 중계 장비', '192.168.219.229', 2000, true, 1, '내부 그리드 중계 장비 Ping 모니터링', 'admin', 'admin'
WHERE NOT EXISTS (SELECT 1 FROM scom.server_ping_targets WHERE ip = '192.168.219.229' AND is_deleted = false);

INSERT INTO scom.server_ping_targets (name, ip, timeout_ms, is_active, sort_order, description, created_by, updated_by)
SELECT 'jin sm 장비', '192.168.219.224', 2000, true, 2, '내부 jin sm 장비 Ping 모니터링', 'admin', 'admin'
WHERE NOT EXISTS (SELECT 1 FROM scom.server_ping_targets WHERE ip = '192.168.219.224' AND is_deleted = false);

-- 2. 메뉴 등록 (상태관리 카탈로그 fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5 아래)
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
    'SYS_SERVER_PING_MANAGE',
    'ServerPingManage',
    '/admin/status/server-ping-manage',
    'admin.status.server-ping-manage',
    'fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5',
    'MENU',
    'Ping 감시 장비 관리',
    'lucide:server',
    11,
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

-- 3. 시스템관리자(SYSTEM_ADMINISTRATOR) 역할에 권한 부여
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'SYS_SERVER_PING_MANAGE',
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

COMMIT;
