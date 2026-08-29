-- 계정 환경설정에서 '파생값' 을 걷어낸다
--
-- 어디에 쓰이나
--   scom.account_preferences — 화면 환경설정을 계정에 붙여 두는 표.
--   fronts/apps/jsini-portal/src/store/preferences-sync.ts 가 읽고 쓴다.
--
-- 무엇이 문제였나
--   `app.isMobile` 은 사람이 고른 설정이 아니라 **창 너비에서 파생되는 런타임 값**이다.
--   그런데 `preferences.app` 안에 진짜 설정들과 같이 살아서, '기본값과 다른 값' 을
--   모아 저장할 때 함께 올라갔다.
--
--   좁은 창에서 한 번 열면 `{"app":{"isMobile":true}}` 가 계정에 박히고,
--   그 뒤로는 **넓은 화면에서 로그인해도 앱이 모바일로 동작했다.**
--   모바일이면 사이드바가 상시 표시가 아니라 오버레이 서랍이 되므로,
--   사이드바 안에 있는 **메뉴 검색칸이 아예 그려지지 않았다.**
--   "메뉴 검색 필터가 동작하지 않는다" 의 실제 원인이 이것이다.
--
--   코드 쪽은 preferences-sync.ts 의 `DERIVED_KEYS` 로 막았다(저장·적용 양쪽).
--   이 문장은 **이미 저장된 값**을 치운다.
--
-- 같은 종류가 하나 더 있다 — `logo.source` · `logo.sourceDark`
--   이것도 사람이 고르는 값이 아니라 **앱의 브랜딩**이다. 계정에 박히면 나중에 로고를
--   바꿔도 옛 주소가 따라와 깨진다. 실제로 겪었다 — 로고 기본값을 브랜드 것으로 바꾸자,
--   옛 경로(`/jsini.svg`)를 들고 있던 브라우저가 그것을 '기본값과 다른 값' 으로 보고
--   서버에 올렸고 그 파일은 이미 지운 뒤였다.
--   (`logo.enable` · `logo.showText` 는 사람이 끄고 켜는 값이라 그대로 둔다)
--
-- 무엇을 지우나
--   `app.isMobile` · `logo.source` · `logo.sourceDark` 만 걷어낸다. 다른 설정은 건드리지 않는다.
--   걷어낸 뒤 그 섹션이 비면 섹션도 지우고, payload 자체가 비면 행을 지운다
--   (빈 껍데기를 남기면 "저장된 설정이 있다" 로 잘못 읽힌다).
--
-- 반복 실행해도 안전하다 (지울 것이 없으면 0건).

BEGIN;

-- 1) 계정에 붙이면 안 되는 값들을 걷어낸다
UPDATE scom.account_preferences
   SET payload    = payload #- '{app,isMobile}'
                            #- '{logo,source}'
                            #- '{logo,sourceDark}',
       updated_at = now(),
       updated_by = 'StripDerived'
 WHERE payload #> '{app,isMobile}'    IS NOT NULL
    OR payload #> '{logo,source}'     IS NOT NULL
    OR payload #> '{logo,sourceDark}' IS NOT NULL;

-- 2) 그 결과 빈 객체만 남은 섹션도 걷어낸다
UPDATE scom.account_preferences
   SET payload    = payload - 'app',
       updated_at = now(),
       updated_by = 'StripDerived'
 WHERE payload -> 'app' = '{}'::jsonb;

UPDATE scom.account_preferences
   SET payload    = payload - 'logo',
       updated_at = now(),
       updated_by = 'StripDerived'
 WHERE payload -> 'logo' = '{}'::jsonb;

-- 3) payload 자체가 비었으면 행을 지운다
DELETE FROM scom.account_preferences
 WHERE payload = '{}'::jsonb;

COMMIT;

-- 확인
--   SELECT account_id, payload FROM scom.account_preferences WHERE NOT is_deleted;
--   -- 어느 행에도 app.isMobile 이 없어야 한다
--   SELECT count(*) FROM scom.account_preferences
--    WHERE payload #> '{app,isMobile}' IS NOT NULL
--       OR payload #> '{logo,source}' IS NOT NULL;
