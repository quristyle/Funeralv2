-- IFrameView 메뉴를 실제로 iframe 으로 동작하게 맞춘다
--
-- 무엇이 문제였나
--   컴포넌트가 `IFrameView` 인데 주소가 `link` 에 들어 있는 메뉴가 있었다.
--   메뉴를 누르면 라우터로 가기 전에 `use-navigation.ts` 가 `meta.link` 를 보고
--   브라우저 새 탭을 열어 버린다 — 컴포넌트는 쓰이지도 않는다.
--   iframe 으로 뜨려면 주소가 `iframe_src` 에 있어야 한다(`iframe-router-view.vue` 가 그 값을 읽는다).
--
-- 유형(type)도 함께 바꾸는 이유
--   유형은 라우팅에 쓰이지 않는다(라우팅은 component 와 meta 만 본다).
--   하지만 **메뉴 수정 화면이 유형을 보고 주소를 어느 칸에 저장할지 정한다** —
--   유형이 MENU 인 채로 두면 그 메뉴를 편집해 저장하는 순간 iframe 주소가 사라진다.
--   그래서 주소를 옮기는 것과 유형을 EMBEDDED 로 바꾸는 것은 한 쌍이다.
--
-- 곁들여
--   유형이 소문자 'menu' 인 행이 하나 있었다(플레이어 다운로드).
--   수정 화면의 판정은 대문자 기준이라 그 메뉴를 열면 유형이 비어 보이고 컴포넌트 칸도 숨겨진다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- 1) 주소를 link → iframe_src 로 옮긴다 (iframe_src 가 비어 있는 것만)
UPDATE scom.system_menus
   SET iframe_src = link,
       link       = NULL,
       updated_at = now(),
       updated_by = 'menu-iframe-fix'
 WHERE component = 'IFrameView'
   AND coalesce(link, '') <> ''
   AND coalesce(iframe_src, '') = '';

-- 2) 유형을 내장페이지로 맞춘다
UPDATE scom.system_menus
   SET type       = 'EMBEDDED',
       updated_at = now(),
       updated_by = 'menu-iframe-fix'
 WHERE component = 'IFrameView'
   AND type <> 'EMBEDDED';

-- 3) 소문자 유형 정리
UPDATE scom.system_menus
   SET type       = upper(type),
       updated_at = now(),
       updated_by = 'menu-iframe-fix'
 WHERE type <> upper(type);

COMMIT;

-- 확인
--   SELECT path, type, component, link, iframe_src
--     FROM scom.system_menus WHERE component = 'IFrameView' ORDER BY path;
--   SELECT type, count(*) FROM scom.system_menus GROUP BY type;

-- ── 되돌리기 ────────────────────────────────────────────
-- BEGIN;
-- UPDATE scom.system_menus
--    SET link = iframe_src, iframe_src = NULL, type = 'MENU'
--  WHERE updated_by = 'menu-iframe-fix' AND component = 'IFrameView'
--    AND coalesce(iframe_src, '') <> '';
-- COMMIT;
--   (소문자 유형이었던 '플레이어 다운로드' 는 되돌릴 이유가 없어 포함하지 않았다)
