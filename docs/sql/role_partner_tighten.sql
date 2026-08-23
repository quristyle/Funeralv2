-- PARTNER 역할에서 관리자 화면을 닫는다
--
-- ⚠ 아직 실행하지 않았다. 권한 범위는 판단이 필요한 일이라 준비만 해 두었다.
--
-- 왜 필요한가
--   Q6-B 로 이관 계정 42개에 PARTNER 를 배정했다. 그런데 PARTNER 의 권한은
--   role_menu_backfill.sql 이 '메뉴가 쓴다고 지정한 항목은 모두 허용' 으로 채워 둔 상태다.
--   그래서 지금 PARTNER 는 계정 관리·권한 관리·메뉴 관리까지 등록·수정·삭제할 수 있다.
--
--   이관 계정의 비밀번호가 로그인 아이디와 같으므로(지시), 아이디를 아는 사람이
--   들어와 **관리자 계정을 만들 수 있는** 상태다. 이 스크립트는 그 경로를 닫는다.
--
-- 안전한가
--   PARTNER 역할은 이번 이관 전까지 **배정된 계정이 하나도 없었다.**
--   따라서 이 변경으로 영향을 받는 것은 이관 계정 42개뿐이고, 기존 사용자는 영향이 없다.
--
-- 무엇을 닫나
--   시스템 관리 · 권한 관리 · 설정 · 배포 도구 · 헬프데스크 설정 · 회사/조직 관리
--   (열람과 편집을 모두 끈다. 개인 설정 화면은 남긴다 — 자기 설정이라 필요하다.)
--
-- 반복 실행해도 안전하다.

UPDATE scom.role_menus rm
   SET can_view = false, can_search = false, can_create = false, can_update = false,
       can_delete = false, can_print = false, can_excel = false,
       can_cust1 = false, can_cust2 = false, can_cust3 = false, can_cust4 = false,
       can_cust5 = false, can_cust6 = false, can_cust7 = false, can_cust8 = false,
       updated_at = now(), updated_by = 'partner-tighten'
  FROM scom.system_menus m
 WHERE rm.menu_id = m.id
   AND rm.role_id = 'PARTNER'
   AND m.path <> '/helpdesk/system/user-properties'          -- 개인 설정은 남긴다
   AND (   m.path LIKE '/system%'
        OR m.path LIKE '/auth%'
        OR m.path LIKE '/setting%'
        OR m.path LIKE '/portal/release%'
        OR m.path LIKE '/helpdesk/system%'
        OR m.path LIKE '/company%' );

-- 확인 — 아래 조회 결과가 0건이어야 한다
--   SELECT m.path, m.title FROM scom.system_menus m
--     JOIN scom.role_menus rm ON rm.menu_id = m.id AND rm.role_id = 'PARTNER'
--    WHERE rm.can_view AND m.status = 1
--      AND (m.path LIKE '/system%' OR m.path LIKE '/auth%' OR m.path LIKE '/portal/release%');

-- ── 되돌리기 ────────────────────────────────────────────
-- 되돌리려면 역할 권한 화면에서 PARTNER 의 항목을 다시 켜거나,
-- role_menu_backfill.sql 을 다시 실행한다(그 스크립트는 '메뉴가 쓴다고 지정한 항목'을 모두 켠다).
