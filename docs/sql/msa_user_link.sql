-- 이관한 포털 계정 ↔ 헬프데스크 계정 연결
--
-- ⚠ 아직 실행하지 않았다. 이 파일은 **헬프데스크 DB(jinrecept)** 에 행을 넣는다.
--   "헬프데스크가 쓰는 DB 는 건드리지 말라" 는 지시를 지켜 준비만 해 두었다.
--
-- 무엇을 하나
--   msa_user_import.sql 로 만든 포털 계정(hd_*)을, 그 계정의 원본인
--   헬프데스크 담당자/고객 레코드에 이어 준다.
--   이걸 실행해야 그 계정으로 로그인했을 때 자기 요청·댓글·담당 건이 보인다.
--   (실행하지 않으면 계정은 있지만 헬프데스크 화면이 빈 채로 뜬다.)
--
-- 안전한가
--   · 대상 테이블은 jsini.auth_user_links 하나뿐이다. 매핑 전용 테이블이라
--     요청·고객·담당자 등 업무 데이터는 건드리지 않는다.
--   · createdby = 'msa-user-import' 로 표시하므로 정확히 골라 지울 수 있다.
--   · authuserid 에 유일 인덱스가 있어 반복 실행해도 행이 늘지 않는다(같은 계정이면 갱신).
--
-- 실행
--   PGPASSWORD=... psql -h jin114.co.kr -p 31015 -U jsini -d jinrecept -f docs/sql/msa_user_link.sql
--
-- 되돌리기
--   DELETE FROM jsini.auth_user_links WHERE createdby = 'msa-user-import';

BEGIN;


-- hd_a0516z (사용자D) → customer #14
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_a0516z', 'customer', 14, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_admin (사용자A) → admin #4
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_admin', 'admin', 4, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_choisunghyun (최성현) → customer #25
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_choisunghyun', 'customer', 25, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_dbrudfo (유경래) → customer #24
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_dbrudfo', 'customer', 24, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_eejinsu (이진수) → customer #27
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_eejinsu', 'customer', 27, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_ezuno (이준호부장) → customer #29
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_ezuno', 'customer', 29, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_frogtok (사용자D) → admin #9
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_frogtok', 'admin', 9, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_gyuyong (정규용) → customer #30
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_gyuyong', 'customer', 30, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_han_cust (한주담당xxx) → customer #18
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_han_cust', 'customer', 18, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_hj_dev (한주개발팀xx) → customer #8
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_hj_dev', 'customer', 8, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_incom2794 (incom) → customer #36
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_incom2794', 'customer', 36, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_jupark (사용자F) → customer #10
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_jupark', 'customer', 10, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_juparka (박부장) → admin #10
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_juparka', 'admin', 10, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_kdh (사용자B) → admin #6
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_kdh', 'admin', 6, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_kdh_c (사용자G) → customer #9
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_kdh_c', 'customer', 9, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_kggmvp (wwe) → admin #11
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_kggmvp', 'admin', 11, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_loveicy (이진문) → customer #35
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_loveicy', 'customer', 35, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_psw0102 (박수완) → customer #31
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_psw0102', 'customer', 31, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_1 (한주공통) → customer #7
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_1', 'customer', 7, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_10 (GHub공통) → customer #19
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_10', 'customer', 19, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_11 (SogoMail공통) → customer #26
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_11', 'customer', 26, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_12 (그리드위즈공통) → customer #32
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_12', 'customer', 32, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_13 (InCom공통) → customer #34
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_13', 'customer', 34, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_2 (회원가입공통) → customer #12
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_2', 'customer', 12, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_3 (진공통) → customer #11
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_3', 'customer', 11, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_7 (접수공통) → customer #5
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_7', 'customer', 5, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_pub_8 (미러포토공통) → customer #13
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_pub_8', 'customer', 13, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_puni (사용자C) → admin #8
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_puni', 'admin', 8, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_puni2 (우선) → customer #17
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_puni2', 'customer', 17, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_quristyle (사용자H) → customer #3
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_quristyle', 'customer', 3, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_sang9062 (유상원) → customer #33
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_sang9062', 'customer', 33, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_sardor2001 (sardor) → customer #37
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_sardor2001', 'customer', 37, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_suzymoon (사용자E) → admin #13
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_suzymoon', 'admin', 13, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

-- hd_uspuni (사용자C) → customer #4
INSERT INTO jsini.auth_user_links (authuserid, usertype, helpdeskuserid, createdat, createdby)
VALUES ('hd_uspuni', 'customer', 4, now(), 'msa-user-import')
ON CONFLICT (authuserid) DO UPDATE
   SET usertype = EXCLUDED.usertype,
       helpdeskuserid = EXCLUDED.helpdeskuserid,
       createdby = EXCLUDED.createdby;

COMMIT;

-- 확인
--   SELECT authuserid, usertype, helpdeskuserid FROM jsini.auth_user_links ORDER BY authuserid;
--   대상 34건 + 기존 수동 연결(quristyle → admin #4) 1건
