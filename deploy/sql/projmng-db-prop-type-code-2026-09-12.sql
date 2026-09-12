-- DB 속성의 「구분」(projmng.dev_db_prop.db_ptype) 을 고르는 공통코드 묶음을 만든다
--
-- 건 곳: jin114.co.kr:31015/projmng · 스키마 projmng (개발·운영이 같은 DB 다)
-- 건 날: 2026-09-12
--
-- ─────────────────────────────────────────────────────────────
-- [왜 만들었나]
--
-- [프로젝트 DB 등록] 화면의 속성 「구분」은 글자를 손으로 받는 칸이었다.
-- 30건 전부가 비어 있다 — 손으로 적는 칸에 아무도 적지 않았다.
--
-- 그런데 **화면 두 곳은 이 칸에 값을 적고 있다.** ERD(ErdView) 와
-- 흐름도(FlowView) 가 배치를 저장할 때 db_ptype = 'diagram' 을 함께 넣는다.
-- 즉 뜻이 정해진 값이 이미 하나 있는데 사람은 그것을 알 길이 없었다.
--
-- 그래서 고르는 칸으로 바꾸고, 고를 목록을 여기서 정한다.
-- 화면이 아니라 코드 표에 두는 이유는 종류를 늘릴 때 배포가 없어야 해서다.
--
-- ─────────────────────────────────────────────────────────────
-- [값을 이렇게 정했다]
--
--   query     질의 — code_master · code_detail 처럼 개발 도구가 실행하는 SQL
--   template  뼈대 — sp_fmt (프로시저 생성 템플릿)
--   diagram   도형 — erd · flow 배치. **ErdView·FlowView 가 적는 글자 그대로다.**
--
-- 소문자다. 이미 저장되고 있는 'diagram' 이 소문자이고, 같은 표의
-- SOURCE_GB · SOURCE_LANG 도 소문자 코드를 쓴다.
--
-- 「기타」는 두지 않았다. 화면에서 비울 수 있게 해 두었으므로(비움 단추)
-- 구분 없는 속성은 그대로 빈 값이다 — 지금 30건이 그렇다.
--
-- 묶음 이름은 USER_PROP_TYPE(사용자속성구분) 과 짝을 맞춰 DB_PROP_TYPE 이다.
--
-- ─────────────────────────────────────────────────────────────
-- [옛 줄은 건드리지 않는다]
--
-- 이미 있는 30건의 db_ptype 을 채우지 않았다. 그 값을 읽는 코드가 없어
-- (검색·판정 어디에도 안 쓴다) 채워도 달라지는 것이 없고, 뜻을 우리가
-- 짐작해 넣으면 틀린 분류가 남는다. 화면에서 하나씩 고르면 된다.
--
-- ─────────────────────────────────────────────────────────────
-- [되돌리기]
--
--   DELETE FROM projmng.devcomm WHERE cm_pcd = 'DB_PROP_TYPE';
--   DELETE FROM projmng.devcomm WHERE cm_cd  = 'DB_PROP_TYPE' AND COALESCE(cm_pcd,'') = '';
--
-- 되돌리면 화면의 고르개가 빈 목록이 된다(값은 남는다).

BEGIN;

-- 묶음. cm_pcd 가 비어 있으면 묶음이라는 것이 이 표의 규칙이다.
-- cm_rid 에 시퀀스가 없어 max+1 로 만든다 — 화면(DevCommonCodeService)도 같다.
INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_type, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'DB_PROP_TYPE', 'DB속성구분', '', 'DataBase', 999
  FROM projmng.devcomm
 WHERE NOT EXISTS (
       SELECT 1 FROM projmng.devcomm
        WHERE cm_cd = 'DB_PROP_TYPE' AND COALESCE(cm_pcd, '') = '');

-- 코드 셋. 한 줄씩 넣는다 — max+1 이라 한 문장에 여럿을 넣으면 번호가 겹친다.
INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'query', '질의', 'DB_PROP_TYPE', 'query', 1
  FROM projmng.devcomm
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm
                    WHERE cm_pcd = 'DB_PROP_TYPE' AND cm_cd = 'query');

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'template', '뼈대', 'DB_PROP_TYPE', 'template', 2
  FROM projmng.devcomm
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm
                    WHERE cm_pcd = 'DB_PROP_TYPE' AND cm_cd = 'template');

INSERT INTO projmng.devcomm (cm_rid, cm_cd, cm_nm, cm_pcd, cm_val, cm_srt)
SELECT COALESCE(MAX(cm_rid), 0) + 1, 'diagram', '도형', 'DB_PROP_TYPE', 'diagram', 3
  FROM projmng.devcomm
 WHERE NOT EXISTS (SELECT 1 FROM projmng.devcomm
                    WHERE cm_pcd = 'DB_PROP_TYPE' AND cm_cd = 'diagram');

COMMIT;

-- 확인
--   SELECT cm_rid, cm_cd, cm_nm, cm_pcd, cm_srt
--     FROM projmng.devcomm
--    WHERE cm_cd = 'DB_PROP_TYPE' OR cm_pcd = 'DB_PROP_TYPE'
--    ORDER BY cm_pcd, cm_srt;
