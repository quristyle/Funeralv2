-- ============================================================
-- 역할-메뉴 권한 채우기
-- ============================================================
--
-- 화면들이 JSini 공통 권한(scom.role_menus)을 따르도록 바꾸기 전에,
-- 권한 데이터부터 채워 둔다.
--
-- 지금까지 role_menus 에는 헬프데스크 메뉴 66건만 들어 있었고
-- 나머지 165건(포털 관리·장례식장·예제)은 행 자체가 없었다.
-- 이 상태로 열람 권한 검사를 켜면 모든 사용자가 시스템 관리 화면까지
-- 전부 막힌다. 그래서 먼저 현재 동작 그대로(전부 허용)를 데이터로 남긴다.
--
-- 채워 넣은 뒤에는 역할 권한 화면에서 필요한 것만 끄면 된다.
-- 이미 행이 있는 조합은 건드리지 않으므로 반복 실행해도 안전하다.
--
-- can_* 값은 메뉴가 그 항목을 쓴다고 지정한 경우(use_*)에만 켠다.
-- 디렉터리(CATALOG)는 화면이 없어 use_* 가 모두 꺼져 있으므로 전부 false 로 들어간다.

BEGIN;

INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT
  r.id, m.id,
  m.use_view, m.use_search, m.use_create, m.use_delete, m.use_update, m.use_print, m.use_excel,
  m.use_cust1, m.use_cust2, m.use_cust3, m.use_cust4,
  m.use_cust5, m.use_cust6, m.use_cust7, m.use_cust8,
  now(), 'permission-unification', false
FROM scom.roles r
CROSS JOIN scom.system_menus m
WHERE r.is_deleted = false
  AND m.is_deleted = false
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = m.id
  );

COMMIT;

-- 확인
SELECT r.id AS 역할,
       count(*)                                AS 권한행,
       count(*) FILTER (WHERE rm.can_view)     AS 열람허용
FROM scom.roles r
JOIN scom.role_menus rm ON rm.role_id = r.id AND rm.is_deleted = false
WHERE r.is_deleted = false
GROUP BY r.id
ORDER BY 1;

SELECT count(*) AS 권한행없는_활성메뉴
FROM scom.system_menus m
WHERE m.status = 1 AND m.is_deleted = false
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm WHERE rm.menu_id = m.id AND rm.is_deleted = false
  );
