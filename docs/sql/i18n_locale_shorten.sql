-- 다국어 자원의 언어 코드를 짧게 바꾼다: ko-KR → ko, en-US → en
--
-- 왜
--   화면 코드에서 언어를 `ko` · `en` 하나로 관리하기로 했다.
--   `ko-KR` 처럼 지역이 붙어 있으면 vue-i18n 이 지역을 뗀 `ko` 도 한 번 더 찾아서,
--   못 찾는 키마다 콘솔 경고가 **두 줄씩** 났다.
--   (경위: docs/analysis/18-i18n-fallback-warning.md)
--
-- 함께 바뀐 곳
--   앞단: packages/locales/src/langs/{ko,en} · SUPPORT_LANGUAGES · fallbackLocale 등
--   이미 쓰던 브라우저에 남은 `ko-KR` 설정은 앱이 읽을 때 한 번 다듬는다
--   (apps/jsini-portal/src/locales/index.ts 의 shortenLocale).
--
-- 반복 실행해도 안전하다.

BEGIN;

UPDATE scom.i18n_resources SET locale = 'ko' WHERE locale = 'ko-KR';
UPDATE scom.i18n_resources SET locale = 'en' WHERE locale = 'en-US';

COMMIT;

-- 확인
--   SELECT locale, count(*) FROM scom.i18n_resources GROUP BY locale ORDER BY 1;
--   → ko / en 두 줄만 나와야 한다

-- ── 되돌리기 ────────────────────────────────────────────
-- BEGIN;
-- UPDATE scom.i18n_resources SET locale = 'ko-KR' WHERE locale = 'ko';
-- UPDATE scom.i18n_resources SET locale = 'en-US' WHERE locale = 'en';
-- COMMIT;
