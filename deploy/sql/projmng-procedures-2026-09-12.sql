-- projmng 저장 프로시저 23개 — **걷어내기 전의 정본**
--
-- 2026-09-12 에 이 프로시저들의 로직을 ProjMngServer 로 옮기고 DB 에서 지웠다.
-- 이 파일은 그때의 원본이다. **되살리려고 두는 것이 아니라** 옮긴 것이
-- 맞는지 나중에 대조하려고 둔다 — 옮기면서 고친 것이 스무 가지가 넘고
-- (web/docs/commgrd-dynamicgrid-merge.md 에 적었다) 그 판단의 근거가 여기 있다.
--
-- 뽑은 곳: jin114.co.kr:31015/projmng · 스키마 projmng

-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
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

$procedure$



-- ─────────────────────────────────────────
CREATE OR REPLACE PROCEDURE projmng.sp_proj_user_map_list(IN p_prj_rid character varying, IN p_user_id character varying, INOUT p_cur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN

  OPEN p_cur FOR
  SELECT m.user_id                          -- 포털 로그인 아이디
       , t.inv_cnt                          -- 참여 프로젝트 수 (필터와 무관한 전체 기준)
       , m.prj_rid
       , p.prj_name
    FROM projmng.dev_proj_user_map m
    LEFT JOIN projmng.dev_proj p
      ON p.prj_rid = m.prj_rid
    JOIN ( SELECT user_id, count(*)::int AS inv_cnt
             FROM projmng.dev_proj_user_map
            GROUP BY user_id ) t
      ON t.user_id = m.user_id
   WHERE ( nvl(p_prj_rid, '') = '' OR m.prj_rid = p_prj_rid::int )
     AND ( nvl(p_user_id, '') = '' OR m.user_id = p_user_id )
   ORDER BY m.user_id, m.prj_rid;

END;
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$



-- ─────────────────────────────────────────
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
	
$procedure$


