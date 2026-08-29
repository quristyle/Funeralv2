-- 5단계에서 지운 루틴 10개. 되돌릴 때 테이블보다 먼저 실행한다.
SET check_function_bodies = off;
SET search_path = "projmng", public;

-- sp_proj_login
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

-- sp_dev_user_exec
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

-- sp_dev_user_exec_all
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

-- sp_dev_user_prop_exec
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

-- sp_dev_user_grp_exec
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

-- sp_dev_user_grp_map_exec
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

-- sp_dev_menu_exec
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

-- sp_dev_menu_auth
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

-- sp_dev_grp_menu_map_exec
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

-- sp_dev_program_exec
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

