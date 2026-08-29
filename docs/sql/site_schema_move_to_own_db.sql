-- 소개 사이트 스키마를 전용 DB(jsinisite)로 옮긴다
--
-- ⚠ 이 파일은 **두 DB 에 나눠서** 돌린다. 한 번에 돌아가지 않는다.
--    postgres 접속으로 1단계, jsinisite 접속으로 2단계, funeralv2 접속으로 4단계다.
--
-- 왜 옮기나
--   처음에는 포털과 같은 `funeralv2` 안에 스키마만 나눠 두었다(운영할 DB 를 늘리지 않으려고).
--   옮긴 이유는 **SiteServer 만 로그인하지 않은 사람의 입력을 받기 때문**이다(문의 접수).
--   익명 쓰기가 닿는 표와 업무 표가 같은 DB 에 있으면, 경계가 DB 권한이 아니라
--   코드 안에만 있게 된다. 나눠 두면 그 경계가 물리적으로 생긴다.
--
-- 대가
--   자료실·대표 이미지는 FileServer 의 파일을 가리키는데(scom.filemetadatas)
--   그것이 이제 다른 DB 다. 공지가 첨부 공개 여부를 맞출 때 쓴 방법
--   (같은 DB 라 한 문장 UPDATE — AuthServer/Services/PublicFileSyncService.cs)을
--   소개 사이트에서는 쓸 수 없다. 필요해지면 `PUT /api/file/public/{id}` 를 거쳐야 한다.
--   경계를 나눈 값을 치르는 자리라, 흠이 아니라 의도다.
--
-- 이름을 `jsinisite` 로 한 이유
--   기존 DB 8개가 전부 구분자 없는 한 단어다(funeralv2 · jinrecept · projmng …).
--   하이픈(`jsini-site`)은 SQL 에서 매번 큰따옴표가 필요해 실수를 부른다.

-- ============================================================
-- 1단계 — DB 를 만든다  (postgres 에 접속해서)
-- ============================================================
-- CREATE DATABASE 는 트랜잭션 안에서 돌지 않는다. BEGIN 없이 그대로 실행한다.
--
--   CREATE DATABASE jsinisite WITH ENCODING 'UTF8' TEMPLATE template0;
--   COMMENT ON DATABASE jsinisite IS '회사 소개 사이트(www.jsini.co.kr). SiteServer 가 소유한다';

-- ============================================================
-- 2단계 — 새 DB 에 스키마를 세운다  (jsinisite 에 접속해서)
-- ============================================================
--   \i docs/sql/site_schema.sql

-- ============================================================
-- 3단계 — 자료를 옮긴다
-- ============================================================
-- 표가 다섯이고 외래키가 없어 순서를 가릴 것이 없다.
-- 같은 서버 안이면 postgres_fdw / dblink 없이 아래처럼 덤프-복원이 가장 간단하다.
--
--   pg_dump -h <host> -p <port> -U <user> -d funeralv2 -n site --data-only \
--     | psql -h <host> -p <port> -U <user> -d jsinisite
--
-- (이 저장소에서는 psql 이 없어 psycopg2 로 행을 옮겼다. 결과는 같다.)

-- ============================================================
-- 4단계 — 옛 스키마를 걷어낸다  (funeralv2 에 접속해서)
-- ============================================================
-- ⚠ **3단계까지 확인한 뒤에** 돌린다. 새 DB 에서 화면이 뜨는 것을 보고 나서다.
--    되돌리려면 site_schema.sql 로 표를 다시 만들고 덤프를 복원하면 된다.
--
-- `DROP SCHEMA site CASCADE` 를 쓰지 않는다. 그것은 **무엇이 딸려 있든 말없이 지운다** —
-- 옮기는 도중 누가 표를 하나 더 만들어 두었어도 조용히 사라진다.
--
-- 대신 지울 표를 이름으로 적고, 마지막에 빈 스키마만 RESTRICT 로 지운다.
-- 그러면 목록에 없는 것이 남아 있을 때 **에러로 멈춰서 알려 준다.**
--
-- (`DROP SCHEMA ... RESTRICT` 만으로는 안 된다. RESTRICT 는 남이 참조하는 경우뿐 아니라
--  **그 스키마 자신의 표**에도 걸려서, 비어 있지 않으면 무조건 거절한다.)

BEGIN;

DROP TABLE IF EXISTS site.visits;
DROP TABLE IF EXISTS site.inquiries;
DROP TABLE IF EXISTS site.downloads;
DROP TABLE IF EXISTS site.posts;
DROP TABLE IF EXISTS site.sections;

DROP SCHEMA IF EXISTS site RESTRICT;

COMMIT;

-- 확인
--   (funeralv2)  SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'site';   -- 0
--   (jsinisite)  SELECT table_name FROM information_schema.tables WHERE table_schema = 'site';  -- 5건
