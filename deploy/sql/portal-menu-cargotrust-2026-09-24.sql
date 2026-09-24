-- ============================================================
-- 운송관리(CargoTrust) 메뉴 두 묶음을 올린다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 설계: docs/cargotrust/01-service-overview.md 4.1 · 23절
--
-- **CargoTrust 자료 DB(cargotrust/cargotrust)와 DB 가 다르다.** 이쪽은 포털 DB
-- (jsiniportal/scom)고, 메뉴 노출과 권한만 다룬다.
--
-- ── 열쇠가 연결의 전부다 ──────────────────────────────────
--
-- 사이드바는 `route_key` 로 화면을 찾는다(web/CLAUDE.md 「라우팅 소유권이
-- 뒤집혔다」). 값이 화면의 `[RouteKey(...)]` 와 다르면 오류 없이 「준비 중」이
-- 뜬다. `path` 는 **권한표와 즐겨찾기의 열쇠**라 한 번 정하면 안 바꾼다.
--
--   사용자  JSini.Web.CargoTrust        /cargotrust/*   cargotrust.*
--   관리자  JSini.Web.CargoTrust.Admin  /cargoadmin/*   cargoadmin.*
--
-- 묶음(CATALOG) 줄의 path 는 `/cargo-trust` · `/cargo-admin` 으로 뺐다. 홈
-- 화면의 `@page` 가 `/cargotrust` · `/cargoadmin` 이고, 화면 안의 권한 판정은
-- **지금 주소를 path 로 권한표에서 찾기** 때문에 그 path 는 홈 줄이 가져야
-- 한다(장례식장 묶음이 `/funerals` 인 것과 같은 까닭).
--
-- 거래처 상세 · 거래 상세처럼 번호를 받는 화면은 메뉴에 올리지 않는다 —
-- 목록에서 눌러 들어간다.
--
-- ── 권한 ────────────────────────────────────────────────────
--
-- 두 묶음 모두 ADMINISTRATOR · SYSTEM_ADMINISTRATOR 에게만 켠다(시험 서비스
-- 단계). 나머지 역할은 줄만 만들어 두고 전부 끈다 — 권한 화면
-- (/admin/auth)에서 켤 자리를 마련해 두는 것이 이 표의 관례다.
--
-- **관리자 화면의 진짜 문지기는 서버다.** 메뉴를 켜 줘도 CargoTrustServer 가
-- `CargoTrust:AdminRoles` 로 다시 따진다(/admin/* 는 403).
--
-- ── 안전한 성질 ─────────────────────────────────────────────
--
--   · 멱등하다. 메뉴는 ON CONFLICT 로 덮고, 권한은 없을 때만 넣는다.
--   · 배포 순서를 안 탄다. 화면이 아직 안 떠 있으면 「준비 중」이 뜬다.
--   · 되돌리기는 맨 아래.

BEGIN;

-- ── 메뉴 ────────────────────────────────────────────────────

WITH m (id, pid, name, path, route_key, type, title, icon, order_no, hide,
        v, s, c, u, d, x) AS (
    VALUES
    -- 사용자 묶음
    ('CARGOTRUST',          NULL,         'CargoTrust',        '/cargo-trust',                 NULL,                       'CATALOG', '운송관리',        'lucide:factory',        11, false, true, false, false, false, false, false),
    ('CT_HOME',             'CARGOTRUST', 'CtHome',            '/cargotrust',                  'cargotrust',               'MENU',    '홈',              'lucide:layout-dashboard', 10, false, true, true,  false, false, false, false),
    ('CT_COMPANIES',        'CARGOTRUST', 'CtCompanies',       '/cargotrust/companies',        'cargotrust.companies',     'MENU',    '거래처 검색',     'lucide:search-check',   20, false, true, true,  true,  false, false, false),
    ('CT_TRANSACTION_NEW',  'CARGOTRUST', 'CtTransactionNew',  '/cargotrust/transactions/new', 'cargotrust.transactions.new', 'MENU', '거래 등록',       'lucide:file-pen',       30, false, true, true,  true,  false, false, false),
    ('CT_PAYMENTS',         'CARGOTRUST', 'CtPayments',        '/cargotrust/payments',         'cargotrust.payments',      'MENU',    '결제 등록',       'lucide:coins',          40, false, true, true,  true,  true,  false, false),
    ('CT_RECEIVABLES',      'CARGOTRUST', 'CtReceivables',     '/cargotrust/receivables',      'cargotrust.receivables',   'MENU',    '미수금',          'lucide:receipt',        50, false, true, true,  false, false, false, true),
    ('CT_TRANSACTIONS',     'CARGOTRUST', 'CtTransactions',    '/cargotrust/transactions',     'cargotrust.transactions',  'MENU',    '내 거래',         'lucide:list',           60, false, true, true,  true,  true,  true,  true),
    ('CT_REPORTS',          'CARGOTRUST', 'CtReports',         '/cargotrust/reports',          'cargotrust.reports',       'MENU',    '내 신고',         'lucide:megaphone',      70, false, true, true,  true,  false, false, false),
    ('CT_DISPUTES',         'CARGOTRUST', 'CtDisputes',        '/cargotrust/disputes',         'cargotrust.disputes',      'MENU',    '이의제기',        'lucide:messages-square', 80, false, true, true,  true,  false, false, false),
    ('CT_ME',               'CARGOTRUST', 'CtMe',              '/cargotrust/me',               'cargotrust.me',            'MENU',    '내 정보',         'lucide:user',           90, false, true, false, false, false, false, false),
    -- 관리자 묶음
    ('CARGOADMIN',          NULL,         'CargoAdmin',        '/cargo-admin',                 NULL,                       'CATALOG', '운송관리 관리자', 'lucide:shield-check',   11, false, true, false, false, false, false, false),
    ('CTA_DASHBOARD',       'CARGOADMIN', 'CtaDashboard',      '/cargoadmin',                  'cargoadmin',               'MENU',    '대시보드',        'lucide:gauge',          10, false, true, true,  false, false, false, false),
    ('CTA_COMPANIES',       'CARGOADMIN', 'CtaCompanies',      '/cargoadmin/companies',        'cargoadmin.companies',     'MENU',    '거래처 관리',     'lucide:building-2',     20, false, true, true,  true,  true,  false, true),
    ('CTA_TRANSACTIONS',    'CARGOADMIN', 'CtaTransactions',   '/cargoadmin/transactions',     'cargoadmin.transactions',  'MENU',    '거래 데이터 관리', 'lucide:table-2',       30, false, true, true,  false, true,  false, true),
    ('CTA_PAYMENTS',        'CARGOADMIN', 'CtaPayments',       '/cargoadmin/payments',         'cargoadmin.payments',      'MENU',    '결제 기록',       'lucide:receipt',        40, false, true, true,  false, false, false, true),
    ('CTA_REVIEWS',         'CARGOADMIN', 'CtaReviews',        '/cargoadmin/reviews',          'cargoadmin.reviews',       'MENU',    '후기 관리',       'lucide:message-square', 50, false, true, true,  false, true,  false, true),
    ('CTA_REPORTS',         'CARGOADMIN', 'CtaReports',        '/cargoadmin/reports',          'cargoadmin.reports',       'MENU',    '신고 관리',       'lucide:megaphone',      60, false, true, true,  false, true,  false, true),
    ('CTA_DISPUTES',        'CARGOADMIN', 'CtaDisputes',       '/cargoadmin/disputes',         'cargoadmin.disputes',      'MENU',    '이의제기 관리',   'lucide:mail-question',  70, false, true, true,  false, true,  false, true),
    ('CTA_USERS',           'CARGOADMIN', 'CtaUsers',          '/cargoadmin/users',            'cargoadmin.users',         'MENU',    '사용자 관리',     'lucide:users',          80, false, true, true,  false, true,  false, true),
    ('CTA_STATISTICS',      'CARGOADMIN', 'CtaStatistics',     '/cargoadmin/statistics',       'cargoadmin.statistics',    'MENU',    '통계',            'lucide:bar-chart-3',    90, false, true, true,  false, false, false, true),
    ('CTA_AUDIT',           'CARGOADMIN', 'CtaAudit',          '/cargoadmin/audit',            'cargoadmin.audit',         'MENU',    '감사 기록',       'lucide:history',       100, false, true, true,  false, false, false, true)
)
INSERT INTO scom.system_menus (
    id, pid, name, path, route_key, type, title, icon,
    order_no, status, hide_in_menu, created_at, created_by,
    use_view, use_search, use_create, use_update, use_delete, use_print, use_excel)
SELECT id, pid, name, path, route_key, type, title, icon,
       order_no, 1, hide, now(), 'cargotrust-setup',
       v, s, c, u, d, false, x
  FROM m
ON CONFLICT (id) DO UPDATE SET
    pid        = EXCLUDED.pid,
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    route_key  = EXCLUDED.route_key,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    order_no   = EXCLUDED.order_no,
    status     = EXCLUDED.status,
    use_view   = EXCLUDED.use_view,
    use_search = EXCLUDED.use_search,
    use_create = EXCLUDED.use_create,
    use_update = EXCLUDED.use_update,
    use_delete = EXCLUDED.use_delete,
    use_print  = EXCLUDED.use_print,
    use_excel  = EXCLUDED.use_excel,
    updated_at = now(),
    updated_by = 'cargotrust-setup';

-- ── 권한 ────────────────────────────────────────────────────
--
-- 역할마다 줄을 만든다. 켜는 것은 두 관리자 역할뿐이고, 켤 때는 그 메뉴가
-- 쓰는 동작(use_*)만 켠다 — 쓰지 않는 동작에 권한을 주면 권한 화면에서
-- 의미 없는 체크가 켜진 채로 보인다.

INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete,
    can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4,
    can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted)
SELECT r.role_id, sm.id,
       on_ AND sm.use_view, on_ AND sm.use_search, on_ AND sm.use_create,
       on_ AND sm.use_update, on_ AND sm.use_delete,
       false, on_ AND sm.use_excel,
       false, false, false, false,
       false, false, false, false,
       now(), 'cargotrust-setup', false
  FROM (SELECT DISTINCT role_id,
               role_id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR') AS on_
          FROM scom.role_menus) r
 CROSS JOIN scom.system_menus sm
 WHERE sm.id IN ('CARGOTRUST', 'CARGOADMIN')
    OR sm.pid IN ('CARGOTRUST', 'CARGOADMIN')
ON CONFLICT (role_id, menu_id) DO NOTHING;

COMMIT;

-- 확인
--
--   SELECT id, pid, path, route_key, title, order_no
--     FROM scom.system_menus
--    WHERE id IN ('CARGOTRUST','CARGOADMIN') OR pid IN ('CARGOTRUST','CARGOADMIN')
--    ORDER BY pid NULLS FIRST, order_no;
--
--   SELECT role_id, count(*) FILTER (WHERE can_view) AS 켜짐, count(*) AS 전체
--     FROM scom.role_menus
--    WHERE menu_id IN (SELECT id FROM scom.system_menus
--                       WHERE id IN ('CARGOTRUST','CARGOADMIN') OR pid IN ('CARGOTRUST','CARGOADMIN'))
--    GROUP BY role_id ORDER BY role_id;
--
-- 되돌리기
--
--   DELETE FROM scom.role_menus WHERE menu_id IN (SELECT id FROM scom.system_menus
--          WHERE id IN ('CARGOTRUST','CARGOADMIN') OR pid IN ('CARGOTRUST','CARGOADMIN'));
--   DELETE FROM scom.system_menus WHERE pid IN ('CARGOTRUST','CARGOADMIN');
--   DELETE FROM scom.system_menus WHERE id  IN ('CARGOTRUST','CARGOADMIN');
