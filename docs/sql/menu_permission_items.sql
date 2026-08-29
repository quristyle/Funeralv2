-- ============================================================
-- 메뉴별 권한 항목 설정
-- ============================================================
--
-- scom.role_menus 는 메뉴마다 15가지 권한(열람·조회·추가·삭제·수정·출력·엑셀,
-- 사용자정의 1~8)을 들고 있지만, 어떤 메뉴가 그중 무엇을 실제로 쓰는지에 대한
-- 정보가 없었다. 그래서 역할 권한 화면이 모든 메뉴에 15개 체크박스를 똑같이
-- 보여주고, 사용자정의 칸은 'C1'~'C8' 이라는 뜻 없는 이름으로 나왔다.
--
-- 헬프데스크(JinReception)는 메뉴 테이블에 UseCreate/UseRead/UseUpdate/UseDelete 와
-- UseExt1~8 + Ext1Name~Ext8Name 을 두어 이 문제를 풀고 있었다.
-- 같은 기능을 포털의 scom.system_menus 에 옮긴다 (jsiniportal DB).
--
-- 반복 실행해도 안전하다.

BEGIN;

-- 기본 권한: 기존 화면 동작이 바뀌지 않도록 모두 사용(true)으로 시작한다.
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_view   boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_search boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_create boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_delete boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_update boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_print  boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_excel  boolean NOT NULL DEFAULT true;

-- 사용자 정의 권한: 이름을 붙여 켠 메뉴에서만 쓰이므로 기본은 꺼둔다.
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust1 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust2 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust3 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust4 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust5 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust6 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust7 boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust8 boolean NOT NULL DEFAULT false;

ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust1_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust2_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust3_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust4_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust5_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust6_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust7_name text;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust8_name text;

-- 디렉터리(CATALOG)와 상위 묶음 메뉴는 실제 화면이 없으므로 권한 항목도 필요 없다.
UPDATE scom.system_menus
SET use_view = false, use_search = false, use_create = false,
    use_delete = false, use_update = false, use_print = false, use_excel = false
WHERE upper(type) = 'CATALOG'
  AND use_view AND use_search AND use_create AND use_delete AND use_update AND use_print AND use_excel;

COMMIT;

-- 확인
SELECT type,
       count(*) AS 메뉴수,
       count(*) FILTER (WHERE use_view) AS 열람사용
FROM scom.system_menus
WHERE is_deleted = false
GROUP BY type
ORDER BY type;
