-- ============================================================
-- WBS 대시보드 화면 열넷을 메뉴에 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 사내망에서 따로 돌던 WBS 대시보드를 프로젝트관리로 들여왔다. 표는
-- `deploy/sql/projmng-wbs-2026-09-23.sql` 이 만들고, 여기서는 **메뉴만** 올린다.
-- 둘을 따로 둔 까닭은 DB 가 다르기 때문이다 — 표는 projmng, 메뉴는 포털이다.
--
-- 두 번 돌려도 안전하다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
--   projmng.wbs.summary → Components/Pages/WbsSummaryBoard.razor
--                         (`@page "/projmng/wbs/summary"`)
--
-- `route_key` 가 화면의 `[RouteKey(...)]` 와 다르면 오류 없이 **「준비 중」이
-- 뜬다.** 화면은 만들어져 있는데 닿을 길이 없는 상태라 찾기가 성가시다.
--
-- ── 새 묶음을 하나 만든다 ────────────────────────────────
--
-- 화면이 열넷이라 기존 묶음에 얹으면 어느 묶음이든 두 배가 된다.
-- 「프로젝트」(10) 바로 다음(15)에 둔다 — 같은 프로젝트를 다른 각도로 보는
-- 화면들이라 설계·DB 보다 그쪽에 붙는 편이 찾기 쉽다.
--
-- ── 옮기지 않은 화면 둘 ──────────────────────────────────
--
--   메뉴 권한   포털의 역할-메뉴 권한과 같은 일을 한다. 표도 안 옮겼다
--               (projmng-wbs SQL 머리말).
--   흐름도 셋   자재업무 흐름도 · 테이블 계통도 · PD 설계 프로세스.
--               사내 업무 문서(HTML)라 꾸러미에 들어 있지 않다.
--
-- ── 권한 ─────────────────────────────────────────────────
--
-- 「프로젝트 WBS」(`PM_PROJ_WBS`)와 **같은 역할**에 준다. 같은 프로젝트를
-- 보는 사람들이고, 한쪽만 보이면 그게 더 헷갈린다.
--
-- 다만 **고치는 권한은 화면마다 다르다.** 현황 화면 일곱은 읽기만 하므로
-- 조회·검색·엑셀만 준다 — 없는 기능에 권한을 주면 「왜 단추가 안 보이나」를
-- 나중에 뒤진다.

BEGIN;

-- ── 묶음 ─────────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print)
SELECT 'PM_WBS', 'PmWbsBoard', '/projmng/wbs', 'PROJMNG', 'CATALOG', 'WBS 대시보드',
       'lucide:gauge', 15, false, 1, now(), 'wbs-board-setup', false,
       true, false, false, false, false, false, false
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_WBS')
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PROJMNG');


-- ── 화면 열넷 ────────────────────────────────────────────
--
-- 차례는 원본 대시보드의 사이드바 순서 그대로다 — 쓰던 사람이 찾던 자리에서
-- 찾을 수 있게.
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
SELECT m.id, m.name, m.path, 'PM_WBS', 'MENU', m.title, m.icon, m.order_no,
       false, 1, now(), 'wbs-board-setup', true,
       true, true, m.use_create, m.use_update, m.use_delete, true, false,
       m.route_key
  FROM (VALUES
    -- 현황 일곱 — **읽기만 한다.**
    ('PM_WBS_SUMMARY',  'PmWbsSummary',   '/projmng/wbs/summary',
     'WBS 요약',            'lucide:layout-dashboard',  1, false, false, false, 'projmng.wbs.summary'),
    ('PM_WBS_PERIOD',   'PmWbsPeriod',    '/projmng/wbs/period',
     '기간별 현황',          'lucide:calendar-range',    2, false, false, false, 'projmng.wbs.period'),
    ('PM_WBS_USERS',    'PmWbsUsers',     '/projmng/wbs/users',
     '개발자별 현황',        'lucide:grid-3x3',          3, false, false, false, 'projmng.wbs.users'),
    ('PM_WBS_MODULES',  'PmWbsModules',   '/projmng/wbs/modules',
     '모듈별 현황',          'lucide:boxes',             4, false, false, false, 'projmng.wbs.modules'),
    ('PM_WBS_PROGRESS', 'PmWbsProgress',  '/projmng/wbs/progress',
     '진척률 현황',          'lucide:trending-up',       5, false, false, false, 'projmng.wbs.progress'),
    ('PM_WBS_DELAY',    'PmWbsDelay',     '/projmng/wbs/delay',
     '지연 현황',            'lucide:alarm-clock',       6, false, false, false, 'projmng.wbs.delay'),

    -- 상세 목록 — 고치기는 되지만 **줄을 새로 만들지 않는다**(엑셀 WBS 가 원본).
    ('PM_WBS_ROWS',     'PmWbsRows',      '/projmng/wbs/rows',
     '상세 목록',            'lucide:table-2',           7, true,  true,  true,  'projmng.wbs.rows'),

    -- GitLab — 읽기만 한다.
    ('PM_WBS_CI',       'PmWbsCi',        '/projmng/wbs/ci',
     '빌드 상태',            'lucide:hammer',            8, false, false, false, 'projmng.wbs.ci'),
    ('PM_WBS_GITLAB',   'PmWbsGitlab',    '/projmng/wbs/gitlab',
     'GitLab 모니터링',      'lucide:git-branch',        9, false, false, false, 'projmng.wbs.gitlab'),

    -- 인터페이스 카탈로그 — 만들고 고치고 지운다.
    ('PM_WBS_IFACE',    'PmWbsInterface', '/projmng/wbs/interfaces',
     '인터페이스 관리',      'lucide:arrow-left-right',  10, true, true, true, 'projmng.wbs.interfaces'),

    -- ProjectView — 캐시를 담고 원장에 반영한다.
    ('PM_WBS_PVSYNC',   'PmWbsPvSync',    '/projmng/wbs/pv-sync',
     'ProjectView 동기화',   'lucide:refresh-cw',        11, true, true, true, 'projmng.wbs.pv-sync'),
    ('PM_WBS_PVFLOW',   'PmWbsPvFlow',    '/projmng/wbs/pv-flow',
     '워크플로 채우기',      'lucide:workflow',          12, false, true, false, 'projmng.wbs.pv-flow'),

    -- 명부와 문서.
    ('PM_WBS_DOCS',     'PmWbsDocs',      '/projmng/wbs/docs',
     '개발자 정보',          'lucide:book-open',         13, true, true, true, 'projmng.wbs.docs'),
    ('PM_WBS_DEVUSER',  'PmWbsDevUser',   '/projmng/wbs/dev-users',
     '개발자 관리',          'lucide:users',             14, true, true, true, 'projmng.wbs.dev-users')
  ) AS m(id, name, path, title, icon, order_no, use_create, use_update, use_delete, route_key)
 WHERE NOT EXISTS (SELECT 1 FROM scom.system_menus x WHERE x.id = m.id)
   AND EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_WBS');


-- ── 권한 ─────────────────────────────────────────────────
--
-- **메뉴만 만들면 아무에게도 안 보인다.** 사이드바는 역할-메뉴 표를 보고
-- 거르므로, 여기 행이 없으면 화면은 있는데 닿을 길이 없다.
--
-- 줄 수준 권한은 그 메뉴가 실제로 여는 기능까지만 준다 — 위 `use_*` 와
-- 같은 값을 쓴다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     -- is_deleted 는 기본값이 없다. 빠뜨리면 NOT NULL 로 끊긴다.
     is_deleted, created_at, created_by)
SELECT r.role_id, m.id,
       true, true, m.use_create, m.use_delete, m.use_update,
       false, true,
       false, false, false, false, false, false, false, false,
       false, now(), 'wbs-board-setup'
  FROM scom.system_menus m
 CROSS JOIN (VALUES
       ('PROJMNG_JSINITEAM'), ('PROJMNG_ADMIN'), ('PROJMNG_MNM_SMG'),
       ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')
     ) AS r(role_id)
 WHERE m.pid = 'PM_WBS'
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = r.role_id AND x.menu_id = m.id
   );

-- 묶음 줄에도 권한이 있어야 사이드바가 그 가지를 편다.
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view, can_search, can_create, can_delete, can_update,
     can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     is_deleted, created_at, created_by)
SELECT r.role_id, 'PM_WBS',
       true, false, false, false, false,
       false, false,
       false, false, false, false, false, false, false, false,
       false, now(), 'wbs-board-setup'
  FROM (VALUES
       ('PROJMNG_JSINITEAM'), ('PROJMNG_ADMIN'), ('PROJMNG_MNM_SMG'),
       ('ADMINISTRATOR'), ('SYSTEM_ADMINISTRATOR')
     ) AS r(role_id)
 WHERE EXISTS (SELECT 1 FROM scom.system_menus WHERE id = 'PM_WBS')
   AND NOT EXISTS (
       SELECT 1 FROM scom.role_menus x
        WHERE x.role_id = r.role_id AND x.menu_id = 'PM_WBS'
   );

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────
-- SELECT id, title, path, route_key, order_no
--   FROM scom.system_menus WHERE pid = 'PM_WBS' ORDER BY order_no;
--
-- SELECT menu_id, count(*) FROM scom.role_menus
--  WHERE menu_id LIKE 'PM_WBS%' GROUP BY menu_id ORDER BY 1;
