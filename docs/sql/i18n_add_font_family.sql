-- 다국어 키 `preferences.fontFamily` 를 추가한다
--
-- 어디에 쓰이나
--   환경설정 › 테마 › 기본 글꼴 (S-CoreDream · Play · 시스템 설정 따름)
--   packages/effects/layouts/src/widgets/preferences/blocks/theme/font-family.vue
--   packages/effects/layouts/src/widgets/preferences/preferences-drawer.vue
--
-- 왜 빠져 있었나
--   글꼴 설정을 넣을 때 한국어 팩(`langs/ko/preferences.json`)에만 넣고
--   영어 팩과 DB 에는 넣지 않았다. 그래서 다국어 관리 화면에서 이 항목이 보이지 않았다.
--
-- 값은 이웃 항목(`preferences.theme.fontSize` = 글꼴 크기 / Font Size)에 맞췄다.
--
-- 이 표에는 (key, locale) 고유 제약이 없어서 ON CONFLICT 대신
-- NOT EXISTS 로 막는다. 반복 실행해도 안전하다.

BEGIN;

INSERT INTO scom.i18n_resources (key, locale, value, category, created_at, created_by)
SELECT v.key, v.locale, v.value, 'preferences', now(), 'System'
  FROM (VALUES
          ('preferences.fontFamily', 'ko', '기본 글꼴'),
          ('preferences.fontFamily', 'en', 'Font Family')
       ) AS v(key, locale, value)
 WHERE NOT EXISTS (
         SELECT 1 FROM scom.i18n_resources r
          WHERE r.key = v.key AND r.locale = v.locale AND r.is_deleted = false
       );

COMMIT;

-- 확인
--   SELECT locale, key, value, category FROM scom.i18n_resources
--    WHERE key = 'preferences.fontFamily' ORDER BY locale;

-- ── 되돌리기 ────────────────────────────────────────────
-- DELETE FROM scom.i18n_resources WHERE key = 'preferences.fontFamily';
