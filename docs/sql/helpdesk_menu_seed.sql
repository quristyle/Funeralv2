-- ============================================================
-- 헬프데스크(구 JinReception) 화면을 funeralv2 메뉴로 등록한다.
--
-- 대상 DB : funeralv2 (AuthServer 가 소유하는 scom 스키마)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d funeralv2 -f docs/sql/helpdesk_menu_seed.sql
--
-- 여러 번 실행해도 안전하다(같은 id 면 갱신).
--
-- status 규칙
--   1 = 활성. 메뉴에 보이고 라우트로 등록된다.
--   0 = 비활성. AuthServer 의 /menu/all 이 걸러내므로 메뉴에 나타나지 않는다.
--       화면(.vue)이 아직 이식되지 않은 항목은 0 으로 두고, 이식이 끝나면 1 로 올린다.
--       (컴포넌트가 없는 메뉴를 활성화하면 프론트에서 "존재하지 않는 컴포넌트 경로" 경고와 함께
--        빈 화면이 뜨기 때문에 이렇게 단계적으로 연다)
-- ============================================================

BEGIN;

CREATE TEMP TABLE tmp_helpdesk_menu (
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
  status        int
) ON COMMIT DROP;

INSERT INTO tmp_helpdesk_menu
  (id, pid, name, path, component, type, title, icon, order_no, hide_in_menu, keep_alive, status)
VALUES
-- ── 최상위 ────────────────────────────────────────────────
('HELPDESK', NULL, 'HelpDesk', '/helpdesk', NULL, 'CATALOG', '헬프데스크', 'lucide:life-buoy', 20, false, false, 1),

-- ── 대시보드 ──────────────────────────────────────────────
('HD_DASHBOARD',      'HELPDESK', 'HelpDeskDashboard',      '/helpdesk/dashboard',          '#/views/helpdesk/dashboard/index.vue',           'MENU', '헬프데스크 현황', 'lucide:layout-dashboard', 1, false, true,  0),
('HD_DASHBOARD_CUST', 'HELPDESK', 'HelpDeskCustomerBoard',  '/helpdesk/dashboard/customer', '#/views/helpdesk/dashboard/customer.vue',        'MENU', '고객사 현황',     'lucide:building',         2, false, true,  0),

-- ── 요청 관리 ─────────────────────────────────────────────
('HD_REQ',         'HELPDESK', 'HelpDeskRequest',       '/helpdesk/request',            NULL,                                          'CATALOG', '요청 관리',   'lucide:inbox',        10, false, false, 1),
('HD_REQ_LIST',    'HD_REQ',   'HelpDeskRequestList',   '/helpdesk/request/list',       '#/views/helpdesk/request/list.vue',           'MENU',    '내 요청',     'lucide:list',          1, false, true,  0),
('HD_REQ_MNG',     'HD_REQ',   'HelpDeskRequestManage', '/helpdesk/request/manage',     '#/views/helpdesk/request/manage.vue',         'MENU',    '요청 처리',   'lucide:clipboard-check', 2, false, true, 0),
('HD_REQ_NEW',     'HD_REQ',   'HelpDeskRequestNew',    '/helpdesk/request/new',        '#/views/helpdesk/request/edit.vue',           'MENU',    '요청 등록',   'lucide:plus',          3, false, false, 0),
('HD_REQ_DETAIL',  'HD_REQ',   'HelpDeskRequestDetail', '/helpdesk/request/detail/:id', '#/views/helpdesk/request/detail.vue',         'MENU',    '요청 상세',   NULL,                   4, true,  false, 0),
('HD_REQ_EDIT',    'HD_REQ',   'HelpDeskRequestEdit',   '/helpdesk/request/edit/:id',   '#/views/helpdesk/request/edit.vue',           'MENU',    '요청 수정',   NULL,                   5, true,  false, 0),
('HD_REQ_MONITOR', 'HD_REQ',   'HelpDeskRequestMonitor','/helpdesk/request/monitor',    '#/views/helpdesk/request/monitor.vue',        'MENU',    '요청 모니터', 'lucide:activity',      6, false, true,  0),
('HD_MY_COMMENTS', 'HD_REQ',   'HelpDeskMyComments',    '/helpdesk/request/my-comments','#/views/helpdesk/request/my-comments.vue',    'MENU',    '내 댓글',     'lucide:message-square',7, false, true,  0),

-- ── 조직 관리 ─────────────────────────────────────────────
('HD_ORG',          'HELPDESK', 'HelpDeskOrg',         '/helpdesk/org',              NULL,                                       'CATALOG', '조직 관리',   'lucide:users',      20, false, false, 1),
('HD_ORG_COMPANY',  'HD_ORG',   'HelpDeskCompany',     '/helpdesk/org/company',      '#/views/helpdesk/org/company.vue',         'MENU',    '고객사',      'lucide:building-2',  1, false, true, 0),
('HD_ORG_CUSTOMER', 'HD_ORG',   'HelpDeskCustomer',    '/helpdesk/org/customer',     '#/views/helpdesk/org/customer.vue',        'MENU',    '고객 사용자', 'lucide:user',        2, false, true, 0),
('HD_ORG_TEAM',     'HD_ORG',   'HelpDeskTeam',        '/helpdesk/org/team',         '#/views/helpdesk/org/team.vue',            'MENU',    '팀',          'lucide:users-round', 3, false, true, 0),
('HD_ORG_TEAM_COM', 'HD_ORG',   'HelpDeskTeamCompany', '/helpdesk/org/team-company', '#/views/helpdesk/org/team-company.vue',    'MENU',    '팀-고객사',   'lucide:link',        4, false, true, 0),
('HD_ORG_ADMIN',    'HD_ORG',   'HelpDeskAdmin',       '/helpdesk/org/admin',        '#/views/helpdesk/org/admin.vue',           'MENU',    '담당자',      'lucide:user-cog',    5, false, true, 0),
('HD_PROFILE',      'HD_ORG',   'HelpDeskProfile',     '/helpdesk/org/profile',      '#/views/helpdesk/org/profile.vue',         'MENU',    '내 프로필',   NULL,                 6, true,  false, 0),

-- ── 프로젝트 / WBS ────────────────────────────────────────
('HD_PRJ',          'HELPDESK', 'HelpDeskProject',      '/helpdesk/project',              NULL,                                        'CATALOG', '프로젝트',      'lucide:folder-kanban', 30, false, false, 1),
('HD_PRJ_MNG',      'HD_PRJ',   'HelpDeskProjectManage','/helpdesk/project/manage',       '#/views/helpdesk/project/manage.vue',       'MENU',    '프로젝트 관리', 'lucide:folder',         1, false, true, 0),
('HD_PRJ_WBS',      'HD_PRJ',   'HelpDeskWbs',          '/helpdesk/project/wbs',          '#/views/helpdesk/project/wbs.vue',          'MENU',    'WBS',           'lucide:list-tree',      2, false, true, 0),
('HD_PRJ_GANTT',    'HD_PRJ',   'HelpDeskWbsGantt',     '/helpdesk/project/wbs-gantt',    '#/views/helpdesk/project/wbs-gantt.vue',    'MENU',    'WBS 간트',      'lucide:chart-gantt',    3, false, false, 0),
('HD_PRJ_GANTTVIEW','HD_PRJ',   'HelpDeskGanttView',    '/helpdesk/project/gantt',        '#/views/helpdesk/project/gantt-view.vue',   'MENU',    '간트 뷰',       'lucide:calendar-range', 4, false, false, 0),
('HD_PRJ_READONLY', 'HD_PRJ',   'HelpDeskWbsReadonly',  '/helpdesk/project/wbs-readonly', '#/views/helpdesk/project/wbs-readonly.vue', 'MENU',    'WBS 열람',      NULL,                    5, true,  false, 0),
('HD_PRJ_INFO',     'HD_PRJ',   'HelpDeskProjectInfo',  '/helpdesk/project/info',         '#/views/helpdesk/project/info.vue',         'MENU',    '프로젝트 정보', NULL,                    6, true,  false, 0),
('HD_PRJ_DIAGRAM',  'HD_UTIL',  'HelpDeskDiagram',      '/helpdesk/util/diagram',         '#/views/helpdesk/util/diagram.vue',      'MENU',    '다이어그램',    'lucide:workflow',       7, false, false, 0),

-- ── 공지 ──────────────────────────────────────────────────
('HD_NOTICE',      'HELPDESK',  'HelpDeskNotice',     '/helpdesk/notice',           NULL,                                    'CATALOG', '공지사항',   'lucide:megaphone', 40, false, false, 1),
('HD_NOTICE_LIST', 'HD_NOTICE', 'HelpDeskNoticeList', '/helpdesk/notice/list',      '#/views/helpdesk/notice/list.vue',      'MENU',    '공지 목록',  'lucide:list',       1, false, true,  0),
('HD_NOTICE_FORM', 'HD_NOTICE', 'HelpDeskNoticeForm', '/helpdesk/notice/form/:id',  '#/views/helpdesk/notice/form.vue',      'MENU',    '공지 작성',  NULL,                2, true,  false, 0),
('HD_NOTICE_VIEW', 'HD_NOTICE', 'HelpDeskNoticeView', '/helpdesk/notice/view/:id',  '#/views/helpdesk/notice/view.vue',      'MENU',    '공지 보기',  NULL,                3, true,  false, 0),

-- ── 일정 ──────────────────────────────────────────────────
('HD_SCHEDULE',     'HELPDESK',    'HelpDeskSchedule',   '/helpdesk/schedule',     NULL,                                     'CATALOG', '일정',      'lucide:calendar-days', 50, false, false, 1),
('HD_SCHEDULE_ALL', 'HD_SCHEDULE', 'HelpDeskScheduleAll','/helpdesk/schedule/all', '#/views/helpdesk/schedule/index.vue',    'MENU',    '전체 일정', 'lucide:calendar',       1, false, true, 0),
('HD_SCHEDULE_MY',  'HD_SCHEDULE', 'HelpDeskScheduleMy', '/helpdesk/schedule/my',  '#/views/helpdesk/schedule/my.vue',       'MENU',    '내 일정',   'lucide:calendar-check', 2, false, true, 0),

-- ── 알림 / 푸시 ───────────────────────────────────────────
('HD_PUSH',           'HELPDESK', 'HelpDeskPush',          '/helpdesk/push',          NULL,                                        'CATALOG', '알림',      'lucide:bell',        60, false, false, 1),
('HD_PUSH_DASHBOARD', 'HD_PUSH',  'HelpDeskPushDashboard', '/helpdesk/push/dashboard','#/views/helpdesk/push/dashboard.vue',       'MENU',    '푸시 현황', 'lucide:bar-chart-3',  1, false, true, 0),
('HD_PUSH_LOGS',      'HD_PUSH',  'HelpDeskPushLogs',      '/helpdesk/push/logs',     '#/views/helpdesk/push/logs.vue',            'MENU',    '발송 이력', 'lucide:scroll-text',  2, false, true, 0),
('HD_PUSH_HISTORY',   'HD_PUSH',  'HelpDeskPushHistory',   '/helpdesk/push/history',  '#/views/helpdesk/push/history.vue',         'MENU',    '내 알림함', 'lucide:inbox',        3, false, true, 0),
('HD_PUSH_SETTING',   'HD_PUSH',  'HelpDeskPushSetting',   '/helpdesk/push/setting',  '#/views/helpdesk/push/setting.vue',         'MENU',    '알림 설정', 'lucide:settings-2',   4, false, true, 0),

-- ── 운영 모니터링 ─────────────────────────────────────────
('HD_MON',             'HELPDESK', 'HelpDeskMonitor',     '/helpdesk/monitor',             NULL,                                          'CATALOG', 'SM 운영',      'lucide:gauge',       70, false, false, 1),
('HD_MON_SM',          'HD_MON',   'HelpDeskSmMonitoring','/helpdesk/monitor/sm',          '#/views/helpdesk/monitor/sm.vue',             'MENU',    'SM 모니터링',  'lucide:monitor-dot',  1, false, true, 0),
('HD_MON_MAINTENANCE', 'HD_MON',   'HelpDeskMaintenance', '/helpdesk/monitor/maintenance', '#/views/helpdesk/monitor/maintenance.vue',    'MENU',    '유지보수 보고','lucide:file-text',    2, false, true, 0),

-- ── 유틸리티 ──────────────────────────────────────────────
('HD_UTIL',        'HELPDESK', 'HelpDeskUtil',       '/helpdesk/util',               NULL,                                     'CATALOG', '유틸리티',     'lucide:wrench',     80, false, false, 1),
('HD_UTIL_ASCII',  'HD_UTIL',  'HelpDeskAsciiParser','/helpdesk/util/ascii-parser',  '#/views/helpdesk/util/ascii-parser.vue', 'MENU',    'ASCII 파서',   'lucide:file-code',   1, false, true, 0),
('HD_UTIL_BINARY', 'HD_UTIL',  'HelpDeskBinaryParser','/helpdesk/util/binary-parser','#/views/helpdesk/util/binary-parser.vue','MENU',    '바이너리 파서','lucide:binary',      2, false, true, 0),
('HD_UTIL_MCMODEL','HD_UTIL',  'HelpDeskMcModel',    '/helpdesk/util/mc-model',      '#/views/helpdesk/util/mc-model.vue',     'MENU',    'MC 모델 관리', 'lucide:database',    3, false, true, 0),

-- ── 한주 설비 (외부 OADR 시스템) ──────────────────────────
('HD_HANJU',        'HELPDESK',  'HelpDeskHanju',          '/helpdesk/hanju',                  NULL,                                          'CATALOG', '한주 설비',     'lucide:factory',      90, false, false, 1),
('HD_HANJU_HEALTH', 'HD_HANJU',  'HelpDeskHanjuHealth',    '/helpdesk/hanju/health-check',     '#/views/helpdesk/hanju/health-check.vue',     'MENU',    '헬스체크',      'lucide:heart-pulse',   1, false, true, 0),
('HD_HANJU_COLLECT','HD_HANJU',  'HelpDeskHanjuCollection','/helpdesk/hanju/collection-status','#/views/helpdesk/hanju/collection-status.vue','MENU',    '수집 현황',     'lucide:download-cloud',2, false, true, 0),
('HD_HANJU_EQUIP',  'HD_HANJU',  'HelpDeskHanjuEquipment', '/helpdesk/hanju/equipment-log',    '#/views/helpdesk/hanju/equipment-log.vue',    'MENU',    '설비 상태 로그','lucide:cpu',           3, false, true, 0),
('HD_HANJU_FMS',    'HD_HANJU',  'HelpDeskHanjuFms',       '/helpdesk/hanju/fms-log',          '#/views/helpdesk/hanju/fms-log.vue',          'MENU',    'FMS 상태 로그', 'lucide:server-cog',    4, false, true, 0),
('HD_HANJU_PROC',   'HD_HANJU',  'HelpDeskHanjuProcedure', '/helpdesk/hanju/procedure-result', '#/views/helpdesk/hanju/procedure-result.vue', 'MENU',    '프로시저 결과', 'lucide:terminal',      5, false, true, 0),

-- ── 리포트 (외부 OADR 시스템) ─────────────────────────────
('HD_RPT',          'HELPDESK', 'HelpDeskReport',           '/helpdesk/report',                  NULL,                                              'CATALOG', '운영 리포트',  'lucide:file-bar-chart', 100, false, false, 1),
('HD_RPT_MON',      'HD_RPT',   'HelpDeskReportMonitoring', '/helpdesk/report/monitoring',       '#/views/helpdesk/report/monitoring.vue',          'MENU',    '운영 모니터링','lucide:activity',         1, false, true, 0),
('HD_RPT_WEEKLY',   'HD_RPT',   'HelpDeskReportWeekly',     '/helpdesk/report/weekly',           '#/views/helpdesk/report/weekly.vue',              'MENU',    '주간 리포트',  'lucide:calendar-days',    2, false, true, 0),
('HD_RPT_MONTHLY',  'HD_RPT',   'HelpDeskReportMonthly',    '/helpdesk/report/monthly',          '#/views/helpdesk/report/monthly.vue',             'MENU',    '월간 리포트',  'lucide:calendar',         3, false, true, 0),
('HD_RPT_PRED',     'HD_RPT',   'HelpDeskReportPrediction', '/helpdesk/report/prediction',       '#/views/helpdesk/report/prediction.vue',          'MENU',    '장애 예측',    'lucide:trending-up',      4, false, true, 0),
('HD_RPT_IO',       'HD_RPT',   'HelpDeskReportIo',         '/helpdesk/report/io-deep-dive',     '#/views/helpdesk/report/io-deep-dive.vue',        'MENU',    'IO 정밀 분석', 'lucide:hard-drive',       5, false, true, 0),
('HD_RPT_AVAIL',    'HD_RPT',   'HelpDeskReportAvailability','/helpdesk/report/availability',    '#/views/helpdesk/report/availability.vue',        'MENU',    '가용성 분석',  'lucide:shield-check',     6, false, true, 0),
('HD_RPT_CAPACITY', 'HD_RPT',   'HelpDeskReportCapacity',   '/helpdesk/report/capacity-planning','#/views/helpdesk/report/capacity-planning.vue',   'MENU',    '용량 계획',    'lucide:database-zap',     7, false, true, 0),
('HD_RPT_RCA',      'HD_RPT',   'HelpDeskReportRootCause',  '/helpdesk/report/root-cause',       '#/views/helpdesk/report/root-cause.vue',          'MENU',    '원인 분석',    'lucide:search-check',     8, false, true, 0),

-- ── 헬프데스크 설정 ───────────────────────────────────────
('HD_SYS',           'HELPDESK', 'HelpDeskSystem',        '/helpdesk/system',                 NULL,                                          'CATALOG', '헬프데스크 설정', 'lucide:settings',    110, false, false, 1),
('HD_SYS_CHECKLIST', 'HD_SYS',   'HelpDeskSysChecklist',  '/helpdesk/system/checklist',       '#/views/helpdesk/system/checklist.vue',       'MENU',    '체크리스트',      'lucide:list-checks',   3, false, true, 0),
('HD_SYS_RELEASE',   'HD_SYS',   'HelpDeskSysRelease',    '/helpdesk/system/release',         '#/views/helpdesk/system/release.vue',         'MENU',    '릴리즈 도구',     'lucide:rocket',        4, false, true, 0),
('HD_SYS_ACCOUNT',   'HD_SYS',   'HelpDeskSysAccountLink','/helpdesk/system/account-link',    '#/views/helpdesk/system/account-link.vue',    'MENU',    '계정 연결',       'lucide:link-2',        5, false, true, 0),
('HD_SYS_USERPROPS', 'HD_SYS',   'HelpDeskSysUserProps',  '/helpdesk/system/user-properties', '#/views/helpdesk/system/user-properties.vue', 'MENU',    '개인 설정',       'lucide:sliders',       6, false, true, 0),

-- ── 문의 ──────────────────────────────────────────────────
('HD_CONTACT', 'HELPDESK', 'HelpDeskContact', '/helpdesk/contact-us', '#/views/helpdesk/contact-us.vue', 'MENU', '문의하기', 'lucide:mail', 120, false, false, 0);

-- 등록 · 갱신
INSERT INTO scom.system_menus (
  id, name, path, component, pid, type, title, icon, order_no,
  hide_in_menu, keep_alive, affix_tab, dom_cached, menu_visible_with_forbidden,
  status, created_at, created_by, updated_at, updated_by, is_deleted
)
SELECT
  t.id, t.name, t.path, t.component, t.pid, t.type, t.title, t.icon, t.order_no,
  t.hide_in_menu, t.keep_alive, false, false, false,
  t.status, now(), 'helpdesk-migration', now(), 'helpdesk-migration', false
FROM tmp_helpdesk_menu t
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
  -- status 는 덮어쓰지 않는다. 화면 이식이 끝나 1 로 올려둔 항목을 재실행이 되돌리면 안 된다.
  updated_at   = now(),
  updated_by   = 'helpdesk-migration',
  is_deleted   = false;

-- 모든 역할에 헬프데스크 메뉴 권한 부여.
-- role_menus 가 비어 있어도 /menu/all 은 활성 메뉴를 그대로 내려주지만,
-- 역할 권한 화면에서 헬프데스크 메뉴가 누락되어 보이지 않도록 함께 채워둔다.
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT
  r.id, t.id,
  true, true, true, true, true, true, true,
  false, false, false, false, false, false, false, false,
  now(), 'helpdesk-migration', false
FROM scom.roles r
CROSS JOIN tmp_helpdesk_menu t
WHERE r.is_deleted = false
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = t.id
  );

-- 헬프데스크 자체 '메뉴 권한' / '역할 관리' 화면은 제거되었다.
-- 메뉴·역할·화면 접근 권한은 funeralv2 쪽(scom.system_menus / scom.roles / scom.role_menus)만 사용한다.
-- 이전 실행으로 심어진 행이 있으면 함께 지운다.
DELETE FROM scom.role_menus   WHERE menu_id IN ('HD_SYS_MENU', 'HD_SYS_ROLE');
DELETE FROM scom.system_menus WHERE id      IN ('HD_SYS_MENU', 'HD_SYS_ROLE');

COMMIT;

-- 확인
SELECT
  count(*) FILTER (WHERE status = 1) AS 활성,
  count(*) FILTER (WHERE status = 0) AS 대기,
  count(*)                           AS 전체
FROM scom.system_menus
WHERE id = 'HELPDESK' OR id LIKE 'HD\_%';
