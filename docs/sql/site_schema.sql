-- 회사 소개 사이트(www.jsini.co.kr) 의 스키마를 만든다
--
-- 어디에 쓰이나
--   microservices/SiteServer (:5480) — 공개 조회 · 문의 접수 · 관리
--   docs/analysis/27-jsini-site-brand.md 3절
--
-- 왜 별도 스키마인가
--   포털과 같은 funeralv2 인스턴스를 쓰되 스키마만 나눈다. 별도 인스턴스까지 가면
--   운영할 것이 하나 더 늘고, 스키마를 나누면 공개 사이트가 쓰는 표와 업무 표가 섞이지 않는다.
--
-- 왜 EF 마이그레이션이 아닌가
--   .gitignore 8행이 `Migrations/` 를 제외한다. 마이그레이션을 만들어도 다른 장비로 가지 않는다.
--   FileServer 만 Database.Migrate() 를 쓰는데 그래서 장비마다 스키마가 어긋난다
--   (별도 작업으로 정리 중). SiteServer 는 처음부터 이 방식으로 둔다.
--
-- 언어는 컬럼이 아니라 행으로 나눈다
--   title_ko · title_en 처럼 컬럼을 늘리면 언어가 셋이 되는 순간 컬럼이 배로 늘어난다.
--   그래서 (열쇠, locale) 을 유일하게 두고 언어마다 한 행을 둔다. 결정 D-S5.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE SCHEMA IF NOT EXISTS site;

-- ── 문구 블록 ────────────────────────────────────────────────
-- 화면의 한 덩어리(히어로 · 사업영역 · 연혁 ...)가 한 행이다.
-- 문구는 표가 아니라 글이라, 컬럼을 잘게 나누지 않고 본문에 맡긴다.
CREATE TABLE IF NOT EXISTS site.sections (
  id           uuid         PRIMARY KEY,
  sectionkey   text         NOT NULL,
  locale       text         NOT NULL DEFAULT 'ko',
  title        text         NOT NULL DEFAULT '',
  subtitle     text,
  body         text,
  sortorder    integer      NOT NULL DEFAULT 0,
  ispublished  boolean      NOT NULL DEFAULT false,
  createdat    timestamptz  NOT NULL DEFAULT now(),
  createdby    text,
  updatedat    timestamptz,
  updatedby    text,
  isdeleted    boolean      NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_site_sections_key_locale
  ON site.sections (sectionkey, locale);

-- ── 뉴스 · 보도자료 ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS site.posts (
  id           uuid         PRIMARY KEY,
  slug         text         NOT NULL,
  locale       text         NOT NULL DEFAULT 'ko',
  title        text         NOT NULL DEFAULT '',
  summary      text,
  body         text,
  coverfileid  uuid,
  publishedat  timestamptz,
  ispublished  boolean      NOT NULL DEFAULT false,
  createdat    timestamptz  NOT NULL DEFAULT now(),
  createdby    text,
  updatedat    timestamptz,
  updatedby    text,
  isdeleted    boolean      NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_site_posts_slug_locale
  ON site.posts (slug, locale);

CREATE INDEX IF NOT EXISTS ix_site_posts_list
  ON site.posts (locale, ispublished, publishedat);

-- ── 공개 자료실 ──────────────────────────────────────────────
-- 파일 자체는 FileServer 가 보관하고 여기에는 아이디만 둔다.
-- 그 파일의 scom.filemetadatas.ispublic 을 켜 두어야 익명 방문자에게 나간다.
CREATE TABLE IF NOT EXISTS site.downloads (
  id             uuid         PRIMARY KEY,
  locale         text         NOT NULL DEFAULT 'ko',
  title          text         NOT NULL DEFAULT '',
  description    text,
  category       text,
  fileid         uuid         NOT NULL,
  filename       text,
  filesize       bigint,
  downloadcount  integer      NOT NULL DEFAULT 0,
  sortorder      integer      NOT NULL DEFAULT 0,
  ispublished    boolean      NOT NULL DEFAULT false,
  createdat      timestamptz  NOT NULL DEFAULT now(),
  createdby      text,
  updatedat      timestamptz,
  updatedby      text,
  isdeleted      boolean      NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS ix_site_downloads_list
  ON site.downloads (locale, ispublished, sortorder);

-- ── 문의 ────────────────────────────────────────────────────
-- 로그인하지 않은 사람이 쓰는 유일한 표다. 개인정보가 들어오므로
-- consentedat 이 비어 있는 행은 만들지 않는다 (서비스가 막는다).
-- 보관 기간과 동의 문구는 회사가 확정해야 한다 — 결정 D-S7.
CREATE TABLE IF NOT EXISTS site.inquiries (
  id            uuid         PRIMARY KEY,
  name          text         NOT NULL DEFAULT '',
  company       text,
  email         text         NOT NULL DEFAULT '',
  phone         text,
  category      text,
  subject       text         NOT NULL DEFAULT '',
  message       text         NOT NULL DEFAULT '',
  consentedat   timestamptz  NOT NULL,
  clientip      text,
  useragent     text,
  locale        text         NOT NULL DEFAULT 'ko',
  status        text         NOT NULL DEFAULT 'new',
  internalnote  text,
  createdat     timestamptz  NOT NULL DEFAULT now(),
  createdby     text,
  updatedat     timestamptz,
  updatedby     text,
  isdeleted     boolean      NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS ix_site_inquiries_status
  ON site.inquiries (status, createdat DESC);

-- ── 조회 집계 ────────────────────────────────────────────────
-- 방문자 한 명 한 명을 남기지 않는다. 개인을 특정할 값을 쌓지 않으려는 것이고,
-- 알고 싶은 것은 "어느 페이지가 읽히는가" 뿐이다.
CREATE TABLE IF NOT EXISTS site.visits (
  id          uuid         PRIMARY KEY,
  visitdate   date         NOT NULL,
  path        text         NOT NULL,
  locale      text         NOT NULL DEFAULT 'ko',
  viewcount   integer      NOT NULL DEFAULT 0,
  createdat   timestamptz  NOT NULL DEFAULT now(),
  createdby   text,
  updatedat   timestamptz,
  updatedby   text,
  isdeleted   boolean      NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_site_visits_day
  ON site.visits (visitdate, path, locale);

-- ── 표·컬럼 설명 ─────────────────────────────────────────────
COMMENT ON SCHEMA site IS '회사 소개 사이트(www.jsini.co.kr). SiteServer 가 소유한다';

COMMENT ON TABLE site.sections  IS '소개 사이트의 문구 블록. 화면의 한 덩어리가 한 행이다';
COMMENT ON TABLE site.posts     IS '뉴스 · 보도자료';
COMMENT ON TABLE site.downloads IS '공개 자료실. 파일 자체는 FileServer 가 보관한다';
COMMENT ON TABLE site.inquiries IS '소개 사이트에서 남긴 문의. 익명 쓰기가 들어오는 유일한 표다';
COMMENT ON TABLE site.visits    IS '날짜 · 경로 · 언어별 조회 수. 개인을 특정할 값은 쌓지 않는다';

COMMENT ON COLUMN site.sections.sectionkey  IS '어느 화면의 어느 자리인지 (예: home.hero). locale 과 함께 유일하다';
COMMENT ON COLUMN site.sections.body        IS '본문. 마크다운';
COMMENT ON COLUMN site.posts.slug           IS '주소에 쓰는 열쇠. locale 과 함께 유일하다';
COMMENT ON COLUMN site.posts.coverfileid    IS 'FileServer 파일 아이디. 그 파일의 ispublic 을 켜 두어야 익명에게 나간다';
COMMENT ON COLUMN site.downloads.fileid     IS 'FileServer 파일 아이디. 그 파일의 ispublic 을 켜 두어야 익명에게 나간다';
COMMENT ON COLUMN site.inquiries.consentedat IS '개인정보 수집·이용 동의 시각. 비어 있을 수 없다';
COMMENT ON COLUMN site.inquiries.clientip   IS '접수 당시 클라이언트 아이피. 스팸 추적용';
COMMENT ON COLUMN site.inquiries.status     IS 'new · reading · answered · spam';

COMMIT;

-- 확인
--   SELECT table_name FROM information_schema.tables WHERE table_schema = 'site' ORDER BY 1;
--   SELECT indexname FROM pg_indexes WHERE schemaname = 'site' ORDER BY 1;
