-- 포털 스키마(scom)를 전용 DB(jsiniportal)로 옮긴다
--
-- 실행일 2026-08-29 · 실행함
--
-- ⚠ 이 파일은 **여러 DB 에 나눠서** 돌린다. 한 번에 돌아가지 않는다.
--    postgres 로 1~2단계, jsiniportal 로 3단계, funeralv2 로 5단계다.
--
-- ⚠ **서비스를 내리고 해야 한다.** 아래 1단계가 `funeralv2` 에 붙어 있는 접속을
--    전부 끊기 때문이다. 소개 사이트를 옮길 때(site_schema_move_to_own_db.sql)는
--    빈 스키마에 행만 복사해서 무중단이었지만, 이번은 다르다.
--
-- ── 왜 옮기나 ─────────────────────────────────────────────
--
-- `funeralv2` 라는 이름이 두 가지를 뜻하고 있었다. 장례식장 시스템(smfr)이면서
-- 동시에 포털 전체가 사는 DB(scom)였다. 장례식장은 포털에 붙은 여러 업무 중
-- 하나일 뿐인데 DB 이름이 그렇게 읽히지 않는다.
--
-- 그래서 이름이 실제를 가리키도록 나눈다. 옮긴 뒤에는 서비스마다 DB 하나다.
--
--   jsiniportal / scom     AuthServer · FileServer · NotificationServer
--   funeralv2   / smfr     funeralv2Api          ← 이제 장례식장만 뜻한다
--   jinrecept   / jsini    HelpDeskServer
--   projmng     / projmng  ProjMngServer
--   jsinisite   / site     SiteServer
--
-- ── 옮기기 전에 확인한 것 ─────────────────────────────────
--
--   · 스키마를 넘는 외래키          0건   (scom ↔ smfr 사이에 아무것도 없다)
--   · scom 안의 뷰 · 함수 · 프로시저 0건   (표만 옮기면 된다)
--   · funeralv2Api 가 scom 을 읽나   아니오 (코드에 `scom` 이 한 번도 안 나온다)
--   · HelpDesk · ProjMng 의 `scom.` 언급  전부 주석이다. 그 DB 에 붙지 않는다.
--
-- 셋 다 아니었기 때문에 통째로 잘라 낼 수 있었다. 하나라도 걸렸으면
-- 그 참조를 API 호출로 바꾸는 일이 먼저였다.
--
-- ── 왜 TEMPLATE 인가 ──────────────────────────────────────
--
-- 이 저장소에는 `pg_dump` 도 `psql` 도 없다. 표 29개의 컬럼 · 기본값 · 인덱스 ·
-- 제약 · 시퀀스 현재값을 손으로 다시 만들면 어딘가 틀린다.
-- `CREATE DATABASE ... TEMPLATE` 는 **DB 를 통째로 그대로 복제**하므로 그럴 일이 없다.
--
-- 대신 대가가 있다. 템플릿으로 쓰는 DB 에는 **다른 접속이 하나도 없어야 한다.**
-- 그래서 서비스를 내리고, 남은 접속(DBeaver 등)도 끊고 시작한다.
--
-- 복제하면 새 DB 에 scom · smfr 이 **둘 다** 들어온다.
-- 그래서 새 쪽에서 smfr 을, 옛 쪽에서 scom 을 각각 걷어낸다. 둘 다 해야 짝이 맞는다.


-- ============================================================
-- 0단계 — 서비스를 내린다  (셸에서)
-- ============================================================
-- funeralv2 에 붙는 넷이다. 게이트웨이 · 프론트는 안 내려도 된다.
--
--   dev.bat stop auth file notify funeral
--   (또는 scripts/dev-stop.ps1 을 포트별로 —
--    auth 5264 · funeral 5320 · file 5350 · notify 5460)
--
-- 내리기 전에 백업을 뜬다. TEMPLATE 복제가 원본을 건드리지는 않지만,
-- 5단계에서 원본의 scom 을 지우기 때문에 그 전 상태가 남아 있어야 한다.


-- ============================================================
-- 1단계 — 남은 접속을 끊는다  (postgres 에 접속해서)
-- ============================================================
-- 여기서 안 끊으면 2단계가
--   ERROR: source database "funeralv2" is being accessed by other users
-- 로 거절한다. 대개는 서비스가 아니라 DB 툴(DBeaver)이 idle 로 잡고 있다.

-- SELECT pid, application_name, state FROM pg_stat_activity WHERE datname = 'funeralv2';

-- SELECT pg_terminate_backend(pid)
--   FROM pg_stat_activity
--  WHERE datname = 'funeralv2' AND pid <> pg_backend_pid();


-- ============================================================
-- 2단계 — DB 를 통째로 복제한다  (postgres 에 접속해서)
-- ============================================================
-- CREATE DATABASE 는 트랜잭션 안에서 돌지 않는다. BEGIN 없이 그대로 실행한다.
-- 12MB 짜리라 0.1초면 끝난다.

-- CREATE DATABASE jsiniportal WITH TEMPLATE funeralv2;

-- COMMENT ON DATABASE jsiniportal IS '업무 포털(jsini-portal). AuthServer · FileServer · NotificationServer 가 scom 스키마를 함께 쓴다';
-- COMMENT ON DATABASE funeralv2   IS '장례식장(funeralv2Api). smfr 스키마만 남는다';


-- ============================================================
-- 3단계 — 새 DB 에서 smfr 을 걷어낸다  (jsiniportal 에 접속해서)
-- ============================================================
-- 복제본에는 장례식장 표 16개가 딸려 왔다. 여기서는 쓰지 않으므로 지운다.
-- 지우기 전에 **원본과 행수가 같은지** 표 29개를 대조했다(전부 일치).
--
-- 표 이름을 일부러 다 적는다. `DROP SCHEMA smfr CASCADE` 는 무엇이 딸려 있든
-- 말없이 지우는데, 여기 적힌 것 말고 뭔가 더 있다면 그건 알고 지나가야 한다.
-- 마지막 `DROP SCHEMA ... RESTRICT` 가 그 몫이다 — 남은 것이 있으면 에러로 멈춘다.

BEGIN;

DROP TABLE IF EXISTS
  smfr."__EFMigrationsHistory", smfr.buildings, smfr.deceased_contractors,
  smfr.deceased_facilities, smfr.deceased_managers, smfr.deceased_mourners,
  smfr.deceased_rooms, smfr.deceaseds, smfr.device_attributes,
  smfr.device_configs, smfr.device_ribbons, smfr.device_text_overlays,
  smfr.devices, smfr.floors, smfr.media_sources, smfr.rooms
CASCADE;

DROP SCHEMA IF EXISTS smfr RESTRICT;

COMMIT;


-- ============================================================
-- 4단계 — 서비스를 새 DB 로 돌린다  (파일 편집 · git 제외 대상)
-- ============================================================
-- appsettings.Local.json 세 곳에서 Database 만 바꾼다. SearchPath=scom 은 그대로다.
--
--   microservices/AuthServer/appsettings.Local.json          jsinicore
--   microservices/FileServer/appsettings.Local.json          jsinifileconn
--   microservices/NotificationServer/appsettings.Local.json  jsinicore
--
--     Database=funeralv2  →  Database=jsiniportal
--
-- funeralv2Api 는 건드리지 않는다. 그대로 funeralv2 / smfr 이다.
--
-- 그리고 올린다:  dev.bat auth file notify funeral


-- ============================================================
-- 5단계 — 옛 DB 에서 scom 을 걷어낸다  (funeralv2 에 접속해서)
-- ============================================================
-- ⚠ **4단계를 확인한 뒤에** 돌린다. 확인은 "떠 있다"로는 부족하다.
--    새 DB 에 **쓰기가 실제로 닿는지**를 봐야 한다. 이번에는 이렇게 확인했다 —
--    로그인을 몇 번 하고 양쪽의 scom.account_login_logs 를 비교했다.
--      funeralv2   120건 (복제 시점에 멈춤)
--      jsiniportal 132건 (새 로그인이 여기에만 쌓임)
--    행수가 갈라지는 것을 본 뒤에 지웠다.
--
-- 여기부터는 되돌리기 어렵다. 되돌리려면 0단계 백업에서 복원해야 한다.

BEGIN;

DROP TABLE IF EXISTS
  scom."__EFMigrationsHistory", scom.account_login_logs,
  scom.account_preferences, scom.account_profile_details, scom.accounts,
  scom.biz_select_configs, scom.common_code_groups, scom.common_codes,
  scom.companies, scom.departments, scom.faqs, scom.filegroups,
  scom.filemetadatas, scom.help_archive_files, scom.help_archives,
  scom.i18n_resources, scom.menu_favorites, scom.notice_files, scom.notices,
  scom.push_subscriptions, scom.qna_posts, scom.release_run_events,
  scom.release_runs, scom.role_accounts, scom.role_companies,
  scom.role_departments, scom.role_menus, scom.roles, scom.system_menus
CASCADE;

DROP SCHEMA IF EXISTS scom RESTRICT;

COMMIT;


-- ============================================================
-- 확인
-- ============================================================
--   (funeralv2)    SELECT schema_name FROM information_schema.schemata
--                   WHERE schema_name NOT LIKE 'pg\_%' AND schema_name <> 'information_schema';
--                  -- public · smfr  (scom 이 없어야 한다)
--
--   (jsiniportal)  SELECT count(*) FROM information_schema.tables WHERE table_schema = 'scom';
--                  -- 29
--
--   셸           bash scripts/smoke-test.sh          -- 31 통과 · 0 실패
--
-- ── 남은 것 ────────────────────────────────────────────────
--
-- FileServer 만 Database.Migrate() 로 표를 만드는데 .gitignore 가 Migrations/ 를
-- 제외한다. 즉 **다른 장비에서는 이 DB 가 저절로 서지 않는다.** 이 파일은 이미
-- 있는 DB 를 옮기는 절차이지, 맨바닥에서 세우는 절차가 아니다.
-- 새 장비에 세우는 방법은 아직 정리되지 않았다.
