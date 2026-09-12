-- [마인드맵] 화면을 메뉴에 건다 — 프로젝트관리 › 설계 › 마인드맵
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/MindMapView.razor
--       @page       /projmng/design/mind-map
--       RouteKey    projmng.design.mind-map
--
-- [연결 고리는 열쇠다]
--
-- 사이드바는 `route_key` 로 화면을 찾는다(web/CLAUDE.md 「라우팅 소유권이
-- 뒤집혔다」). `path` 는 **권한표와 즐겨찾기의 열쇠**라 한 번 정하면 안
-- 바꾼다 — 여기서는 새로 넣는 줄이라 둘을 같은 뜻으로 맞춰 둔다.
--
-- [권한을 함께 넣는 까닭]
--
-- `role_menus` 에 줄이 없으면 그 역할에게 메뉴가 **안 보인다.** 화면만 만들고
-- 메뉴만 넣으면 "만들었는데 아무한테도 안 보인다" 가 되는데, 증상이 「메뉴가
-- 없다」라서 원인을 화면 쪽에서 찾게 된다. 설계 묶음의 옆 화면
-- (`PM_DESIGN_USECASE`)이 가진 역할을 그대로 따라간다 — 같은 묶음의 같은
-- 성격의 화면이라 권한이 갈릴 이유가 없다.
--
-- 유즈케이스와 다른 칸이 둘 있다 — **등록과 삭제**. 저 화면은 그림 한 장을
-- 고치기만 하지만, 이쪽은 마인드맵을 **새로 만들고 통째로 지우는** 단추가
-- 화면에 있다(`MenuAction.Create` · `MenuAction.Delete`).
--
-- [안전한 성질]
--
--   · 멱등하다. 여러 번 돌려도 결과가 같다.
--   · 되돌리려면 맨 아래 주석의 두 줄을 돌린다.
--   · 배포 순서를 안 탄다. 화면이 아직 안 떠 있으면 그 메뉴만 「준비 중」이
--     뜨고(`_Pending.razor`), 떠 있으면 바로 열린다.

BEGIN;

-- ── 메뉴 ────────────────────────────────────────────────────────

INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
VALUES (
    'PM_DESIGN_MINDMAP', 'PM_DESIGN', 'PmDesignMindMap',
    '/projmng/design/mind-map', 'projmng.design.mind-map', 'MENU',
    '마인드맵', 'lucide:network',
    4, 1, false, now(), 'system',
    true, true, true, true, true, false, false)
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
    use_print  = EXCLUDED.use_print,
    use_excel  = EXCLUDED.use_excel,
    updated_at = now(),
    updated_by = 'system';

-- ── 권한 ────────────────────────────────────────────────────────
--
-- 옆 화면(유즈케이스)을 볼 수 있는 역할에게 준다. 못 보는 역할
-- (FUNERAL_OPERATOR)은 줄은 생기되 전부 꺼진 채다 — 권한 화면에서 나중에
-- 켤 수 있게 자리를 만들어 두는 것이 이 표의 관례다.

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete,
    can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4,
    can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted)
SELECT
    r.role_id, 'PM_DESIGN_MINDMAP',
    r.can_view, r.can_view, r.can_view, r.can_view, r.can_view,
    false, false,
    false, false, false, false,
    false, false, false, false,
    now(), 'system', false
  FROM scom.role_menus r
 WHERE r.menu_id = 'PM_DESIGN_USECASE'
ON CONFLICT (role_id, menu_id) DO NOTHING;

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus WHERE id = 'PM_DESIGN_MINDMAP';
--
--   SELECT role_id, can_view, can_update, can_delete
--     FROM scom.role_menus WHERE menu_id = 'PM_DESIGN_MINDMAP' ORDER BY role_id;
--
-- 되돌리기
--
--   DELETE FROM scom.role_menus   WHERE menu_id = 'PM_DESIGN_MINDMAP';
--   DELETE FROM scom.system_menus WHERE id      = 'PM_DESIGN_MINDMAP';
