-- 헬프데스크 아래에 「접수 대시보드」 메뉴를 꽂는다.
--
-- 화면은 이미 있다(`/helpdesk/dashboard/reception`,
-- 열쇠 `helpdesk.dashboard.reception`). 이 SQL 은 **사이드바에 보이게 하고
-- 역할 권한을 걸 수 있게** 할 뿐이다 — 라우팅 소유권은 DB 가 아니라 `@page` 에
-- 있어서, 안 돌려도 주소를 치면 열린다(다만 권한이 없으면 막힌다).
--
-- 돌리는 곳: 운영 DB `jsiniportal`. **개발 장비도 같은 DB 를 본다** —
-- 여기서 돌린 것이 곧 운영이다.
--
--   psql -h <host> -U <user> -d jsiniportal -f docs/helpdesk-reception-dashboard.sql
--
-- 멱등하다 — 두 번 돌려도 한 줄만 생긴다.

BEGIN;

INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon, order_no,
    status, hide_in_menu, keep_alive, use_mobile, use_tablet,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel,
    created_at, created_by, is_deleted
)
SELECT
    'HD_DASHBOARD_RCP',
    parent.id,
    'HelpDeskReceptionDashboard',
    '/helpdesk/dashboard/reception',
    'helpdesk.dashboard.reception',
    'MENU',
    '접수 대시보드',

    -- **이미 아이콘 CSS 에 들어 있는 이름을 쓴다.** `menu-icons.css` 는
    -- 빌드 스크립트가 **DB 에 실제로 든 이름만** 모아 만든 것이라, 새 이름을
    -- 적으면 그림을 못 찾아 기본값(동그라미)이 나온다.
    -- 고리 둘이 이 화면의 얼굴이라 원 그래프를 고른다.
    'lucide:pie-chart',

    -- 현황 둘(1·2) 바로 다음.
    3,

    1,      -- status: 사용
    false,  -- hide_in_menu
    true,   -- keep_alive

    -- 휴대폰·태블릿에서도 쓴다. 위 세 덩이와 카드 묶음이 모두 좁은 화면에서
    -- 아래로 쌓인다(helpdesk.css 의 `hd-rcp__top`).
    true,   -- use_mobile
    true,   -- use_tablet

    -- 조회만 하는 화면이다. 등록·수정·삭제 칸을 켜 두면 역할 권한 화면에
    -- 아무 일도 안 하는 체크박스가 셋 생긴다.
    -- 엑셀도 끈다 — 이 화면에는 내려받을 표가 없다(카드와 고리뿐이다).
    true,   -- use_view
    true,   -- use_search
    false,  -- use_create
    false,  -- use_update
    false,  -- use_delete
    false,  -- use_print
    false,  -- use_excel

    NOW(),
    'helpdesk-reception-dashboard',
    false
FROM scom.system_menus parent
WHERE parent.path = '/helpdesk'
  AND parent.is_deleted = false
  AND NOT EXISTS (
      SELECT 1 FROM scom.system_menus m
      WHERE m.route_key = 'helpdesk.dashboard.reception' AND m.is_deleted = false
  );

SELECT id, pid, path, route_key, title, order_no
  FROM scom.system_menus
 WHERE route_key = 'helpdesk.dashboard.reception' AND is_deleted = false;

-- ── 역할 권한 ────────────────────────────────────────────────
--
-- **이것까지 해야 메뉴가 보인다.** 역할-메뉴 권한이 없으면 사이드바에 안 뜨고,
-- 주소로 들어가도 막힌다(2026-10-13 부터 권한관리 밖의 주소는 기본 차단이다).
--
-- 대상은 **헬프데스크가 담당자로 보는 두 역할**이다 —
-- `HelpdeskIdentityOptions.AdminRoles` 의 값과 같다. 형제인 「헬프데스크
-- 현황」은 고객 역할(HELPDESK_USER · PARTNER …)에게도 열려 있지만 이 화면은
-- 그러면 안 된다: **담당자별 카드가 남의 접수율과 완료율을 그대로 보여 준다.**
--
-- **id 를 적지 않는다.** `role_menus.id` 는 identity 정수다. can_* 열다섯이
-- 전부 NOT NULL 이고 기본값이 없어서 하나라도 빠뜨리면 통째로 깨진다.
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted
)
SELECT
    r.id, m.id,
    true,  -- 열람
    true,  -- 조회
    false, false, false, false, false,
    false, false, false, false, false, false, false, false,
    NOW(), 'helpdesk-reception-dashboard', false
  FROM scom.roles r
  CROSS JOIN scom.system_menus m
 WHERE m.route_key = 'helpdesk.dashboard.reception' AND m.is_deleted = false
   AND r.id IN ('SYSTEM_ADMINISTRATOR', 'ADMINISTRATOR') AND r.is_deleted = false
   AND NOT EXISTS (SELECT 1 FROM scom.role_menus rm
                    WHERE rm.role_id = r.id AND rm.menu_id = m.id AND rm.is_deleted = false);

SELECT rm.role_id, rm.can_view, rm.can_search
  FROM scom.role_menus rm
  JOIN scom.system_menus m ON m.id = rm.menu_id
 WHERE m.route_key = 'helpdesk.dashboard.reception'
   AND rm.is_deleted = false AND m.is_deleted = false;

COMMIT;
