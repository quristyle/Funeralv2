-- ============================================================
-- 메뉴롤 화면(/auth/menu-role) — 권한 항목과 역할 권한
-- ============================================================
--
-- 지시: "/auth/menu-role 는 메뉴에 할당된 사용자·부서·회사를 보고 관리하는 화면이다."
--
-- 이 화면은 **권한 자체를 고치는 화면**이다. 역할↔메뉴 권한을 켜고 끄고,
-- 회사·부서·사람에게 걸린 역할을 해제한다.
--
-- ── 왜 권한을 좁히나 ──────────────────────────────────────
--
-- 손대기 전 상태를 재어 보니 **네 역할 모두 can_view·can_update 가 켜져 있었다**
-- (role_menu_backfill.sql 이 "메뉴가 쓴다고 지정한 항목은 모두 허용" 으로 채워 둔 탓).
--
-- 그대로 두면 파트너 계정 39개가 이 화면에서
--   · 자기 역할에 아무 메뉴나 열람 권한을 켤 수 있고
--   · 남의 회사·부서에 걸린 역할을 해제할 수 있다.
--
-- 즉 **권한을 스스로 올릴 수 있는 문**이 된다. 화면을 만들면서 그 문을 열어 둘 수는 없다.
-- 관리자 역할만 쓰게 한다.
--
--   ADMINISTRATOR          관리자        → 열람 + 수정
--   SYSTEM_ADMINISTRATOR   시스템관리자   → 열람 + 수정
--   PARTNER_ADMINISTRATOR  파트너 관리자  → 접근 없음
--   PARTNER                파트너        → 접근 없음
--
-- 관리자 역할 목록은 F.A.Q · 자료실과 같게 잡았다
-- (help_faq_qna.sql · help_archive.sql). PARTNER_ADMINISTRATOR 를 넣지 않은 이유도 같다 —
-- 이름은 관리자지만 파트너 쪽 관리자이고, 이 화면은 포털 전체의 권한을 다룬다.
-- → 21-help-faq-qna.md 의 D-H1 과 같은 건이다.
--
-- **읽기까지 막는 이유**: 이 화면은 어느 회사·부서·사람이 어떤 메뉴에 닿는지를
-- 통째로 보여 준다. 조직 구조가 그대로 드러나므로 열람도 관리자만 둔다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- ── 메뉴가 쓰는 권한 항목 ──────────────────────────────────
--
-- 이 화면은 '열람' 과 '수정' 만 쓴다. 등록·삭제·출력·엑셀은 쓰지 않는다 —
-- 새로 만들거나 지우는 것이 아니라 이미 있는 권한을 켜고 끄는 화면이다.
-- 쓰지 않는 항목을 켜 두면 역할 관리 화면에 쓸모없는 체크박스가 생기고,
-- 켜도 아무 일이 없어 "켰는데 왜 안 되지" 로 헤매게 된다.
UPDATE scom.system_menus SET
  use_view   = true,  use_search = true,
  use_create = false, use_update = true,  use_delete = false,
  use_print  = false, use_excel  = false,
  use_cust1  = false, cust1_name = NULL,
  status = 1, is_deleted = false,
  updated_at = now(), updated_by = 'menu-role-screen'
WHERE path = '/auth/menu-role';

COMMIT;


BEGIN;

-- 관리자 역할 — 열람·조회·수정을 켠다.
UPDATE scom.role_menus SET
  can_view = true, can_search = true, can_update = true,
  updated_at = now(), updated_by = 'menu-role-screen'
WHERE menu_id = 'MENU_ROLE'
  AND role_id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND (can_view = false OR can_search = false OR can_update = false);

-- 그 밖의 역할 — 전부 끈다.
-- 화면 열람 자체를 막으므로 라우터 가드가 /403 으로 보낸다.
UPDATE scom.role_menus SET
  can_view = false, can_search = false, can_update = false,
  can_create = false, can_delete = false, can_print = false, can_excel = false,
  updated_at = now(), updated_by = 'menu-role-screen'
WHERE menu_id = 'MENU_ROLE'
  AND role_id NOT IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND (can_view OR can_search OR can_update OR can_create OR can_delete
       OR can_print OR can_excel);

COMMIT;

-- 확인
SELECT r.name AS 역할, rm.can_view AS 열람, rm.can_search AS 조회,
       rm.can_update AS 수정, rm.can_create AS 등록, rm.can_delete AS 삭제
FROM scom.role_menus rm
JOIN scom.roles r ON r.id = rm.role_id
WHERE rm.menu_id = 'MENU_ROLE'
ORDER BY r.id;
