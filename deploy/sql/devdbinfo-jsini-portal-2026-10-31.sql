-- ============================================================================
-- 프로젝트 DB 등록(/projmng/db/list) — 「jsini통합포탈」에 MSA 가 쓰는 DB 전부 (2026-10-31)
-- ============================================================================
--
-- 이 프로젝트(prj_rid 1)에는 jsini-comm(jsiniportal.scom) 하나만 있었다.
-- 서비스가 실제로 붙는 DB 는 일곱이다 — 접속 이름은 각 서비스의
-- GetConnectionString(...) 이고, DB·스키마는 운영 서버에서 표가 있는 곳으로 대조했다.
--
--   ConnectionStrings      서비스                                  DB.스키마
--   jsinicore              AuthServer·NotificationServer·AIAgent   jsiniportal.scom   (jsini-comm, 있던 줄)
--   jsinifileconn          FileServer                              jsiniportal.scom   (jsini-comm, 있던 줄)
--   funeralv2              funeralv2Api                            funeralv2.smfr
--   helpdesk               HelpDeskServer                          helpdesk.helpdesk
--   jsini                  ProjMngServer                           projmng.projmng
--   jsinisiteconn          SiteServer                              jsinisite.site
--   jsinilifeenvconn       LifeEnvServer                           ghub.ghub
--   cargotrust             CargoTrustServer                        cargotrust.cargotrust
--
-- ApiGateway · McpServer 는 DB 가 없다. 기존 `jsini`(projmng) 줄은 개발 도구가
-- 별칭으로 찾고 있어 「JsiniProjMng」에 그대로 두고, 이 프로젝트에는 따로 단다.
-- cargotrust 계정은 비밀번호를 모르므로 표를 읽을 수 있는 funeralv2 로 단다.
--
-- 비밀번호는 커밋하지 않는다 — psql 변수로 넘긴다.
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -v ON_ERROR_STOP=1 \
--        -v pw_funeralv2=… -v pw_projmng=… -v pw_helpdesk=… -v pw_ghub=… \
--        -f deploy/sql/devdbinfo-jsini-portal-2026-10-31.sql
-- 이미 들어간 별칭은 건너뛰므로 다시 돌려도 된다.
-- ============================================================================

BEGIN;

-- 새 줄을 이 프로젝트 바로 뒤(차례 3~8)에 끼우려고 뒤 줄을 민다. 한 번만.
UPDATE projmng.devdbinfo
   SET db_srt = db_srt + 6
 WHERE db_srt >= 3
   AND NOT EXISTS (SELECT 1 FROM projmng.devdbinfo WHERE db_nick = 'jsini-funeral');

UPDATE projmng.devdbinfo
   SET db_comm = '포털 공용 DB. AuthServer·NotificationServer·AIAgentServer(ConnectionStrings:jsinicore), FileServer(jsinifileconn)'
 WHERE db_nick = 'jsini-comm';

INSERT INTO projmng.devdbinfo
     ( db_rid, db_srt, prj_rid, db_nick, db_type, db_ip, db_port,
       db_database, db_schema, db_id, db_pwd, db_cert, db_comm )
SELECT (SELECT COALESCE(MAX(db_rid), 0) FROM projmng.devdbinfo) + v.ord,
       v.srt, 1, v.nick, 'POSTGRESQL', 'jin114.co.kr', '31015',
       v.db, v.sch, v.uid, v.pwd, 'Pooling=true', v.comm
  FROM (VALUES
        (1, 3, 'jsini-funeral',  'funeralv2',  'smfr',       'funeralv2', :'pw_funeralv2',
         '장례식장 핵심. funeralv2Api(ConnectionStrings:funeralv2)'),
        (2, 4, 'jsini-helpdesk', 'helpdesk',   'helpdesk',   'helpdesk',  :'pw_helpdesk',
         '헬프데스크. HelpDeskServer(ConnectionStrings:helpdesk)'),
        (3, 5, 'jsini-projmng',  'projmng',    'projmng',    'projmng',   :'pw_projmng',
         '프로젝트관리. ProjMngServer(ConnectionStrings:jsini) — 개발 도구용 별칭 jsini 와 같은 DB'),
        (4, 6, 'jsini-site',     'jsinisite',  'site',       'funeralv2', :'pw_funeralv2',
         '회사 소개 사이트. SiteServer(ConnectionStrings:jsinisiteconn)'),
        (5, 7, 'jsini-lifeenv',  'ghub',       'ghub',       'ghub',      :'pw_ghub',
         '생활과환경(기상·생일). LifeEnvServer(ConnectionStrings:jsinilifeenvconn)'),
        (6, 8, 'jsini-cargo',    'cargotrust', 'cargotrust', 'funeralv2', :'pw_funeralv2',
         '운송관리. CargoTrustServer(ConnectionStrings:cargotrust) — 표 주인은 cargotrust 계정')
       ) AS v(ord, srt, nick, db, sch, uid, pwd, comm)
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devdbinfo d WHERE d.db_nick = v.nick);

COMMIT;
