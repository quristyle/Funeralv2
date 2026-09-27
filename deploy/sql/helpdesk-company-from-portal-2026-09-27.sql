-- 헬프데스크가 회사를 스스로 관리하지 않게 한다 (2026-09-27)
--
-- 회사는 포털(`jsiniportal.scom.companies`)이 정본이다. 헬프데스크가 제
-- 회사 표(`helpdesk.customercompany`)를 들고 있던 것은 단독 시스템이던 시절의
-- 잔재라, 표를 걷어내고 업무 자료가 **포털 회사 아이디**를 그대로 가리키게
-- 바꿨다.
--
-- 여기 적는 것은 **DB 가 둘이라 한 번에 못 도는 일 셋**이다.
--
--   1. helpdesk DB — 스키마 바꾸기. EF 마이그레이션
--      `20260927132200_DropHelpdeskOwnedCompany` 가 한다. **여기 다시 적지
--      않는다** — 두 곳에 두면 한쪽만 고치는 날이 온다.
--   2. helpdesk DB — 고객의 회사 다시 잡기 (아래 ①). 마이그레이션은 칸을
--      비우기만 한다. 옛 정수 아이디를 포털 아이디로 바꾸는 대조표가
--      **포털 DB 에만** 있어(`scom.companies.remark` 의 `helpdesk:company:<원본ID>`)
--      마이그레이션 안에서는 읽을 수 없다.
--   3. jsiniportal DB — 회사 고르개가 포털을 보게 하기 (아래 ②).
--
-- 2026-09-27 운영 반영 완료.


-- ① helpdesk DB (`helpdesk` / 스키마 `helpdesk`)
--
-- 고객의 회사는 **그 사람의 포털 계정 소속 회사**다. 회사가 포털 것이 된
-- 지금 그것이 유일하게 맞는 값이고, 옛 회사 표의 정수 아이디를 되짚는 것보다
-- 정확하다 — 운영에 있던 회사 한 줄은 요청 등록이 자동으로 만든
-- 「포털 사용자」 자리표시였고 포털에 짝이 아예 없었다.
--
-- DB 가 갈려 조인할 수 없으므로 짝을 적어 넣는다. 짝은 이렇게 뽑는다 —
--   jsiniportal: select user_id, company_id from scom.accounts where user_id in (...);
--   helpdesk:    select id, loginid from helpdesk.customer;
--
-- 2026-09-27 운영에는 고객이 한 명(quristyle → jsini)이었다.

UPDATE helpdesk.customer SET companyid = 'jsini'
 WHERE loginid = 'quristyle' AND companyid IS NULL;


-- ② jsiniportal DB (`jsiniportal` / 스키마 `scom`)
--
-- 회사 고르개(biz-select `helpdesk_company`)가 헬프데스크의 `/companys` 를
-- 부르고 있었다. 그 엔드포인트를 걷어냈으므로 포털로 옮긴다 — 장례식장의
-- `funeralCompany` 와 같은 모양이고 사용처 코드값만 다르다.
--
-- 코드값은 `HELP_DESK` 다. **`HELPDESK` 가 아니다** — 틀리면 오류 없이
-- 목록이 빈 채로 와서 「회사 선택이 비어 있다」로만 보인다.

UPDATE scom.biz_select_configs
   SET api_url       = '/system/companies',
       service_code  = 'auth',
       http_method   = 'GET',
       label_field   = 'name',
       value_field   = 'id',
       result_path   = 'result',
       static_params = '{"usageLocation":"HELP_DESK"}',
       updated_at    = now(),
       updated_by    = 'system'
 WHERE biz_type = 'helpdesk_company';


-- 반영 확인
--
--   helpdesk:    select id, loginid, companyid from helpdesk.customer;
--   helpdesk:    select to_regclass('helpdesk.customercompany');   -- 비어 있어야 한다
--   jsiniportal: select api_url, service_code, static_params
--                  from scom.biz_select_configs where biz_type = 'helpdesk_company';
--
-- 고르개에 실제로 실리는 회사는 **사용처가 헬프데스크로 배정된 회사**뿐이다.
--   select c.id, c.name from scom.companies c
--     join scom.company_usage_locations u
--       on u.company_id = c.id and not u.is_deleted and u.code_value = 'HELP_DESK';
-- 회사가 고르개에 안 보이면 회사 관리 화면에서 사용처를 배정한다.
