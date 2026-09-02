-- ============================================================
-- 장례식장 업무 설정 화면을 설정 메뉴 아래에 더한다.
--
-- 대상 DB : jsiniportal (scom)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/funeral_menu_work_options.sql
--
-- 여러 번 실행해도 안전하다 (있으면 갱신, 없으면 추가).
--
-- ── 왜 ───────────────────────────────────────────────────────
-- 옛 시스템 이식 1차에서 `/setting/environment`(ENV_SETTING) 화면을 옛
-- `page/ui_config.jsp` 의 업무 설정으로 덮어썼다. **그 자리는 원래 vben 개인
-- 환경설정**(테마 · 사이드바 · 탭)이었다. 화면을 되돌리고, 업무 설정은
-- 여기 새 메뉴로 옮긴다.
--
-- 둘은 다루는 것이 다르다.
--   /setting/environment   개인 환경설정 — 테마 · 사이드바 · 탭 (vben)
--   /setting/work-options  장례식장 업무 규칙 — 기존고객연결 · 고인명칭 · 장비 기본값 · 회사 숨김
--
-- 옛 코드 여덟 중 화면 관련 넷은 옮기지 않았다(vben 것과 겹친다).
-- 자세한 것은 docs/analysis/40-old-funeral-migration.md 의 D-F3.
-- ============================================================

BEGIN;

INSERT INTO scom.system_menus (
    id, name, path, component, pid, type,
    title, icon, order_no, hide_in_menu, status,
    created_at, created_by, is_deleted,
    affix_tab, dom_cached, keep_alive, menu_visible_with_forbidden,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel
) VALUES (
    'WORK_OPTIONS', '업무 설정', '/setting/work-options',
    '#/views/funeral/setting/work-options/index.vue', 'SETTING', 'MENU',
    '업무 설정', 'lucide:sliders-horizontal', 1, false, 1,
    now(), 'migration', false,
    false, false, false, false,
    true, true, false, false, true, false, false
)
ON CONFLICT (id) DO UPDATE SET
    name       = EXCLUDED.name,
    path       = EXCLUDED.path,
    component  = EXCLUDED.component,
    pid        = EXCLUDED.pid,
    type       = EXCLUDED.type,
    title      = EXCLUDED.title,
    icon       = EXCLUDED.icon,
    status     = EXCLUDED.status,
    is_deleted = false,
    updated_at = now(),
    updated_by = 'migration';

-- 환경설정 화면을 원래대로 되돌린다 (컴포넌트 경로는 그대로였지만 확인 삼아 맞춘다).
UPDATE scom.system_menus
   SET component = '#/views/funeral/setting/environment/index.vue',
       updated_at = now(),
       updated_by = 'migration'
 WHERE id = 'ENV_SETTING';

COMMIT;

-- 확인용
-- SELECT id, name, path, component, order_no FROM scom.system_menus
--  WHERE pid = 'SETTING' ORDER BY order_no;
