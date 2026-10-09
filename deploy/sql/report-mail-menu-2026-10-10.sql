-- ============================================================
-- 메뉴 등록 — 포털관리 › 시스템관리 › 보고서 메일 (2026-10-10)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/ReportMailList.razor
-- 표:   deploy/sql/report-mail-2026-10-10.sql (scom.report_mail_schedules 외 둘)
-- 문서: docs/report-mail.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f report-mail-menu-2026-10-10.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
--
-- ── 왜 시스템관리 묶음인가 ──────────────────────────────────
--
-- 바로 옆이 「계정 관리」·「메뉴 관리」·「차량 관리」다. 이 화면이 고치는 것도
-- 역할(scom.roles)에 걸린 **사람 묶음**이지 헬프데스크의 보고서 자체가 아니다 —
-- 보고서는 고르기만 하고 자료에는 손대지 않는다. 그래서 헬프데스크의 「운영
-- 리포트」 아래가 아니라 관리 쪽에 둔다. order_no 는 그 다음(5)이다.
--
-- ── path 와 route_key 를 둘 다 적는다 ───────────────────────
--
-- 연결 고리는 route_key 다(web/CLAUDE.md 「연결 고리는 URL 이 아니라 열쇠다」).
-- path 는 **권한표(role_menus)와 즐겨찾기의 열쇠**라 한 번 정하면 안 바꾼다 —
-- 새로 만드는 메뉴라 처음부터 @page 와 같은 값으로 적어 둔다.
--
-- ── 시스템관리자만 본다 ─────────────────────────────────────
--
-- 이 화면은 **누구에게 메일이 나가는지를 정하는 자리**다. 권한을 넓게 열면
-- 역할 하나를 더하는 것으로 수십 명의 메일함에 주기 발송을 걸 수 있다.
-- 그래서 SYSTEM_ADMINISTRATOR 하나만 넣는다 — 더 줄 역할이 생기면 메뉴 권한
-- 화면(/admin/auth/menu-role)에서 더한다. **서버의 조회·저장도 같은 표를
-- 본다**(AuthServer 의 MenuViewAccess) — 역할을 더하면 그 순간부터 따라가고
-- 코드를 고칠 일이 없다.
--
-- 권한 항목은 다섯을 켠다(열람 · 조회 · 등록 · 수정 · 삭제). 출력·엑셀은
-- 끈다 — 내보낼 것이 없는 화면에 눌러도 아무 일 없는 체크칸을 만들지 않는다.
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
    'SYS_REPORT_MAIL',
    '보고서 메일',
    '/admin/system/report-mail',
    'admin.system.report-mail',
    'b24f9105-0dbf-4d30-a13d-b7997def1d5d',   -- 시스템관리(CATALOG)
    'MENU',
    '보고서 메일',
    'lucide:mail-check',
    5,
    1, false, false, false, false, false,
    false, false, false, false,
    true, true,
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'report-mail', '-infinity'
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
    use_print  = false,
    use_excel  = false,
    updated_at = now(),
    updated_by = 'report-mail';

-- 시스템관리자에게만 준다(머리말).
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
VALUES (
    'SYSTEM_ADMINISTRATOR', 'SYS_REPORT_MAIL',
    true, true, true, true, true, false, false,
    false, false, false, false, false, false, false, false,
    now(), 'report-mail', false
)
ON CONFLICT (role_id, menu_id) DO UPDATE SET
    can_view   = true,
    can_search = true,
    can_create = true,
    can_update = true,
    can_delete = true,
    can_print  = false,
    can_excel  = false,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'report-mail';

COMMIT;

-- 넣은 결과 확인
SELECT m.id, m.title, m.path, m.route_key, m.order_no, m.status,
       r.role_id, r.can_view, r.can_create, r.can_update, r.can_delete
  FROM scom.system_menus m
  LEFT JOIN scom.role_menus r ON r.menu_id = m.id AND NOT r.is_deleted
 WHERE m.id = 'SYS_REPORT_MAIL';
