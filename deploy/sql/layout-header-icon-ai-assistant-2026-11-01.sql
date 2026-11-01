-- ============================================================
-- 레이아웃 상단 아이콘 등록 — 업무 비서 (2026-11-01)
-- ============================================================
--
-- scom.layout_header_icons 에 'ai_assistant' 아이콘을 AI 대화(2) 바로 뒤(3)에 넣는다.
-- 누르면 /admin/ai/assistant 로 옮긴다. 메뉴 권한이 없는 사람에게는 안 보인다.
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/layout-header-icon-ai-assistant-2026-11-01.sql
--
-- 두 번 돌려도 안전하다 — 이미 3 에 있으면 뒤 번호를 다시 밀지 않는다.
-- ============================================================

BEGIN;

UPDATE scom.layout_header_icons
   SET order_no = order_no + 1, updated_at = now()
 WHERE order_no >= 3 AND icon_key <> 'ai_assistant'
   AND NOT EXISTS (SELECT 1 FROM scom.layout_header_icons
                    WHERE icon_key = 'ai_assistant' AND order_no = 3 AND NOT is_deleted);

INSERT INTO scom.layout_header_icons (id, icon_key, title, icon, route_key, path, default_enabled, order_no, description, created_at, is_deleted)
VALUES (
    'ai_assistant',
    'ai_assistant',
    '업무 비서',
    'jsini-icon-briefcase',
    'admin.ai.assistant',
    '/admin/ai/assistant',
    true,
    3,
    '업무 비서 화면 열기',
    now(),
    false
)
ON CONFLICT (icon_key) DO UPDATE SET
    title = EXCLUDED.title,
    icon = EXCLUDED.icon,
    route_key = EXCLUDED.route_key,
    path = EXCLUDED.path,
    default_enabled = EXCLUDED.default_enabled,
    order_no = 3,
    description = EXCLUDED.description,
    is_deleted = false,
    updated_at = now();

COMMIT;

-- 넣은 결과 확인
SELECT id, icon_key, title, icon, route_key, path, default_enabled, order_no
  FROM scom.layout_header_icons
 WHERE NOT is_deleted
 ORDER BY order_no;
