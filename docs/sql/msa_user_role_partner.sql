-- 이관 계정에 PARTNER 역할 배정 (결정 Q6-B)
--
-- 왜 필요한가
--   역할이 하나도 없는 계정은 화면 접근 가드가 '막지 않는다'(fail-open).
--   그 규칙은 역할 없는 계정 2개가 통째로 잠기는 것을 막으려고 둔 것인데,
--   사용자 이관으로 역할 없는 계정이 44개가 되면서 규칙의 전제가 무너졌다.
--   역할을 배정하면 그 순간부터 role_menus 의 실제 권한이 적용된다.
--
-- 대상
--   scom.accounts 중 created_by = 'msa-user-import' (msa_user_import.sql 로 만든 42건)
--
-- ⚠ 지금 PARTNER 가 무엇을 볼 수 있는지 반드시 확인할 것
--   role_menu_backfill.sql 이 4개 역할 × 전 메뉴를 '메뉴가 쓴다고 지정한 항목은 모두 허용' 으로
--   채워 두었기 때문에, PARTNER 는 현재 **활성 화면 136개 중 105개**를 열람할 수 있고
--   그중 115개에서 등록·수정·삭제가 열려 있다(프로젝트관리 31개만 막혀 있다).
--   즉 이 스크립트만으로는 열람 범위가 좁아지지 않는다.
--   실제로 좁히려면 역할 권한 화면에서 PARTNER 의 항목을 꺼야 한다.
--
-- 반복 실행해도 안전하다((role_id, account_id) 유일 인덱스).

INSERT INTO scom.role_accounts (role_id, account_id, created_at, created_by, updated_at, updated_by, is_deleted)
SELECT 'PARTNER', a.id, now(), 'msa-user-import', now(), 'msa-user-import', false
  FROM scom.accounts a
 WHERE a.created_by = 'msa-user-import'
ON CONFLICT (role_id, account_id) DO NOTHING;

-- 확인
--   SELECT r.name, count(*) FROM scom.role_accounts ra
--     JOIN scom.roles r ON r.id = ra.role_id
--    WHERE ra.created_by = 'msa-user-import' GROUP BY r.name;

-- ── 되돌리기 ────────────────────────────────────────────
-- DELETE FROM scom.role_accounts WHERE created_by = 'msa-user-import';
