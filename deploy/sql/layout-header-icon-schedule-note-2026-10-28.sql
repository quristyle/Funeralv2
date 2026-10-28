-- ============================================================
-- 레이아웃 상단 아이콘 등록 — AI 일정 메모 (2026-10-28)
-- ============================================================
--
-- scom.layout_header_icons 에 'schedule_note' 아이콘을 등록한다.
-- 상단 헤더에서 항상 접근 가능한 AI 일정 메모 공통 팝업을 연다.
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/layout-header-icon-schedule-note-2026-10-28.sql
--
-- 두 번 돌려도 안전하다(ON CONFLICT).
-- ============================================================

BEGIN;

UPDATE scom.layout_header_icons
   SET order_no = order_no + 1, updated_at = now()
 WHERE order_no >= 3 AND icon_key <> 'schedule_note';

INSERT INTO scom.layout_header_icons (id, icon_key, title, icon, route_key, path, default_enabled, order_no, description, created_at, is_deleted)
VALUES (
    'schedule_note',
    'schedule_note',
    'AI 일정 메모',
    'jsini-icon-calendar',
    'projmng.proj.schedule-note',
    '/projmng/proj/schedule-note',
    true,
    3,
    'AI 일정 메모 팝업 열기',
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
