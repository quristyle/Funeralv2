-- [메시지 발송] 화면을 메뉴에 건다 — 알림관리 › 메시지 발송
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/MessageSendPage.razor
--       @page       /admin/push/send
--       RouteKey    admin.push.send
--
-- [연결 고리는 열쇠다]
--
-- 사이드바는 `route_key` 로 화면을 찾는다(web/CLAUDE.md 「라우팅 소유권이
-- 뒤집혔다」). `path` 는 **권한표와 즐겨찾기의 열쇠**라 한 번 정하면 안 바꾼다.
--
-- **그래서 처음부터 새 주소로 넣는다** — `/admin/push/send`.
--
-- 옆 형제들은 아직 Vue 시절 경로(`/system/push/...`)이고 `menu-path-cutover.sql`
-- 이 그것을 `/admin/push/...` 으로 옮길 참이다. 그 모양을 따라 옛 주소로 넣으면
-- **이 화면의 단추가 전부 사라진다** — 권한 판정의 열쇠가 「지금 열려 있는 화면의
-- 주소」(`PermissionPath.Current`)인데 그것은 `/admin/push/send` 이고, 표에는
-- `/system/push/send` 가 들어 있어 서로 안 맞기 때문이다. 그 어긋남은 오류가
-- 아니라 **「권한 없음」으로 조용히 떨어져서**, 증상이 「단추가 안 보인다」 하나다.
--
-- 새로 넣는 줄이라 지켜야 할 옛 권한도 즐겨찾기도 없다. 이행 SQL 은 지정한
-- 옛 주소만 바꾸므로 이 줄을 건드리지 않는다.
--
-- [권한을 함께 넣는 까닭]
--
-- `role_menus` 에 줄이 없으면 그 역할에게 메뉴가 **안 보이고**, 화면을 직접
-- 열어도 **보내기 단추가 안 나온다**. 권한표가 실린 뒤로는 표에 없는 경로가
-- 곧 「권한 없음」이기 때문이다(`PermissionContext.Can`). 증상이 「메뉴가
-- 없다」·「단추가 없다」로만 보여 원인을 화면에서 찾게 된다.
--
-- 같은 묶음의 옆 화면(`HD_PUSH_DASHBOARD`)이 가진 역할을 그대로 따라간다 —
-- 같은 알림 묶음이고 성격도 같아서 권한이 갈릴 이유가 없다.
--
-- **다만 이 화면은 「보내는」 화면이다.** 등록(`can_create`)이 곧 발송이므로,
-- 옆 화면에서 볼 수만 있던 역할에게 발송까지 열어 주지 않는다 — 옆 화면의
-- `can_create` 를 그대로 물려받는다(그 표에서 조회 권한과 함께 켜져 있다).
--
-- [안전한 성질]
--
--   · 멱등하다. 여러 번 돌려도 결과가 같다.
--   · 되돌리려면 맨 아래 주석의 두 줄을 돌린다.
--   · 배포 순서를 안 탄다. 화면이 아직 안 떠 있으면 그 메뉴만 「준비 중」이
--     뜨고(`_Pending.razor`), 떠 있으면 바로 열린다.

BEGIN;

-- ── 메뉴 ────────────────────────────────────────────────────────
--
-- 자리는 묶음의 **맨 위**다(order_no = 0). 보내는 일이 이 묶음에서 가장 자주
-- 하는 일이고, 현황·이력은 그 결과를 보는 자리라 뒤에 오는 것이 맞다.

INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
VALUES (
    'HD_PUSH_SEND', 'HD_PUSH', 'HdPushSend',
    '/admin/push/send', 'admin.push.send', 'MENU',
    '메시지 발송', 'lucide:send',
    0, 1, false, now(), 'system',
    true, true, true, false, false, false, false)
ON CONFLICT (id) DO UPDATE SET
    pid        = EXCLUDED.pid,
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = EXCLUDED.status,
    use_create = EXCLUDED.use_create,
    use_update = EXCLUDED.use_update,
    use_delete = EXCLUDED.use_delete,
    use_print  = EXCLUDED.use_print,
    use_excel  = EXCLUDED.use_excel,
    updated_at = now(),
    updated_by = 'system';

-- ── 권한 ────────────────────────────────────────────────────────
--
-- 「푸시 현황」을 보는 역할이 그대로 따라온다. 고치기·지우기는 이 화면에
-- 없는 동작이라 꺼 둔다 — 표에 자리는 만들어 두되 켜지 않는 것이 이 표의
-- 관례다(권한 화면에서 나중에 켤 수 있다).

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete,
    can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4,
    can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted)
SELECT
    r.role_id, 'HD_PUSH_SEND',
    r.can_view, r.can_view, r.can_create, false, false,
    false, false,
    false, false, false, false,
    false, false, false, false,
    now(), 'system', false
  FROM scom.role_menus r
 WHERE r.menu_id = 'HD_PUSH_DASHBOARD'
ON CONFLICT (role_id, menu_id) DO NOTHING;

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus WHERE id = 'HD_PUSH_SEND';
--
--   SELECT role_id, can_view, can_create
--     FROM scom.role_menus WHERE menu_id = 'HD_PUSH_SEND' ORDER BY role_id;
--
-- 되돌리기
--
--   DELETE FROM scom.role_menus   WHERE menu_id = 'HD_PUSH_SEND';
--   DELETE FROM scom.system_menus WHERE id      = 'HD_PUSH_SEND';
