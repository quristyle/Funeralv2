-- ============================================================
-- 메뉴 등록 — 프로젝트관리 › 프로젝트 › 메일 템플릿 (2026-10-31)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/MailTemplateList.razor
-- 표:   deploy/sql/projmng-mail-template-2026-10-31.sql (projmng.mail_template)
--
--   psql -h … -U funeralv2 -d jsiniportal -f projmng-mail-template-menu-2026-10-31.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
--
-- 프로젝트 일정표(PM_PROJ_SCHED)의 「메일발송」이 쓰는 틀이라 같은 묶음(PM_PROJ)의 끝(9)에 둔다.
-- 시스템관리자에게만 준다 — 템플릿 하나가 여러 사람이 보내는 메일의 모양을 통째로 바꾼다.
-- 서버도 고치기를 역할로 한 번 더 막는다(ProjMngServer MailTemplate:ManageRoles,
-- 기본 SYSTEM_ADMINISTRATOR · ADMINISTRATOR). 읽기는 일정표를 쓰는 누구나 한다.
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
    'PM_PROJ_MAILTPL',
    '메일 템플릿',
    '/projmng/proj/mail-template',
    'projmng.proj.mail-template',
    'PM_PROJ',
    'MENU',
    '메일 템플릿',
    'lucide:mails',
    9,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, true, true, true, false, true,
    false, false, false, false, false, false, false, false,
    now(), 'mail-template', '-infinity'
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
    updated_by = 'mail-template';

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'PM_PROJ_MAILTPL',
    true, true, true, true, true, false, true,
    false, false, false, false, false, false, false, false,
    now(), 'mail-template', false
)
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view   = true,
    can_search = true,
    can_create = true,
    can_update = true,
    can_delete = true,
    can_excel  = true,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'mail-template';

COMMIT;

-- 넣은 결과 확인
SELECT m.id, m.title, m.path, m.route_key, m.pid, m.order_no, m.status,
       r.role_id, r.can_view, r.can_create, r.can_update, r.can_delete
  FROM scom.system_menus m
  LEFT JOIN scom.role_menus r ON r.menu_id = m.id AND NOT r.is_deleted
 WHERE m.id = 'PM_PROJ_MAILTPL';
