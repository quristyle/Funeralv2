-- ============================================================
-- 공지 관리 메뉴 등록 + 헬프데스크 공지 제거
-- ============================================================
--
-- 공지는 JSini 관리 포털이 공통으로 관리한다.
-- 헬프데스크가 따로 들고 있던 공지 화면·메뉴는 더 쓰지 않으므로 지운다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- ── 포털 공지 관리 메뉴 ────────────────────────────────────
INSERT INTO scom.system_menus (
  id, pid, name, path, component, type, title, icon,
  order_no, hide_in_menu, keep_alive, status,
  created_at, created_by,
  affix_tab, dom_cached, menu_visible_with_forbidden, is_deleted,
  use_view, use_search, use_create, use_delete, use_update, use_print, use_excel
) VALUES (
  'PORTAL_NOTICE', NULL, 'PortalNotice', '/portal/notice',
  '#/views/portal/notice/list.vue', 'MENU', '공지 관리', 'lucide:megaphone',
  95, false, true, 1,
  now(), 'notice-feature',
  false, false, false, false,
  true, true, true, true, true, false, false
)
ON CONFLICT (id) DO UPDATE SET
  name         = EXCLUDED.name,
  path         = EXCLUDED.path,
  component    = EXCLUDED.component,
  type         = EXCLUDED.type,
  title        = EXCLUDED.title,
  icon         = EXCLUDED.icon,
  order_no     = EXCLUDED.order_no,
  updated_at   = now(),
  updated_by   = 'notice-feature',
  is_deleted   = false;

-- 모든 역할에 권한 부여 (메뉴가 쓴다고 지정한 항목만)
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT r.id, m.id,
       m.use_view, m.use_search, m.use_create, m.use_delete, m.use_update, m.use_print, m.use_excel,
       false, false, false, false, false, false, false, false,
       now(), 'notice-feature', false
FROM scom.roles r
CROSS JOIN scom.system_menus m
WHERE r.is_deleted = false
  AND m.id = 'PORTAL_NOTICE'
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = m.id
  );

-- ── 헬프데스크 공지 제거 ───────────────────────────────────
DELETE FROM scom.role_menus
WHERE menu_id IN ('HD_NOTICE', 'HD_NOTICE_LIST', 'HD_NOTICE_FORM', 'HD_NOTICE_VIEW');

DELETE FROM scom.system_menus
WHERE id IN ('HD_NOTICE_LIST', 'HD_NOTICE_FORM', 'HD_NOTICE_VIEW', 'HD_NOTICE');

COMMIT;

-- 확인
SELECT id, path, title, status FROM scom.system_menus
WHERE id = 'PORTAL_NOTICE' OR id LIKE 'HD_NOTICE%';
