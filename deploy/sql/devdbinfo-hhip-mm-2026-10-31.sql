-- ============================================================================
-- 프로젝트 DB 등록(/projmng/db/list) — 「HHIP_MM」(prj_rid 8) 의 수빅 Oracle 두 스키마 (2026-10-31)
-- ============================================================================
--
-- 테이블 계통도(/projmng/design/table-lineage)는 DB 접속 한 줄 = DB 하나 · 스키마 하나로
-- 도면을 담는다. 옛 WBS 도구(#/tables2)가 스키마마다 도면을 한 장씩 두었으므로
-- (M10 · M20 — 같은 이름의 표가 두 스키마에 다 있다) 접속도 둘로 단다.
--
--   db_nick    DB(서비스)  스키마   계정
--   hhip-m10   ORAHHIP     M10      m10
--   hhip-m20   ORAHHIP     M20      m10   ← M20 전용 계정이 없다. 옛 도구도 M10 으로 붙어 M20 을 읽었다
--
-- 접속 정보는 옛 도구의 api/appsettings.json(OraSession)에서 옮겼다 — 10.103.13.10:1521/ORAHHIP.
-- 사내 폐쇄망 DB 라 이 서버에서는 붙지 않는다. 화면은 붙지 못해도 저장된 도면을 그린다.
--
-- DB 종류 ORACLE 이 공통코드 DB 묶음(projmng.devcomm, cm_pcd='DB')에 없어 함께 더한다 —
-- 종류 고르개가 그 묶음을 읽고, 서버는 글자 그대로 비교한다(지금은 ORACLE 에 등록 질의가 없어
-- 「표 목록」 같은 조회는 지원하지 않는 종류로 돌아온다).
--
-- 비밀번호는 커밋하지 않는다 — psql 변수로 넘긴다.
--   psql -h jin114.co.kr -p 31015 -U projmng -d projmng -v ON_ERROR_STOP=1 \
--        -v pw_hhip=… -f deploy/sql/devdbinfo-hhip-mm-2026-10-31.sql
-- 이미 들어간 별칭은 건너뛰므로 다시 돌려도 된다.
-- ============================================================================

BEGIN;

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT (SELECT COALESCE(MAX(cm_rid), 0) + 1 FROM projmng.devcomm),
       'ORACLE', 'Oracle', 'DB', 'ORACLE', 5
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm WHERE cm_pcd = 'DB' AND cm_cd = 'ORACLE');

INSERT INTO projmng.devdbinfo
     ( db_rid, db_srt, prj_rid, db_nick, db_type, db_ip, db_port,
       db_database, db_schema, db_id, db_pwd, db_cert, db_comm )
SELECT (SELECT COALESCE(MAX(db_rid), 0) FROM projmng.devdbinfo) + v.ord,
       (SELECT COALESCE(MAX(db_srt), 0) FROM projmng.devdbinfo) + v.ord,
       8, v.nick, 'ORACLE', '10.103.13.10', '1521',
       'ORAHHIP', v.sch, 'm10', :'pw_hhip', '', v.comm
  FROM (VALUES
        (1, 'hhip-m10', 'M10', '수빅 자재(MM) 업무 DB · M10 스키마. 사내 폐쇄망이라 이 서버에서는 붙지 않는다. 테이블 계통도 도면(M10)이 이 접속에 있다'),
        (2, 'hhip-m20', 'M20', '수빅 자재(MM) 업무 DB · M20 스키마(M10 계정으로 읽는다). 사내 폐쇄망이라 이 서버에서는 붙지 않는다. 테이블 계통도 도면(M20)이 이 접속에 있다')
       ) AS v(ord, nick, sch, comm)
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devdbinfo d WHERE d.db_nick = v.nick);

COMMIT;

-- 확인
--   SELECT db_rid, db_srt, prj_rid, db_nick, db_type, db_ip, db_port, db_database, db_schema, db_id
--     FROM projmng.devdbinfo WHERE prj_rid = 8;
