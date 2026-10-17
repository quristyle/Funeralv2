-- ============================================================
-- 레이아웃 상단 아이콘 관리 테이블 및 초기 데이터
-- ============================================================
-- 
-- 권한이 있는 항목에 대해 기본값은 모두 사용(true)으로 관리한다.
-- 환경설정 화면에서 권한이 부여된 목록만 보여주고 스위치로 사용 여부를 제어한다.
-- ============================================================

CREATE TABLE IF NOT EXISTS scom.layout_header_icons (
    id VARCHAR(50) PRIMARY KEY,
    icon_key VARCHAR(50) NOT NULL UNIQUE,
    title VARCHAR(100) NOT NULL,
    icon VARCHAR(100) NOT NULL,
    route_key VARCHAR(100),
    path VARCHAR(200),
    default_enabled BOOLEAN NOT NULL DEFAULT true,
    order_no INT NOT NULL DEFAULT 0,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT false
);

COMMENT ON TABLE scom.layout_header_icons IS '레이아웃 상단 도구 아이콘 목록 및 기본 사용 여부';
COMMENT ON COLUMN scom.layout_header_icons.id IS '식별자';
COMMENT ON COLUMN scom.layout_header_icons.icon_key IS '아이콘 고유 키';
COMMENT ON COLUMN scom.layout_header_icons.title IS '표시 이름';
COMMENT ON COLUMN scom.layout_header_icons.icon IS '아이콘 CSS 클래스명';
COMMENT ON COLUMN scom.layout_header_icons.route_key IS '관련 메뉴 라우트 키';
COMMENT ON COLUMN scom.layout_header_icons.path IS '관련 메뉴 경로';
COMMENT ON COLUMN scom.layout_header_icons.default_enabled IS '기본 사용 여부 (기본값: true)';
COMMENT ON COLUMN scom.layout_header_icons.order_no IS '정렬 순서';
COMMENT ON COLUMN scom.layout_header_icons.description IS '아이콘 설명';

INSERT INTO scom.layout_header_icons (id, icon_key, title, icon, route_key, path, default_enabled, order_no, description, created_at, is_deleted)
VALUES
    ('quick_ask', 'quick_ask', '빠른 지시', 'jsini-icon-bolt', 'projmng.ai.ask', '/projmng/ai/ask', true, 1, 'AI 에게 한 줄 보내기 (서랍 또는 화면)', now(), false),
    ('ai_chat', 'ai_chat', 'AI 대화', 'jsini-icon-robot', 'site.ai.chat', '/ai/chat', true, 2, 'AI 대화 서랍 열기', now(), false),
    ('note', 'note', '쪽지', 'jsini-icon-mail', 'admin.note.write', '/admin/note/write', true, 3, '쪽지 쓰기 및 안 읽은 쪽지 확인', now(), false),
    ('notification', 'notification', '알림', 'jsini-icon-bell', 'admin.push.history', '/system/push/history', true, 4, '알림함 서랍 열기', now(), false),
    ('favorite', 'favorite', '즐겨찾기', 'jsini-icon-star', NULL, NULL, true, 5, '현재 화면 즐겨찾기 추가/해제', now(), false),
    ('refresh', 'refresh', '화면 새로 고침', 'jsini-icon-refresh', NULL, NULL, true, 6, '현재 화면 새로 고침', now(), false),
    ('fullscreen', 'fullscreen', '전체화면', 'jsini-icon-expand', NULL, NULL, true, 7, '브라우저 전체화면 전환', now(), false),
    ('theme', 'theme', '테마 변경', 'jsini-icon-palette', NULL, NULL, true, 8, '테마 및 색상 설정 서랍 열기', now(), false)
ON CONFLICT (icon_key) DO UPDATE SET
    title = EXCLUDED.title,
    icon = EXCLUDED.icon,
    route_key = EXCLUDED.route_key,
    path = EXCLUDED.path,
    default_enabled = EXCLUDED.default_enabled,
    order_no = EXCLUDED.order_no,
    description = EXCLUDED.description,
    is_deleted = EXCLUDED.is_deleted,
    updated_at = now();
