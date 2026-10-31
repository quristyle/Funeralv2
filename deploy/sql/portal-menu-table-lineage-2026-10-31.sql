-- ============================================================
-- 「테이블 계통도」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   테이블 계통도  PM_DESIGN_LINEAGE  /projmng/design/table-lineage  projmng.design.table-lineage
--
-- 프로젝트관리 › 설계(PM_DESIGN) 아래, 마인드맵 다음에 붙인다. ERD 와 같은 갈래다 —
-- 프로젝트 · DB 접속을 고르고 그 스키마의 표를 그린다.
--
-- 권한은 「프로젝트 ERD」(PM_DESIGN_ERD)에서 베낀다. 같은 사람들이 같은 접속의 그림을
-- 보고 고친다. 역할 이름을 손으로 적지 않는다 — 베껴 두면 역할이 늘고 줄 때 함께 따라간다.
--
-- 두 번 돌려도 안전하다.

BEGIN;

INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     use_mobile, use_tablet,
     route_key)
VALUES
    ('PM_DESIGN_LINEAGE', 'PmDesignLineage', '/projmng/design/table-lineage', 'PM_DESIGN', 'MENU',
     '테이블 계통도', 'lucide:table-properties', 5,
     false, 1, now(), 'table-lineage-menu', false,
     true, true, false, true, false, false, false,
     true, true,
     'projmng.design.table-lineage')
ON CONFLICT (id) DO NOTHING;

INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT s.role_id, 'PM_DESIGN_LINEAGE',
       s.can_view, s.can_search, false, false, s.can_update,
       false, false,
       false, false, false, false, false, false, false, false,
       false, now(), 'table-lineage-menu'
  FROM scom.role_menus s
 WHERE s.menu_id = 'PM_DESIGN_ERD'
   AND NOT s.is_deleted
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = s.role_id AND x.menu_id = 'PM_DESIGN_LINEAGE');

COMMIT;

-- 확인
--   SELECT id, title, path, route_key, pid, order_no, icon
--     FROM scom.system_menus WHERE id = 'PM_DESIGN_LINEAGE';
--   SELECT role_id, can_view, can_search, can_update
--     FROM scom.role_menus WHERE menu_id = 'PM_DESIGN_LINEAGE' ORDER BY role_id;
