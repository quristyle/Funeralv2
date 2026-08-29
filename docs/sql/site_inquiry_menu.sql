-- ============================================================
-- [사이트 문의내역] 화면을 포털 메뉴로 등록한다.
--
-- 대상 DB : jsiniportal (scom)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/site_inquiry_menu.sql
--
-- 여러 번 실행해도 안전하다 (같은 id 면 갱신, status 는 덮어쓰지 않는다).
--
-- 회사 소개 사이트(www.jsini.co.kr)의 문의 폼 접수를 보고 답장하는 화면이다.
-- [회사 관리] 아래에 둔다 — 소개 사이트 관리 화면이 늘어나면 그때 폴더로 묶는다.
-- ============================================================

BEGIN;

INSERT INTO scom.system_menus (
  id, name, path, component, pid, type, title, icon, order_no,
  hide_in_menu, keep_alive, affix_tab, dom_cached, menu_visible_with_forbidden,
  status, created_at, created_by, updated_at, updated_by, is_deleted,
  use_view, use_search, use_create, use_update, use_delete, use_print, use_excel
)
SELECT
  'SITE_INQUIRY', 'SiteInquiry', '/company/site-inquiries',
  '#/views/portal/site/inquiries.vue',
  m.id, 'MENU', '사이트 문의내역', 'lucide:mail-question', 90,
  false, true, false, false, false,
  1, now(), 'site-inquiry', now(), 'site-inquiry', false,
  true, true, false, true, false, false, false
FROM scom.system_menus m
WHERE m.title = 'menu.company-management' AND m.is_deleted = false
ON CONFLICT (id) DO UPDATE SET
  path         = EXCLUDED.path,
  component    = EXCLUDED.component,
  pid          = EXCLUDED.pid,
  title        = EXCLUDED.title,
  icon         = EXCLUDED.icon,
  order_no     = EXCLUDED.order_no,
  use_update   = EXCLUDED.use_update,
  updated_at   = now(),
  updated_by   = 'site-inquiry',
  is_deleted   = false;

-- 관리자 두 역할에만 준다. 넓히려면 역할 권한 화면에서 켠다.
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT
  r.id, 'SITE_INQUIRY',
  true, true, false, false, true, false, false,
  false, false, false, false, false, false, false, false,
  now(), 'site-inquiry', false
FROM scom.roles r
WHERE r.is_deleted = false
  AND r.id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = 'SITE_INQUIRY'
  );

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT id, pid, title, path, status FROM scom.system_menus WHERE id = 'SITE_INQUIRY';
