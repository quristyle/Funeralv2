-- ASIS jsini.co.kr:15432/jsini 의 projmng 스키마 구조
-- dump_asis.py 가 만든 것이다. 손으로 고치지 말고 다시 뽑는다.

SET client_min_messages = warning;
SET check_function_bodies = off;

CREATE SCHEMA IF NOT EXISTS "projmng";
SET search_path = "projmng", public;

-- ===== 테이블 21개 =====

CREATE TABLE IF NOT EXISTS projmng."dev_activityinfo" (
  "servicename" character varying,
  "transitionname" character varying,
  "transitionvalue" character varying,
  "dao" character varying,
  "procedurename" character varying,
  "resultkey" character varying,
  "activity" character varying,
  "src_rid" character varying,
  "activity_type" character varying,
  "active_context" text
);

CREATE TABLE IF NOT EXISTS projmng."dev_db_prop" (
  "db_rid" integer NOT NULL,
  "db_prid" integer,
  "db_pkey" character varying,
  "db_pvalue" text,
  "db_pcomment" character varying,
  "db_ptype" character varying,
  "cre_dt" timestamp without time zone,
  "mod_dt" timestamp without time zone
);

CREATE TABLE IF NOT EXISTS projmng."dev_excel" (
  "xls_id" integer NOT NULL,
  "ttl" character varying(1000),
  "cont" character varying
);

CREATE TABLE IF NOT EXISTS projmng."dev_grp_menu_map" (
  "grp_id" character varying(30) NOT NULL,
  "menu_id" character varying(30) NOT NULL,
  "remark" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_menu" (
  "cre_id" character varying(20),
  "cre_dt" timestamp(6) without time zone,
  "mod_id" character varying(20),
  "mod_dt" timestamp(6) without time zone,
  "owner_id" character varying(15),
  "mnu_cd" character varying(50),
  "mnu_nm" character varying(100),
  "disp_seq" numeric(3,0),
  "parent_mnu_cd" character varying(10),
  "mnu_grp_yn" character varying(1),
  "pgm_id" character varying(300),
  "mnu_id" character varying(10) NOT NULL,
  "use_yn" character varying(1) DEFAULT 'Y'::character varying,
  "pgm_ty" character varying(1),
  "mnu_url" character varying(1000),
  "mnu_desc" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_menu_favorites" (
  "cre_id" character varying(20),
  "cre_dt" timestamp(6) without time zone,
  "mod_id" character varying(20),
  "mod_dt" timestamp(6) without time zone,
  "user_id" character varying(20) NOT NULL,
  "mnu_id" character varying(10) NOT NULL,
  "disp_seq" numeric(3,0),
  "group_yn" character varying(1),
  "group_nm" character varying(50),
  "parent_id" character varying(10)
);

CREATE TABLE IF NOT EXISTS projmng."dev_proj" (
  "prj_rid" integer,
  "prj_name" character varying(2000),
  "prj_desc" character varying(2000),
  "prj_sdt" date,
  "prj_edt" date,
  "prj_nick" character varying(2000),
  "prj_type" character varying(2000),
  "proj_pay" integer,
  "prj_use_pay" integer,
  "mod_dt" timestamp without time zone,
  "cre_dt" timestamp without time zone,
  "prj_srt" integer DEFAULT 99999
);

CREATE TABLE IF NOT EXISTS projmng."dev_proj_prop" (
  "prj_rid" character varying,
  "prop_cd" character varying,
  "prop_val" character varying,
  "prop_comm" character varying,
  "prop_use_yn" character varying,
  "prop_type" character varying
);

CREATE TABLE IF NOT EXISTS projmng."dev_proj_user_map" (
  "prj_rid" integer,
  "user_id" character varying(30) NOT NULL
);

CREATE TABLE IF NOT EXISTS projmng."dev_srcinfo" (
  "src_rid" integer NOT NULL,
  "src_os" character varying,
  "src_path" character varying,
  "src_nick" character varying,
  "src_type" character varying,
  "src_lang" character varying,
  "src_comm" character varying,
  "prj_rid" integer,
  "src_ui_root" character varying,
  "prj_namespace" character varying
);

CREATE TABLE IF NOT EXISTS projmng."dev_srcinfo_dtl" (
  "src_dtl_rid" integer NOT NULL,
  "src_extend" character varying,
  "src_pattern_grp" character varying,
  "url_pattern" text,
  "src_pattern_comment" character varying,
  "src_pattern_nullvalue" character varying,
  "src_rid" integer
);

CREATE TABLE IF NOT EXISTS projmng."dev_user" (
  "user_id" character varying(30) NOT NULL,
  "upwd" character varying(512),
  "role_grp_id" character varying(50),
  "user_name" character varying(100),
  "user_name_eng" character varying(100),
  "emp_no" character varying(20),
  "cust_code" character varying(10),
  "dept_code" character varying(10),
  "office_num" character varying(20),
  "phone_num" character varying(20),
  "address_1" character varying(400),
  "address_2" character varying(400),
  "country" character varying(200),
  "use_yn" character varying(1),
  "remark" character varying(2000),
  "reg_id" character varying(30),
  "reg_dt" date,
  "upt_id" character varying(30),
  "upt_dt" date,
  "user_photo" character varying(2000),
  "email" character varying(1000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_user_grp" (
  "grp_id" character varying(30) NOT NULL,
  "grp_name" character varying(100),
  "remark" character varying(2000),
  "role_id" character varying(50),
  "use_yn" character varying(1),
  "cre_id" character varying(30),
  "cre_dt" date,
  "mod_id" character varying(30),
  "mod_dt" date,
  "grp_photo" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_user_grp_map" (
  "grp_id" character varying(30) NOT NULL,
  "user_id" character varying(30) NOT NULL,
  "remark" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_user_prop" (
  "user_id" character varying(30) NOT NULL,
  "prop_type" character varying(1000),
  "prop_val" character varying(2000),
  "cre_id" character varying(100),
  "cre_dt" timestamp without time zone,
  "mod_id" character varying(100),
  "mod_dt" timestamp without time zone,
  "cre_user" character varying(100),
  "mod_user" character varying(100),
  "prop_val2" character varying(2000),
  "prop_val3" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."dev_wbs" (
  "prj_rid" integer NOT NULL,
  "wbs_id" integer NOT NULL,
  "proc_id" character varying(1000),
  "gb1" character varying(1000),
  "gb2" character varying(1000),
  "proc_nm" character varying(1000),
  "proc_tp" character varying(1000),
  "proc_lvl" character varying(1000),
  "build_user" character varying(1000),
  "build_status" character varying(1000),
  "dev_user" character varying(1000) NOT NULL,
  "plan_sdt" date,
  "plan_edt" date,
  "dev_sdt" date,
  "dev_edt" date,
  "dev_chk" character varying(1000),
  "build_chk" character varying(1000),
  "build_chk_dt" date,
  "qc_user" character varying(1000),
  "qc_chk" character varying(1000),
  "qc_chk_dt" date,
  "cre_user" character varying(1000),
  "cre_dt" date,
  "mod_user" character varying(1000),
  "mod_dt" date,
  "comm" character varying(1000),
  "schedule_type" character varying(100) DEFAULT 'WBS'::character varying NOT NULL,
  "column1" character varying(50)
);

CREATE TABLE IF NOT EXISTS projmng."devcomm" (
  "cm_rid" integer,
  "cm_cd" character varying,
  "cm_nm" character varying,
  "cm_prop" character varying,
  "cm_pcd" character varying,
  "cm_val" character varying,
  "cm_type" character varying,
  "cm_val2" character varying,
  "cm_val3" character varying,
  "cm_srt" integer,
  "cm_rmk" character varying(2000)
);

CREATE TABLE IF NOT EXISTS projmng."devdbinfo" (
  "db_rid" integer NOT NULL,
  "db_ip" character varying,
  "db_port" character varying,
  "db_database" character varying,
  "db_id" character varying,
  "db_pwd" character varying,
  "db_cert" character varying,
  "db_comm" character varying,
  "db_nick" character varying,
  "db_type" character varying,
  "db_schema" character varying,
  "prj_rid" integer
);

CREATE TABLE IF NOT EXISTS projmng."devsqlresp" (
  "dsl_id" bigint,
  "dsl_type" character varying,
  "dsl_cd" character varying,
  "dsl_query" text,
  "comm" character varying
);

CREATE TABLE IF NOT EXISTS projmng."devsqlresp_base" (
  "dsl_cd" character varying,
  "comm" character varying,
  "sort" bigint
);

CREATE TABLE IF NOT EXISTS projmng."home_todo" (
  "todo_key" bigint,
  "title" character varying,
  "is_complete" boolean,
  "cre_dt" timestamp without time zone,
  "comp_dt" timestamp without time zone,
  "cre_id" character varying,
  "comp_id" character varying,
  "mod_dt" timestamp without time zone,
  "mod_id" character varying,
  "comments" character varying,
  "target_day" timestamp without time zone,
  "target_user" character varying,
  "fix_point" bigint,
  "todo_state" character varying DEFAULT 'R'::character varying
);

-- ===== 함수·프로시저 40개 =====
-- nvl 처럼 이름이 같고 인자가 다른 것이 있어 인자까지 적어 둔다.

-- sp_dev_activityinfo_exec(IN p_srch character varying, IN p_src_rid character varying, IN p_servicename character varying, IN p_transitionname character varying, IN p_transitionvalue character varying, IN p_dao character varying, IN p_procedurename character varying, IN p_resultkey character varying, IN p_activity character varying, IN p_activity_type character varying, IN p_active_context character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_activityinfo_exec(IN p_srch character varying, IN p_src_rid character varying, IN p_servicename character varying, IN p_transitionname character varying, IN p_transitionvalue character varying, IN p_dao character varying, IN p_procedurename character varying, IN p_resultkey character varying, IN p_activity character varying, IN p_activity_type character varying, IN p_active_context character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then

        -- 먼저 UPDATE 시도
        UPDATE projmng.dev_activityinfo
           set transitionvalue = p_transitionvalue -- 
             , dao = p_dao -- 
             , procedurename = p_procedurename -- 
             , resultkey = p_resultkey -- 
             , activity = p_activity -- 
             , activity_type = p_activity_type
             , active_context = p_active_context
         WHERE servicename = p_servicename -- 
           and transitionname = p_transitionname
           and src_rid = p_src_rid
             ;

        -- 변경된 행이 없으면 INSERT
        IF NOT FOUND THEN
            
          insert into projmng.dev_activityinfo
          ( servicename -- 
          ,transitionname -- 
          ,transitionvalue -- 
          ,dao -- 
          ,procedurename -- 
          ,resultkey -- 
          ,activity --  
          , activity_type
          , active_context
          , src_rid
            )
          values
          ( p_servicename -- 
          , p_transitionname -- 
          , p_transitionvalue -- 
          , p_dao -- 
          , p_procedurename -- 
          , p_resultkey -- 
          , p_activity --  
          , p_activity_type
          , p_active_context
          , p_src_rid
            )
          ;

        END IF;


    else 

      open p_cur for

      select servicename -- 
           , transitionname -- 
           , transitionvalue -- 
           , dao -- 
           , procedurename -- 
           , resultkey -- 
           , activity --
           , activity_type 
           , active_context
           , src_rid
        from projmng.dev_activityinfo a
       where 1=1
         and (  ( nvl(p_src_rid, '') = '' and 1=1  )
             or ( nvl(p_src_rid, '') != '' and a.src_rid = p_src_rid )
             )

      ;

    end if;

  END;

$procedure$;

-- sp_dev_db_prop_exec(IN p_db_rid character varying, IN p_db_prid character varying, IN p_db_pkey character varying, IN p_db_pvalue character varying, IN p_db_pcomment character varying, IN p_db_ptype character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_db_prop_exec(IN p_db_rid character varying, IN p_db_prid character varying, IN p_db_pkey character varying, IN p_db_pvalue character varying, IN p_db_pcomment character varying, IN p_db_ptype character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$


declare

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_db_prid, '') != '' then

	      update projmng.dev_db_prop
	         set db_pvalue = p_db_pvalue
               , mod_dt = now()
	       where db_prid = p_db_prid::int
		     and db_rid = p_db_rid::int
		     and db_pkey = p_db_pkey
	      ;

      else

	      insert into projmng.dev_db_prop
	      ( db_rid, db_prid, db_pkey, db_pvalue, mod_dt, cre_dt )
	      values
	      ( p_db_rid::int
          , nvl( ( select max(db_prid) +1 from projmng.dev_db_prop ), '0')::int
          , p_db_pkey
          , p_db_pvalue 
          , now()
          , now()
          )
	      ;

      end if;

    end if;



      open p_cur for

      select     a.db_rid 
  , a.db_prid 
  , a.db_pkey 
  , REGEXP_REPLACE(a.db_pvalue , '\\r\\n\\r\\n', '','g') as db_pvalue
  , a.db_pcomment 
  , a.db_ptype 
, mod_dt, cre_dt 
        from projmng.dev_db_prop a
       where 1=1
         and ( ( nvl(p_db_rid, '') = '' and 1=1  )
             or  ( nvl(p_db_rid, '') != '' and a.db_rid = p_db_rid::int )
             )
         and ( ( nvl(p_db_pkey, '') = '' and 1=1  )
             or  ( nvl(p_db_pkey, '') != '' and a.db_pkey = p_db_pkey )
             )

      ;


	END;

$procedure$;

-- sp_dev_excel_exec(IN p_srch character varying, IN p_xls_id character varying, IN p_ttl character varying, IN p_cont character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_excel_exec(IN p_srch character varying, IN p_xls_id character varying, IN p_ttl character varying, IN p_cont character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then

        -- 먼저 UPDATE 시도
        UPDATE projmng.dev_excel
           set cont = p_cont 
         WHERE xls_id = 1
             ;


    else 

      open p_cur for

      select xls_id -- 
           , ttl -- 
           , cont
        from projmng.dev_excel a
       where xls_id = 1

      ;

    end if;

  END;

$procedure$;

-- sp_dev_grp_menu_map_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_menu_id character varying, IN p_remark character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_grp_menu_map_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_menu_id character varying, IN p_remark character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then




        -- 먼저 UPDATE 시도

        update projmng.dev_grp_menu_map
           set remark = p_remark -- 
         where grp_id = p_grp_id
           and menu_id = p_menu_id -- 
        ;

        -- 변경된 행이 없으면 INSERT
        IF NOT FOUND THEN
          insert into projmng.dev_grp_menu_map
          ( grp_id -- 
          , menu_id -- 
          , remark --  
          )
          values
          ( p_grp_id -- 
          , p_menu_id -- 
          , p_remark --  
          )
          ;
        END IF;

    elsif p_req_type = 'delete' then


      open p_cur for
     select p_req_type as bbbbb
          , p_grp_id as cccccccc
, p_menu_id as ddddddddddddd

 ;

          delete from projmng.dev_grp_menu_map
           where grp_id = p_grp_id
             and menu_id = p_menu_id -- 
          ;

    else 

      open p_cur for

      select grp_id -- 
, menu_id -- 
, menu_id as mnu_id
, remark -- 
        from projmng.dev_grp_menu_map a
       where 1=1
         and ( ( nvl(p_grp_id, '') = '' and 1=1  )
             or  ( nvl(p_grp_id, '') != '' and a.grp_id = p_grp_id )
             )

      ;

    end if;

  END;

$procedure$;

-- sp_dev_menu_auth(IN ss_user_id character varying, IN p_srch character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_owner_id character varying, IN p_mnu_url character varying, IN p_mnu_cd character varying, IN p_mnu_nm character varying, IN p_disp_seq character varying, IN p_parent_mnu_cd character varying, IN p_mnu_grp_yn character varying, IN p_pgm_id character varying, IN p_mnu_id character varying, IN p_use_yn character varying, IN p_pgm_ty character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_menu_auth(IN ss_user_id character varying, IN p_srch character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_owner_id character varying, IN p_mnu_url character varying, IN p_mnu_cd character varying, IN p_mnu_nm character varying, IN p_disp_seq character varying, IN p_parent_mnu_cd character varying, IN p_mnu_grp_yn character varying, IN p_pgm_id character varying, IN p_mnu_id character varying, IN p_use_yn character varying, IN p_pgm_ty character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

v_is_admin int;

  BEGIN

  v_is_admin = ( select count(*) from dev_user_grp_map dugm where user_id = ss_user_id and grp_id = 'Administrator' );


        open p_cur for
		select cre_id -- 
		           , cre_dt -- 
		           , mod_id -- 
		           , mod_dt -- 
		           , owner_id -- Menu Owner ID
		           , mnu_cd -- Menu Code
		           , mnu_nm -- Menu Name
		           , disp_seq -- Display Sequence
		           , parent_mnu_cd -- Parent Menu Code
		           , mnu_grp_yn -- Group Y/N
		           , pgm_id -- Program ID
		           , mnu_id -- Menu ID
		           , use_yn -- Use Yes or No
		           , pgm_ty -- Program Type
		           , mnu_url
		        from projmng.dev_menu a
		       where 1=1
                 and (  ( v_is_admin > 0 and 1=1 )
                     or ( v_is_admin <= 0 and -- 1=1
                                              
                                              (

                                                a.mnu_id in (
													          select menu_id from dev_grp_menu_map dgmm 
													           where grp_id in (
													          select grp_id from dev_user_grp_map dugm 
													           where user_id = ss_user_id
													                           )
													      )
										        or nvl(pgm_id, '') = ''

                                              )
                                              
                        )
                     )



                     
		       order
		          by disp_seq

      ;

  END;

$procedure$;

-- sp_dev_menu_exec(IN p_srch character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_owner_id character varying, IN p_mnu_url character varying, IN p_mnu_cd character varying, IN p_mnu_nm character varying, IN p_disp_seq character varying, IN p_parent_mnu_cd character varying, IN p_mnu_grp_yn character varying, IN p_pgm_id character varying, IN p_mnu_id character varying, IN p_use_yn character varying, IN p_pgm_ty character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_menu_exec(IN p_srch character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_owner_id character varying, IN p_mnu_url character varying, IN p_mnu_cd character varying, IN p_mnu_nm character varying, IN p_disp_seq character varying, IN p_parent_mnu_cd character varying, IN p_mnu_grp_yn character varying, IN p_pgm_id character varying, IN p_mnu_id character varying, IN p_use_yn character varying, IN p_pgm_ty character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare


v_menu_id varchar(100);

	BEGIN





    if p_req_type = 'save' then

      if nvl(p_mnu_id, '') = '' then

        v_menu_id = ( select nvl(max(mnu_id::int), '0')::int + 1 from projmng.dev_menu );

	      insert into projmng.dev_menu
	      ( cre_id -- 
          , cre_dt -- 
          , owner_id -- Menu Owner ID
          , mnu_cd -- Menu Code
          , mnu_nm -- Menu Name
       --   , disp_seq -- Display Sequence
          , parent_mnu_cd -- Parent Menu Code
          , mnu_grp_yn -- Group Y/N
          , pgm_id -- Program ID
          , mnu_id -- Menu ID
          , use_yn -- Use Yes or No
          , pgm_ty -- Program Type 
          , mnu_url
          )
	      values
	      ( p_cre_id -- 
          , now() -- 
          , p_owner_id -- Menu Owner ID
          , p_mnu_cd -- Menu Code
          , p_mnu_nm -- Menu Name
       --   , p_disp_seq -- Display Sequence
          , p_parent_mnu_cd -- Parent Menu Code
          , p_mnu_grp_yn -- Group Y/N
          , p_pgm_id -- Program ID
          , v_menu_id -- Menu ID
          , p_use_yn -- Use Yes or No
          , p_pgm_ty -- Program Type 
          , p_mnu_url
          )
	      ;


      open p_cur for
      select * from projmng.dev_menu where mnu_id = v_menu_id;

      else

	      update projmng.dev_menu
	         set mod_dt = now() -- 
			     , owner_id = p_owner_id -- Menu Owner ID
		       , mnu_cd = p_mnu_cd -- Menu Code
		       , mnu_nm = p_mnu_nm -- Menu Name
		       , disp_seq = nvl(p_disp_seq, '999')::decimal -- Display Sequence
		       , parent_mnu_cd = p_parent_mnu_cd -- Parent Menu Code
		       , mnu_grp_yn = p_mnu_grp_yn -- Group Y/N
		       , pgm_id = p_pgm_id -- Program ID
		       , use_yn = p_use_yn -- Use Yes or No
		       , pgm_ty = p_pgm_ty -- Program Type
           , mnu_url = p_mnu_url
	       where mnu_id = p_mnu_id
	      ;

      open p_cur for
      select * from projmng.dev_menu where mnu_id = v_menu_id;

      end if;

    elseif p_req_type = 'delete' then
      delete from projmng.dev_menu 
           where mnu_id = p_mnu_id
          ;
    else 


      -- menu data 가 어무것도 없으면 하나 만들어 준다.
      if (select count(*) from projmng.dev_menu) <= 0 then

        insert into projmng.dev_menu
          ( cre_id -- 
            , cre_dt -- 
            , owner_id -- Menu Owner ID
            , mnu_cd -- Menu Code
            , mnu_nm -- Menu Name
          --  , parent_mnu_cd -- Parent Menu Code
          --  , mnu_grp_yn -- Group Y/N
          --  , pgm_id -- Program ID
            , mnu_id -- Menu ID
            , use_yn -- Use Yes or No
            , pgm_ty -- Program Type 
            )
          values
          ( 'system' -- 
            , now() -- 
            , 'ROOT' -- Menu Owner ID
            , 'Config' -- Menu Code
            , '설정' -- Menu Name
          --  , p_parent_mnu_cd -- Parent Menu Code
          --  , p_mnu_grp_yn -- Group Y/N
          --  , p_pgm_id -- Program ID
            , '0' -- Menu ID
            , 'Y' -- Use Yes or No
            , 'F' -- Program Type 
            )
          ;




      end if;




      open p_cur for
      select cre_id -- 
           , cre_dt -- 
           , mod_id -- 
           , mod_dt -- 
           , owner_id -- Menu Owner ID
           , mnu_cd -- Menu Code
           , mnu_nm -- Menu Name
           , disp_seq -- Display Sequence
           , parent_mnu_cd -- Parent Menu Code
           , mnu_grp_yn -- Group Y/N
           , pgm_id -- Program ID
           , mnu_id -- Menu ID
           , use_yn -- Use Yes or No
           , pgm_ty -- Program Type
           , mnu_url
           , mnu_desc
        from projmng.dev_menu a
       where 1=1
--         and ( ( nvl(p_cre_id, '') = '' and 1=1  )
--             or  ( nvl(p_cre_id, '') != '' and a.cre_id = p_cre_id )
--                 )
       order
          by disp_seq

      ;

    end if;

	END;

$procedure$;

-- sp_dev_program_exec(IN p_pgm_id character varying, IN p_pgm_name character varying, IN p_fullname character varying, IN p_url character varying, IN p_name character varying, IN p_title character varying, IN p_pgm_name_eng character varying, IN p_pgm_class character varying, IN p_pgm_path character varying, IN p_remark character varying, IN p_reg_id character varying, IN p_reg_dt character varying, IN p_upt_id character varying, IN p_upt_dt character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_program_exec(IN p_pgm_id character varying, IN p_pgm_name character varying, IN p_fullname character varying, IN p_url character varying, IN p_name character varying, IN p_title character varying, IN p_pgm_name_eng character varying, IN p_pgm_class character varying, IN p_pgm_path character varying, IN p_remark character varying, IN p_reg_id character varying, IN p_reg_dt character varying, IN p_upt_id character varying, IN p_upt_dt character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if ( select count(*)
             from projmng.dev_program
	        where pgm_id = p_url ) > 0 then

	      update projmng.dev_program
	         set pgm_id = p_url
			   , pgm_class = replace(replace(replace(p_fullname, '.razor', ''), 'c:\projects\ProjMng\', ''), '\', '.')
	       where pgm_id = p_url
	      ;

      else

	      insert into projmng.dev_program
	      ( pgm_id, pgm_name, pgm_path, pgm_class )
	      values
	      ( p_url, p_name, p_url, replace(replace(replace(p_fullname, '.razor', ''), 'c:\projects\ProjMng\', ''), '\', '.') )
	      ;

      end if;

    end if;

      open p_cur for

      select     a.pgm_id 
  , a.pgm_name 
  , a.pgm_name_eng 
  , a.pgm_class 
  , a.pgm_path 
  , a.remark 
  , a.reg_id 
  , a.reg_dt 
  , a.upt_id 
  , a.upt_dt 

        from projmng.dev_program a
       where 1=1
         and ( ( nvl(p_pgm_id, '') = '' and 1=1  )
             or  ( nvl(p_pgm_id, '') != '' and a.pgm_id = p_pgm_id )
             )

      ;

	END;

$procedure$;

-- sp_dev_proj_exec(IN p_srch character varying, IN p_prj_rid character varying, IN p_prj_name character varying, IN p_prj_desc character varying, IN p_prj_sdt character varying, IN p_prj_edt character varying, IN p_prj_nick character varying, IN p_prj_type character varying, IN p_proj_pay character varying, IN p_prj_use_pay character varying, IN p_prj_srt character varying, IN p_mod_dt character varying, IN p_cre_dt character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_proj_exec(IN p_srch character varying, IN p_prj_rid character varying, IN p_prj_name character varying, IN p_prj_desc character varying, IN p_prj_sdt character varying, IN p_prj_edt character varying, IN p_prj_nick character varying, IN p_prj_type character varying, IN p_proj_pay character varying, IN p_prj_use_pay character varying, IN p_prj_srt character varying, IN p_mod_dt character varying, IN p_cre_dt character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_prj_rid, '') = '' then

	      insert into projmng.dev_proj
	      ( prj_rid -- seq
        , prj_name -- 프로젝트명
        , prj_desc -- 설명
        , prj_sdt -- 시작
        , prj_edt -- 종료
        , prj_nick -- 별칭
        , prj_type -- 구분
        , proj_pay -- 총 수주비용
        , prj_use_pay -- 총  투입 비용
        , mod_dt -- 
        , cre_dt --  
        )
	      values
	      ( ( select max(prj_rid)+1 from dev_proj ) -- seq
        , p_prj_name -- 프로젝트명
        , p_prj_desc -- 설명
        , to_date(replace(p_prj_sdt, '-', ''),'YYYYMMDD')
        , to_date(replace(p_prj_edt, '-', ''),'YYYYMMDD')
        , p_prj_nick -- 별칭
        , p_prj_type -- 구분
        , p_proj_pay::bigint -- 총 수주비용
        , p_prj_use_pay::bigint -- 총  투입 비용
        , now() -- 
        , now() --  
        )
	      ;

      else

	      update projmng.dev_proj
	         set prj_name = p_prj_name -- 프로젝트명
    			   , prj_desc = p_prj_desc -- 설명
    			   , prj_sdt = to_date(replace(p_prj_sdt, '-', ''),'YYYYMMDD')
    			   , prj_edt = to_date(replace(p_prj_edt, '-', ''),'YYYYMMDD')
    			   , prj_nick = p_prj_nick -- 별칭
    			   , prj_type = p_prj_type -- 구분
    			   , proj_pay = p_proj_pay::int -- 총 수주비용
    			   , prj_use_pay = p_prj_use_pay::int -- 총  투입 비용
    			   , mod_dt = now() -- 
    			   , cre_dt = now() -- 
             , prj_srt = nvl(p_prj_srt, '0')::int
	       where prj_rid = p_prj_rid::int
	      ;

      end if;

else
      open p_cur for

      select prj_rid -- seq
           , prj_name -- 프로젝트명
           , prj_desc -- 설명
           , prj_sdt -- 시작
           , prj_edt -- 종료
           , prj_nick -- 별칭
           , prj_type -- 구분
           , proj_pay -- 총 수주비용
           , prj_use_pay -- 총  투입 비용
           , mod_dt -- 
           , cre_dt -- 
           , prj_srt
        from projmng.dev_proj a
       where 1=1
         and ( ( nvl(p_prj_rid, '') = '' and 1=1  )
             or  ( nvl(p_prj_rid, '') != '' and a.prj_rid = p_prj_rid::int )
             )
       order by prj_rid
      ;

    end if;

	END;

$procedure$;

-- sp_dev_proj_prop_exec(IN p_srch character varying, IN p_prj_rid character varying, IN p_prop_cd character varying, IN p_prop_val character varying, IN p_prop_comm character varying, IN p_prop_use_yn character varying, IN p_prop_type character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_proj_prop_exec(IN p_srch character varying, IN p_prj_rid character varying, IN p_prop_cd character varying, IN p_prop_val character varying, IN p_prop_comm character varying, IN p_prop_use_yn character varying, IN p_prop_type character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

is_exsit int;

BEGIN




if p_req_type = 'save' then


  is_exsit = (  select count(*) from projmng.dev_proj_prop a where a.prj_rid = p_prj_rid and a.prop_cd = p_prop_cd and a.prop_type = p_prop_type  );


    if is_exsit <= 0 then
      insert into projmng.dev_proj_prop
	      ( prj_rid -- 
			,prop_cd -- 
			,prop_val -- 
			,prop_comm -- 
			,prop_use_yn -- 
			,prop_type --  
			)
	      values
	      ( p_prj_rid -- 
			, p_prop_cd -- 
			, p_prop_val -- 
			, p_prop_comm -- 
			, p_prop_use_yn -- 
			, p_prop_type --  
			)
	      ;

      else

	      update projmng.dev_proj_prop a
	         set 
  prop_val = p_prop_val -- 
, prop_comm = p_prop_comm -- 
, prop_use_yn = p_prop_use_yn -- 
	       where prj_rid = p_prj_rid
             and a.prop_cd = p_prop_cd 
             and a.prop_type = p_prop_type
	      ;

      end if;

    else 

      open p_cur for

      select prj_rid -- 
, prop_cd -- 
, prop_val -- 
, prop_comm -- 
, prop_use_yn -- 
, prop_type -- 
        from projmng.dev_proj_prop a
       where 1=1
         and ( ( nvl(p_prj_rid, '') = '' and 1=1  )
             or  ( nvl(p_prj_rid, '') != '' and a.prj_rid = p_prj_rid )
             )
         and ( ( nvl(p_prop_cd, '') = '' and 1=1  )
             or  ( nvl(p_prop_cd, '') != '' and a.prop_cd = p_prop_cd )
             )

      ;

    end if;


	END;

$procedure$;

-- sp_dev_proj_user_map_exec(IN p_prj_rid character varying, IN p_accept_proj character varying, IN p_user_id character varying, IN p_prj_name character varying, IN p_prj_desc character varying, IN p_prj_sdt character varying, IN p_prj_edt character varying, IN p_prj_nick character varying, IN p_prj_type character varying, IN p_proj_pay character varying, IN p_prj_use_pay character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_proj_user_map_exec(IN p_prj_rid character varying, IN p_accept_proj character varying, IN p_user_id character varying, IN p_prj_name character varying, IN p_prj_desc character varying, IN p_prj_sdt character varying, IN p_prj_edt character varying, IN p_prj_nick character varying, IN p_prj_type character varying, IN p_proj_pay character varying, IN p_prj_use_pay character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN



    if p_req_type = 'save' then

      if p_accept_proj = 'True' then
	      
	      insert into projmng.dev_proj_user_map
		  (prj_rid, user_id)
		   values
		   ( p_prj_rid::int, p_user_id )
			 ;

      else
        
	      delete from projmng.dev_proj_user_map
		   where prj_rid = p_prj_rid::int
		     and user_id = p_user_id
	      ;

      end if;

    end if;



	

      open p_cur for

      select case when nvl(b.prj_rid, '') = '' then false else true end as accept_proj
	       , a.prj_rid 
		   , a.prj_name 
		   , a.prj_desc 
		   , a.prj_sdt 
		   , a.prj_edt 
        from projmng.dev_proj a
		left outer join ( select * 
		                    from projmng.dev_proj_user_map 
						   where 1=1						     
					         and ( ( nvl(p_user_id, '') = '' and 1=1  )
					             or  ( nvl(p_user_id, '') != '' and user_id = p_user_id ) 
					             )
		                ) b
          on a.prj_rid = b.prj_rid
       where 1=1
         and ( ( nvl(p_prj_rid, '') = '' and 1=1  )
             or  ( nvl(p_prj_rid, '') != '' and a.prj_rid = p_prj_rid::int )
             )

      ;


	END;

$procedure$;

-- sp_dev_srcinfo_dtl_exec(IN p_src_dtl_rid character varying, IN p_src_extend character varying, IN p_src_pattern_grp character varying, IN p_url_pattern character varying, IN p_src_pattern_comment character varying, IN p_src_pattern_nullvalue character varying, IN p_src_rid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_srcinfo_dtl_exec(IN p_src_dtl_rid character varying, IN p_src_extend character varying, IN p_src_pattern_grp character varying, IN p_url_pattern character varying, IN p_src_pattern_comment character varying, IN p_src_pattern_nullvalue character varying, IN p_src_rid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_src_dtl_rid, '') != '' then

	      update projmng.dev_srcinfo_dtl
	         set url_pattern = p_url_pattern
    			   , src_pattern_grp = p_src_pattern_grp
    			   , src_pattern_comment = p_src_pattern_comment
             , src_extend = p_src_extend
	       where src_dtl_rid = p_src_dtl_rid::int
	      ;

      else

	      insert into projmng.dev_srcinfo_dtl
	      ( src_dtl_rid
        , src_extend 
        , src_pattern_grp 
        , url_pattern 
        , src_pattern_comment 
        , src_pattern_nullvalue 
        , src_rid 
		    )
	      values
	      ( ( select max(src_dtl_rid::int) + 1 from projmng.dev_srcinfo_dtl )
        , p_src_extend 
        , p_src_pattern_grp 
        , p_url_pattern 
        , p_src_pattern_comment 
        , p_src_pattern_nullvalue 
        , p_src_rid::int
		    )
	      ;

      end if;


    elsif p_req_type = 'delete' then

        delete
          from projmng.dev_srcinfo_dtl
         where src_dtl_rid = p_src_dtl_rid::int
        ;

    end if;

      open p_cur for

      select a.src_dtl_rid 
           , a.src_extend 
           , a.src_pattern_grp 
           , a.url_pattern 
           , a.src_pattern_comment 
           , a.src_pattern_nullvalue 
           , a.src_rid 
        from projmng.dev_srcinfo_dtl a
       where 1=1
         and ( ( nvl(p_src_dtl_rid, '') = '' and 1=1  )
             or  ( nvl(p_src_dtl_rid, '') != '' and a.src_dtl_rid = p_src_dtl_rid::int )
             )
         and ( ( nvl(p_src_rid, '') = '' and 1=1  )
             or  ( nvl(p_src_rid, '') != '' and a.src_rid = p_src_rid::int )
             )
       order by src_dtl_rid
      ;

	END;

$procedure$;

-- sp_dev_srcinfo_exec(IN p_src_rid character varying, IN p_src_os character varying, IN p_src_path character varying, IN p_src_nick character varying, IN p_src_type character varying, IN p_src_lang character varying, IN p_src_comm character varying, IN p_prj_rid character varying, IN p_req_type character varying, IN p_src_ui_root character varying, IN p_prj_namespace character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_srcinfo_exec(IN p_src_rid character varying, IN p_src_os character varying, IN p_src_path character varying, IN p_src_nick character varying, IN p_src_type character varying, IN p_src_lang character varying, IN p_src_comm character varying, IN p_prj_rid character varying, IN p_req_type character varying, IN p_src_ui_root character varying, IN p_prj_namespace character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_src_rid, '') = '' then

	      insert into projmng.dev_srcinfo
	      ( src_rid ,		  
        	src_os ,
        	src_path ,
        	src_nick ,
        	src_type ,
        	src_lang ,
        	src_comm ,
        	prj_rid ,
        	src_ui_root ,
        	prj_namespace
		    )
	      values
	      ( ( select max(src_rid )+1 from projmng.dev_srcinfo ),
        	p_src_os ,
        	p_src_path ,
        	p_src_nick ,
        	p_src_type ,
        	p_src_lang ,
        	p_src_comm ,
        	p_prj_rid::int ,
        	p_src_ui_root ,
        	p_prj_namespace
		  )
	      ;

      else

		  
	      update projmng.dev_srcinfo
	         set src_os = p_src_os ,
               src_path = p_src_path ,
               src_nick = p_src_nick ,
               src_type = p_src_type ,
               src_lang = p_src_lang ,
               src_comm = p_src_comm ,
               prj_rid = p_prj_rid::int ,
               src_ui_root = p_src_ui_root ,
               prj_namespace = p_prj_namespace
	       where src_rid = p_src_rid::int
	      ;
		  

      end if;

    end if;

      open p_cur for

      select a.src_rid 
           , a.prj_rid 
           , b.prj_name 
           , b.prj_nick 
           , a.src_os 
           , a.src_path 
           , a.src_nick 
           , a.src_type 
           , a.src_lang 
           , a.src_comm 
           , a.src_ui_root
           , a.prj_namespace
           , ( select min(url_pattern) from projmng.dev_srcinfo_dtl where src_pattern_grp = 'url' and src_rid = a.src_rid ) as url_pattern
        from projmng.dev_srcinfo a
        left outer join dev_proj b
          on a.prj_rid = b.prj_rid
       where 1=1
         and ( ( nvl(p_src_rid, '') = '' and 1=1  )
             or  ( nvl(p_src_rid, '') != '' and a.src_rid = p_src_rid::int )
             )
         and ( ( nvl(p_prj_rid, '') = '' and 1=1  )
             or  ( nvl(p_prj_rid, '') != '' and a.prj_rid = p_prj_rid::int )
             )

      ;

	END;

$procedure$;

-- sp_dev_user_exec(IN p_last_page_yn character varying, IN p_email character varying, IN p_prj_rid character varying, IN p_req_type character varying, IN p_user_id character varying, IN p_password character varying, IN p_role_grp_id character varying, IN p_user_name character varying, IN p_user_name_eng character varying, IN p_emp_no character varying, IN p_cust_code character varying, IN p_dept_code character varying, IN p_office_num character varying, IN p_phone_num character varying, IN p_address_1 character varying, IN p_address_2 character varying, IN p_country character varying, IN p_use_yn character varying, IN p_remark character varying, IN p_reg_id character varying, IN p_reg_dt character varying, IN p_upt_id character varying, IN p_upt_dt character varying, IN p_user_photo character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_user_exec(IN p_last_page_yn character varying, IN p_email character varying, IN p_prj_rid character varying, IN p_req_type character varying, IN p_user_id character varying, IN p_password character varying, IN p_role_grp_id character varying, IN p_user_name character varying, IN p_user_name_eng character varying, IN p_emp_no character varying, IN p_cust_code character varying, IN p_dept_code character varying, IN p_office_num character varying, IN p_phone_num character varying, IN p_address_1 character varying, IN p_address_2 character varying, IN p_country character varying, IN p_use_yn character varying, IN p_remark character varying, IN p_reg_id character varying, IN p_reg_dt character varying, IN p_upt_id character varying, IN p_upt_dt character varying, IN p_user_photo character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if ( select count(*) 
             from projmng.dev_user
	        where user_id = p_user_id ) > 0 then

	      update projmng.dev_user
	         set user_name = p_user_name
			       , user_photo = nvl(p_user_photo, user_photo)
             , email = nvl(p_email, email)
             , user_name_eng = p_user_name_eng
	       where user_id = p_user_id
	      ;

      else
        
	      insert into projmng.dev_user
	      ( user_id, user_name, user_photo )
	      values
	      ( p_user_id, p_user_name, p_user_photo )
	      ;

      end if;


--    if nvl(p_last_page_yn, '') != '' then

--      call sp_dev_user_prop_exec(null, p_user_id, 'LASTPAGE_OPEN_YN', p_last_page_yn, null, null, null, null, null, null, null, null, 'save', p_cur) ; -- INOUT p_cur refcursor

--    end if;



    end if;

      open p_cur for

      select sum( case when nvl(b.user_id, '') = '' then 0 else 1 end) as inv_cnt
			  , a.user_id -- 아이디
			 -- , a.upwd 
			 -- , a.role_grp_id 
			  , a.user_name -- 사용자 이름
			 -- , a.user_name_eng 
			 -- , a.emp_no 
			 -- , a.cust_code 
			 -- , a.dept_code 
			 -- , a.office_num 
			 -- , a.phone_num 
			 -- , a.address_1 
			 -- , a.address_2 
			 -- , a.country 
			 -- , a.use_yn 
			 -- , a.remark 
			 -- , a.reg_id 
			 -- , a.reg_dt 
			 -- , a.upt_id 
			 -- , a.upt_dt 
			 -- , a.user_type 
			 , a.user_photo
        from projmng.dev_user a
        left outer join projmng.dev_proj_user_map b
          on a.user_id = b.user_id
       where 1=1
         and ( ( nvl(p_user_id, '') = '' and 1=1  )
             or  ( nvl(p_user_id, '') != '' and a.user_id = p_user_id ) 
             )
			 
		 and ( ( nvl(p_prj_rid, '') = '' and 1=1  )
			 or  ( nvl(p_prj_rid, '') != '' and b.prj_rid = p_prj_rid::int ) 
			 )
         
       group by a.user_id , a.user_name, a.user_photo
       order by a.user_id 
      ;

	END;

$procedure$;

-- sp_dev_user_exec_all(IN p_fontsize character varying, IN p_user_id character varying, IN p_serverurl character varying, IN p_sideauto_close character varying, IN p_page character varying, IN p_page_nm character varying, IN p_theme character varying, IN p_page_yn character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_user_exec_all(IN p_fontsize character varying, IN p_user_id character varying, IN p_serverurl character varying, IN p_sideauto_close character varying, IN p_page character varying, IN p_page_nm character varying, IN p_theme character varying, IN p_page_yn character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN


    if nvl(p_page, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'LASTPAGE', p_page, p_page_nm, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;

    if nvl(p_theme, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'THEME', p_theme, null, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;

    if nvl(p_page_yn, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'LASTPAGE_OPEN_YN', p_page_yn, null, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;


    if nvl(p_sideauto_close, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'SIDEBAR_AUTO_CLOSE', p_sideauto_close, null, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;

    if nvl(p_serverurl, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'SERVER_URL', p_serverurl, null, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;

    if nvl(p_fontsize, '') != '' then
      call sp_dev_user_prop_exec(null, p_user_id, 'FONTSIZE', p_fontsize, null, null, null, null, null, null, null, null, 'save', p_cur) ; --
    end if;


  END;

$procedure$;

-- sp_dev_user_grp_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_grp_name character varying, IN p_remark character varying, IN p_role_id character varying, IN p_use_yn character varying, IN p_grp_photo character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_user_grp_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_grp_name character varying, IN p_remark character varying, IN p_role_id character varying, IN p_use_yn character varying, IN p_grp_photo character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then

        -- 먼저 UPDATE 시도
   
        update projmng.dev_user_grp
           set grp_id = p_grp_id -- 
             , grp_name = p_grp_name -- 
             , remark = p_remark -- 
             , role_id = p_role_id -- 
             , use_yn = p_use_yn --
             , grp_photo = p_grp_photo -- 
         where grp_id = p_grp_id
        ;

        -- 변경된 행이 없으면 INSERT
        IF NOT FOUND THEN
          insert into projmng.dev_user_grp
          ( grp_id -- 
          , grp_name -- 
          , remark -- 
          , role_id -- 
          , use_yn -- 
          , grp_photo --  
          )
          values
          ( p_grp_id -- 
          , p_grp_name -- 
          , p_remark -- 
          , p_role_id -- 
          , p_use_yn -- 
          , p_grp_photo --  
          )
          ;
        END IF;

    elsif p_req_type = 'delete' then

      delete from projmng.dev_user_grp
       where grp_id = p_grp_id;

    else 

      open p_cur for

      select grp_id -- 
           , grp_name -- 
           , remark -- 
           , role_id -- 
           , use_yn -- 
           , grp_photo -- 
        from projmng.dev_user_grp a
       where 1=1
         and ( ( nvl(p_grp_id, '') = '' and 1=1  )
             or  ( nvl(p_grp_id, '') != '' and a.grp_id = p_grp_id )
                 )

      ;

    end if;

  END;

$procedure$;

-- sp_dev_user_grp_map_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_user_id character varying, IN p_remark character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_user_grp_map_exec(IN p_srch character varying, IN p_grp_id character varying, IN p_user_id character varying, IN p_remark character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then

        -- 먼저 UPDATE 시도        
        update projmng.dev_user_grp_map
           set remark = p_remark -- 
         where grp_id = p_grp_id
           and user_id = p_user_id
        ;

        -- 변경된 행이 없으면 INSERT
        IF NOT FOUND THEN

          insert into projmng.dev_user_grp_map
          ( grp_id -- 
          ,user_id -- 
          ,remark --  
            )
          values
          ( p_grp_id -- 
          , p_user_id -- 
          , p_remark --  
            )
          ;

        END IF;


    elsif p_req_type = 'delete' then

  
        delete from projmng.dev_user_grp_map
         where grp_id = p_grp_id
           and user_id = p_user_id
        ;


    else 

      open p_cur for

      select grp_id -- 
, user_id -- 
, remark -- 
        from projmng.dev_user_grp_map a
       where 1=1
         and ( ( nvl(p_grp_id, '') = '' and 1=1  )
             or  ( nvl(p_grp_id, '') != '' and a.grp_id = p_grp_id )
                 )

      ;

    end if;

  END;

$procedure$;

-- sp_dev_user_prop_exec(IN p_srch character varying, IN p_user_id character varying, IN p_prop_type character varying, IN p_prop_val character varying, IN p_prop_val2 character varying, IN p_prop_val3 character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_cre_user character varying, IN p_mod_user character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_dev_user_prop_exec(IN p_srch character varying, IN p_user_id character varying, IN p_prop_type character varying, IN p_prop_val character varying, IN p_prop_val2 character varying, IN p_prop_val3 character varying, IN p_cre_id character varying, IN p_cre_dt character varying, IN p_mod_id character varying, IN p_mod_dt character varying, IN p_cre_user character varying, IN p_mod_user character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

  BEGIN

    if p_req_type = 'save' then



        -- 먼저 UPDATE 시도
        UPDATE projmng.dev_user_prop
           SET prop_val = p_prop_val,
               prop_val2 = p_prop_val2,
               prop_val3 = p_prop_val3,
               mod_dt = now()
         WHERE user_id = p_user_id
           AND prop_type = p_prop_type;

        -- 변경된 행이 없으면 INSERT
        IF NOT FOUND THEN
            INSERT INTO projmng.dev_user_prop (
                user_id, prop_type, prop_val, prop_val2, prop_val3, cre_dt, mod_dt
            ) VALUES (
                p_user_id, p_prop_type, p_prop_val, p_prop_val2, p_prop_val3, now(), now()
            );
        END IF;


    else 

      open p_cur for

      select user_id -- 
           , prop_type -- 
           , prop_val --  
           , prop_val2 --  
           , prop_val3 -- 
           , cre_id -- 
           , cre_dt -- 
           , mod_id -- 
           , mod_dt -- 
           , cre_user -- 
           , mod_user -- 
        from projmng.dev_user_prop a
       where 1=1
         and (  ( nvl(p_user_id, '') = '' and 1=1  )
             or ( nvl(p_user_id, '') != '' and a.user_id = p_user_id )
             )

      ;

    end if;

  END;

$procedure$;

-- sp_devcomm_exec(IN p_srch character varying, IN p_cm_srt character varying, IN p_srch_type character varying, IN p_cm_rid character varying, IN p_cm_cd character varying, IN p_cm_nm character varying, IN p_cm_prop character varying, IN p_cm_pcd character varying, IN p_cm_val character varying, IN p_cm_type character varying, IN p_cm_val2 character varying, IN p_cm_val3 character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_devcomm_exec(IN p_srch character varying, IN p_cm_srt character varying, IN p_srch_type character varying, IN p_cm_rid character varying, IN p_cm_cd character varying, IN p_cm_nm character varying, IN p_cm_prop character varying, IN p_cm_pcd character varying, IN p_cm_val character varying, IN p_cm_type character varying, IN p_cm_val2 character varying, IN p_cm_val3 character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_cm_rid, '') = '' then

	      insert into projmng.devcomm
	      ( cm_rid -- 
,cm_cd -- 
,cm_nm -- 
,cm_prop -- 
,cm_pcd -- 
,cm_val -- 
,cm_type -- 
,cm_val2 -- 
,cm_val3 --  
          )
	      values
	      ( (select max(cm_rid)+1 from projmng.devcomm) -- 
, p_cm_cd -- 
, p_cm_nm -- 
, p_cm_prop -- 
, p_cm_pcd -- 
, p_cm_val -- 
, p_cm_type -- 
, p_cm_val2 -- 
, p_cm_val3 --  
          )
	      ;

      else

	      update projmng.devcomm
	         set  cm_cd = p_cm_cd -- 
				, cm_nm = p_cm_nm -- 
				, cm_prop = p_cm_prop -- 
				, cm_pcd = p_cm_pcd -- 
				, cm_val = p_cm_val -- 
				, cm_type = p_cm_type -- 
				, cm_val2 = p_cm_val2 -- 
        , cm_val3 = p_cm_val3 -- 
        , cm_srt = nvl(p_cm_srt, '999')::int -- 
	       where cm_rid = p_cm_rid::int
	      ;

      end if;

    else 

      open p_cur for

      select cm_rid -- 
, cm_cd -- 
, cm_nm -- 
, cm_prop -- 
, cm_pcd -- 
, cm_val -- 
, cm_type -- 
, cm_val2 -- 
, cm_val3 -- 
, cm_srt
        from projmng.devcomm a
       where 1=1
         and ( ( nvl(p_cm_rid, '') = '' and 1=1  )
             or  ( nvl(p_cm_rid, '') != '' and a.cm_rid = p_cm_rid::bigint )
             )
         and ( ( nvl(p_srch_type, '') = '' and 1=1  )
             or  ( nvl(p_srch_type, 'main') != '' and nvl(a.cm_pcd, '') = '' )
             )
         and ( ( nvl(p_cm_pcd, '') = '' and 1=1  )
             or  ( nvl(p_cm_pcd, '') != '' and a.cm_pcd = p_cm_pcd )
             )
       order 
          by a.cm_srt

      ;
    end if;

	END;

$procedure$;

-- sp_devsqlresp_base_exec(IN p_dsl_cd character varying, IN p_comm character varying, IN p_sort character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_devsqlresp_base_exec(IN p_dsl_cd character varying, IN p_comm character varying, IN p_sort character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN



    if p_req_type = 'save' then

      if ( select count(*)
             from projmng.devsqlresp_base
	        where dsl_cd = p_dsl_cd ) > 0 then

	      update projmng.devsqlresp_base
	         set dsl_cd = p_dsl_cd
	           , comm = p_comm
	           , sort = nvl(p_sort, '999')::int
	       where dsl_cd = p_dsl_cd
	      ;

      else

	      insert into projmng.devsqlresp_base
	      ( dsl_cd, comm, sort )
	      values
	      ( p_dsl_cd, p_comm, nvl(p_sort, '999')::int )
	      ;

      end if;

    end if;









      open p_cur for

      select     a.dsl_cd 
  , a.comm 
  , a.sort 
        from projmng.devsqlresp_base a
       where 1=1
         and ( ( nvl(p_dsl_cd, '') = '' and 1=1  )
             or  ( nvl(p_dsl_cd, '') != '' and a.dsl_cd = p_dsl_cd )
             )

      ;


	END;

$procedure$;

-- sp_home_todo_exec(IN p_srch character varying, IN p_todo_key character varying, IN p_todo_state character varying, IN p_title character varying, IN p_is_complete character varying, IN p_cre_dt character varying, IN p_comp_dt character varying, IN p_cre_id character varying, IN p_comp_id character varying, IN p_mod_dt character varying, IN p_mod_id character varying, IN p_comments character varying, IN p_target_day character varying, IN p_target_user character varying, IN p_req_type character varying, IN p_fix_point character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_home_todo_exec(IN p_srch character varying, IN p_todo_key character varying, IN p_todo_state character varying, IN p_title character varying, IN p_is_complete character varying, IN p_cre_dt character varying, IN p_comp_dt character varying, IN p_cre_id character varying, IN p_comp_id character varying, IN p_mod_dt character varying, IN p_mod_id character varying, IN p_comments character varying, IN p_target_day character varying, IN p_target_user character varying, IN p_req_type character varying, IN p_fix_point character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

    if p_req_type = 'save' then

      if nvl(p_todo_key, '') = '' then

	      insert into projmng.home_todo
	      ( todo_key -- 
			,title -- 제목
			,is_complete -- 완료여부
			,cre_dt -- 등록일시
			--,comp_dt -- 완료일자
			,cre_id -- 등록자
			,comp_id -- 완료확인자
			--,mod_dt -- 수정일
			--,mod_id -- 수정자
			,comments -- 코멘트
			,target_day -- 타겟일자 
            , fix_point 
            , target_user
          )
	      values
	      (   ( select nvl(max(todo_key) +1, '0')::bigint from projmng.home_todo )  -- 
			, p_title -- 제목
			, case when p_is_complete = 'Y' then true else false end -- 완료여부
			, now() -- 등록일시
			--, p_comp_dt -- 완료일자
			, p_cre_id -- 등록자
			, p_comp_id -- 완료확인자
			--, p_mod_dt -- 수정일
			--, p_mod_id -- 수정자
			, p_comments -- 코멘트
			, nvl(p_target_day, null)::date -- 타겟일자
            , nvl(p_fix_point, '0')::bigint
            , p_target_user
          )
	      ;

      else

	      update projmng.home_todo
	         set 
				  title = p_title -- 제목
				, is_complete = case when p_is_complete = 'True' then true else false end -- 완료여부
				--, cre_dt = p_cre_dt -- 등록일시
				--, comp_dt = p_comp_dt -- 완료일자
				--, cre_id = p_cre_id -- 등록자
				, comp_id = p_comp_id -- 완료확인자
				, mod_dt = now() -- 수정일
				, mod_id = p_mod_id -- 수정자
				, comments = p_comments -- 코멘트
				, target_day = nvl(p_target_day, null)::date -- 타겟일자
                , fix_point = nvl(p_fix_point, '0')::bigint
                , todo_state = p_todo_state
                , target_user = p_target_user
	       where todo_key = p_todo_key::bigint
	      ;

      end if;

    elseif p_req_type = 'delete' then

	      delete from projmng.home_todo
	       where todo_key = p_todo_key::bigint
	      ;
    else 

      open p_cur for

      select todo_key -- 
		, target_day -- 타겟일자
		, title -- 제목
		, is_complete -- 완료여부
		, cre_dt -- 등록일시
		, comp_dt -- 완료일자
		, cre_id -- 등록자
		, comp_id -- 완료확인자
		, mod_dt -- 수정일
		, mod_id -- 수정자
		, comments -- 코멘트
        , target_user
        , nvl(fix_point, '0') as fix_point -- 지정금액
        , todo_state
        , b.cm_nm as todo_state_name
        from projmng.home_todo a
        left outer join ( select cm_cd, cm_nm
  from devcomm
 where cm_pcd = 'TODO_STATE' ) b
        on a.todo_state = b.cm_cd
       where 1=1
         --and target_day >= current_date
         and ( ( nvl(p_todo_key, '') = '' and 1=1  )
             or  ( nvl(p_todo_key, '') != '' and a.todo_key = p_todo_key::bigint )
             )
         and ( ( nvl(p_target_user, '') = '' and 1=1  )
             or  ( nvl(p_target_user, '') != '' and a.target_user = p_target_user )
             )
         and ( ( nvl(p_todo_state, '') = '' and 1=1  )
             or  ( nvl(p_todo_state, '') != '' and a.todo_state = p_todo_state )
             )
         and ( ( nvl(p_is_complete, '') = '' and 1=1  )
             or  ( nvl(p_is_complete, '') != '' and a.is_complete = case when p_is_complete = 'True' then true else false end )
             )

         and ( ( nvl(p_target_day, '') = '' and 1=1  )
             or  ( nvl(p_target_day, '') != '' and ( a.target_day >= p_target_day::date 
                                                   and a.target_day < ( p_target_day::date + INTERVAL '1 day' )
                                                   )
                 )
             )
order by target_day, fix_point desc, title








      ;

    end if;

	END;

$procedure$;

-- sp_home_todo_make(IN p_srch character varying, IN p_todo_key character varying, IN p_title character varying, IN p_is_complete character varying, IN p_cre_dt character varying, IN p_comp_dt character varying, IN p_cre_id character varying, IN p_comp_id character varying, IN p_mod_dt character varying, IN p_mod_id character varying, IN p_comments character varying, IN p_target_day character varying, IN p_req_type character varying, IN p_fix_point character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_home_todo_make(IN p_srch character varying, IN p_todo_key character varying, IN p_title character varying, IN p_is_complete character varying, IN p_cre_dt character varying, IN p_comp_dt character varying, IN p_cre_id character varying, IN p_comp_id character varying, IN p_mod_dt character varying, IN p_mod_id character varying, IN p_comments character varying, IN p_target_day character varying, IN p_req_type character varying, IN p_fix_point character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

 
	      insert into projmng.home_todo
	      ( todo_key -- 
			,title -- 제목
			,is_complete -- 완료여부
			,cre_dt -- 등록일시
			--,comp_dt -- 완료일자
			,cre_id -- 등록자
			--,mod_dt -- 수정일
			--,mod_id -- 수정자
			,comments -- 코멘트
			,target_day -- 타겟일자 
            , fix_point
            , target_user
            , todo_state
          )       
select ( select nvl(max(todo_key) +x.rn, '0')::bigint from projmng.home_todo )
     , cm_nm
     , false
     , now()
     , 'system'
     , ''
     , p_target_day::date
     , cm_val::bigint
     , 'jjstyle'
     , cm_val2
  from (
select (row_number() over()) as rn
     , cm_cd
     , cm_nm
     , cm_val 
     , cm_val2
  from projmng.devcomm
 where cm_pcd = 'HOMEWORK'
   and cm_val2 in ('R', 'M')
       ) x

;


 
	      insert into projmng.home_todo
	      ( todo_key -- 
			,title -- 제목
			,is_complete -- 완료여부
			,cre_dt -- 등록일시
			--,comp_dt -- 완료일자
			,cre_id -- 등록자
			--,mod_dt -- 수정일
			--,mod_id -- 수정자
			,comments -- 코멘트
			,target_day -- 타겟일자 
            , fix_point
            , target_user
            , todo_state
          )       
select ( select nvl(max(todo_key) +x.rn, '0')::bigint from projmng.home_todo )
     , cm_nm
     , false
     , now()
     , 'system'
     , ''
     , p_target_day::date
     , cm_val::bigint
     , 'hsstyle'
     , cm_val2
  from (
select (row_number() over()) as rn
     , cm_cd
     , cm_nm
     , cm_val 
     , cm_val2
  from projmng.devcomm
 where cm_pcd = 'HOMEWORK'
   and cm_val2 in ('R', 'M')
       ) x

;





      open p_cur for

      select todo_key -- 
		, target_day -- 타겟일자
		, title -- 제목
		, is_complete -- 완료여부
		, cre_dt -- 등록일시
		, comp_dt -- 완료일자
		, cre_id -- 등록자
		, comp_id -- 완료확인자
		, mod_dt -- 수정일
		, mod_id -- 수정자
		, comments -- 코멘트
        , nvl(fix_point, '0') as fix_point -- 지정금액
        from projmng.home_todo a
       where 1=1
         and ( ( nvl(p_todo_key, '') = '' and 1=1  )
             or  ( nvl(p_todo_key, '') != '' and a.todo_key = p_todo_key::bigint )
                 )

      ;


	END;

$procedure$;

-- sp_home_todo_pay(IN p_req_type character varying, IN p_target_user character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_home_todo_pay(IN p_req_type character varying, IN p_target_user character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare

	BEGIN

 
      open p_cur for


      select a.*, nvl(b.today_pay::int, 0) as today_pay
        from (
      select target_user, sum( fix_point ) as total_pay
        from projmng.home_todo 
       where 1=1
         and is_complete = true
       group by target_user
             ) a
        left outer join
             (
      select target_user, sum( fix_point ) as today_pay
        from projmng.home_todo a
       where 1=1
         and is_complete = true
         and target_day >= current_date
       group by target_user
             ) b
             on a.target_user = b.target_user
       where 1=1
         and ( ( nvl(p_target_user, '') = '' and 1=1  )
             or  ( nvl(p_target_user, '') != '' and a.target_user = p_target_user )
             )


      ;


	END;

$procedure$;

-- sp_proj_login(IN p_srch character varying, IN p_userid character varying, IN p_pwd character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_proj_login(IN p_srch character varying, IN p_userid character varying, IN p_pwd character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$


declare 
    v_isLogin int;
	BEGIN


-- id, password 체크.
v_isLogin = ( select count(*) from projmng.dev_user
 where user_id = p_userid
   and upwd = p_pwd );
-- login 성공
if v_isLogin > 0 then

      open p_cur for
      select a.*
           , a.user_name as LastName
           , a.user_id as UserId
           , ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='THEME' ) as theme
           , ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='LASTPAGE' ) as last_page
           , ( select prop_val2 from dev_user_prop b where b.user_id = a.user_id and prop_type='LASTPAGE' ) as last_page_nm
           , ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='LAST_LOGIN_TM' ) as last_login_tm
           , nvl( ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='LASTPAGE_OPEN_YN' ), 'false') as last_page_yn
           , nvl( ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='SIDEBAR_AUTO_CLOSE' ), 'false') as SideBarAutoClose
           , ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='SERVER_URL' ) as UserServerUrl
           , ( select prop_val from dev_user_prop b where b.user_id = a.user_id and prop_type='FONTSIZE' ) as fontsize
        from dev_user a
       where a.user_id = p_userid
         and a.upwd = p_pwd
      ;

-- login 실패
--else


end if;
	
      
	
	END;
	
$procedure$;

-- sp_proj_wbs_exec(IN p_schedule_type character varying, IN p_compstat character varying, IN p_prj_rid character varying, IN p_wbs_id character varying, IN p_plan_sdt character varying, IN p_plan_edt character varying, IN p_dev_sdt character varying, IN p_dev_edt character varying, IN p_proc_id character varying, IN p_gb1 character varying, IN p_gb2 character varying, IN p_proc_nm character varying, IN p_proc_tp character varying, IN p_proc_lvl character varying, IN p_build_user character varying, IN p_build_status character varying, IN p_dev_user character varying, IN p_comm character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_proj_wbs_exec(IN p_schedule_type character varying, IN p_compstat character varying, IN p_prj_rid character varying, IN p_wbs_id character varying, IN p_plan_sdt character varying, IN p_plan_edt character varying, IN p_dev_sdt character varying, IN p_dev_edt character varying, IN p_proc_id character varying, IN p_gb1 character varying, IN p_gb2 character varying, IN p_proc_nm character varying, IN p_proc_tp character varying, IN p_proc_lvl character varying, IN p_build_user character varying, IN p_build_status character varying, IN p_dev_user character varying, IN p_comm character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
  v_wbs_id int;

	BEGIN

		  
    if p_req_type = 'save' then

    	if nvl(p_wbs_id, '') = '' then -- insert 
    

        v_wbs_id = ( select max(wbs_id) + 1 from projmng.dev_wbs );

    	  insert into projmng.dev_wbs
    	  (
            prj_rid,
    		wbs_id,
    		proc_id,
    		gb1,
    		gb2,
    		proc_nm,
    		proc_tp,
    		proc_lvl,
    		build_user,
    		build_status,
    		dev_user,
    		plan_sdt,
    		plan_edt,
    		dev_sdt,
    		dev_edt,
    		dev_chk,
    		build_chk,
    		build_chk_dt,
    		qc_user,
    		qc_chk,
    		qc_chk_dt,
    		cre_user,
    		cre_dt,
    		mod_user,
    		mod_dt,
        schedule_type,
    		comm
    	  )
    	  values
    	  (
            p_prj_rid::int,
    		v_wbs_id ,
    		p_proc_id,
    		p_gb1,
    		p_gb2,
    		p_proc_nm,
    		p_proc_tp,
    		p_proc_lvl,
    		p_build_user,
    		p_build_status,
    		p_dev_user,
    		to_date(nvl(to_ymd(p_plan_sdt), nvl(to_ymd(p_dev_sdt), to_char(current_date, 'YYYYMMDD'))), 'YYYYMMDD'),
    		to_date(nvl(to_ymd(p_plan_edt), nvl(to_ymd(p_dev_edt), to_char(current_date, 'YYYYMMDD'))), 'YYYYMMDD'),
    		to_date(nvl(to_ymd(p_dev_sdt), null), 'YYYYMMDD'),
    		to_date(nvl(to_ymd(p_dev_edt), null), 'YYYYMMDD'),
    		null, --p_dev_chk,
    		null, --p_build_chk,
    		null, --p_build_chk_dt,
    		null, --p_qc_user,
    		null, --p_qc_chk,
    		null, --p_qc_chk_dt,
    		sess_userid, --p_cre_user,
    		now(), --p_cre_dt,
    		sess_userid, --p_mod_user,
    		now(), --p_mod_dt,
        p_schedule_type,
    		p_comm
    
    	  )
    	  ;



      open p_cur for
      
  select a.*
       , (plan_edt - plan_sdt)+1 as plan_gap
       , case when dev_sdt is null then 'READY' 
              when dev_sdt is not null and dev_edt is null then 'RUNNING' 
              when dev_sdt is not null and dev_edt is not null then 'COMP' 
         end as wbs_state
    from projmng.dev_wbs a
   where 1=1
     and prj_rid = p_prj_rid::int
     and wbs_id = v_wbs_id
      ;



    else  -- update
	
        update projmng.dev_wbs
           set dev_sdt = case when nvl(to_ymd(p_dev_sdt), '') = '' then null else to_date(to_ymd(p_dev_sdt), 'YYYYMMDD')  end
             , dev_edt = case when nvl(to_ymd(p_dev_edt), '') = '' then null else to_date(to_ymd(p_dev_edt), 'YYYYMMDD')  end
             , plan_sdt = to_date(nvl(to_ymd(p_plan_sdt), nvl(to_ymd(p_dev_sdt), nvl(to_ymd(p_dev_edt), to_char(current_date, 'YYYYMMDD')))), 'YYYYMMDD')
             , plan_edt = to_date(nvl(to_ymd(p_plan_edt), nvl(to_ymd(p_dev_edt), to_char(current_date, 'YYYYMMDD'))), 'YYYYMMDD')
             , dev_user = nvl(p_dev_user, dev_user) 
             , mod_dt = now() 
      	     , proc_id = p_proc_id
             , proc_tp = p_proc_tp
             , proc_nm = p_proc_nm
             , comm = p_comm
          --   , qc_user = p_qc_user
             , schedule_type = p_schedule_type
             , prj_rid = p_prj_rid::int
         where wbs_id = p_wbs_id::int
         
            ;
    end if;



	elsif p_req_type = 'delete' then
	
    delete from projmng.dev_wbs
     where wbs_id = p_wbs_id::int
     
        ;

  elsif p_req_type = 'srch' then

    
    -- 데이터 정리 plan
    UPDATE dev_wbs
    SET 
        plan_sdt = COALESCE(plan_sdt, dev_edt),  -- plan_sdt가 null일 경우 dev_edt로 설정
        plan_edt = COALESCE(plan_edt, dev_edt),  -- plan_edt가 null일 경우 dev_edt로 설정
        dev_sdt  = COALESCE(dev_sdt, dev_edt)    -- dev_sdt가 null일 경우 dev_edt로 설정
    WHERE 
        prj_rid = p_prj_rid::int
        AND dev_edt IS NOT NULL
        AND (plan_sdt IS NULL OR plan_edt IS NULL OR dev_sdt IS NULL)
    ;
    
        
    
      open p_cur for
      
  select a.*
       , (plan_edt - plan_sdt)+1 as plan_gap
       , case when dev_sdt is null then 'READY' 
              when dev_sdt is not null and dev_edt is null then 'RUNNING' 
              when dev_sdt is not null and dev_edt is not null then 'COMP' 
         end as wbs_state
    from projmng.dev_wbs a
   where 1=1
     and prj_rid = p_prj_rid::int
     and ( ( nvl(p_compstat, '') = '' and 1=1 )
         or ( nvl(p_compstat, '') in ('READY') and plan_sdt is not null and plan_edt is not null and dev_sdt is null )
         or ( nvl(p_compstat, '') in ('RUNNING') and plan_sdt is not null and plan_edt is not null and dev_sdt is not null  and dev_edt is null  )
         or ( nvl(p_compstat, '') in ('DISCOMP') and plan_sdt is not null and plan_edt is not null and ( dev_sdt is null or dev_edt is null ) )
         or ( nvl(p_compstat, '') in ('COMP') and plan_sdt is not null and plan_edt is not null and dev_sdt is not null and dev_edt is not null )
         )

     and ( ( nvl(p_schedule_type, '') = '' and 1=1 )
         or ( nvl(p_schedule_type, '') != '' and schedule_type = p_schedule_type )
         )





   order 
      by proc_id, gb1, gb2, proc_tp, proc_nm, wbs_id
      ;

    

  end if;


	
	END;
	
$procedure$;

-- sp_proj_wbs_moniter(IN p_prj_rid character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_proj_wbs_moniter(IN p_prj_rid character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN

      open p_cur for
      
WITH base AS (
    -- 대상 프로젝트의 작업 정보 필터링
    SELECT *
    FROM dev_wbs
    WHERE prj_rid = p_prj_rid::int
),
counts AS (
    SELECT
        COUNT(*)::decimal AS total_task_count,  -- 전체 작업 건수

        COUNT(CASE 
                  WHEN dev_edt IS NOT NULL THEN 1 
             END) AS completed_task_count,  -- 개발 완료된 작업 수

        COUNT(CASE 
                  WHEN dev_edt IS NOT NULL AND dev_edt <= plan_edt 
             THEN 1 END) AS completed_within_plan_count,  -- 계획 기간 내 개발 완료 수

        COUNT(CASE 
                  WHEN dev_edt IS NULL 
                   AND plan_edt < CURRENT_DATE 
             THEN 1 END) AS delayed_task_count,  -- 계획 종료일이 지났지만 개발 미완료 (지연된 작업 수)

        COUNT(CASE 
                  WHEN dev_edt IS NULL 
                   AND plan_sdt <= CURRENT_DATE 
                   AND plan_edt >= CURRENT_DATE 
             THEN 1 END) AS in_progress_task_count,  -- 계획 중이며 개발 미완료 (진행 중 작업 수)

        COUNT(CASE 
                  WHEN dev_edt IS NULL 
                   AND plan_sdt > CURRENT_DATE 
             THEN 1 END) AS not_started_yet_task_count,  -- 계획 시작 전이며 개발 미완료 (미시작 작업 수)


        COUNT(CASE 
                  WHEN plan_sdt <= CURRENT_DATE 
             THEN 1 END) AS planneds_until_now_count,  -- 현시점까지 시작 예정이었던 전체 작업 수
             
        COUNT(CASE 
                  WHEN plan_edt <= CURRENT_DATE 
             THEN 1 END) AS planned_until_now_count  -- 현시점까지 종료 예정이었던 전체 작업 수

    FROM base
)

-- 최종 결과 출력
SELECT 
    total_task_count,  -- 전체 작업 수

    completed_task_count,  -- 완료된 작업 수
    ROUND(completed_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS completed_task_pct,  -- 완료율 (%)

    completed_task_count + in_progress_task_count as comp_and_ing_cnt,

    completed_within_plan_count,  -- 계획 내 완료 수
    ROUND(completed_within_plan_count * 100.0 / NULLIF(total_task_count, 0), 1) AS completed_within_plan_pct,  -- 계획 내 완료율 (%)

    delayed_task_count,  -- 지연된 작업 수
    ROUND(delayed_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS delayed_task_pct,  -- 지연율 (%)

    in_progress_task_count,  -- 진행 중 작업 수
    ROUND(in_progress_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS in_progress_task_pct,  -- 진행 중 비율 (%)

    not_started_yet_task_count,  -- 계획은 됐지만 시작 전 작업 수
    ROUND(not_started_yet_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS not_started_yet_pct,  -- 미시작 비율 (%)


    planneds_until_now_count,  -- 현시점까지 계획된 작업 수
    ROUND(planneds_until_now_count * 100.0 / NULLIF(total_task_count, 0), 1) AS planneds_until_now_pct,  -- 현시점까지 계획된 작업 비율 (%)
    
    planned_until_now_count,  -- 현시점까지 종료 계획된 작업 수
    ROUND(planned_until_now_count * 100.0 / NULLIF(total_task_count, 0), 1) AS planned_until_now_pct  -- 현시점까지 계획된 작업 비율 (%)


FROM counts;


	  
		
	
	END;
	
$procedure$;

-- sp_projcommon(IN ss_user_id character varying, IN p_code_id character varying, IN p_code_nm character varying, IN p_etc0 character varying, IN p_etc1 character varying, IN p_etc2 character varying, IN p_etc3 character varying, IN p_etc4 character varying, IN p_etc5 character varying, IN p_etc6 character varying, IN p_etc7 character varying, IN sess_userid character varying, INOUT p_cur refcursor)
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

    elsif p_code_id = 'user' then

      open p_cur for
      select a.user_id as code
           , a.user_name as name
           , '' as desc
           , a.*
        from projmng.dev_user a
      ; 


    elsif p_code_id = 'family' then

      open p_cur for
      select a.user_id as code
           , a.user_name as name
           , '' as desc
           , a.*
        from projmng.dev_user a
       inner join projmng.dev_user_grp_map b
          on a.user_id = b.user_id
      ; 



    end if;
		
	
    end if;

	END;
	
$procedure$;

-- sp_projdbdel(IN p_db_rid character varying, IN p_db_ip character varying, IN p_db_nick character varying, IN p_srch character varying, IN p_proj_id character varying, IN p_prj_rid character varying, IN p_proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdbdel(IN p_db_rid character varying, IN p_db_ip character varying, IN p_db_nick character varying, IN p_srch character varying, IN p_proj_id character varying, IN p_prj_rid character varying, IN p_proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN
	


delete from projmng.devdbinfo where db_rid = p_db_rid::int 
    ;

      open p_cur for      
  select 'xxxx' as test
   
   
      ;
		

	
	END;
	
$procedure$;

-- sp_projdblist(IN p_srch character varying, IN p_proj_rid character varying, IN p_proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdblist(IN p_srch character varying, IN p_proj_rid character varying, IN p_proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$


declare 
    
	BEGIN
	
      open p_cur for
      
  select a.*, b.prj_name, b.prj_nick
    from projmng.devdbinfo a
    left outer join projmng.dev_proj b
      on a.prj_rid = b.prj_rid
   where 1=1
     and ( ( nvl(p_proj_rid, '') = '' and 1=1  )
         or  ( nvl(p_proj_rid, '') != '' and a.prj_rid = p_proj_rid::int )
         )
   
      ;
		
	
	END;
	
$procedure$;

-- sp_projdblist2(IN p_srch character varying, IN proj_id character varying, IN proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdblist2(IN p_srch character varying, IN proj_id character varying, IN proj_nm character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN
	
      open p_cur for
      
  select *
    from devdbinfo a
   
   
      ;
		
	
	END;
	
$procedure$;

-- sp_projdbrspolist(IN p_dsl_type character varying, IN p_dsl_cd character varying, IN p_dsl_query character varying, IN sess_userid character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdbrspolist(IN p_dsl_type character varying, IN p_dsl_cd character varying, IN p_dsl_query character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN
	
      open p_cur for
      
  select b.dsl_cd
       , a.dsl_id
       , a.dsl_type
       , a.dsl_query
       , a.comm
    from projmng.devsqlresp_base b
    left outer join projmng.devsqlresp a
      on b.dsl_cd = a.dsl_cd
     and ( ( nvl(p_dsl_type, '') = '' and 1=1  )
         or  ( nvl(p_dsl_type, '') != '' and a.dsl_type = p_dsl_type )
         )
   where 1=1
   order by a.dsl_type, b.dsl_cd
      ;
		
	
	END;
	
$procedure$;

-- sp_projdbrspolist(IN p_dsl_type character varying, IN p_dsl_id character varying, IN p_dsl_cd character varying, IN p_dsl_query character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdbrspolist(IN p_dsl_type character varying, IN p_dsl_id character varying, IN p_dsl_cd character varying, IN p_dsl_query character varying, IN sess_userid character varying, IN p_req_type character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN


		if ( p_req_type = 'save' ) then

		
		
			if ( nvl(p_dsl_id, '') = '' ) then
	
			
				insert into projmng.devsqlresp
			    ( dsl_id, dsl_cd, dsl_type, dsl_query )
			    values
			    ( ( select max(dsl_id)+1 from projmng.devsqlresp ) , 
			      p_dsl_cd, 
			      p_dsl_type, 
			      p_dsl_query 
			    )
                ;
			else

				update projmng.devsqlresp
			       set dsl_query = p_dsl_query
			     where dsl_id = p_dsl_id::int
                ;

			end if;
		
		
		end if;


	
      open p_cur for
      
	  select b.dsl_cd
	       , a.dsl_id
	       , a.dsl_type
	       , a.dsl_query
	       , a.comm
	    from projmng.devsqlresp_base b
	    left outer join projmng.devsqlresp a
	      on b.dsl_cd = a.dsl_cd
	     and ( ( nvl(p_dsl_type, '') = '' and 1=1  )
	         or  ( nvl(p_dsl_type, '') != '' and a.dsl_type = p_dsl_type )
	         )
	   where 1=1
	   order by b.sort, a.dsl_type, b.dsl_cd
	      ;
		
	
	END;
	
$procedure$;

-- sp_projdbsave(IN p_db_rid character varying, IN p_db_ip character varying, IN p_db_nick character varying, IN p_srch character varying, IN p_proj_id character varying, IN p_prj_rid character varying, IN p_proj_nm character varying, IN p_db_cert character varying, IN p_db_type character varying, IN p_db_id character varying, IN p_db_pwd character varying, IN p_db_database character varying, IN p_db_port character varying, IN p_db_schema character varying, IN sess_userid character varying, INOUT p_cur refcursor)
CREATE OR REPLACE PROCEDURE projmng.sp_projdbsave(IN p_db_rid character varying, IN p_db_ip character varying, IN p_db_nick character varying, IN p_srch character varying, IN p_proj_id character varying, IN p_prj_rid character varying, IN p_proj_nm character varying, IN p_db_cert character varying, IN p_db_type character varying, IN p_db_id character varying, IN p_db_pwd character varying, IN p_db_database character varying, IN p_db_port character varying, IN p_db_schema character varying, IN sess_userid character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$

declare 
    
	BEGIN
	


if nvl(p_db_rid, '') != ''  then

  update projmng.devdbinfo
     set prj_rid = p_prj_rid::int
       , db_schema = p_db_schema
	   , db_ip        = p_db_ip        
	   , db_nick      = p_db_nick      
	   , db_cert      = p_db_cert      
	   , db_type      = p_db_type      
	   , db_id        = p_db_id        
	   , db_pwd       = p_db_pwd       
	   , db_database  = p_db_database  
	   , db_port      = p_db_port      
   where db_rid = p_db_rid::int
  ;


else
    
    insert into projmng.devdbinfo
    ( db_rid, db_ip, db_nick, db_cert, db_type, db_id, db_pwd, db_database, db_port, db_schema, prj_rid )
    values
    ( ( select max(db_rid)+1 from projmng.devdbinfo )
    , p_db_ip
    , p_db_nick
    , p_db_cert
    , p_db_type
    , p_db_id
    , p_db_pwd
    , p_db_database
    , p_db_port
    , p_db_schema
    , p_prj_rid::int
    )
    ;

      open p_cur for      
  select 'xxxx' as test
   
   
      ;
		

end if
;
	
	END;
	
$procedure$;

-- nvl(bigint, integer)
CREATE OR REPLACE FUNCTION projmng.nvl(bigint, integer)
 RETURNS text
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then null else $1::bigint end, null ), $2);
$function$;

-- nvl(bigint, text)
CREATE OR REPLACE FUNCTION projmng.nvl(bigint, text)
 RETURNS text
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then '' else $1::text end, '' ), $2);
$function$;

-- nvl(text, date)
CREATE OR REPLACE FUNCTION projmng.nvl(text, date)
 RETURNS date
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then null else $1::date end, null ), $2);
$function$;

-- nvl(text, text)
CREATE OR REPLACE FUNCTION projmng.nvl(text, text)
 RETURNS text
 LANGUAGE sql
 IMMUTABLE
AS $function$

select coalesce(nullif( $1, '' ), $2);
$function$;

-- nvl(text, timestamp with time zone)
CREATE OR REPLACE FUNCTION projmng.nvl(text, timestamp with time zone)
 RETURNS timestamp with time zone
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then null else $1::timestamp with time zone end, null ), $2);
$function$;

-- nvl(text, timestamp without time zone)
CREATE OR REPLACE FUNCTION projmng.nvl(text, timestamp without time zone)
 RETURNS timestamp without time zone
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then null else $1::timestamp end, null ), $2);
$function$;

-- nvl(timestamp with time zone, timestamp with time zone)
CREATE OR REPLACE FUNCTION projmng.nvl(timestamp with time zone, timestamp with time zone)
 RETURNS timestamp with time zone
 LANGUAGE sql
 IMMUTABLE
AS $function$


select coalesce(nullif( case when $1 is null then null else $1::timestamp with time zone end, null ), $2);
$function$;

-- to_ymd(date_text text)
CREATE OR REPLACE FUNCTION projmng.to_ymd(date_text text)
 RETURNS text
 LANGUAGE plpgsql
 IMMUTABLE
AS $function$
DECLARE
    dt DATE;
BEGIN

-- 빈 문자열이면 NULL 반환
IF trim(date_text) = '' THEN
    RETURN NULL;
END IF;


    -- 다양한 포맷 시도
    BEGIN
        dt := to_date(date_text, 'YYYY-MM-DD');
    EXCEPTION WHEN others THEN
        BEGIN
            dt := to_date(date_text, 'YYYY.MM.DD');
        EXCEPTION WHEN others THEN
            BEGIN
                dt := to_date(date_text, 'YYYY/MM/DD');
            EXCEPTION WHEN others THEN
                BEGIN
                    dt := to_date(left(date_text, 10), 'YYYY-MM-DD');
                EXCEPTION WHEN others THEN
                  BEGIN
                      dt := to_date(left(date_text, 8), 'YYYYMMDD');
                  EXCEPTION WHEN others THEN
                      RETURN NULL;
                  END;
                END;
            END;
        END;
    END;

    RETURN to_char(dt, 'YYYYMMDD');
END;
$function$;

