-- ============================================================
-- 개발 도구가 볼 DB 를 TOBE 로 돌린다 (프로젝트관리 6단계)
-- ============================================================
--
-- 대상 DB : projmng (TOBE) / projmng 스키마
-- 배경    : docs/analysis/36-projmng-tobe-feature-cleanup.md 4.7
--
-- `devdbinfo` 는 **개발 도구가 접속할 대상 DB 목록**이다.
-- 서비스 자신의 접속 문자열(ConnectionStrings:jsini)과는 별개다 —
-- [DB 개체 탐색]·[테이블 · 컬럼 설명]·[DB 쿼리 테스터]가 이 표를 보고 붙는다.
--
-- 12행 중 고칠 것은 **한 행**이다.
--   db_rid 4 `jsini` — jsini.co.kr:15432 / jsini / projmng
--                      프로젝트관리 DB 그 자체다(prj_rid 3 = JsiniProjMng).
--                      이관이 끝났으므로 TOBE 를 보게 한다.
--
-- 나머지는 손대지 않는다.
--   db_rid 12 `jmcs` 도 jsini.co.kr:15432 를 가리키지만 **다른 데이터베이스**(jmcs)다.
--                      다른 프로젝트(prj_rid 4 = JinmoonCarSrch) 것이라 이관 대상이 아니다.
--   나머지 10행은 한주·LSMnM·장례 프레임 등 외부 시스템이다.
--
-- ⚠ 이 표에는 접속 비밀번호가 **평문**으로 들어 있다. 원본부터 그랬고 이관과 별개 문제다.
--   여기서도 그 방식을 그대로 따른다 — 서버가 이 컬럼으로 접속 문자열을 만든다
--   (Models/DbInfo.cs 의 postgresqlConstrFormat).
--
-- ⚠ 서버가 이 표를 **메모리에 캐시한다**(AppData.DB_Infos).
--   고친 뒤 캐시를 비워야 반영된다 —
--     POST /api/Sys  {"MainParam":{"req_cname":"dbinfo"}}
--   서비스를 다시 띄워도 된다.
--
-- 반복 실행해도 안전하다.
-- ============================================================

BEGIN;

UPDATE projmng.devdbinfo
   SET db_ip       = 'jin114.co.kr',
       db_port     = '31015',
       db_database = 'projmng',
       db_id       = 'projmng',
       db_pwd      = 'projmng',
       db_schema   = 'projmng',
       -- db_cert 는 접속 문자열 뒤에 그대로 붙는 여분 항목이다. 'Pooling=true' 를 지킨다.
       db_cert     = 'Pooling=true',
       db_comm     = '프로젝트관리 DB (TOBE). 2026-08-29 이관 전에는 jsini.co.kr:15432/jsini 였다.'
 WHERE db_nick = 'jsini'
   AND db_rid  = 4;

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT db_rid, db_nick, db_type, db_ip, db_port, db_database, db_schema, db_id, prj_rid
  FROM projmng.devdbinfo
 ORDER BY db_rid;
