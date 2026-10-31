-- ============================================================
-- 등록 질의 「objectlist」 — 대상 DB 의 표 · 뷰 · 프로시저 · 함수를 한 목록으로 (projmng)
-- ============================================================
--
-- 테이블 계통도(/projmng/design/table-lineage)의 「객체 놓기」가 부른다.
-- 옛 tablelist 는 DB 종류마다 칸 모양이 제각각이고(MYSQL 은 칸 목록이다) 표만 준다.
-- 이것은 종류와 상관없이 같은 세 칸을 준다.
--
--   ObjectType   TABLE · VIEW · MVIEW · FOREIGN · PROCEDURE · FUNCTION
--   ObjectName   이름 (함수 겹이름은 한 줄로 모은다 — 도면의 칸은 이름이 열쇠다)
--   Description  코멘트
--
-- 매개변수: @schema (서버가 접속의 db_schema 로 채운다) · @q (비우면 전부, 있으면 이름·코멘트 부분 일치)
-- 실행기(DevService)가 붙는 종류가 POSTGRESQL · EDB · MSSQL 뿐이라 그 셋만 둔다.
-- 확장(pgcrypto · pg_stat_statements 따위)이 깐 함수·뷰는 뺀다 — 사람이 만든 것이 아니다.
--
-- 두 번 돌려도 안전하다(objectlist 줄을 지우고 다시 넣는다).

BEGIN;

INSERT INTO projmng.devsqlresp_base (dsl_cd, comm, sort)
SELECT 'objectlist', '객체 목록 — 표 · 뷰 · 프로시저 · 함수 (테이블 계통도)', 3
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devsqlresp_base WHERE dsl_cd = 'objectlist');

DELETE FROM projmng.devsqlresp WHERE dsl_cd = 'objectlist';

INSERT INTO projmng.devsqlresp (dsl_id, dsl_type, dsl_cd, dsl_query, comm)
SELECT COALESCE(MAX(dsl_id), 0) + v.n, v.t, 'objectlist', v.q, '객체 목록 — 표 · 뷰 · 프로시저 · 함수'
  FROM projmng.devsqlresp,
       (VALUES
         (1, 'POSTGRESQL', $q$
SELECT o.ObjectType, o.ObjectName, o.Description
  FROM (
        SELECT CASE c.relkind WHEN 'r' THEN 'TABLE' WHEN 'p' THEN 'TABLE'
                              WHEN 'v' THEN 'VIEW'  WHEN 'm' THEN 'MVIEW'
                              ELSE 'FOREIGN' END     AS ObjectType
             , c.relname                             AS ObjectName
             , obj_description(c.oid, 'pg_class')    AS Description
          FROM pg_catalog.pg_class c
          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = @schema
           AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
           AND NOT c.relispartition
           AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_depend d
                            WHERE d.classid = 'pg_catalog.pg_class'::regclass
                              AND d.objid = c.oid AND d.deptype = 'e')
        UNION ALL
        SELECT CASE WHEN bool_or(p.prokind = 'p') THEN 'PROCEDURE' ELSE 'FUNCTION' END
             , p.proname
             , max(obj_description(p.oid, 'pg_proc'))
          FROM pg_catalog.pg_proc p
          JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = @schema
           AND p.prokind IN ('f', 'p')
           AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_depend d
                            WHERE d.classid = 'pg_catalog.pg_proc'::regclass
                              AND d.objid = p.oid AND d.deptype = 'e')
         GROUP BY p.proname
       ) o
 WHERE (@q = '' OR o.ObjectName ILIKE '%' || @q || '%' OR COALESCE(o.Description, '') ILIKE '%' || @q || '%')
 ORDER BY CASE o.ObjectType WHEN 'TABLE' THEN 1 WHEN 'VIEW' THEN 2 WHEN 'MVIEW' THEN 3
                            WHEN 'FOREIGN' THEN 4 WHEN 'PROCEDURE' THEN 5 ELSE 6 END
        , o.ObjectName
$q$),
         (2, 'EDB', $q$
SELECT o.ObjectType, o.ObjectName, o.Description
  FROM (
        SELECT CASE c.relkind WHEN 'r' THEN 'TABLE' WHEN 'p' THEN 'TABLE'
                              WHEN 'v' THEN 'VIEW'  WHEN 'm' THEN 'MVIEW'
                              ELSE 'FOREIGN' END     AS ObjectType
             , c.relname                             AS ObjectName
             , obj_description(c.oid, 'pg_class')    AS Description
          FROM pg_catalog.pg_class c
          JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = @schema
           AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
        UNION ALL
        SELECT CASE WHEN bool_or(p.prokind = 'p') THEN 'PROCEDURE' ELSE 'FUNCTION' END
             , p.proname
             , max(obj_description(p.oid, 'pg_proc'))
          FROM pg_catalog.pg_proc p
          JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = @schema
           AND p.prokind IN ('f', 'p')
         GROUP BY p.proname
       ) o
 WHERE (@q = '' OR o.ObjectName ILIKE '%' || @q || '%' OR COALESCE(o.Description, '') ILIKE '%' || @q || '%')
 ORDER BY CASE o.ObjectType WHEN 'TABLE' THEN 1 WHEN 'VIEW' THEN 2 WHEN 'MVIEW' THEN 3
                            WHEN 'FOREIGN' THEN 4 WHEN 'PROCEDURE' THEN 5 ELSE 6 END
        , o.ObjectName
$q$),
         (3, 'MSSQL', $q$
SELECT CASE o.type WHEN 'U' THEN 'TABLE' WHEN 'V' THEN 'VIEW' WHEN 'P' THEN 'PROCEDURE'
                   ELSE 'FUNCTION' END                AS ObjectType
     , o.name                                         AS ObjectName
     , CAST(ep.value AS nvarchar(4000))               AS Description
  FROM sys.objects o
  JOIN sys.schemas s ON s.schema_id = o.schema_id
  LEFT JOIN sys.extended_properties ep
         ON ep.class = 1 AND ep.major_id = o.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
 WHERE s.name = @schema
   AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
   AND o.is_ms_shipped = 0
   AND (@q = '' OR o.name LIKE '%' + @q + '%' OR CAST(ep.value AS nvarchar(4000)) LIKE '%' + @q + '%')
 ORDER BY CASE o.type WHEN 'U' THEN 1 WHEN 'V' THEN 2 WHEN 'P' THEN 5 ELSE 6 END, o.name
$q$)
       ) AS v(n, t, q)
 GROUP BY v.n, v.t, v.q;

COMMIT;

-- 확인
SELECT dsl_id, dsl_type, dsl_cd, length(dsl_query) AS len
  FROM projmng.devsqlresp WHERE dsl_cd = 'objectlist' ORDER BY dsl_type;
