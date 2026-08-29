-- 5단계에서 지운 테이블 7개. 되돌릴 때 이 파일을 실행한다.
SET search_path = "projmng", public;

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
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('quristyle', '1', 'R', '사용자A', NULL, NULL, NULL, NULL, NULL, '010-0000-0000', NULL, NULL, NULL, NULL, NULL, NULL, '2020-01-15', NULL, '2022-01-22', 'https://www.siminsori.com/news/photo/201907/214004_63243_312.jpg', 'user15@example.invalid');
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('yws', '1', NULL, 'ywsname333', 'ywseng', NULL, NULL, NULL, NULL, '010', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'C', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('kspark', NULL, NULL, '박경수', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'https://cdn.pixabay.com/photo/2020/07/01/12/58/icon-5359553_640.png', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('bmkim', NULL, NULL, '김병만', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'https://cdn.pixabay.com/photo/2020/07/01/12/58/icon-5359553_640.png', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('jjstyle', '1', NULL, '이재준', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('hsstyle', '1', NULL, '이현서', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('kggmvp', 'a', NULL, '김원욱', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'https://fpost.co.kr/board/data/editor/2503/thumb-9c8412107f03c46c52c6d1625658b276_1741056098_1227_835x557.jpg', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('jskim', '1', NULL, '사용자D', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'https://cdn.pixabay.com/photo/2020/07/01/12/58/icon-5359553_640.png', NULL);
INSERT INTO projmng."dev_user" ("user_id", "upwd", "role_grp_id", "user_name", "user_name_eng", "emp_no", "cust_code", "dept_code", "office_num", "phone_num", "address_1", "address_2", "country", "use_yn", "remark", "reg_id", "reg_dt", "upt_id", "upt_dt", "user_photo", "email") VALUES ('sglee', '1', NULL, '이상기', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'https://cdn.imweb.me/upload/S202001098ea329692089a/368ed896f5fbb.jpg', NULL);
-- dev_user: 9행

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
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('kggmvp', 'LASTPAGE_OPEN_YN', 'True', NULL, '2025-07-06 16:40:08.339940', NULL, '2025-07-09 17:25:02.827414', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('kggmvp', 'SIDEBAR_AUTO_CLOSE', 'True', NULL, '2025-07-06 16:40:08.339940', NULL, '2025-07-09 17:25:02.827414', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('kggmvp', 'SERVER_URL', 'https://10.2.110.191:51669', NULL, '2025-07-07 11:38:35.882527', NULL, '2025-07-09 17:25:02.827414', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('sglee', 'LASTPAGE', 'ProjMngWasm.Pages.Proj.ProjWbs', NULL, '2025-09-14 22:32:54.204933', NULL, '2025-09-14 22:40:23.945197', NULL, NULL, 'WBS', NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('sglee', 'LASTPAGE_OPEN_YN', 'False', NULL, '2025-09-14 22:32:54.204933', NULL, '2025-09-14 22:40:23.945197', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('sglee', 'SIDEBAR_AUTO_CLOSE', 'False', NULL, '2025-09-14 22:32:54.204933', NULL, '2025-09-14 22:40:23.945197', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('sglee', 'FONTSIZE', '12', NULL, '2025-09-14 22:32:54.204933', NULL, '2025-09-14 22:40:23.945197', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('jskim', 'LASTPAGE', 'ProjMngWasm.Pages.Proj.ProjUseCase', NULL, '2025-09-09 13:13:34.292249', NULL, '2025-09-09 17:25:37.306181', NULL, NULL, 'USE CASE', NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('jskim', 'LASTPAGE_OPEN_YN', 'False', NULL, '2025-09-09 13:13:34.292249', NULL, '2025-09-09 17:25:37.306181', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('jskim', 'SIDEBAR_AUTO_CLOSE', 'False', NULL, '2025-09-09 13:13:34.292249', NULL, '2025-09-09 17:25:37.306181', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('jskim', 'FONTSIZE', '12', NULL, '2025-09-09 13:13:34.292249', NULL, '2025-09-09 17:25:37.306181', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'LASTPAGE', 'ProjMngWasm.Pages.Proj.ProjUserSetting', NULL, '2025-06-27 09:09:37.799789', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, 'User', NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'THEME', 'standard-dark', NULL, '2025-06-26 17:13:34.739226', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'LASTPAGE_OPEN_YN', 'True', NULL, '2025-06-27 10:45:49.935233', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'SIDEBAR_AUTO_CLOSE', 'False', NULL, '2025-06-27 16:31:39.512265', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'SERVER_URL', 'https://10.2.110.191:51669/', NULL, '2025-07-04 10:35:37.735369', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('quristyle', 'FONTSIZE', '14', NULL, '2025-07-09 15:22:42.287434', NULL, '2026-08-29 08:21:12.512202', NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_prop" ("user_id", "prop_type", "prop_val", "cre_id", "cre_dt", "mod_id", "mod_dt", "cre_user", "mod_user", "prop_val2", "prop_val3") VALUES ('kggmvp', 'LASTPAGE', 'ProjMngWasm.Pages.Proj.ProjUseCase', NULL, '2025-07-06 16:40:08.339940', NULL, '2025-07-09 17:25:02.827414', NULL, NULL, 'USE CASE', NULL);
-- dev_user_prop: 18행

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
INSERT INTO projmng."dev_user_grp" ("grp_id", "grp_name", "remark", "role_id", "use_yn", "cre_id", "cre_dt", "mod_id", "mod_dt", "grp_photo") VALUES ('Family', '가족', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_grp" ("grp_id", "grp_name", "remark", "role_id", "use_yn", "cre_id", "cre_dt", "mod_id", "mod_dt", "grp_photo") VALUES ('Project', '프로젝트', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO projmng."dev_user_grp" ("grp_id", "grp_name", "remark", "role_id", "use_yn", "cre_id", "cre_dt", "mod_id", "mod_dt", "grp_photo") VALUES ('MNM_SMG', 'mnm안전', '', '', '', NULL, NULL, NULL, NULL, '');
INSERT INTO projmng."dev_user_grp" ("grp_id", "grp_name", "remark", "role_id", "use_yn", "cre_id", "cre_dt", "mod_id", "mod_dt", "grp_photo") VALUES ('JsiniTeam', '비영리개발팀', '', '', '', NULL, NULL, NULL, NULL, '');
-- dev_user_grp: 4행

CREATE TABLE IF NOT EXISTS projmng."dev_user_grp_map" (
  "grp_id" character varying(30) NOT NULL,
  "user_id" character varying(30) NOT NULL,
  "remark" character varying(2000)
);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('Family', 'hsstyle', NULL);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('Family', 'jjstyle', NULL);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('Project', 'quristyle', NULL);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('MNM_SMG', 'kggmvp', '');
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('Family', 'quristyle', NULL);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('Administrator', 'quristyle', NULL);
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('MNM_SMG', 'jskim', '');
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('JsiniTeam', 'jskim', '');
INSERT INTO projmng."dev_user_grp_map" ("grp_id", "user_id", "remark") VALUES ('JsiniTeam', 'sglee', '');
-- dev_user_grp_map: 9행

CREATE TABLE IF NOT EXISTS projmng."dev_grp_menu_map" (
  "grp_id" character varying(30) NOT NULL,
  "menu_id" character varying(30) NOT NULL,
  "remark" character varying(2000)
);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '37', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '22', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '16', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '24', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '9', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '10', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '36', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('MNM_SMG', '26', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('Project', '11', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '26', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '22', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '16', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '24', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '9', NULL);
INSERT INTO projmng."dev_grp_menu_map" ("grp_id", "menu_id", "remark") VALUES ('JsiniTeam', '11', NULL);
-- dev_grp_menu_map: 15행

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
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.317927', NULL, '2025-06-20 09:32:23.527754', '30', 'ProjMonitoring', '모니터링', '2', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjMonitoring', '20', NULL, NULL, '/proj-monitoring', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.274803', NULL, '2025-06-17 16:16:37.853216', '29', 'ProjDbTester', 'DB Query', NULL, NULL, NULL, 'ProjMngWasm.Pages.Develop.ProjDbTester', '10', NULL, NULL, '/projdb-tester', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-10 16:35:23.293305', NULL, NULL, '29', 'SourceTraceMng', '소스 추적기', NULL, NULL, NULL, 'ProjMngWasm.Pages.Develop.SourceTraceMng', '44', NULL, NULL, '/source-trace', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:52:12.220340', NULL, NULL, 'ROOT', '', '시스템', NULL, NULL, NULL, '', '31', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.731830', NULL, '2025-06-20 09:32:59.204692', '30', 'ProjUseCase', 'USE CASE', '6', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjUseCase', '24', NULL, NULL, '/use-case', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-09-09 21:57:30.253372', NULL, '2025-09-09 22:00:40.903971', '30', 'luckysheet', 'luckysheet', '999', NULL, NULL, 'ProjMngWasm.Pages.Proj.LuckysheetPage', '45', NULL, NULL, 'luckysheet', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.115224', NULL, '2025-06-20 09:32:59.532251', '30', 'ProjFlow', 'Flows', '5', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjFlow', '18', NULL, NULL, '/flows', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:11.038503', NULL, '2025-06-20 09:34:54.439196', '31', 'DbLogic', 'DB Logic', '1', NULL, NULL, 'ProjMngWasm.Pages.Sys.DbLogic', '27', NULL, NULL, '/db-logic', 'adfadfadfs');
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:11.134021', NULL, '2025-06-20 09:34:54.574401', '31', 'DbLogicItem', 'DbLogicItem', '0', NULL, NULL, 'ProjMngWasm.Pages.Sys.DbLogicItem', '28', NULL, NULL, '/dblogic-item', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:41.264840', NULL, '2025-06-23 08:52:01.167745', 'ROOT', 'DevTools', '개발툴', '2', NULL, NULL, '', '29', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:14:38.083188', NULL, '2025-06-23 08:52:01.303331', 'ROOT', 'Projects', '프로젝트', '1', NULL, NULL, '', '30', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-23 15:49:18.555308', NULL, NULL, 'ROOT', '', '실험실', NULL, NULL, NULL, '', '34', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-23 15:51:15.860019', NULL, NULL, '34', 'ProjFastTest', '다이나믹그리드개선', NULL, NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjFastTest', '35', NULL, NULL, '/proj-fasttest', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.909518', NULL, '2025-06-24 10:32:47.448876', '30', 'ProjERD', 'ERD', '5', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjERD', '16', NULL, NULL, '/erd', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.522283', NULL, '2025-06-24 10:32:47.621713', '30', 'ProjScheduler', 'Schedule', '4', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjScheduler', '22', NULL, NULL, '/proj-shceduler', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.937497', NULL, '2025-06-25 13:19:23.079375', '30', 'ProjWbs', 'WBS', '3', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjWbs', '26', NULL, NULL, '/proj-wbs', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.621548', NULL, '2025-07-02 15:13:53.801973', '30', 'ProjSource', 'Source', '2', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjSource', '23', NULL, NULL, '/proj-src', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-02 17:21:32.137242', NULL, '2025-07-02 17:21:50.366872', '30', 'ProjCodeMng', '프로젝트 코드 정보', '2', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjCodeMng', '37', NULL, NULL, '/proj-code-mng', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:01.948830', NULL, NULL, '1', 'Signin', '/login', NULL, NULL, NULL, 'ProjMngWasm.Layout.Signin', '38', NULL, NULL, '/login', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:02.096299', NULL, NULL, '1', 'Funeralfr', '/funeral', NULL, NULL, NULL, 'ProjMngWasm.Pages.Funeralfr', '39', NULL, NULL, '/funeral', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:02.296315', NULL, NULL, '1', 'FuneralfrData', 'funeralw', NULL, NULL, NULL, 'ProjMngWasm.Pages.FuneralfrData', '40', NULL, NULL, '/funeralw', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:02.451276', NULL, NULL, '1', 'Jsini', '/jsini', NULL, NULL, NULL, 'ProjMngWasm.Pages.Jsini', '41', NULL, NULL, '/jsini', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:02.750654', NULL, NULL, '1', 'ProjUserSetting', 'User', NULL, NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjUserSetting', '43', NULL, NULL, '/proj-user-setting', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-04 11:49:02.603306', NULL, '2025-07-04 11:49:21.934653', '1', 'UserGroupMng', '권한그룹관리', '3', NULL, NULL, 'ProjMngWasm.Pages.Comm.UserGroupMng', '42', NULL, NULL, '/user-group-manager', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-07-01 17:39:40.387833', NULL, '2025-07-07 16:20:07.934598', '29', 'GlueTraceMng', 'glue server 추적', '999', NULL, NULL, 'ProjMngWasm.Pages.Develop.GlueTraceMng', '36', NULL, NULL, '/glue', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.480926', NULL, NULL, '2', 'HomeTodo', '할일 목록', NULL, NULL, NULL, 'ProjMngWasm.Pages.Home.HomeTodo', '12', NULL, NULL, '/home-todo', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.584749', NULL, NULL, '2', 'HomeTodoMonitor', '할일 목록 모니터링', NULL, NULL, NULL, 'ProjMngWasm.Pages.Home.HomeTodoMonitor', '13', NULL, NULL, '/home-todo-monitor', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.683400', NULL, NULL, '2', 'ProjComTest', 'projcombotest', NULL, NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjComTest', '14', NULL, NULL, '/projcombotest', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.420499', NULL, NULL, '2', 'ProjScaner', '소스 정보', NULL, NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjScaner', '21', NULL, NULL, '/proj-scaner', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.174838', NULL, '2025-06-17 16:14:21.533626', '29', 'DBTools', 'Dev DB Tool', NULL, NULL, NULL, 'ProjMngWasm.Pages.Develop.DBTools', '9', NULL, NULL, '/dbtool', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.387188', NULL, '2025-06-17 16:14:21.617671', '29', 'ProjTableMng', '테이블 관리', NULL, NULL, NULL, 'ProjMngWasm.Pages.Develop.ProjTableMng', '11', NULL, NULL, '/proj-table-comment', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-16 17:33:41.560474', NULL, '2025-06-17 17:00:09.872958', '30', 'Schedules', '일정관리', NULL, NULL, NULL, '', '2', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-16 17:30:44.604079', NULL, '2025-06-19 11:30:30.062023', 'ROOT', 'Common', '공통', '0', NULL, NULL, '', '1', NULL, NULL, '', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.833293', NULL, '2025-06-19 11:35:15.814201', '1', 'ProjUser', '사용자관리', '2', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjUser', '25', NULL, NULL, '/proj-user', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-18 10:20:18.837494', NULL, '2025-06-19 11:35:16.026524', '1', 'CommCodeMng', '공통코드관리', '0', NULL, NULL, 'ProjMngWasm.Pages.Comm.CommCodeMng', '32', NULL, NULL, '/commcode', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-18 10:20:18.952464', NULL, '2025-06-19 11:35:16.169078', '1', 'MenuMng', '메뉴관리', '1', NULL, NULL, 'ProjMngWasm.Pages.Comm.MenuMng', '33', NULL, NULL, '/menumng', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:09.802291', NULL, '2025-06-20 09:31:21.178275', '30', 'ProjDb', '프로젝트 DB', '1', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjDb', '15', NULL, NULL, '/projdb', NULL);
INSERT INTO projmng."dev_menu" ("cre_id", "cre_dt", "mod_id", "mod_dt", "owner_id", "mnu_cd", "mnu_nm", "disp_seq", "parent_mnu_cd", "mnu_grp_yn", "pgm_id", "mnu_id", "use_yn", "pgm_ty", "mnu_url", "mnu_desc") VALUES (NULL, '2025-06-17 16:07:10.217784', NULL, '2025-06-20 09:31:21.313982', '30', 'ProjMng', '프로젝트', '0', NULL, NULL, 'ProjMngWasm.Pages.Proj.ProjMng', '19', NULL, NULL, '/projmng', NULL);
-- dev_menu: 38행

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
-- dev_menu_favorites: 0행

