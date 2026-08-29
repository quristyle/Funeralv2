-- 데모 · 샘플 메뉴와 그 권한을 지운다 (2026-08-28)
--
-- 지시: "해당 시스템에서 데모와 샘플을 모두 제거 시키고 싶다."
--
-- [무엇을 지우나]
--   · VbenProject (/vben-admin)  — vben 프로젝트 소개 · 외부 링크
--   · Examples    (/examples)    — 컴포넌트 예시
--   · Demos       (/demos)       — 기능 데모
--   · VbenAbout   (/vben-admin/about)
--   · SystemPermSample (/system/perm-sample) — 권한 제어 '샘플'
--   위 넷은 트리 전체(자손 포함)를 지운다.
--
-- [되돌리기] docs/sql/demo_menu_backup.sql 을 실행하면 지우기 직전 상태로 돌아온다.
--
-- [반복 실행 안전] 이름으로 뿌리를 찾으므로, 이미 지운 뒤 다시 돌리면 0건을 지운다.
--
-- 지운 뒤에는 **AuthServer 재기동 없이도** 메뉴가 사라진다(메뉴는 매번 DB 에서 읽는다).
-- 다만 이미 떠 있는 브라우저는 새로고침하거나 좌측 메뉴의 '메뉴 다시 읽기' 를 눌러야 한다.

BEGIN;

-- 지울 메뉴 = 데모 뿌리 넷의 트리 전체 + 권한 샘플 한 개
CREATE TEMP TABLE demo_menu_ids ON COMMIT DROP AS
WITH RECURSIVE roots AS (
    SELECT id
    FROM scom.system_menus
    WHERE is_deleted = false
      AND (
        -- 최상위에 있는 데모 뿌리들. 이름이 같은 하위 메뉴를 잘못 잡지 않도록
        -- pid 가 비어 있는 것으로 한정한다.
        (COALESCE(pid, '') = '' AND name IN ('Demos', 'Examples', 'VbenAbout', 'VbenProject'))
        -- 시스템 트리 안에 끼어 있는 권한 샘플
        OR name = 'SystemPermSample'
      )
),
tree AS (
    SELECT id FROM roots
    UNION ALL
    SELECT m.id
    FROM scom.system_menus m
    JOIN tree t ON m.pid = t.id
    WHERE m.is_deleted = false
)
SELECT DISTINCT id FROM tree;

-- 1) 역할↔메뉴 권한. 외래키가 걸려 있어 메뉴보다 먼저 지워야 한다.
DELETE FROM scom.role_menus
WHERE menu_id IN (SELECT id FROM demo_menu_ids);

-- 2) 즐겨찾기. 지금은 해당분이 없지만, 나중에 생겨도 막히지 않게 함께 지운다.
DELETE FROM scom.menu_favorites
WHERE menu_id IN (SELECT id FROM demo_menu_ids);

-- 3) 메뉴 자체.
DELETE FROM scom.system_menus
WHERE id IN (SELECT id FROM demo_menu_ids);

-- 4) 데모 전용 다국어 문구.
--
-- 남는 화면 어디에서도 이 접두사를 쓰지 않는 것을 확인하고 지운다
-- (`demos.` · `examples.` 로 시작하는 키는 전부 데모 것이다).
DELETE FROM scom.i18n_resources
WHERE key LIKE 'demos.%'
   OR key LIKE 'examples.%';

COMMIT;
