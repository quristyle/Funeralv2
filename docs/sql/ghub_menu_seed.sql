-- ============================================================
-- GHUB(SK가스 지허브) 이식 화면을 JSini 포털 메뉴로 등록한다.
--   이식 범위: 기상(WEATHER) · 생일(BIRTHDAY) — docs/analysis/38-ghub-migration.md
--
-- 대상 DB : jsiniportal (AuthServer 가 소유하는 scom 스키마)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/ghub_menu_seed.sql
--
-- 여러 번 실행해도 안전하다(같은 id 면 갱신, status 는 덮어쓰지 않는다).
--
-- ── 원본(ASIS) 과의 대응 ─────────────────────────────────────
-- ASIS 는 jin114.co.kr:45750 ghub.sys_menus 다 (숫자 id · category 컬럼 구조).
-- 이 파일은 ASIS 를 읽기만 해서 만든 것이고, ASIS 는 절대 바꾸지 않는다.
--
--   ASIS id  ASIS 이름           ASIS path              → 포털 id
--   105      날씨 (최상위)        -                      → LIFE_WEATHER
--   9        실시간 날씨 현황     /weather               → LIFE_WEATHER_DASH
--   35       날씨 예보            /weather/forecast      → LIFE_WEATHER_FORECAST
--   38       기상 특보            /weather/warning       → LIFE_WEATHER_WARNING
--   75       날씨 대응            /weather/responses     → LIFE_WEATHER_RESPONSE
--   32       날씨 기록            /weather/history       → LIFE_WEATHER_HISTORY
--   79       날씨 이벤트 기록     /weather/events        → LIFE_WEATHER_EVENTS
--   111      날씨 관리 (하위폴더) -                      → LIFE_WEATHER_MNG
--   31       날씨 지역            /weather-info          → LIFE_WEATHER_LOCATION
--   74       날씨 기준 관리       /weather/standards     → LIFE_WEATHER_STANDARD
--   107      생일 (최상위)        -                      → LIFE_BIRTHDAY
--   10       생일 목록            /birthdays             → LIFE_BIRTHDAY_LIST
--   34       생일 캘린더          /birthdays/calendar    → LIFE_BIRTHDAY_CAL
--   71       생일 메시지 확인     /birthdays/messages    → LIFE_BIRTHDAY_MSG
--
-- ASIS Developer 카테고리의 [특보 테스트](/weather/warning-test, id 95)는
-- 개발 검증 도구라 이식하지 않는다.
--
-- ASIS 에서 날씨·생일이 각각 최상위였지만, 포털에서는
-- [생활과환경](LIFEENV) 최상위 하나 아래에 담는다 — 이식 지시가 그렇다.
-- ============================================================

BEGIN;

CREATE TEMP TABLE tmp_ghub_menu (
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
  use_create    boolean,
  use_update    boolean,
  use_delete    boolean,
  use_excel     boolean
) ON COMMIT DROP;

INSERT INTO tmp_ghub_menu
  (id, pid, name, path, component, type, title, icon, order_no,
   hide_in_menu, keep_alive, status, use_create, use_update, use_delete, use_excel)
VALUES
-- ── 최상위 ────────────────────────────────────────────────────
('LIFEENV', NULL, 'LifeEnv', '/life', NULL, 'CATALOG', '생활과환경', 'lucide:sun', 12, false, false, 1, false, false, false, false),

-- ── 날씨 ──────────────────────────────────────────────────────
('LIFE_WEATHER',          'LIFEENV',      'LifeWeather',         '/life/weather',           NULL,                                   'CATALOG', '날씨',            'lucide:cloud-sun',          10, false, false, 1, false, false, false, false),
('LIFE_WEATHER_DASH',     'LIFE_WEATHER', 'LifeWeatherDash',     '/life/weather/dashboard', '#/views/life/weather/dashboard.vue',   'MENU',    '실시간 날씨 현황', 'lucide:gauge',              10, false, true,  1, false, false, false, false),
('LIFE_WEATHER_FORECAST', 'LIFE_WEATHER', 'LifeWeatherForecast', '/life/weather/forecast',  '#/views/life/weather/forecast.vue',    'MENU',    '날씨 예보',        'lucide:cloud-sun-rain',     20, false, true,  1, false, false, false, false),
('LIFE_WEATHER_WARNING',  'LIFE_WEATHER', 'LifeWeatherWarning',  '/life/weather/warning',   '#/views/life/weather/warning.vue',     'MENU',    '기상 특보',        'lucide:siren',              30, false, true,  1, false, false, false, false),
('LIFE_WEATHER_RESPONSE', 'LIFE_WEATHER', 'LifeWeatherResponse', '/life/weather/responses', '#/views/life/weather/responses.vue',   'MENU',    '날씨 대응',        'lucide:clipboard-check',    40, false, true,  1, true,  true,  true,  true),
('LIFE_WEATHER_HISTORY',  'LIFE_WEATHER', 'LifeWeatherHistory',  '/life/weather/history',   '#/views/life/weather/history.vue',     'MENU',    '날씨 기록',        'lucide:history',            50, false, true,  1, false, false, false, true),
('LIFE_WEATHER_EVENTS',   'LIFE_WEATHER', 'LifeWeatherEvents',   '/life/weather/events',    '#/views/life/weather/events.vue',      'MENU',    '날씨 이벤트 기록', 'lucide:list-checks',        60, false, true,  1, false, false, false, true),

-- ── 날씨 > 날씨 관리 (ASIS 111 하위폴더 유지) ─────────────────
('LIFE_WEATHER_MNG',      'LIFE_WEATHER',     'LifeWeatherMng',      '/life/weather/manage',           NULL,                                        'CATALOG', '날씨 관리',      'lucide:settings-2',         70, false, false, 1, false, false, false, false),
('LIFE_WEATHER_LOCATION', 'LIFE_WEATHER_MNG', 'LifeWeatherLocation', '/life/weather/manage/locations', '#/views/life/weather/locations.vue',        'MENU',    '날씨 지역',      'lucide:map-pin',            10, false, true,  1, true,  true,  true,  false),
('LIFE_WEATHER_STANDARD', 'LIFE_WEATHER_MNG', 'LifeWeatherStandard', '/life/weather/manage/standards', '#/views/life/weather/standards.vue',        'MENU',    '날씨 기준 관리', 'lucide:sliders-horizontal', 20, false, true,  1, true,  true,  true,  false),

-- ── 생일 ──────────────────────────────────────────────────────
('LIFE_BIRTHDAY',      'LIFEENV',       'LifeBirthday',    '/life/birthday',          NULL,                                  'CATALOG', '생일',             'lucide:cake',           20, false, false, 1, false, false, false, false),
('LIFE_BIRTHDAY_LIST', 'LIFE_BIRTHDAY', 'LifeBirthdayList','/life/birthday/list',     '#/views/life/birthday/list.vue',      'MENU',    '생일 목록',        'lucide:list',           10, false, true,  1, true,  false, false, false),
('LIFE_BIRTHDAY_CAL',  'LIFE_BIRTHDAY', 'LifeBirthdayCal', '/life/birthday/calendar', '#/views/life/birthday/calendar.vue',  'MENU',    '생일 캘린더',      'lucide:calendar-days',  20, false, true,  1, true,  false, false, false),
('LIFE_BIRTHDAY_MSG',  'LIFE_BIRTHDAY', 'LifeBirthdayMsg', '/life/birthday/messages', '#/views/life/birthday/messages.vue',  'MENU',    '생일 메시지 확인', 'lucide:mail-open',      30, false, true,  1, false, false, false, false);

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
  t.status, now(), 'ghub-migration', now(), 'ghub-migration', false,
  true, (t.type = 'MENU'), t.use_create, t.use_update, t.use_delete, false, t.use_excel
FROM tmp_ghub_menu t
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
  -- status 는 덮어쓰지 않는다. 운영에서 닫아 둔 화면을 재실행이 되돌리면 안 된다.
  updated_at   = now(),
  updated_by   = 'ghub-migration',
  is_deleted   = false;

-- ── 역할 권한 ────────────────────────────────────────────────
-- 우선 관리자 역할에만 준다. ASIS 는 roles 가 비어 있어 전 직원 공개였지만,
-- 포털에서 어느 역할까지 열지는 운영 결정이라 역할 권한 화면에서 넓히면 된다.
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
  now(), 'ghub-migration', false
FROM scom.roles r
CROSS JOIN tmp_ghub_menu t
WHERE r.is_deleted = false
  AND r.id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = r.id AND rm.menu_id = t.id
  );

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT
  count(*) FILTER (WHERE type = 'CATALOG')                   AS 폴더,
  count(*) FILTER (WHERE type = 'MENU')                      AS 화면,
  count(*)                                                   AS 전체
FROM scom.system_menus
WHERE id = 'LIFEENV' OR id LIKE 'LIFE\_%';

SELECT id, title, component
FROM scom.system_menus
WHERE (id = 'LIFEENV' OR id LIKE 'LIFE\_%') AND component IS NOT NULL
ORDER BY order_no, id;
