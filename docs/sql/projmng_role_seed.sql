-- ============================================================
-- 프로젝트관리 역할 — ASIS 그룹 권한을 포털 역할로 옮긴다
-- ============================================================
--
-- 배경: [36-projmng-tobe-feature-cleanup.md] 4.1
--   프로젝트관리 메뉴는 ADMINISTRATOR · SYSTEM_ADMINISTRATOR 에만 붙어 있었다.
--   ASIS 사용자 9명 중 8명은 PARTNER 라 화면이 하나도 보이지 않는다.
--
-- 무엇을 기준으로 삼았나
--   ASIS 는 `projmng.dev_grp_menu_map`(그룹→메뉴)으로 권한을 걸고 있었고,
--   `sp_dev_menu_auth` 가 로그인 사용자의 그룹으로 메뉴를 걸러 냈다.
--   그 자료를 그대로 포털 역할로 옮긴다 — 쓰던 사람이 쓰던 화면을 그대로 본다.
--
-- ASIS 그룹 → 포털 역할
--   Administrator → PROJMNG_ADMIN      (관리자 우회였다. 전체 화면)
--   JsiniTeam     → PROJMNG_JSINITEAM  (6화면)
--   MNM_SMG       → PROJMNG_MNM_SMG    (8화면)
--   Project       → 만들지 않았다. 유일한 구성원이 quristyle 이고
--                   그 화면(PM_DB_TABLE)은 PROJMNG_ADMIN 에 이미 들어 있다.
--   Family        → 만들지 않았다. 걸린 메뉴가 0개다.
--                   (구성원 hsstyle · jjstyle 은 ASIS 에서도 화면이 안 보였다)
--
-- ⚠ [DB 쿼리 테스터](PM_DB_TESTER)가 PROJMNG_MNM_SMG 에 들어간다.
--   이 화면은 임의 SQL 을 실행한다. 포털 규약은 SYSTEM_ADMINISTRATOR 단독이지만
--   **ASIS 에서 MNM_SMG 그룹(jskim · kggmvp)이 실제로 쓰던 화면**이라 그대로 옮겼다.
--   서버측 역할 가드(`DevTools:RawSqlRoles`)는 별개다 — 그쪽도 함께 열어야
--   실제로 동작한다. 열지 않으면 화면은 뜨고 실행만 403 이 된다.
--
-- 권한을 주지 않는 사람 (ASIS 와 같다)
--   bmkim · hsstyle · jjstyle · kspark · yws — ASIS 에서도 보이는 화면이 0개였다.
--
-- 반복 실행해도 안전하다. 이미 있으면 건너뛰고, 지워진 행이 있으면 되살린다.
-- ============================================================

BEGIN;

-- ── 1. 역할 ──────────────────────────────────────────────────
INSERT INTO scom.roles (id, name, status, remark, created_at, created_by, is_deleted)
SELECT v.id, v.name, 1, v.remark, now(), 'projmng-role-seed', false
FROM (VALUES
  ('PROJMNG_ADMIN',     '프로젝트관리 관리자',      'ASIS dev_user_grp_map 의 Administrator 그룹'),
  ('PROJMNG_JSINITEAM', '프로젝트관리 비영리개발팀', 'ASIS dev_user_grp: JsiniTeam'),
  ('PROJMNG_MNM_SMG',   '프로젝트관리 mnm안전',     'ASIS dev_user_grp: MNM_SMG')
) v(id, name, remark)
WHERE NOT EXISTS (SELECT 1 FROM scom.roles r WHERE r.id = v.id);

-- 지워진 상태로 남아 있으면 되살린다.
UPDATE scom.roles
   SET is_deleted = false, status = 1, updated_at = now(), updated_by = 'projmng-role-seed'
 WHERE id IN ('PROJMNG_ADMIN', 'PROJMNG_JSINITEAM', 'PROJMNG_MNM_SMG')
   AND (is_deleted OR status <> 1);

-- ── 2. 역할이 가질 화면 ──────────────────────────────────────
-- ASIS `dev_grp_menu_map` 을 옮긴 것이다. 오른쪽 주석이 ASIS 의 메뉴 이름이다.
CREATE TEMP TABLE tmp_pm_role_menu (role_id text, menu_id text) ON COMMIT DROP;

INSERT INTO tmp_pm_role_menu (role_id, menu_id) VALUES
  -- JsiniTeam(비영리개발팀) 6화면
  ('PROJMNG_JSINITEAM', 'PM_DESIGN_ERD'),      -- ERD
  ('PROJMNG_JSINITEAM', 'PM_DESIGN_USECASE'),  -- USE CASE
  ('PROJMNG_JSINITEAM', 'PM_PROJ_WBS'),        -- WBS
  ('PROJMNG_JSINITEAM', 'PM_PROJ_SCHED'),      -- Schedule
  ('PROJMNG_JSINITEAM', 'PM_DB_TABLE'),        -- 테이블 관리
  ('PROJMNG_JSINITEAM', 'PM_DB_TOOLS'),        -- Dev DB Tool
  -- MNM_SMG(mnm안전) 8화면
  ('PROJMNG_MNM_SMG',   'PM_DESIGN_ERD'),      -- ERD
  ('PROJMNG_MNM_SMG',   'PM_DESIGN_USECASE'),  -- USE CASE
  ('PROJMNG_MNM_SMG',   'PM_PROJ_WBS'),        -- WBS
  ('PROJMNG_MNM_SMG',   'PM_PROJ_SCHED'),      -- Schedule
  ('PROJMNG_MNM_SMG',   'PM_DB_TOOLS'),        -- Dev DB Tool
  ('PROJMNG_MNM_SMG',   'PM_DB_TESTER'),       -- DB Query  (위 ⚠ 참조)
  ('PROJMNG_MNM_SMG',   'PM_SRC_GLUE'),        -- glue server 추적
  ('PROJMNG_MNM_SMG',   'PM_DB_CODE');         -- 프로젝트 코드 정보

-- 관리자는 전체다. ASIS 의 Administrator 그룹이 그랬다.
INSERT INTO tmp_pm_role_menu (role_id, menu_id)
SELECT 'PROJMNG_ADMIN', m.id
  FROM scom.system_menus m
 WHERE (m.id = 'PROJMNG' OR m.id LIKE 'PM\_%')
   AND NOT m.is_deleted;

-- 화면만 줘서는 사이드바에 뜨지 않는다. 조상 폴더까지 함께 준다.
CREATE TEMP TABLE tmp_pm_grant ON COMMIT DROP AS
WITH RECURSIVE up(role_id, menu_id, pid) AS (
  SELECT r.role_id, m.id, m.pid
    FROM tmp_pm_role_menu r
    JOIN scom.system_menus m ON m.id = r.menu_id AND NOT m.is_deleted
  UNION
  SELECT u.role_id, p.id, p.pid
    FROM up u
    JOIN scom.system_menus p ON p.id = u.pid AND NOT p.is_deleted
)
SELECT DISTINCT role_id, menu_id FROM up;

-- ── 3. 역할-메뉴 권한 ────────────────────────────────────────
-- 세부 권한은 화면이 스스로 선언한 것(use_*)을 그대로 따른다.
-- 메뉴 시드(projmng_menu_seed.sql)가 ADMINISTRATOR 에 넣을 때 쓴 규칙과 같다.
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT g.role_id, m.id,
       true, (m.type = 'MENU'), m.use_create, m.use_delete, m.use_update, false, m.use_excel,
       false, false, false, false, false, false, false, false,
       now(), 'projmng-role-seed', false
  FROM tmp_pm_grant g
  JOIN scom.system_menus m ON m.id = g.menu_id
 WHERE NOT EXISTS (
   SELECT 1 FROM scom.role_menus rm
    WHERE rm.role_id = g.role_id AND rm.menu_id = m.id
 );

UPDATE scom.role_menus rm
   SET is_deleted = false, updated_at = now(), updated_by = 'projmng-role-seed'
  FROM tmp_pm_grant g
 WHERE rm.role_id = g.role_id AND rm.menu_id = g.menu_id AND rm.is_deleted;

-- ── 4. 사람에게 역할 주기 ────────────────────────────────────
-- ASIS `dev_user_grp_map` 그대로다.
CREATE TEMP TABLE tmp_pm_role_user (role_id text, user_id text) ON COMMIT DROP;

INSERT INTO tmp_pm_role_user (role_id, user_id) VALUES
  ('PROJMNG_ADMIN',     'quristyle'),  -- ASIS Administrator + Family + Project
  ('PROJMNG_JSINITEAM', 'jskim'),
  ('PROJMNG_JSINITEAM', 'sglee'),
  ('PROJMNG_MNM_SMG',   'jskim'),
  ('PROJMNG_MNM_SMG',   'kggmvp');

INSERT INTO scom.role_accounts (role_id, account_id, created_at, created_by, is_deleted)
SELECT v.role_id, a.id, now(), 'projmng-role-seed', false
  FROM tmp_pm_role_user v
  JOIN scom.accounts a ON a.user_id = v.user_id AND NOT a.is_deleted
 WHERE NOT EXISTS (
   SELECT 1 FROM scom.role_accounts ra
    WHERE ra.role_id = v.role_id AND ra.account_id = a.id
 );

UPDATE scom.role_accounts ra
   SET is_deleted = false, updated_at = now(), updated_by = 'projmng-role-seed'
  FROM tmp_pm_role_user v
  JOIN scom.accounts a ON a.user_id = v.user_id
 WHERE ra.role_id = v.role_id AND ra.account_id = a.id AND ra.is_deleted;

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- 역할별 화면 수 (폴더 제외)
SELECT rm.role_id, count(*) AS 화면
  FROM scom.role_menus rm
  JOIN scom.system_menus m ON m.id = rm.menu_id
 WHERE rm.role_id LIKE 'PROJMNG\_%' AND NOT rm.is_deleted AND m.type = 'MENU'
 GROUP BY 1 ORDER BY 1;

-- 사람별로 실제 보이게 된 프로젝트관리 화면 수
SELECT a.user_id,
       string_agg(DISTINCT ra.role_id, ', ' ORDER BY ra.role_id) AS 역할,
       count(DISTINCT m.id) AS 화면
  FROM scom.accounts a
  LEFT JOIN scom.role_accounts ra ON ra.account_id = a.id AND NOT ra.is_deleted
  LEFT JOIN scom.role_menus rm    ON rm.role_id = ra.role_id AND NOT rm.is_deleted
  LEFT JOIN scom.system_menus m   ON m.id = rm.menu_id AND m.type = 'MENU'
                                 AND m.id LIKE 'PM\_%' AND NOT m.is_deleted
 WHERE a.user_id IN ('bmkim','hsstyle','jjstyle','jskim','kggmvp',
                     'kspark','quristyle','sglee','yws')
 GROUP BY 1 ORDER BY 1;
