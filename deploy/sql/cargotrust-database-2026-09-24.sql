-- CargoTrust 전용 역할 · 데이터베이스 · 스키마 (2026-09-24)
--
-- 헬프데스크v2(helpdesk) · 생활과환경(ghub) 과 같은 관례다 — 서비스 하나에
-- 역할 하나 · DB 하나 · 같은 이름의 스키마 하나. 포털 DB(jsiniportal) 를
-- 건드리지 않으므로 이 서비스가 무엇을 하든 포털 로그인에는 번지지 않는다.
--
-- 실행: **superuser 로** postgres(또는 아무) DB 에 붙어 돌린다.
--       CREATE DATABASE 는 트랜잭션 안에서 못 돌려서 BEGIN 이 없다.
--
-- [비밀번호는 이 파일에 없다]
--
-- 역할을 만든 뒤 따로 정한다. 값은 CargoTrustServer 의 appsettings.Local.json
-- (git 제외)과 운영 서버 /srv/jsini/config/CargoTrustServer/appsettings.Local.json
-- 에만 둔다.
--
--   ALTER ROLE cargotrust PASSWORD '<새 값>';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cargotrust') THEN
        CREATE ROLE cargotrust LOGIN;
    END IF;
END
$$;

-- 이미 있으면 오류가 나고 끝난다(그래도 해가 없다). psql 이면 \gexec 로 감싼다:
--   SELECT 'CREATE DATABASE cargotrust OWNER cargotrust ENCODING ''UTF8'' TEMPLATE template0'
--    WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'cargotrust') \gexec
CREATE DATABASE cargotrust OWNER cargotrust ENCODING 'UTF8' TEMPLATE template0;

-- ↓ 여기부터는 cargotrust DB 에 붙어서 돌린다.

CREATE SCHEMA IF NOT EXISTS cargotrust AUTHORIZATION cargotrust;
ALTER ROLE cargotrust IN DATABASE cargotrust SET search_path TO cargotrust, public;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- 되돌리기 (자료까지 사라진다)
--
--   DROP DATABASE cargotrust;
--   DROP ROLE cargotrust;
