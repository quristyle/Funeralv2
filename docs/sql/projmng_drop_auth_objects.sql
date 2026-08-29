-- ============================================================
-- 자체 인증·사용자·메뉴 객체 제거 (프로젝트관리 5단계)
-- ============================================================
--
-- 대상 DB : projmng (TOBE) / projmng 스키마
-- 배경    : docs/analysis/36-projmng-tobe-feature-cleanup.md 2절
--
-- 인증 · 사용자 · 메뉴는 포털이 단독으로 맡는다. 프로젝트관리가 자기 것으로
-- 들고 있던 표와 프로시저를 걷어낸다. 업무 자료(테이블 14개 · 4,643행)는 그대로다.
--
-- ⚠ 되돌리기 어려운 단계다. 실행 전에 백업이 있어야 한다 —
--   scripts/projmng-db-migration/backup_before_drop.py 가
--   out/backup-step5/{routines,tables,projcommon}.sql 로 뽑아 둔다.
--   ASIS(jsini.co.kr:15432)도 손대지 않은 채 살아 있다.
--
-- 앞 단계가 끝나 있어야 한다. 프론트에서 아래 프로시저를 부르는 곳이 0 이어야 한다
-- (3·4단계에서 확인했다).
--
-- 반복 실행해도 안전하다(IF EXISTS · CREATE OR REPLACE).
-- ============================================================

BEGIN;

-- ── 1. sp_projcommon 에서 dev_user 를 보는 분기를 걷어낸다 ────
--
-- `user` · `family` 두 코드가 dev_user 를 읽는다. 표를 지우면 이 분기만 실행 시점에
-- 터진다(다른 분기는 멀쩡하다) — 부르는 곳이 없더라도 지뢰를 남기지 않는다.
-- 담당자 목록은 2단계에서 포털 계정(portal_account 셀렉트)으로 옮겼다.
--
-- 아래 정의는 **원본에서 그 두 분기만 잘라낸 것**이다. 손으로 옮겨 적지 않았다
-- (인자 이름 하나만 달라도 CREATE OR REPLACE 가 거부한다 — 12번째는 sess_userid 다).

CREATE OR REPLACE PROCEDURE projmng.sp_projcommon(IN ss_user_id character varying, IN p_code_id character varying, IN p_code_nm character varying, IN p_etc0 character varying, IN p_etc1 character varying, IN p_etc2 character varying, IN p_etc3 character varying, IN p_etc4 character varying, IN p_etc5 character varying, IN p_etc6 character varying, IN p_etc7 character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
 v_isExsit int4;
    
	BEGIN





	v_isExsit = ( select count(*) from devcomm where cm_pcd = upper(p_code_id) );
	
	if v_isExsit > 0 then


      open p_cur for
select cm_cd as code
     , cm_nm as name
     , cm_val as desc
     , a.*
  from devcomm a
 where cm_pcd = upper(p_code_id)
 order 
    by cm_srt,cm_nm 
    ;
	



else






	
    if p_code_id = 'projlist' then

      open p_cur for
      select prj_rid as code
           , prj_name as name
           , prj_desc as desc
           , a.*
        from projmng.dev_proj a
       where 1=1
         and ( ( nvl(ss_user_id, '') = '' and 1=1)
             or ( nvl(ss_user_id, '') != '' and prj_rid in ( select prj_rid from dev_proj_user_map where user_id = ss_user_id ) )
             )
       order by prj_srt 
      ; 


    elsif p_code_id = 'sourcelist' then

      open p_cur for
      select src_rid as code
           , src_nick as name
           , src_comm as desc
           , a.*
        from projmng.dev_srcinfo a
       where ( ( nvl(p_etc0, '') = '' and 1=1  )
             or  ( nvl(p_etc0, '') != '' and a.prj_rid = p_etc0::int )
             )

      
      ; 

    elsif p_code_id = 'projdb' then

      open p_cur for
      select db_rid as code
           , db_nick as name
           , db_comm as desc
           , a.*
        from projmng.devdbinfo a
       where ( ( nvl(p_etc0, '') = '' and 1=1  )
             or  ( nvl(p_etc0, '') != '' and a.prj_rid = p_etc0::int )
             )
      ; 

    elsif p_code_id = 'wbsflowlist' then

      open p_cur for
        select proc_tp  as code
             , proc_tp  as name
             , ''  as desc
		  from dev_wbs dw 
		 where nvl(proc_tp, '') <> ''
           and  ( ( nvl(p_etc0, '') = '' and 1=1  )
                or  ( nvl(p_etc0, '') != '' and dw.prj_rid = p_etc0::int )
                )
		 group
		    by proc_tp
     order by proc_tp
      ; 
    elsif p_code_id = 'projdb2' then

      open p_cur for
      select db_nick as code
           , db_nick as name
           , db_comm as desc
           , a.*
        from projmng.devdbinfo a
       where ( ( nvl(p_etc0, '') = '' and 1=1  )
             or  ( nvl(p_etc0, '') != '' and a.prj_rid = p_etc0::int )
             )
      ; 

    -- 'user' · 'family' 분기는 걷어냈다. 사람 목록은 포털 계정이 낸다
    -- (scom.biz_select_configs 의 portal_account · docs/analysis/36 2단계).

    end if;
		
	
    end if;

	END;
	
$procedure$;

-- ── 2. 자체 인증·사용자·메뉴 프로시저 ────────────────────────
-- 인자 목록이 길고 오버로드가 있을 수 있어 이름으로 찾아 지운다.
DO $do$
DECLARE
  r    record;
  gone int := 0;
BEGIN
  FOR r IN
    SELECT p.oid::regprocedure AS sig
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = 'projmng'
       AND p.proname IN (
         'sp_proj_login',              -- 자체 로그인      (AuthServer JWT 가 대신한다)
         'sp_dev_user_exec',           -- 자체 사용자 조회·저장
         'sp_dev_user_exec_all',       -- 자체 사용자 환경설정 일괄 저장
         'sp_dev_user_prop_exec',      -- THEME·FONTSIZE·LASTPAGE… (포털 환경설정)
         'sp_dev_user_grp_exec',       -- 자체 그룹        (포털 roles)
         'sp_dev_user_grp_map_exec',   -- 자체 그룹-사용자  (포털 role_accounts)
         'sp_dev_menu_exec',           -- 자체 메뉴        (포털 system_menus)
         'sp_dev_menu_auth',           -- 자체 메뉴 권한    (포털 role_menus)
         'sp_dev_grp_menu_map_exec',   -- 자체 그룹-메뉴    (포털 role_menus)
         'sp_dev_program_exec'         -- 참조 테이블(dev_program)이 없어 부르면 무조건 실패했다
       )
  LOOP
    EXECUTE 'DROP ROUTINE IF EXISTS ' || r.sig;
    gone := gone + 1;
  END LOOP;
  RAISE NOTICE '루틴 % 개를 지웠다', gone;
END
$do$;

-- ── 3. 자체 인증·사용자·메뉴 테이블 ──────────────────────────
-- dev_menu_favorites → dev_menu 로 FK 가 걸려 있어 자식을 먼저 지운다.
DROP TABLE IF EXISTS projmng.dev_menu_favorites;   -- 즐겨찾기    (포털 menu_favorites)
DROP TABLE IF EXISTS projmng.dev_grp_menu_map;     -- 그룹-메뉴   (포털 role_menus)
DROP TABLE IF EXISTS projmng.dev_menu;             -- 메뉴        (포털 system_menus)
DROP TABLE IF EXISTS projmng.dev_user_grp_map;     -- 그룹-사용자 (포털 role_accounts)
DROP TABLE IF EXISTS projmng.dev_user_grp;         -- 그룹        (포털 roles)
DROP TABLE IF EXISTS projmng.dev_user_prop;        -- 사용자 환경설정 (포털 account_preferences)
DROP TABLE IF EXISTS projmng.dev_user;             -- 사용자      (포털 accounts)

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'projmng' AND c.relkind = 'r'
           AND c.oid NOT IN (SELECT objid FROM pg_depend WHERE deptype = 'e')) AS 테이블,
       (SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'projmng'
           AND p.oid NOT IN (SELECT objid FROM pg_depend WHERE deptype = 'e')) AS 루틴;
