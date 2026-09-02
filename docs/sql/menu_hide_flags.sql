-- ============================================================
-- 메뉴 — 숨김 설정 세 가지를 실제로 저장한다
-- ============================================================
--
-- DB: jsiniportal (스키마 scom)
--
-- 메뉴 관리 수정 창에는 숨김 체크가 넷 있었다.
--   메뉴에서 숨김 · 하위 메뉴 숨기기 · 브레드크럼에서 숨김 · 탭 바에서 숨김
--
-- 그런데 저장되는 것은 첫째(`hide_in_menu`) 하나뿐이었다.
-- 나머지 셋은 표에 칸이 없고 DTO 에도 없어서, 켜고 저장해도 아무 일이 없었다
-- (요청 본문에는 실려 나가지도 않았다). 화면에는 있는데 저장되지 않는 항목이라
-- 켠 사람은 왜 안 듣는지 알 수 없었다.
--
--   hide_children_in_menu : 이 메뉴의 하위를 메뉴목록에서 감춘다(자기만 보인다)
--   hide_in_breadcrumb    : 브레드크럼(현재 위치 표시)에서 감춘다
--   hide_in_tab           : 탭 바에서 감춘다
--
-- 셋 다 **메뉴목록 · 브레드크럼 · 탭 바에서만** 감추는 설정이다.
-- 라우트는 그대로 만들어지므로 주소로 들어가면 화면은 열린다
-- (`status = 0` 과 다른 뜻이고, `use_mobile` · `use_tablet` 과 같은 성격이다).
--
-- 기존 동작이 바뀌지 않도록 기본값은 셋 다 false(감추지 않음) 다.
--
-- 반복 실행해도 안전하다.

BEGIN;

ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS hide_children_in_menu boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS hide_in_breadcrumb    boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS hide_in_tab           boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN scom.system_menus.hide_children_in_menu IS '하위 메뉴를 메뉴목록에서 감출지. 라우트는 유지된다.';
COMMENT ON COLUMN scom.system_menus.hide_in_breadcrumb    IS '브레드크럼에서 감출지. 라우트는 유지된다.';
COMMENT ON COLUMN scom.system_menus.hide_in_tab           IS '탭 바에서 감출지. 라우트는 유지된다.';

COMMIT;

-- ============================================================
-- 메뉴 제목의 다국어 — 조회를 빠르게 하려고 붙는 인덱스
-- ============================================================
--
-- 메뉴 관리 화면은 메뉴 180건의 제목을 그려야 한다. 예전에는 화면이 제목마다
-- `$t()` 를 불러 옮겼는데, 제목 대부분(180건 중 166건)이 번역 키가 아니라
-- 이미 완성된 글자여서 vue-i18n 이 "키를 못 찾았다" 경고를 쏟아냈다 —
-- 한 번 새로 그릴 때마다 492줄이었다. 이것이 화면이 늦게 뜨던 진짜 이유다.
--
-- 이제 **백엔드가 제목을 옮겨 `meta.titleText` 로 함께 내려준다.**
-- 그때 `scom.i18n_resources` 를 (locale, key) 로 한 번에 읽으므로
-- 그 짝에 인덱스를 둔다.

CREATE INDEX IF NOT EXISTS ix_i18n_resources_locale_key
    ON scom.i18n_resources (locale, key);

-- 확인
SELECT count(*) AS 전체,
       count(*) FILTER (WHERE hide_children_in_menu) AS 하위숨김,
       count(*) FILTER (WHERE hide_in_breadcrumb)    AS 브레드크럼숨김,
       count(*) FILTER (WHERE hide_in_tab)           AS 탭숨김
FROM scom.system_menus;
