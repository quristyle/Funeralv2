-- BizSelect 메타데이터에 MSA 지정을 추가하고, 헬프데스크·프로젝트관리 셀렉트를 등록한다.
--
-- 왜 MSA 컬럼이 필요한가
--   서비스마다 응답 봉투가 다르다.
--     포털/장례식장 : { code: 'S000', data: ... }
--     헬프데스크     : { success: true, data: ... }
--     프로젝트관리   : { code: 0(숫자), message, cols, data }
--   프론트는 service_code 로 봉투를 벗길 요청 클라이언트를 고른다. 단순 URL 프리픽스가 아니다.
--
-- 반복 실행 안전.

-- ============================================================
-- 1. 컬럼 추가
-- ============================================================
ALTER TABLE scom.biz_select_configs
    ADD COLUMN IF NOT EXISTS service_code  VARCHAR(30),
    ADD COLUMN IF NOT EXISTS static_params TEXT,
    ADD COLUMN IF NOT EXISTS param_path    VARCHAR(100);

COMMENT ON COLUMN scom.biz_select_configs.service_code
    IS '호출 대상 MSA (auth·funeral·helpdesk·projmng·file·ai). 게이트웨이 프리픽스이자 응답 봉투 해석 키';
COMMENT ON COLUMN scom.biz_select_configs.api_url
    IS 'MSA 프리픽스를 뺀 서비스 내부 경로';
COMMENT ON COLUMN scom.biz_select_configs.static_params
    IS '호출 시 항상 함께 보내는 고정 파라미터 (JSON 객체)';
COMMENT ON COLUMN scom.biz_select_configs.param_path
    IS '화면이 넘긴 런타임 파라미터를 본문의 어느 자리에 넣을지 (점 표기). 비면 최상위';

-- ============================================================
-- 2. 기존 8건 백필 — api_url 첫 세그먼트를 service_code 로 떼어낸다
--    (/auth/system/companies -> service_code='auth', api_url='/system/companies')
-- ============================================================
UPDATE scom.biz_select_configs
   SET service_code = split_part(api_url, '/', 2),
       api_url      = '/' || substring(api_url from position('/' in substring(api_url from 2)) + 2)
 WHERE service_code IS NULL
   AND split_part(api_url, '/', 2) IN ('auth', 'funeral', 'helpdesk', 'projmng', 'file', 'ai');

-- 프리픽스를 못 알아본 나머지는 포털로 본다 (지금은 해당 없음).
UPDATE scom.biz_select_configs SET service_code = 'auth' WHERE service_code IS NULL;

ALTER TABLE scom.biz_select_configs ALTER COLUMN service_code SET NOT NULL;
ALTER TABLE scom.biz_select_configs ALTER COLUMN service_code SET DEFAULT 'auth';

-- ============================================================
-- 2-1. biz_type 유일성 복원
--      최초 마이그레이션 SQL 에는 UNIQUE 가 있었는데 EF 가 만든 실제 테이블에는 빠져 있다.
--      BizSelect 는 bizType 하나로 설정을 찾으므로 중복이 생기면 어느 쪽이 걸릴지 알 수 없다.
-- ============================================================
CREATE UNIQUE INDEX IF NOT EXISTS ux_biz_select_configs_biz_type
    ON scom.biz_select_configs (biz_type);

-- ============================================================
-- 3. 헬프데스크 셀렉트
--    헬프데스크 클라이언트가 봉투의 data 를 이미 벗겨 주므로 result_path 는 비운다.
-- ============================================================
INSERT INTO scom.biz_select_configs
    (id, biz_type, service_code, api_url, http_method, label_field, value_field,
     result_path, processor_type, static_params, param_path, remark,
     is_deleted, created_at, created_by, updated_at, updated_by)
VALUES
    ('hd-company',  'helpdesk_company',  'helpdesk', '/companys',  'GET', 'name',     'id',     NULL, NULL, NULL, NULL, '헬프데스크 고객사 목록',            FALSE, NOW(), 'System', NOW(), 'System'),
    ('hd-team',     'helpdesk_team',     'helpdesk', '/teams',     'GET', 'name',     'id',     NULL, NULL, NULL, NULL, '헬프데스크 팀 목록',                FALSE, NOW(), 'System', NOW(), 'System'),
    ('hd-admin',    'helpdesk_admin',    'helpdesk', '/admins',    'GET', 'userName', 'id',     NULL, NULL, NULL, NULL, '헬프데스크 담당자 목록',            FALSE, NOW(), 'System', NOW(), 'System'),
    ('hd-customer', 'helpdesk_customer', 'helpdesk', '/customers', 'GET', 'userName', 'id',     NULL, NULL, NULL, NULL, '헬프데스크 고객 목록',              FALSE, NOW(), 'System', NOW(), 'System'),
    ('hd-user',     'helpdesk_user',     'helpdesk', '/users/',    'GET', 'userName', 'userId', NULL, NULL, NULL, NULL, '헬프데스크 담당자+고객 통합 목록',  FALSE, NOW(), 'System', NOW(), 'System'),
    ('hd-project',  'helpdesk_project',  'helpdesk', '/project',   'GET', 'name',     'id',     NULL, NULL, NULL, NULL, '헬프데스크 프로젝트 목록',          FALSE, NOW(), 'System', NOW(), 'System')
ON CONFLICT (biz_type) DO UPDATE
   SET service_code  = EXCLUDED.service_code,
       api_url       = EXCLUDED.api_url,
       http_method   = EXCLUDED.http_method,
       label_field   = EXCLUDED.label_field,
       value_field   = EXCLUDED.value_field,
       result_path   = EXCLUDED.result_path,
       processor_type= EXCLUDED.processor_type,
       static_params = EXCLUDED.static_params,
       param_path    = EXCLUDED.param_path,
       remark        = EXCLUDED.remark,
       updated_at    = NOW(),
       updated_by    = 'System';

-- ============================================================
-- 4. 프로젝트관리 공통코드 셀렉트
--
--    프로젝트관리의 모든 드롭다운은 프로시저 sp_projCommon 하나를 code_id 만 바꿔 부른다
--    (projlist · projdb · schedule_type · user · srclist · compstat · yn · todo_state · srclang · db).
--    그래서 code_id 마다 행을 만들지 않고 한 행으로 전부 태운다.
--    화면이 넘기는 { code_id, etc0 } 는 param_path 가 가리키는 MainParam 안으로 들어간다.
--
--    프로젝트관리 클라이언트는 봉투를 벗기지 않으므로(cols 를 화면이 써야 한다) result_path 는 data.
-- ============================================================
INSERT INTO scom.biz_select_configs
    (id, biz_type, service_code, api_url, http_method, label_field, value_field,
     result_path, processor_type, static_params, param_path, remark,
     is_deleted, created_at, created_by, updated_at, updated_by)
VALUES
    ('pm-common', 'projmng_common', 'projmng', '/Proj', 'POST', 'name', 'code',
     'data', NULL, '{"ProcName":"sp_projCommon","ProcType":"srch"}', 'MainParam',
     '프로젝트관리 공통코드 (code_id 로 목록을 가른다)', FALSE, NOW(), 'System', NOW(), 'System')
ON CONFLICT (biz_type) DO UPDATE
   SET service_code  = EXCLUDED.service_code,
       api_url       = EXCLUDED.api_url,
       http_method   = EXCLUDED.http_method,
       label_field   = EXCLUDED.label_field,
       value_field   = EXCLUDED.value_field,
       result_path   = EXCLUDED.result_path,
       processor_type= EXCLUDED.processor_type,
       static_params = EXCLUDED.static_params,
       param_path    = EXCLUDED.param_path,
       remark        = EXCLUDED.remark,
       updated_at    = NOW(),
       updated_by    = 'System';
