-- ============================================================
-- 메일 HTML 템플릿 — projmng.mail_template (2026-10-31)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.ProjMng/Components/Pages/MailTemplateList.razor
--       (/projmng/proj/mail-template — 만들고 고친다)
--       일정표(/projmng/proj/scheduler)의 「메일발송」 창이 이 표에서 골라 쓴다.
-- 메뉴: deploy/sql/projmng-mail-template-menu-2026-10-31.sql
--
-- 본문은 {{timeline}} · {{message}} 같은 치환자를 담은 HTML 이다. 치환자 목록은
-- web/src/Apps/JSini.Web.ProjMng/Api/ScheduleMail.cs 의 ScheduleMailBody.Placeholders.
--
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -f deploy/sql/projmng-mail-template-2026-10-31.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS).

CREATE TABLE IF NOT EXISTS projmng.mail_template (
    tpl_id     bigserial    PRIMARY KEY,
    tpl_kind   varchar(50)  NOT NULL DEFAULT 'SCHEDULE',
    tpl_nm     varchar(200) NOT NULL,
    tpl_desc   varchar(1000),
    subject    varchar(500),
    body_html  text         NOT NULL,
    use_yn     boolean      NOT NULL DEFAULT true,
    sort_no    integer      NOT NULL DEFAULT 0,
    cre_id     varchar(100),
    cre_dt     timestamptz  NOT NULL DEFAULT now(),
    mod_id     varchar(100),
    mod_dt     timestamptz
);

CREATE INDEX IF NOT EXISTS ix_mail_template_kind ON projmng.mail_template (tpl_kind, sort_no);

COMMENT ON TABLE  projmng.mail_template           IS '메일 본문 HTML 템플릿. 일정표 메일발송이 골라 쓴다.';
COMMENT ON COLUMN projmng.mail_template.tpl_kind  IS '쓰는 자리. SCHEDULE = 프로젝트 일정표 메일발송.';
COMMENT ON COLUMN projmng.mail_template.subject   IS '제목 틀. 비우면 화면이 기본 제목을 쓴다. 치환자를 쓸 수 있다.';
COMMENT ON COLUMN projmng.mail_template.body_html IS '본문 HTML. {{timeline}} 등 치환자를 담는다.';
COMMENT ON COLUMN projmng.mail_template.use_yn    IS '거짓이면 메일발송 창의 고르개에 안 나온다.';
