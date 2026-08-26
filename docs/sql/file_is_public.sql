-- 파일에 '익명 열람 허용' 플래그를 추가한다
--
-- 어디에 쓰이나
--   회사 소개 사이트(www.jsini.co.kr)의 공개 자료실. 로그인하지 않은 사람에게도
--   내려줘야 하는 파일만 이 플래그를 켠다.
--   FileServer/Entities/FileMetadata.cs · Endpoints/PublicFileAccessFilter.cs
--   docs/analysis/27-jsini-site-brand.md 5절
--
-- 왜 필요했나
--   게이트웨이의 파일 읽기 라우트(`/api/file/download|thumbnail|medium|large|resize/**`)가
--   `Anonymous` 다. 브라우저가 `<img src>` 로 직접 부르는 경로라 토큰을 붙일 수 없어서
--   그렇게 열어 둔 것인데, 그 결과 **파일 아이디만 알면 누구나** 헬프데스크 첨부까지
--   내려받을 수 있었다. "아무도 아이디를 모른다" 에 의존하는 상태였다.
--
--   이 컬럼이 그 판정의 기준이 된다. 기본값 false 라 이 문장을 돌려도
--   기존 파일은 전부 보호 대상으로 남는다. 공개할 파일만 나중에 켠다.
--
-- 왜 EF 마이그레이션이 아닌가
--   FileServer 는 유일하게 `Database.Migrate()` 를 부르는 서비스지만
--   `.gitignore` 8행이 `Migrations/` 를 제외한다. 마이그레이션을 만들어도 다른 장비로
--   가지 않으므로 그쪽에서는 컬럼이 생기지 않는다. 그래서 다른 여섯 서비스와 같은
--   방식(이 폴더의 SQL)으로 둔다.
--
-- 반복 실행해도 안전하다.

BEGIN;

ALTER TABLE scom.filemetadatas
  ADD COLUMN IF NOT EXISTS ispublic boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN scom.filemetadatas.ispublic IS
  '로그인하지 않은 사람에게도 내려줄 파일인지 여부. 기본은 false 다. 회사 소개 사이트의 공개 자료실처럼 익명 열람이 필요한 파일만 켠다.';

COMMIT;

-- 확인
--   SELECT column_name, data_type, column_default, is_nullable
--     FROM information_schema.columns
--    WHERE table_schema = 'scom' AND table_name = 'filemetadatas' AND column_name = 'ispublic';
--
--   SELECT count(*) FILTER (WHERE ispublic) AS 공개, count(*) AS 전체
--     FROM scom.filemetadatas WHERE NOT isdeleted;
