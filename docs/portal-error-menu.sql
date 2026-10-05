-- 포털관리 > 상태 관리 아래에 「오류 추적」 메뉴를 꽂는다.
--
-- 화면은 이미 있다(`/admin/status/error`, 열쇠 `admin.status.error`).
-- 이 SQL 은 **사이드바에 보이게 하고 역할 권한을 걸 수 있게** 할 뿐이다 —
-- 안 돌려도 주소를 치면 열린다(라우팅 소유권은 DB 가 아니라 @page 에 있다).
--
-- 돌리는 곳: 운영 DB `jsiniportal`.
-- 멱등하다 — 두 번 돌려도 한 줄만 생긴다.
--
--   psql -h <host> -U <user> -d jsiniportal -f docs/portal-error-menu.sql
--
-- 메뉴 한 줄과 역할 권한을 **함께** 넣는다. 권한을 안 걸면 메뉴가 생겨도
-- 아무에게도 안 보이므로, 둘을 나누면 반만 한 상태로 끝나기 쉽다.
--
-- 서버 쪽(AuthServer `PortalErrorEndpoints`)이 역할을 한 번 더 본다 —
-- 메뉴 권한만 풀어 주고 역할이 아니면 403 이다. 스택 추적이 나가는 자리라
-- 두 겹으로 둔다.

BEGIN;

INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon, order_no,
    status, hide_in_menu, keep_alive, use_mobile, use_tablet,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel,
    created_at, is_deleted
)
SELECT
    gen_random_uuid()::text,
    parent.id,
    'ErrorTrace',
    '/system/error-trace',
    'admin.status.error',
    'menu',
    '오류 추적',

    -- **이미 아이콘 CSS 에 들어 있는 이름을 쓴다.** `menu-icons.css` 는
    -- 빌드 스크립트가 **DB 에 실제로 든 이름만** 모아 만든 것이라, 새 이름을
    -- 적으면 그림을 못 찾아 기본값(동그라미)이 나온다. 새 그림이 필요하면
    -- `scripts/build-menu-icons.py` 를 다시 돌려야 한다.
    'lucide:file-search',

    -- 상태 관리 아래 맨 뒤. 형제 중 가장 큰 값 + 1.
    COALESCE((SELECT MAX(m.order_no) + 1
              FROM scom.system_menus m
              WHERE m.pid = parent.id AND m.is_deleted = false), 0),

    1,      -- status: 사용
    false,  -- hide_in_menu
    true,   -- keep_alive

    -- **휴대폰 메뉴목록에서는 뺀다.** 이 화면은 스택을 가로로 굴려 읽는
    -- 자리라 작은 화면에서 쓸모가 없다. 라우트는 살아 있으므로 주소로는 열린다.
    false,  -- use_mobile
    true,   -- use_tablet

    -- 조회만 하는 화면이다. 등록·수정·삭제 권한 칸을 켜 두면 역할 권한
    -- 화면에 아무 일도 안 하는 체크박스가 셋 생긴다.
    -- 엑셀은 켠다 — 표에 내려받기가 달려 있다(`ExportName="오류추적"`).
    true,   -- use_view
    true,   -- use_search
    false,  -- use_create
    false,  -- use_update
    false,  -- use_delete
    false,  -- use_print
    true,   -- use_excel

    NOW(),
    false
FROM scom.system_menus parent
WHERE parent.path = '/system/status'
  AND parent.is_deleted = false
  AND NOT EXISTS (
      SELECT 1 FROM scom.system_menus m
      WHERE m.route_key = 'admin.status.error' AND m.is_deleted = false
  );

-- 꽂혔는지 확인. 0 줄이면 부모(`/system/status`)를 못 찾은 것이다 —
-- 그때는 아래로 부모의 실제 경로를 확인하고 위 WHERE 를 고친다.
--
--   SELECT id, path, title FROM scom.system_menus
--    WHERE title LIKE '%상태%' AND is_deleted = false;
SELECT id, pid, path, route_key, title, order_no
  FROM scom.system_menus
 WHERE route_key = 'admin.status.error' AND is_deleted = false;

-- ── 역할 권한 ────────────────────────────────────────────────
--
-- **이것까지 해야 메뉴가 보인다.** 역할-메뉴 권한이 없으면 사이드바에
-- 안 뜨고, 주소로 들어가도 포털관리 화면의 권한 판정에 걸린다.
--
-- 대상은 형제 메뉴(배포 현황)와 같다 — 관리자 · 시스템관리자.
-- **열람·조회만 준다.** 이 화면은 읽기만 하는 자리라 나머지 권한 칸은
-- 메뉴 쪽 use_* 에서 이미 꺼 두었다(위 INSERT 참고).
--
-- 역할을 가리키는 값은 roles.id 다 — 토큰의 역할 클레임이 그것이고
-- (`AccessTokenFactory` 가 `ClaimTypes.Role` 에 role_id 를 담는다),
-- 서버의 관리자 판정(`PortalErrorEndpoints.IsAdmin`)도 같은 값을 본다.

-- **id 를 적지 않는다.** `role_menus.id` 는 identity 정수다(메뉴와 달리
-- GUID 가 아니다) — 적으면 「column "id" is of type integer」로 통째로 깨진다.
--
-- can_* 열다섯이 전부 NOT NULL 이고 기본값이 없다. 하나라도 빠뜨리면
-- 같은 자리에서 멈춘다.
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, is_deleted
)
SELECT
    r.id, m.id,
    true,  -- 열람
    true,  -- 조회
    false, false, false, false,
    true,  -- 엑셀 내려받기
    false, false, false, false, false, false, false, false,
    NOW(), false
  FROM scom.roles r
  CROSS JOIN scom.system_menus m
 WHERE m.route_key = 'admin.status.error' AND m.is_deleted = false
   AND r.id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR') AND r.is_deleted = false
   AND NOT EXISTS (SELECT 1 FROM scom.role_menus rm
                    WHERE rm.role_id = r.id AND rm.menu_id = m.id AND rm.is_deleted = false);

SELECT rm.role_id, rm.can_view, rm.can_search, rm.can_excel
  FROM scom.role_menus rm
  JOIN scom.system_menus m ON m.id = rm.menu_id
 WHERE m.route_key = 'admin.status.error' AND rm.is_deleted = false AND m.is_deleted = false;

COMMIT;
