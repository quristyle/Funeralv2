-- ============================================================
-- 배포 도구 메뉴를 헬프데스크 → JSini 포털로 이전
-- ============================================================
--
-- 배포는 JSini 관리 포털이 관장한다. 대상은 서버 설정(Release:Targets)에서 읽으므로
-- 시스템이 늘어도 화면·메뉴는 그대로다.
--
-- 배포 실행은 CRUD 어디에도 맞지 않아 사용자 정의 1번 권한(can_cust1)에 묶었다.
-- 메뉴에 'C1 = 배포 실행' 이름을 붙여 두어 역할 권한 화면에서 그 이름으로 보인다.
--
-- 반복 실행해도 안전하다.

BEGIN;

INSERT INTO scom.system_menus (
  id, pid, name, path, component, type, title, icon,
  order_no, hide_in_menu, keep_alive, status,
  created_at, created_by,
  affix_tab, dom_cached, menu_visible_with_forbidden, is_deleted,
  use_view, use_search, use_create, use_delete, use_update, use_print, use_excel,
  use_cust1, cust1_name
) VALUES (
  'PORTAL_RELEASE', NULL, 'PortalRelease', '/portal/release',
  '#/views/portal/release/index.vue', 'MENU', '배포 도구', 'lucide:rocket',
  96, false, true, 1,
  now(), 'release-migration',
  false, false, false, false,
  true, false, false, false, false, false, false,
  true, '배포 실행'
)
ON CONFLICT (id) DO UPDATE SET
  name        = EXCLUDED.name,
  path        = EXCLUDED.path,
  component   = EXCLUDED.component,
  type        = EXCLUDED.type,
  title       = EXCLUDED.title,
  icon        = EXCLUDED.icon,
  order_no    = EXCLUDED.order_no,
  use_view    = EXCLUDED.use_view,
  use_cust1   = EXCLUDED.use_cust1,
  cust1_name  = EXCLUDED.cust1_name,
  updated_at  = now(),
  updated_by  = 'release-migration',
  is_deleted  = false;

-- 권한 부여. 배포 실행(can_cust1)은 위험한 동작이라 최고 관리자 역할에만 준다.
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT r.id, 'PORTAL_RELEASE',
       true, false, false, false, false, false, false,
       (r.id = 'SYSTEM_ADMINISTRATOR'),
       false, false, false, false, false, false, false,
       now(), 'release-migration', false
FROM scom.roles r
WHERE r.is_deleted = false
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = 'PORTAL_RELEASE'
  );

-- 헬프데스크 배포 메뉴 제거
DELETE FROM scom.role_menus   WHERE menu_id = 'HD_SYS_RELEASE';
DELETE FROM scom.system_menus WHERE id      = 'HD_SYS_RELEASE';

COMMIT;

-- 확인
SELECT m.id, m.path, m.title, m.cust1_name,
       count(*) FILTER (WHERE rm.can_cust1) AS 배포실행_허용역할
FROM scom.system_menus m
LEFT JOIN scom.role_menus rm ON rm.menu_id = m.id AND rm.is_deleted = false
WHERE m.id IN ('PORTAL_RELEASE', 'HD_SYS_RELEASE')
GROUP BY m.id, m.path, m.title, m.cust1_name;
