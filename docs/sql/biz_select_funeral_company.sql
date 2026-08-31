-- ============================================================
-- BizSelect — 장례식장 관리시스템 전용 회사 목록
-- ============================================================
--
-- DB: jsiniportal (스키마 scom)
--
-- 장례식장 관리시스템의 모든 화면은 **그 시스템에 배정된 회사만** 골라야 한다.
-- 배정은 회사 관리의 '사용처'(공통코드 `COMPANY_USAGE_LOCATION`)로 정하고,
-- 장례식장은 `FUNERAL_HOME_MANAGEMENT_SYSTEM` 이다(42번 문서).
--
-- 화면마다 조건을 적지 않고 **셀렉트 타입을 하나 더 둔다.**
-- 장례식장 화면은 `<BizSelect type="funeralCompany" />` 하나만 쓰면 되고,
-- 조건이 바뀌면 이 행의 `static_params` 만 고치면 전 화면에 함께 반영된다.
-- (설정은 코드가 아니라 이 표에 둔다 — `#/api/biz-select` 머리말 참고)
--
-- 기존 `company` 타입은 그대로 둔다. 포털의 회사 관리·조직도 등은 전체 목록이 맞다.
--
-- 반복 실행해도 안전하다.

BEGIN;

INSERT INTO scom.biz_select_configs (
    id, biz_type, api_url, http_method, label_field, value_field,
    result_path, service_code, static_params, remark,
    created_at, created_by, updated_at, updated_by, is_deleted
)
VALUES (
    'funeral-company',
    'funeralCompany',
    '/system/companies',
    'GET',
    'name',
    'id',
    'result',
    'auth',
    '{"usageLocation":"FUNERAL_HOME_MANAGEMENT_SYSTEM"}',
    '장례식장 관리시스템 회사 목록 (사용처가 FUNERAL_HOME_MANAGEMENT_SYSTEM 인 회사만)',
    now(), 'System', now(), 'System', false
)
ON CONFLICT (biz_type) DO UPDATE
SET api_url       = EXCLUDED.api_url,
    http_method   = EXCLUDED.http_method,
    label_field   = EXCLUDED.label_field,
    value_field   = EXCLUDED.value_field,
    result_path   = EXCLUDED.result_path,
    service_code  = EXCLUDED.service_code,
    static_params = EXCLUDED.static_params,
    remark        = EXCLUDED.remark,
    updated_at    = now(),
    updated_by    = 'System',
    is_deleted    = false;

COMMIT;

-- 확인
SELECT biz_type, api_url, service_code, static_params, remark
FROM scom.biz_select_configs
WHERE biz_type IN ('company', 'funeralCompany')
ORDER BY biz_type;
