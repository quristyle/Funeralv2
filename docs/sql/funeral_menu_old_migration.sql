-- ============================================================
-- 옛 장례식장 시스템에서 이식한 화면의 메뉴 등록.
--
-- 대상 DB : jsiniportal (scom)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/funeral_menu_old_migration.sql
--
-- 여러 번 실행해도 안전하다 (있으면 갱신, 없으면 추가).
--
-- ── 왜 ───────────────────────────────────────────────────────
-- 옛 메뉴 54개를 현 메뉴와 맞춰 본 결과, 대부분은 이미 있거나 포털 공통
-- (시스템 · 권한 · 회사 · 공통코드)으로 대체됐다. 남은 것이 아래 하나다.
-- 무엇이 무엇에 대응하는지는 docs/analysis/40-old-funeral-migration.md.
--
--   rsrc/music_build (건물별 음원) → /building/music-build
--
-- 나머지 옛 메뉴는 다음 이유로 만들지 않았다.
--   acct/build_acct 건물 관리자 관리 → 포털 '회사 사용자관리'
--   auth/rol_menu   롤메뉴          → 포털 '메뉴롤'
--   comm/code       코드관리        → 포털 '공통코드'
--   help/cont_us    문의            → 헬프데스크 문의
--   client_machine  미리보기 4종    → 하나로 합쳐 '/info/preview' 에 있다
--
-- 메뉴가 없으면 라우트 자체가 생기지 않는다. 화면 파일만 만들어 두고 여기에
-- 등록하지 않으면 아무도 그 화면에 갈 수 없다.
-- ============================================================

BEGIN;

-- 건물별 음원 배정. 소스 관리(SOURCE) 아래, 음원(AUDIO) 바로 다음에 둔다.
INSERT INTO scom.system_menus (
    id, name, path, component, pid, type,
    title, icon, order_no, hide_in_menu, status,
    created_at, created_by, is_deleted,
    affix_tab, dom_cached, keep_alive, menu_visible_with_forbidden,
    use_view, use_search, use_create, use_delete, use_update, use_print, use_excel
) VALUES (
    'MUSIC_BUILD', '건물별 음원', '/building/music-build',
    '#/views/funeral/building/music-build/index.vue', 'SOURCE', 'MENU',
    '건물별 음원', 'lucide:building-2', 2, false, 1,
    now(), 'migration', false,
    false, false, false, false,
    true, true, true, true, true, false, false
)
ON CONFLICT (id) DO UPDATE SET
    name         = EXCLUDED.name,
    path         = EXCLUDED.path,
    component    = EXCLUDED.component,
    pid          = EXCLUDED.pid,
    type         = EXCLUDED.type,
    title        = EXCLUDED.title,
    icon         = EXCLUDED.icon,
    status       = EXCLUDED.status,
    is_deleted   = false,
    updated_at   = now(),
    updated_by   = 'migration';

-- 음원(AUDIO) 다음에 오도록 뒤 형제들의 순서를 한 칸씩 민다.
-- 장식관리(order_no 2)가 3 이 된다.
UPDATE scom.system_menus
   SET order_no = order_no + 1, updated_at = now(), updated_by = 'migration'
 WHERE pid = 'SOURCE'
   AND id <> 'MUSIC_BUILD'
   AND order_no >= 2
   AND NOT EXISTS (
       -- 이미 밀어 둔 뒤 다시 실행하는 경우를 막는다.
       SELECT 1 FROM scom.system_menus x
        WHERE x.pid = 'SOURCE' AND x.id <> 'MUSIC_BUILD' AND x.order_no = 3
   );

COMMIT;

-- 확인용
-- SELECT id, name, path, component, order_no, status
--   FROM scom.system_menus WHERE pid = 'SOURCE' ORDER BY order_no;
