-- 헬프데스크 > 한주 설비 아래에 「한주계량기 수집상태」 메뉴를 꽂는다.
--
-- 화면은 이미 있다(`/helpdesk/hanju/gauge-collection`,
-- 열쇠 `helpdesk.hanju.gauge-collection`). 이 SQL 은 **사이드바에 보이게 하고
-- 역할 권한을 걸 수 있게** 할 뿐이다 — 라우팅 소유권은 DB 가 아니라 `@page` 에
-- 있어서, 안 돌려도 주소를 치면 열린다(다만 권한이 없으면 막힌다).
--
-- 돌리는 곳: 운영 DB `jsiniportal`. **개발 장비도 같은 DB 를 본다** —
-- 여기서 돌린 것이 곧 운영이다.
--
--   psql -h <host> -U <user> -d jsiniportal -f docs/helpdesk-hanju-gauge-collection.sql
--
-- 멱등하다 — 두 번 돌려도 한 줄만 생긴다.
--
-- 메뉴 한 줄과 역할 권한을 **함께** 넣는다. 권한을 안 걸면 메뉴가 생겨도
-- 아무에게도 안 보이므로, 둘을 나누면 반만 한 상태로 끝나기 쉽다.

BEGIN;

INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon, order_no,
    status, hide_in_menu, keep_alive, use_mobile, use_tablet,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel,
    created_at, created_by, is_deleted
)
SELECT
    'HD_HANJU_GAUGE',
    parent.id,
    'HelpDeskHanjuGaugeCollection',
    '/helpdesk/hanju/gauge-collection',
    'helpdesk.hanju.gauge-collection',
    'MENU',
    '한주계량기 수집상태',

    -- **이미 아이콘 CSS 에 들어 있는 이름을 쓴다.** `menu-icons.css` 는
    -- 빌드 스크립트가 **DB 에 실제로 든 이름만** 모아 만든 것이라, 새 이름을
    -- 적으면 그림을 못 찾아 기본값(동그라미)이 나온다.
    'lucide:gauge',

    -- 한주 설비 아래 맨 뒤. 형제 중 가장 큰 값 + 1.
    COALESCE((SELECT MAX(m.order_no) + 1
              FROM scom.system_menus m
              WHERE m.pid = parent.id AND m.is_deleted = false), 0),

    1,      -- status: 사용
    false,  -- hide_in_menu
    true,   -- keep_alive

    -- **휴대폰 메뉴목록에서는 뺀다.** 좌우로 나눈 판이라 좁은 화면에서는
    -- 나무와 이력이 위아래로 쌓여 쓸모가 없다. 라우트는 살아 있으므로
    -- 주소로는 열린다.
    false,  -- use_mobile
    true,   -- use_tablet

    -- 조회가 전부인 화면이다(ADJ 재계산만 쓰는데 그것은 한주 쪽 자료라
    -- 우리 권한 칸으로 가르지 않는다). 등록·수정·삭제 칸을 켜 두면 역할
    -- 권한 화면에 아무 일도 안 하는 체크박스가 셋 생긴다.
    -- 엑셀은 켠다 — 표에 내려받기가 달려 있다.
    true,   -- use_view
    true,   -- use_search
    false,  -- use_create
    false,  -- use_update
    false,  -- use_delete
    false,  -- use_print
    true,   -- use_excel

    NOW(),
    'hanju-gauge-collection',
    false
FROM scom.system_menus parent
WHERE parent.path = '/helpdesk/hanju'
  AND parent.is_deleted = false
  AND NOT EXISTS (
      SELECT 1 FROM scom.system_menus m
      WHERE m.route_key = 'helpdesk.hanju.gauge-collection' AND m.is_deleted = false
  );

-- 꽂혔는지 확인. 0 줄이면 부모(`/helpdesk/hanju`)를 못 찾은 것이다.
SELECT id, pid, path, route_key, title, order_no
  FROM scom.system_menus
 WHERE route_key = 'helpdesk.hanju.gauge-collection' AND is_deleted = false;

-- ── 역할 권한 ────────────────────────────────────────────────
--
-- **이것까지 해야 메뉴가 보인다.** 역할-메뉴 권한이 없으면 사이드바에 안 뜨고,
-- 주소로 들어가도 막힌다(2026-10-13 부터 권한관리 밖의 주소는 기본 차단이다).
--
-- 대상은 **시스템관리자 하나**다 — 형제인 「한주운영시스템 모니터링」과 같다.
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
    false, false, false, false,
    true,  -- 엑셀 내려받기
    false, false, false, false, false, false, false, false,
    NOW(), 'hanju-gauge-collection', false
  FROM scom.roles r
  CROSS JOIN scom.system_menus m
 WHERE m.route_key = 'helpdesk.hanju.gauge-collection' AND m.is_deleted = false
   AND r.id = 'SYSTEM_ADMINISTRATOR' AND r.is_deleted = false
   AND NOT EXISTS (SELECT 1 FROM scom.role_menus rm
                    WHERE rm.role_id = r.id AND rm.menu_id = m.id AND rm.is_deleted = false);

SELECT rm.role_id, rm.can_view, rm.can_search, rm.can_excel
  FROM scom.role_menus rm
  JOIN scom.system_menus m ON m.id = rm.menu_id
 WHERE m.route_key = 'helpdesk.hanju.gauge-collection'
   AND rm.is_deleted = false AND m.is_deleted = false;

COMMIT;
