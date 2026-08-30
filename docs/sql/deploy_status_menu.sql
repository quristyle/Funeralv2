-- DB: jsiniportal (scom)
-- 상태관리 > 배포 현황 메뉴 등록 + 관리자 역할 연결. 반복 실행 안전.
-- 화면: fronts/apps/jsini-portal/src/views/portal/system/deploy-status/index.vue
-- API: AuthServer /auth/deploy-status (docs/analysis/39 참조)

INSERT INTO scom.system_menus
    (id, name, path, component, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, affix_tab, dom_cached, keep_alive,
     menu_visible_with_forbidden, is_deleted)
VALUES
    ('SYS_DEPLOY_STATUS', 'DeployStatus', '/system/deploy-status',
     '#/views/portal/system/deploy-status/index.vue',
     'fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5',  -- 상태관리 CATALOG
     'MENU', '배포 현황', 'lucide:rocket', 4,
     false, 1, now(), false, false, false, false, false)
ON CONFLICT (id) DO UPDATE SET
    path = EXCLUDED.path,
    component = EXCLUDED.component,
    title = EXCLUDED.title,
    icon = EXCLUDED.icon,
    status = 1,
    is_deleted = false,
    updated_at = now();

-- 배포 내부 정보라 관리자 계열에만 연다 (파트너 제외).
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel, can_cust1, can_cust2, can_cust3, can_cust4,
     can_cust5, can_cust6, can_cust7, can_cust8, created_at, is_deleted)
SELECT t.role_id, 'SYS_DEPLOY_STATUS', true, true, false, false, false,
       false, false, false, false, false, false,
       false, false, false, false, now(), false
FROM (VALUES ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')) AS t(role_id)
WHERE NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = t.role_id AND rm.menu_id = 'SYS_DEPLOY_STATUS'
);
