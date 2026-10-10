-- ============================================================
-- News Breaking Menu and Schema
-- ============================================================

CREATE SCHEMA IF NOT EXISTS news;

CREATE TABLE IF NOT EXISTS news.news_items (
    id SERIAL PRIMARY KEY,
    title VARCHAR(500) NOT NULL,
    url VARCHAR(1000) NOT NULL,
    category VARCHAR(50),
    urgency VARCHAR(50),
    importance INT,
    source VARCHAR(100),
    published_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE IF NOT EXISTS news.news_keywords (
    id SERIAL PRIMARY KEY,
    account_id VARCHAR(100) NOT NULL,
    keyword VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- Register 'quristyle' keyword '울산'
INSERT INTO news.news_keywords (account_id, keyword) 
VALUES ('quristyle', '울산')
ON CONFLICT DO NOTHING;

-- Register Menu under system administrator
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_excel, use_print,
     route_key)
VALUES ('NEWS_BREAKING', 'NewsBreaking', '/admin/news/breaking', NULL, 'MENU', '뉴스속보 목록',
       'lucide:newspaper', 10, false, 1, now(), 'news-setup', true,
       true, true, true, true, true, false, false, 'admin.news.breaking')
-- 사이드바가 링크를 거는 것은 path 가 아니라 route_key 다(web/CLAUDE.md
-- 「연결 고리는 URL 이 아니라 열쇠다」). 처음 넣은 값이 'news.breaking' ·
-- '/news/breaking' 이었는데 **그 화면은 포털관리 모듈에 있다** — 모듈의
-- `@page` 는 제 접두사로 시작해야 하고(의존 규칙 3) RouteKey 도 제 모듈
-- 이름으로 시작해야 한다(RouteKeyTests). 둘을 'admin.…' 으로 바로잡았으니
-- **이 스크립트를 다시 돌려 DB 의 값도 함께 옮긴다** — 안 옮기면 메뉴는
-- 보이는데 눌러도 안 열린다(풀 열쇠가 어느 화면과도 안 맞는다).
ON CONFLICT (id) DO UPDATE
    SET path = EXCLUDED.path,
        route_key = EXCLUDED.route_key;

-- Grant permissions to SYSTEM_ADMINISTRATOR
INSERT INTO scom.role_menus (
    role_id, menu_id,
    can_view, can_search, can_create, can_update, can_delete,
    can_print, can_excel,
    can_cust1, can_cust2, can_cust3, can_cust4,
    can_cust5, can_cust6, can_cust7, can_cust8,
    created_at, created_by, is_deleted)
SELECT 'SYSTEM_ADMINISTRATOR', 'NEWS_BREAKING',
       true, true, true, true, true, false, false,
       false, false, false, false,
       false, false, false, false,
       now(), 'news-setup', false
WHERE NOT EXISTS (
    SELECT 1 FROM scom.role_menus WHERE role_id = 'SYSTEM_ADMINISTRATOR' AND menu_id = 'NEWS_BREAKING'
);
