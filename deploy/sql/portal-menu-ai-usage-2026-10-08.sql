-- ============================================================
-- 메뉴 — 포털관리 > 상태관리 > 「AI 사용량」 (2026-10-08)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/AiUsageList.razor
-- 자료: scom.ai_usage_logs (deploy/sql/ai-usage-2026-10-08.sql 를 먼저 돌린다)
--
--   psql -h … -U funeralv2 -d jsiniportal -f portal-menu-ai-usage-2026-10-08.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── 어디에 두나 — 「LLM 장비 상태」 옆 ──────────────────────
--
-- 상태관리 묶음(admin.status)에 넣는다. 바로 위가 「LLM 장비 상태」다 —
-- 그쪽이 **장비가 어떤가**를 보고 이쪽이 **그 장비를 누가 쓰는가**를 본다.
-- 「AI 가 느리다」를 받았을 때 장비 탓인지 사람이 몰린 탓인지 한 자리에서
-- 가를 수 있어야 한다.
--
-- ── path 와 @page 가 같아야 한다 ────────────────────────────
--
-- 라우팅의 주인은 화면의 `@page` 이고 DB 는 메뉴 노출·권한 표다
-- (web/CLAUDE.md). 그래서 여기 적는 path 는 화면이 선언한 주소 그대로다.
-- 사이드바가 실제로 거는 링크는 route_key 로 푼다.
--
-- ── 권한은 「LLM 장비 상태」와 같게 준다 ────────────────────
--
-- **남이 무엇을 얼마나 물었는지 보는 화면**이다. 협력사 역할에 줄 것이
-- 아니다 — 그 선이 이미 그어져 있는 자리를 그대로 따른다(시스템관리자 ·
-- 서버관리자). 서버도 같은 판정을 한 번 더 한다(AiUsageEndpoints) —
-- 게이트웨이의 ai-route 가 /api/ai/** 를 익명으로 열어 두기 때문이다.
-- ============================================================

-- ── 메뉴 한 줄 ──────────────────────────────────────────────
INSERT INTO scom.system_menus (
    id, name, path, route_key, title, icon, type,
    pid, order_no, hide_in_menu, status,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel,
    use_mobile, use_tablet,
    created_at, created_by
)
SELECT
    'SYS_AI_USAGE',
    'AI 사용량',
    '/admin/status/ai-usage',
    'admin.status.ai-usage',
    'AI 사용량',
    -- 장비 상태가 쓰는 것과 같은 계열의 아이콘. 없는 이름을 넣으면 동그라미가
    -- 뜨고 그뿐이라(MenuIcons) 급하지 않다.
    'lucide:activity',
    'MENU',
    -- 상태관리 묶음.
    'fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5',
    -- 「LLM 장비 상태」(6) 바로 다음.
    7,
    false,
    1,
    -- 조회와 엑셀만 쓴다. 이 화면은 **쌓인 것을 보는 자리**라 등록·수정·삭제가
    -- 없다 — 켜 두면 권한 화면에 뜻 없는 칸이 생긴다.
    true,  true,  false, false, false, false, true,
    -- 휴대폰·태블릿에서도 보인다. 「누가 많이 쓰나」는 자리에 없을 때도 보는 값이다.
    true,  true,
    now(),
    'ai-usage'
WHERE NOT EXISTS (
    SELECT 1 FROM scom.system_menus WHERE id = 'SYS_AI_USAGE'
);

-- 이미 있으면 길과 열쇠만 맞춰 둔다. 화면을 옮겼을 때 손으로 고치지 않으려는 것이다.
UPDATE scom.system_menus
   SET path = '/admin/status/ai-usage',
       route_key = 'admin.status.ai-usage',
       is_deleted = false,
       updated_at = now(),
       updated_by = 'ai-usage'
 WHERE id = 'SYS_AI_USAGE'
   AND (path IS DISTINCT FROM '/admin/status/ai-usage'
     OR route_key IS DISTINCT FROM 'admin.status.ai-usage'
     OR is_deleted);

-- ── 권한 ────────────────────────────────────────────────────
--
-- 조회와 엑셀만 준다. 나머지 동작은 이 화면에 없다.
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    is_deleted, created_at, created_by
)
SELECT r.role_id, 'SYS_AI_USAGE',
       true, true, false, false, false, false, true,
       false, false, false, false, false, false, false, false,
       -- NOT NULL 인데 기본값이 없다. 안 적으면 그 자리에서 거절한다.
       false, now(), 'ai-usage'
  FROM (VALUES ('SYSTEM_ADMINISTRATOR'), ('SERVER_ADMIN')) AS r(role_id)
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus
      WHERE menu_id = 'SYS_AI_USAGE' AND role_id = r.role_id
 );
