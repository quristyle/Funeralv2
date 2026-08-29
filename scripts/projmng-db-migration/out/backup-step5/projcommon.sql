-- 손대기 전 sp_projcommon. user·family 코드 분기가 살아 있는 판이다.
SET search_path = "projmng", public;

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
