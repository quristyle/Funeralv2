-- ============================================================
-- 프로젝트관리(구 ProjMngWasm) 화면을 JSini 포털 메뉴로 등록한다.
--
-- 대상 DB : funeralv2 (AuthServer 가 소유하는 scom 스키마)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d funeralv2 -f docs/sql/projmng_menu_seed.sql
--
-- 여러 번 실행해도 안전하다(같은 id 면 갱신).
--
-- ── 이름 규칙 ────────────────────────────────────────────────
-- 포털에는 이미 헬프데스크와 장례식장 메뉴가 있고 겹치는 기능이 여럿이다.
-- (프로젝트 관리 · WBS · 일정 · 공통코드 · 메뉴 관리 · 서버 상태 …)
-- 그래서 프로젝트관리 쪽 메뉴는 **어느 시스템 것인지 이름에서 드러나게** 붙였다.
--   헬프데스크 "프로젝트 관리"  vs  프로젝트관리 "프로젝트 목록"
--   포털       "공통코드"        vs  프로젝트관리 "프로젝트 공통코드"
--   포털       "메뉴 관리"       vs  프로젝트관리 "프로젝트 화면 메뉴"
--   포털       "서버 상태"       vs  프로젝트관리 "JSini 서버 모니터"
--
-- ── status 규칙 ──────────────────────────────────────────────
--   1 = 활성. 메뉴에 보이고 라우트로 등록된다.
--   0 = 비활성. AuthServer 의 /menu/all 이 걸러낸다.
-- 이 스크립트의 화면은 .vue 파일이 모두 존재하므로 1 로 넣는다.
-- 단, 자료 조회는 ProjMngServer 의 DB 접속(ConnectionStrings:jsini)이 설정된 뒤에 된다.
--
-- ── 숨긴 메뉴 ────────────────────────────────────────────────
-- 원본에서 화면 경로(@page)가 없던 둘은 hide_in_menu = true 로 넣는다.
--   · 일정 편집  — 스케줄러가 띄우는 대화창이다(원본도 URL 이 없었다)
--   · 부품 모음  — 원본 파일이 비어 있던 자리다
-- 권한 화면에서는 보이므로 필요하면 나중에 노출로 바꿀 수 있다.
-- ============================================================

BEGIN;

CREATE TEMP TABLE tmp_projmng_menu (
  id            text,
  pid           text,
  name          text,
  path          text,
  component     text,
  type          text,
  title         text,
  icon          text,
  order_no      int,
  hide_in_menu  boolean,
  keep_alive    boolean,
  status        int,
  -- 화면에서 쓸 수 있는 동작. 역할 권한 화면이 이 값으로 체크박스를 보여 준다.
  use_create    boolean,
  use_update    boolean,
  use_delete    boolean,
  use_excel     boolean
) ON COMMIT DROP;

INSERT INTO tmp_projmng_menu
  (id, pid, name, path, component, type, title, icon, order_no,
   hide_in_menu, keep_alive, status, use_create, use_update, use_delete, use_excel)
VALUES
-- ── 최상위 ────────────────────────────────────────────────────
('PROJMNG', NULL, 'ProjMng', '/projmng', NULL, 'CATALOG', '프로젝트관리', 'lucide:folder-git-2', 30, false, false, 1, false, false, false, false),

-- ── 프로젝트 ──────────────────────────────────────────────────
('PM_PROJ',          'PROJMNG', 'PmProject',        '/projmng/proj',              NULL,                                      'CATALOG', '프로젝트',           'lucide:folder',            10, false, false, 1, false, false, false, false),
('PM_PROJ_LIST',     'PM_PROJ', 'PmProjectList',    '/projmng/proj/manage',       '#/views/projmng/proj/manage.vue',         'MENU',    '프로젝트 목록',      'lucide:list',               1, false, true,  1, true,  true,  false, true),
('PM_PROJ_WBS',      'PM_PROJ', 'PmProjectWbs',     '/projmng/proj/wbs',          '#/views/projmng/proj/wbs.vue',            'MENU',    '프로젝트 WBS',       'lucide:list-tree',          2, false, true,  1, true,  true,  true,  true),
('PM_PROJ_SCHED',    'PM_PROJ', 'PmProjectSchedule','/projmng/proj/scheduler',    '#/views/projmng/proj/scheduler.vue',      'MENU',    '프로젝트 일정표',    'lucide:calendar-days',      3, false, true,  1, true,  true,  true,  false),
('PM_PROJ_APPT',     'PM_PROJ', 'PmProjectAppointment','/projmng/proj/appointment','#/views/projmng/proj/modules/appointment-form.vue', 'MENU', '일정 편집',    NULL,                        4, true,  false, 1, true,  true,  true,  false),
('PM_PROJ_USER',     'PM_PROJ', 'PmProjectUser',    '/projmng/proj/user',         '#/views/projmng/proj/user.vue',           'MENU',    '프로젝트 참여자',    'lucide:users',              5, false, true,  1, true,  true,  true,  true),
('PM_PROJ_SRCINFO',  'PM_PROJ', 'PmProjectSource',  '/projmng/proj/source',       '#/views/projmng/proj/source.vue',         'MENU',    '프로젝트 소스 정보', 'lucide:file-code',          6, false, true,  1, true,  true,  true,  true),
('PM_PROJ_MONITOR',  'PM_PROJ', 'PmProjectMonitor', '/projmng/proj/monitoring',   '#/views/projmng/proj/monitoring.vue',     'MENU',    '프로젝트 진행 현황', 'lucide:gauge',              7, false, true,  1, false, false, false, true),
('PM_PROJ_MYINFO',   'PM_PROJ', 'PmProjectMyInfo',  '/projmng/proj/user-setting', '#/views/projmng/proj/user-setting.vue',   'MENU',    '내 프로젝트 정보',   'lucide:user-cog',           8, false, true,  1, false, true,  false, false),

-- ── 설계 ──────────────────────────────────────────────────────
('PM_DESIGN',        'PROJMNG',   'PmDesign',      '/projmng/design',          NULL,                                    'CATALOG', '설계',                'lucide:shapes',        20, false, false, 1, false, false, false, false),
('PM_DESIGN_ERD',    'PM_DESIGN', 'PmDesignErd',   '/projmng/design/erd',      '#/views/projmng/proj/erd.vue',          'MENU',    '프로젝트 ERD',        'lucide:table-2',        1, false, false, 1, false, true,  false, false),
('PM_DESIGN_FLOW',   'PM_DESIGN', 'PmDesignFlow',  '/projmng/design/flow',     '#/views/projmng/proj/flow.vue',         'MENU',    '프로젝트 업무 흐름',  'lucide:workflow',       2, false, false, 1, false, true,  false, false),
('PM_DESIGN_USECASE','PM_DESIGN', 'PmDesignUseCase','/projmng/design/use-case','#/views/projmng/proj/use-case.vue',     'MENU',    '프로젝트 유즈케이스', 'lucide:git-branch',     3, false, false, 1, false, true,  false, false),

-- ── DB ────────────────────────────────────────────────────────
('PM_DB',            'PROJMNG', 'PmDb',        '/projmng/db',        NULL,                                       'CATALOG', '데이터베이스',           'lucide:database',        30, false, false, 1, false, false, false, false),
('PM_DB_LIST',       'PM_DB',   'PmDbList',    '/projmng/db/list',   '#/views/projmng/proj/db.vue',              'MENU',    '프로젝트 DB 등록',       'lucide:server',           1, false, true,  1, true,  true,  true,  true),
('PM_DB_CODE',       'PM_DB',   'PmDbCode',    '/projmng/db/code',   '#/views/projmng/proj/code.vue',            'MENU',    '프로젝트 코드 정보',     'lucide:list-ordered',     2, false, true,  1, false, false, false, true),
('PM_DB_TOOLS',      'PM_DB',   'PmDbTools',   '/projmng/db/tools',  '#/views/projmng/develop/db-tools.vue',     'MENU',    'DB 개체 탐색',           'lucide:wrench',           3, false, true,  1, false, false, false, true),
('PM_DB_TESTER',     'PM_DB',   'PmDbTester',  '/projmng/db/tester', '#/views/projmng/develop/db-tester.vue',    'MENU',    'DB 쿼리 테스터',         'lucide:terminal',         4, false, false, 1, false, false, false, true),
('PM_DB_TABLE',      'PM_DB',   'PmDbTable',   '/projmng/db/table',  '#/views/projmng/develop/table-manage.vue', 'MENU',    '테이블 · 컬럼 설명',     'lucide:table-properties', 5, false, true,  1, false, true,  false, true),

-- ── 소스 ──────────────────────────────────────────────────────
('PM_SRC',           'PROJMNG', 'PmSource',       '/projmng/source',        NULL,                                       'CATALOG', '소스 분석',        'lucide:file-search', 40, false, false, 1, false, false, false, false),
('PM_SRC_TRACE',     'PM_SRC',  'PmSourceTrace',  '/projmng/source/trace',  '#/views/projmng/develop/source-trace.vue', 'MENU',    '소스 추적',        'lucide:search-code',  1, false, true,  1, false, false, false, true),
('PM_SRC_GLUE',      'PM_SRC',  'PmSourceGlue',   '/projmng/source/glue',   '#/views/projmng/develop/glue-trace.vue',   'MENU',    'Glue 서비스 추적', 'lucide:link',         2, false, true,  1, false, true,  false, true),
('PM_SRC_SCAN',      'PM_SRC',  'PmSourceScaner', '/projmng/source/scaner', '#/views/projmng/proj/scaner.vue',          'MENU',    '소스 화면 스캐너', 'lucide:scan-line',    3, false, true,  1, false, false, false, true),

-- ── 기준정보 ──────────────────────────────────────────────────
('PM_COMM',          'PROJMNG', 'PmComm',        '/projmng/comm',             NULL,                                      'CATALOG', '기준정보',              'lucide:settings-2', 50, false, false, 1, false, false, false, false),
('PM_COMM_CODE',     'PM_COMM', 'PmCommCode',    '/projmng/comm/common-code', '#/views/projmng/comm/common-code.vue',    'MENU',    '프로젝트 공통코드',     'lucide:tags',        1, false, true,  1, true,  true,  true,  true),
('PM_COMM_MENU',     'PM_COMM', 'PmCommMenu',    '/projmng/comm/menu',        '#/views/projmng/comm/menu.vue',           'MENU',    '프로젝트 화면 메뉴',    'lucide:menu',        2, false, true,  1, true,  true,  true,  false),
('PM_COMM_USERGRP',  'PM_COMM', 'PmCommUserGrp', '/projmng/comm/user-group',  '#/views/projmng/comm/user-group.vue',     'MENU',    '프로젝트 사용자 그룹',  'lucide:users-round', 3, false, true,  1, true,  true,  true,  true),

-- ── 시스템 ────────────────────────────────────────────────────
('PM_SYS',           'PROJMNG', 'PmSys',           '/projmng/sys',                NULL,                                        'CATALOG', 'DB 로직',      'lucide:code-2',    60, false, false, 1, false, false, false, false),
('PM_SYS_LOGIC',     'PM_SYS',  'PmSysDbLogic',    '/projmng/sys/db-logic',       '#/views/projmng/sys/db-logic.vue',          'MENU',    'DB 로직 등록', 'lucide:file-code-2', 1, false, true,  1, false, true,  false, true),
('PM_SYS_LOGICITEM', 'PM_SYS',  'PmSysDbLogicItem','/projmng/sys/db-logic-item',  '#/views/projmng/sys/db-logic-item.vue',     'MENU',    'DB 로직 항목', 'lucide:list-checks', 2, false, true,  1, true,  true,  true,  true),

-- ── 할일 ──────────────────────────────────────────────────────
('PM_TODO',          'PROJMNG', 'PmTodo',        '/projmng/todo',         NULL,                                     'CATALOG', '할일',           'lucide:check-square', 70, false, false, 1, false, false, false, false),
('PM_TODO_LIST',     'PM_TODO', 'PmTodoList',    '/projmng/todo/list',    '#/views/projmng/home/todo.vue',          'MENU',    '할일 관리',      'lucide:list-todo',     1, false, true,  1, true,  true,  true,  true),
('PM_TODO_MONITOR',  'PM_TODO', 'PmTodoMonitor', '/projmng/todo/monitor', '#/views/projmng/home/todo-monitor.vue',  'MENU',    '할일 정산 현황', 'lucide:coins',         2, false, true,  1, false, true,  true,  true),

-- ── 도구 ──────────────────────────────────────────────────────
('PM_TOOL',          'PROJMNG', 'PmTool',        '/projmng/tool',           NULL,                                    'CATALOG', '개발 도구',           'lucide:hammer',       80, false, false, 1, false, false, false, false),
('PM_TOOL_SHEET',    'PM_TOOL', 'PmToolSheet',   '/projmng/tool/sheet',     '#/views/projmng/proj/sheet.vue',        'MENU',    '엑셀 시트',           'lucide:sheet',         1, false, true,  1, true,  true,  false, true),
('PM_TOOL_FAST',     'PM_TOOL', 'PmToolFastTest','/projmng/tool/fast-test', '#/views/projmng/proj/fast-test.vue',    'MENU',    'Fast 호출 테스트',    'lucide:zap',           2, false, false, 1, false, false, false, true),
('PM_TOOL_COMTEST',  'PM_TOOL', 'PmToolComTest', '/projmng/tool/com-test',  '#/views/projmng/proj/com-test.vue',     'MENU',    '그리드 부품 테스트',  'lucide:test-tube',     3, false, false, 1, false, true,  false, true),
('PM_TOOL_COMPONENT','PM_TOOL', 'PmToolComponent','/projmng/tool/component','#/views/projmng/proj/component.vue',    'MENU',    '부품 모음',           NULL,                   4, true,  false, 1, false, false, false, false),

-- ── 외부 시스템 ───────────────────────────────────────────────
('PM_EXT',           'PROJMNG', 'PmExternal',        '/projmng/external',                  NULL,                                             'CATALOG', '외부 시스템',             'lucide:globe',       90, false, false, 1, false, false, false, false),
('PM_EXT_JSINI',     'PM_EXT',  'PmExternalJsini',   '/projmng/external/jsini',            '#/views/projmng/external/jsini.vue',             'MENU',    'JSini 서버 모니터',       'lucide:activity',     1, false, false, 1, false, false, false, false),
('PM_EXT_FRMON',     'PM_EXT',  'PmExternalFrMonitor','/projmng/external/funeral-monitor', '#/views/projmng/external/funeral-monitor.vue',    'MENU',    '장례 프레임 서버 모니터', 'lucide:monitor-dot',  2, false, false, 1, false, false, false, false);

-- 등록 · 갱신
INSERT INTO scom.system_menus (
  id, name, path, component, pid, type, title, icon, order_no,
  hide_in_menu, keep_alive, affix_tab, dom_cached, menu_visible_with_forbidden,
  status, created_at, created_by, updated_at, updated_by, is_deleted,
  use_view, use_search, use_create, use_update, use_delete, use_print, use_excel
)
SELECT
  t.id, t.name, t.path, t.component, t.pid, t.type, t.title, t.icon, t.order_no,
  t.hide_in_menu, t.keep_alive, false, false, false,
  t.status, now(), 'projmng-migration', now(), 'projmng-migration', false,
  true, (t.type = 'MENU'), t.use_create, t.use_update, t.use_delete, false, t.use_excel
FROM tmp_projmng_menu t
ON CONFLICT (id) DO UPDATE SET
  name         = EXCLUDED.name,
  path         = EXCLUDED.path,
  component    = EXCLUDED.component,
  pid          = EXCLUDED.pid,
  type         = EXCLUDED.type,
  title        = EXCLUDED.title,
  icon         = EXCLUDED.icon,
  order_no     = EXCLUDED.order_no,
  hide_in_menu = EXCLUDED.hide_in_menu,
  keep_alive   = EXCLUDED.keep_alive,
  use_view     = EXCLUDED.use_view,
  use_search   = EXCLUDED.use_search,
  use_create   = EXCLUDED.use_create,
  use_update   = EXCLUDED.use_update,
  use_delete   = EXCLUDED.use_delete,
  use_print    = EXCLUDED.use_print,
  use_excel    = EXCLUDED.use_excel,
  -- status 는 덮어쓰지 않는다. 운영에서 특정 화면을 닫아 둔 것을 재실행이 되돌리면 안 된다.
  updated_at   = now(),
  updated_by   = 'projmng-migration',
  is_deleted   = false;

-- ── 역할 권한 ────────────────────────────────────────────────
-- 프로젝트관리는 개발자용 도구다. 아무 역할에나 열어 줄 것이 아니라
-- 관리자 역할(ADMINISTRATOR / SYSTEM_ADMINISTRATOR)에만 권한을 준다.
-- 파트너 역할에는 주지 않는다 — 필요해지면 역할 권한 화면에서 켜면 된다.
--
-- 그중 DB 쿼리 테스터는 임의 SQL 을 실행하는 화면이라
-- 시스템관리자에게만 준다(서버도 역할을 한 번 더 확인한다: DevTools:RawSqlRoles).
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT
  r.id, t.id,
  true, (t.type = 'MENU'), t.use_create, t.use_delete, t.use_update, false, t.use_excel,
  false, false, false, false, false, false, false, false,
  now(), 'projmng-migration', false
FROM scom.roles r
CROSS JOIN tmp_projmng_menu t
WHERE r.is_deleted = false
  AND r.id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND NOT (t.id = 'PM_DB_TESTER' AND r.id <> 'SYSTEM_ADMINISTRATOR')
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = t.id
  );

-- ── 쓰지 않기로 한 화면 정리 ──────────────────────────────────
-- [장례 프레임 빈소 현황](PM_EXT_FRDATA) 은 쓰지 않는 기능이라 제거했다.
-- 화면(.vue)과 게이트웨이 라우트(/api/funeralfr)도 함께 지웠다.
-- 이전 실행으로 심어진 행이 있으면 여기서 지운다.
DELETE FROM scom.role_menus   WHERE menu_id = 'PM_EXT_FRDATA';
DELETE FROM scom.system_menus WHERE id      = 'PM_EXT_FRDATA';

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT
  count(*) FILTER (WHERE type = 'CATALOG')                  AS 폴더,
  count(*) FILTER (WHERE type = 'MENU' AND hide_in_menu)    AS 숨긴화면,
  count(*) FILTER (WHERE type = 'MENU' AND NOT hide_in_menu) AS 보이는화면,
  count(*)                                                  AS 전체
FROM scom.system_menus
WHERE id = 'PROJMNG' OR id LIKE 'PM\_%';

-- 컴포넌트 경로가 실제 파일과 맞는지는 프론트에서 확인한다.
--   fronts/apps/jsini-portal/src/router/access.ts 의
--   import.meta.glob('../views/**/*.vue') 가 이 경로를 해석한다.
SELECT id, title, component
FROM scom.system_menus
WHERE (id = 'PROJMNG' OR id LIKE 'PM\_%') AND component IS NOT NULL
ORDER BY order_no, id;
