-- 헬프데스크·프로젝트관리 사용자를 JSini 포털 계정으로 이관
--
--   원본 : jsini.admin(7) · jsini.customer(27) · projmng.dev_user(9)
--   대상 : scom.accounts + scom.account_profile_details
--
-- 규칙
--   · 로그인 아이디에 출처 접두어를 붙인다 (hd_ / pm_).
--     원본 아이디를 그대로 쓰면 서로 다른 사람이 겹친다 —
--     헬프데스크 admin(사용자A) ↔ 포털 admin(미르작은사장님),
--     헬프데스크 고객 quristyle(사용자H) ↔ 포털 quristyle(사용자A).
--   · 원본 아이디와 이름이 **둘 다** 같은 포털 계정이 있으면 같은 사람으로 보고 만들지 않는다.
--   · 비밀번호는 로그인 아이디와 같은 값이다(지시). PBKDF2-HMAC-SHA256 600,000회로 해시해 넣는다.
--     ⚠ 추측 가능한 값이므로 실제로 쓰기 전에 반드시 바꿔야 한다.
--   · 원본에서 삭제 표시된 계정은 Status=DISABLED 로 넣는다(계정 목록에 '비활성'으로 보인다).
--   · 회사·부서는 비워 둔다. 포털에는 원본의 회사가 없고, 회사를 넣으면 부서까지 맞춰야 한다
--     (accounts 의 (company_id, department_id) 복합 외래키). 원본 회사명은 MsaCompany 로 남긴다.
--   · created_by = 'msa-user-import' 로 표시한다. 되돌릴 때 이 표시로 정확히 골라낸다.
--
-- 반복 실행해도 안전하다(이미 있으면 건너뛴다). 비밀번호는 다시 만들지 않는다.

BEGIN;


-- helpdesk:admin:4  사용자A
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-4', 'hd_admin', '사용자A', '사용자A', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-4-status', 'msa-hd-adm-4', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-4-homepath', 'msa-hd-adm-4', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-4-msasource', 'msa-hd-adm-4', 'MsaSource', 'helpdesk:admin:4', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-4-email', 'msa-hd-adm-4', 'Email', 'quristyle@jinnets.co.kr', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:6  사용자B
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-6', 'hd_kdh', '사용자B', '사용자B', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-6-status', 'msa-hd-adm-6', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-6-homepath', 'msa-hd-adm-6', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-6-msasource', 'msa-hd-adm-6', 'MsaSource', 'helpdesk:admin:6', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-6-email', 'msa-hd-adm-6', 'Email', 'user09@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:8  사용자C
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-8', 'hd_puni', '사용자C', '사용자C', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-8-status', 'msa-hd-adm-8', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-8-homepath', 'msa-hd-adm-8', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-8-msasource', 'msa-hd-adm-8', 'MsaSource', 'helpdesk:admin:8', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-8-email', 'msa-hd-adm-8', 'Email', 'user18@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:9  사용자D
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-9', 'hd_frogtok', '사용자D', '사용자D', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-9-status', 'msa-hd-adm-9', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-9-homepath', 'msa-hd-adm-9', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-9-msasource', 'msa-hd-adm-9', 'MsaSource', 'helpdesk:admin:9', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-9-email', 'msa-hd-adm-9', 'Email', 'user01@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:10  박부장
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-10', 'hd_juparka', '박부장', '박부장', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-10-status', 'msa-hd-adm-10', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-10-homepath', 'msa-hd-adm-10', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-10-msasource', 'msa-hd-adm-10', 'MsaSource', 'helpdesk:admin:10', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:11  wwe
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-11', 'hd_kggmvp', 'wwe', 'wwe', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-11-status', 'msa-hd-adm-11', 'Status', 'DISABLED', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-11-homepath', 'msa-hd-adm-11', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-11-msasource', 'msa-hd-adm-11', 'MsaSource', 'helpdesk:admin:11', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:admin:13  사용자E
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-adm-13', 'hd_suzymoon', '사용자E', '사용자E', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-13-status', 'msa-hd-adm-13', 'Status', 'DISABLED', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-13-homepath', 'msa-hd-adm-13', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-13-msasource', 'msa-hd-adm-13', 'MsaSource', 'helpdesk:admin:13', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-adm-13-email', 'msa-hd-adm-13', 'Email', 'user02@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:3  사용자H
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-3', 'hd_quristyle', '사용자H', '사용자H', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-3-status', 'msa-hd-cus-3', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-3-homepath', 'msa-hd-cus-3', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-3-msasource', 'msa-hd-cus-3', 'MsaSource', 'helpdesk:customer:3', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-3-email', 'msa-hd-cus-3', 'Email', 'user15@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-3-msacompany', 'msa-hd-cus-3', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:4  사용자C
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-4', 'hd_uspuni', '사용자C', '사용자C', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-4-status', 'msa-hd-cus-4', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-4-homepath', 'msa-hd-cus-4', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-4-msasource', 'msa-hd-cus-4', 'MsaSource', 'helpdesk:customer:4', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-4-email', 'msa-hd-cus-4', 'Email', 'user18@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-4-msacompany', 'msa-hd-cus-4', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:5  접수공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-5', 'hd_pub_7', '접수공통', '접수공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-5-status', 'msa-hd-cus-5', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-5-homepath', 'msa-hd-cus-5', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-5-msasource', 'msa-hd-cus-5', 'MsaSource', 'helpdesk:customer:5', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-5-email', 'msa-hd-cus-5', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-5-msacompany', 'msa-hd-cus-5', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:7  한주공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-7', 'hd_pub_1', '한주공통', '한주공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-7-status', 'msa-hd-cus-7', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-7-homepath', 'msa-hd-cus-7', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-7-msasource', 'msa-hd-cus-7', 'MsaSource', 'helpdesk:customer:7', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-7-email', 'msa-hd-cus-7', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-7-msacompany', 'msa-hd-cus-7', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:8  한주개발팀xx
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-8', 'hd_hj_dev', '한주개발팀xx', '한주개발팀xx', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-8-status', 'msa-hd-cus-8', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-8-homepath', 'msa-hd-cus-8', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-8-msasource', 'msa-hd-cus-8', 'MsaSource', 'helpdesk:customer:8', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-8-email', 'msa-hd-cus-8', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-8-msacompany', 'msa-hd-cus-8', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:9  사용자G
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-9', 'hd_kdh_c', '사용자G', '사용자G', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-9-status', 'msa-hd-cus-9', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-9-homepath', 'msa-hd-cus-9', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-9-msasource', 'msa-hd-cus-9', 'MsaSource', 'helpdesk:customer:9', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-9-email', 'msa-hd-cus-9', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-9-msacompany', 'msa-hd-cus-9', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:10  사용자F
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-10', 'hd_jupark', '사용자F', '사용자F', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-10-status', 'msa-hd-cus-10', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-10-homepath', 'msa-hd-cus-10', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-10-msasource', 'msa-hd-cus-10', 'MsaSource', 'helpdesk:customer:10', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-10-email', 'msa-hd-cus-10', 'Email', 'user10@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-10-msacompany', 'msa-hd-cus-10', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:11  진공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-11', 'hd_pub_3', '진공통', '진공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-11-status', 'msa-hd-cus-11', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-11-homepath', 'msa-hd-cus-11', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-11-msasource', 'msa-hd-cus-11', 'MsaSource', 'helpdesk:customer:11', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-11-email', 'msa-hd-cus-11', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-11-msacompany', 'msa-hd-cus-11', 'MsaCompany', '진네트웍스', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:12  회원가입공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-12', 'hd_pub_2', '회원가입공통', '회원가입공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-12-status', 'msa-hd-cus-12', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-12-homepath', 'msa-hd-cus-12', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-12-msasource', 'msa-hd-cus-12', 'MsaSource', 'helpdesk:customer:12', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-12-email', 'msa-hd-cus-12', 'Email', 'user03@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-12-msacompany', 'msa-hd-cus-12', 'MsaCompany', '회원가입', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:13  미러포토공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-13', 'hd_pub_8', '미러포토공통', '미러포토공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-13-status', 'msa-hd-cus-13', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-13-homepath', 'msa-hd-cus-13', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-13-msasource', 'msa-hd-cus-13', 'MsaSource', 'helpdesk:customer:13', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-13-email', 'msa-hd-cus-13', 'Email', 'user11@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-13-msacompany', 'msa-hd-cus-13', 'MsaCompany', '미러포트', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:14  사용자D
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-14', 'hd_a0516z', '사용자D', '사용자D', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-14-status', 'msa-hd-cus-14', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-14-homepath', 'msa-hd-cus-14', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-14-msasource', 'msa-hd-cus-14', 'MsaSource', 'helpdesk:customer:14', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-14-email', 'msa-hd-cus-14', 'Email', 'user01@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-14-msacompany', 'msa-hd-cus-14', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:17  우선
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-17', 'hd_puni2', '우선', '우선', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-17-status', 'msa-hd-cus-17', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-17-homepath', 'msa-hd-cus-17', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-17-msasource', 'msa-hd-cus-17', 'MsaSource', 'helpdesk:customer:17', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-17-email', 'msa-hd-cus-17', 'Email', 'user18@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-17-msacompany', 'msa-hd-cus-17', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:18  한주담당xxx
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-18', 'hd_han_cust', '한주담당xxx', '한주담당xxx', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-18-status', 'msa-hd-cus-18', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-18-homepath', 'msa-hd-cus-18', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-18-msasource', 'msa-hd-cus-18', 'MsaSource', 'helpdesk:customer:18', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-18-email', 'msa-hd-cus-18', 'Email', 'xxxx@hanjucorp.co.kr', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-18-msacompany', 'msa-hd-cus-18', 'MsaCompany', '접수시스템', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:19  GHub공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-19', 'hd_pub_10', 'GHub공통', 'GHub공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-19-status', 'msa-hd-cus-19', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-19-homepath', 'msa-hd-cus-19', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-19-msasource', 'msa-hd-cus-19', 'MsaSource', 'helpdesk:customer:19', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-19-email', 'msa-hd-cus-19', 'Email', 'sk gas@company.com', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-19-msacompany', 'msa-hd-cus-19', 'MsaCompany', 'GHUB', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:24  유경래
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-24', 'hd_dbrudfo', '유경래', '유경래', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-24-status', 'msa-hd-cus-24', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-24-homepath', 'msa-hd-cus-24', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-24-msasource', 'msa-hd-cus-24', 'MsaSource', 'helpdesk:customer:24', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-24-email', 'msa-hd-cus-24', 'Email', 'user04@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-24-msacompany', 'msa-hd-cus-24', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:25  최성현
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-25', 'hd_choisunghyun', '최성현', '최성현', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-25-status', 'msa-hd-cus-25', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-25-homepath', 'msa-hd-cus-25', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-25-msasource', 'msa-hd-cus-25', 'MsaSource', 'helpdesk:customer:25', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-25-email', 'msa-hd-cus-25', 'Email', 'user16@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-25-msacompany', 'msa-hd-cus-25', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:26  SogoMail공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-26', 'hd_pub_11', 'SogoMail공통', 'SogoMail공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-26-status', 'msa-hd-cus-26', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-26-homepath', 'msa-hd-cus-26', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-26-msasource', 'msa-hd-cus-26', 'MsaSource', 'helpdesk:customer:26', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-26-email', 'msa-hd-cus-26', 'Email', 'SogoMail@company.com', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-26-msacompany', 'msa-hd-cus-26', 'MsaCompany', 'SogoMail', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:27  이진수
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-27', 'hd_eejinsu', '이진수', '이진수', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-27-status', 'msa-hd-cus-27', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-27-homepath', 'msa-hd-cus-27', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-27-msasource', 'msa-hd-cus-27', 'MsaSource', 'helpdesk:customer:27', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-27-email', 'msa-hd-cus-27', 'Email', 'user05@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-27-msacompany', 'msa-hd-cus-27', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:29  이준호부장
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-29', 'hd_ezuno', '이준호부장', '이준호부장', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-29-status', 'msa-hd-cus-29', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-29-homepath', 'msa-hd-cus-29', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-29-msasource', 'msa-hd-cus-29', 'MsaSource', 'helpdesk:customer:29', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-29-email', 'msa-hd-cus-29', 'Email', 'user06@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-29-msacompany', 'msa-hd-cus-29', 'MsaCompany', 'GHUB', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:30  정규용
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-30', 'hd_gyuyong', '정규용', '정규용', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-30-status', 'msa-hd-cus-30', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-30-homepath', 'msa-hd-cus-30', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-30-msasource', 'msa-hd-cus-30', 'MsaSource', 'helpdesk:customer:30', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-30-email', 'msa-hd-cus-30', 'Email', 'user07@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-30-msacompany', 'msa-hd-cus-30', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:31  박수완
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-31', 'hd_psw0102', '박수완', '박수완', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-31-status', 'msa-hd-cus-31', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-31-homepath', 'msa-hd-cus-31', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-31-msasource', 'msa-hd-cus-31', 'MsaSource', 'helpdesk:customer:31', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-31-email', 'msa-hd-cus-31', 'Email', 'user14@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-31-msacompany', 'msa-hd-cus-31', 'MsaCompany', '한주', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:32  그리드위즈공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-32', 'hd_pub_12', '그리드위즈공통', '그리드위즈공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-32-status', 'msa-hd-cus-32', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-32-homepath', 'msa-hd-cus-32', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-32-msasource', 'msa-hd-cus-32', 'MsaSource', 'helpdesk:customer:32', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-32-email', 'msa-hd-cus-32', 'Email', '그리드위즈@company.com', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-32-msacompany', 'msa-hd-cus-32', 'MsaCompany', '그리드위즈', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:33  유상원
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-33', 'hd_sang9062', '유상원', '유상원', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-33-status', 'msa-hd-cus-33', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-33-homepath', 'msa-hd-cus-33', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-33-msasource', 'msa-hd-cus-33', 'MsaSource', 'helpdesk:customer:33', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-33-email', 'msa-hd-cus-33', 'Email', 'user17@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-33-msacompany', 'msa-hd-cus-33', 'MsaCompany', '그리드위즈', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:34  InCom공통
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-34', 'hd_pub_13', 'InCom공통', 'InCom공통', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-34-status', 'msa-hd-cus-34', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-34-homepath', 'msa-hd-cus-34', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-34-msasource', 'msa-hd-cus-34', 'MsaSource', 'helpdesk:customer:34', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-34-email', 'msa-hd-cus-34', 'Email', 'InCom@company.com', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-34-msacompany', 'msa-hd-cus-34', 'MsaCompany', 'InCom', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:35  이진문
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-35', 'hd_loveicy', '이진문', '이진문', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-35-status', 'msa-hd-cus-35', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-35-homepath', 'msa-hd-cus-35', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-35-msasource', 'msa-hd-cus-35', 'MsaSource', 'helpdesk:customer:35', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-35-email', 'msa-hd-cus-35', 'Email', 'user13@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-35-msacompany', 'msa-hd-cus-35', 'MsaCompany', 'InCom', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:36  incom
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-36', 'hd_incom2794', 'incom', 'incom', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-36-status', 'msa-hd-cus-36', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-36-homepath', 'msa-hd-cus-36', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-36-msasource', 'msa-hd-cus-36', 'MsaSource', 'helpdesk:customer:36', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-36-email', 'msa-hd-cus-36', 'Email', 'user08@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-36-msacompany', 'msa-hd-cus-36', 'MsaCompany', 'InCom', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- helpdesk:customer:37  sardor
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-hd-cus-37', 'hd_sardor2001', 'sardor', 'sardor', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-37-status', 'msa-hd-cus-37', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-37-homepath', 'msa-hd-cus-37', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-37-msasource', 'msa-hd-cus-37', 'MsaSource', 'helpdesk:customer:37', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-37-email', 'msa-hd-cus-37', 'Email', 'user12@example.invalid', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-hd-cus-37-msacompany', 'msa-hd-cus-37', 'MsaCompany', 'InCom', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:bmkim  김병만
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-bmkim', 'pm_bmkim', '김병만', '김병만', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-bmkim-status', 'msa-pm-bmkim', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-bmkim-homepath', 'msa-pm-bmkim', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-bmkim-msasource', 'msa-pm-bmkim', 'MsaSource', 'projmng:dev_user:bmkim', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:hsstyle  이현서
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-hsstyle', 'pm_hsstyle', '이현서', '이현서', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-hsstyle-status', 'msa-pm-hsstyle', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-hsstyle-homepath', 'msa-pm-hsstyle', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-hsstyle-msasource', 'msa-pm-hsstyle', 'MsaSource', 'projmng:dev_user:hsstyle', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:jjstyle  이재준
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-jjstyle', 'pm_jjstyle', '이재준', '이재준', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jjstyle-status', 'msa-pm-jjstyle', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jjstyle-homepath', 'msa-pm-jjstyle', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jjstyle-msasource', 'msa-pm-jjstyle', 'MsaSource', 'projmng:dev_user:jjstyle', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:jskim  사용자D
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-jskim', 'pm_jskim', '사용자D', '사용자D', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jskim-status', 'msa-pm-jskim', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jskim-homepath', 'msa-pm-jskim', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-jskim-msasource', 'msa-pm-jskim', 'MsaSource', 'projmng:dev_user:jskim', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:kggmvp  김원욱
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-kggmvp', 'pm_kggmvp', '김원욱', '김원욱', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kggmvp-status', 'msa-pm-kggmvp', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kggmvp-homepath', 'msa-pm-kggmvp', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kggmvp-msasource', 'msa-pm-kggmvp', 'MsaSource', 'projmng:dev_user:kggmvp', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:kspark  박경수
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-kspark', 'pm_kspark', '박경수', '박경수', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kspark-status', 'msa-pm-kspark', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kspark-homepath', 'msa-pm-kspark', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-kspark-msasource', 'msa-pm-kspark', 'MsaSource', 'projmng:dev_user:kspark', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:sglee  이상기
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-sglee', 'pm_sglee', '이상기', '이상기', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-sglee-status', 'msa-pm-sglee', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-sglee-homepath', 'msa-pm-sglee', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-sglee-msasource', 'msa-pm-sglee', 'MsaSource', 'projmng:dev_user:sglee', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

-- projmng:dev_user:yws  ywsname333
INSERT INTO scom.accounts (id, user_id, user_name, real_name, password,
                           company_id, department_id, is_deleted,
                           created_at, created_by, updated_at, updated_by)
VALUES ('msa-pm-yws', 'pm_yws', 'ywsname333', 'ywsname333', '[REDACTED-PASSWORD-HASH]',
        NULL, NULL, false, now(), 'msa-user-import', now(), 'msa-user-import')
ON CONFLICT (id) DO NOTHING;
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-yws-status', 'msa-pm-yws', 'Status', 'ACTIVE', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-yws-homepath', 'msa-pm-yws', 'HomePath', '/workspace', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-yws-msasource', 'msa-pm-yws', 'MsaSource', 'projmng:dev_user:yws', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';
INSERT INTO scom.account_profile_details
       (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by, is_deleted)
VALUES ('msa-pm-yws-phone', 'msa-pm-yws', 'Phone', '010', true,
        now(), 'msa-user-import', now(), 'msa-user-import', false)
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, updated_at = now(), updated_by = 'msa-user-import';

COMMIT;

-- ── 되돌리기 ────────────────────────────────────────────
-- DELETE FROM scom.accounts WHERE created_by = 'msa-user-import';
--   (account_profile_details 는 ON DELETE CASCADE 로 함께 지워진다)

-- ── 비밀번호 일괄 무효화 (권장) ─────────────────────────
-- 아래를 실행하면 이관 계정 전부가 로그인 불가가 되고, 계정 관리 화면에서 지정해야 한다.
-- UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';
