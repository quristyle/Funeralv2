# 42. 회사 사용처 (COMPANY_USAGE_LOCATION)

2026-08-31. 회사가 **어느 시스템에서 쓰이는지**를 공통코드로 보관한다.
회사 하나가 여러 개를 가질 수 있고, 하나도 없을 수도 있다.

## 자료 구조

코드 그룹 `COMPANY_USAGE_LOCATION`(회사 사용처)은 이미 등록돼 있었다.

| code_value | code_name |
|---|---|
| `FUNERAL_HOME_MANAGEMENT_SYSTEM` | 장례식장관리 시스템 |
| `PROJECT_MANAGEMENT_SYSTEM` | 프로젝트 관리 시스템 |
| `HELP_DESK` | 헬프 데스크 |

회사 표에 칸을 더하지 않고 잇는 표를 뒀다 — `scom.company_usage_locations`
(`docs/sql/company_usage_location.sql`, jsiniportal · 반복 실행 안전).
모양은 `scom.role_companies` 와 같다(정수 identity 키 + 공통 감사 칸 + 유일 색인).

### 코드의 id 가 아니라 `code_value` 를 담는다

화면의 공통코드 셀렉트(`DictSelect`)와 시스템 사이에서 주고받는 값이 모두
`code_value`(예: `HELP_DESK`)다. id(GUID)를 담으면 값을 쓸 때마다 코드 표를 한 번 더
들러야 하고, 코드를 지우고 다시 만들면 id 가 바뀌어 연결이 끊긴다.

대신 **공통코드로의 외래키는 없다.** 코드가 사라지면 행이 남는데, 화면이 이름을 못 찾아
코드값을 그대로(회색 태그로) 보여 주므로 눈에 띈다.

## "안 보내면 건드리지 않는다"

`CompanyCreateDto.UsageLocations` 는 **일부러 nullable** 이다.

| 보낸 값 | 결과 |
|---|---|
| 없음(`null`) | 사용처를 **건드리지 않는다** |
| `[]` | 전부 해제 |
| `["HELP_DESK", …]` | 그 목록으로 맞춘다 |

회사 목록 화면은 셀을 고칠 때 `onEditClosed` 가 **일부 항목만** 담은 요청을 보낸다.
그때 사용처가 지워지면 안 되므로 이 규칙이 필요하다
(메뉴의 `Status` 가 nullable 인 것과 같은 이유다).

**그래서 폼은 반드시 배열로 보낸다.** 다중 선택을 전부 지우면 ant Select 는 `[]` 이
아니라 `undefined` 를 준다 — 그대로 보내면 JSON 에서 빠져 '전부 해제' 가 아무 일도
하지 않는 것이 된다. `modules/form.vue` 가 `values.usageLocations ?? []` 로 맞춘다.

`ApplyUsageLocationsAsync` 는 **차이만** 반영한다(있던 것을 지우고 다시 넣지 않는다).
바꾸지 않은 행의 등록 정보가 남는다. 지울 때는 행을 실제로 지운다 —
`(company_id, code_value)` 에 유일 색인이 걸려 있어 지운 표시만 남기면 같은 코드를
다시 넣을 때 부딪힌다.

## 화면

| 자리 | 무엇 |
|---|---|
| 등록/수정 폼 | `DictSelect` 다중 선택 (`mode: 'multiple'`, 2칸 폭) |
| 목록 그리드 | 태그로 이름 표시. 하나도 없으면 `-` |
| 목록 필터 | 코드 목록에서 **여러 개** 고르기(OR). 판정은 '들어 있는가' |

**그리드에서는 못 고친다.** 셀 편집기 하나에 여러 값을 담기 어렵고, [수정] 폼의 다중
선택이 이미 그 일을 한다.

코드 목록은 비동기로 오므로 `list.vue` 가 받은 뒤 `gridApi.setGridOptions` 로 컬럼을
다시 심는다 — 컬럼을 만드는 시점에는 아직 목록이 없다. 못 받아도 화면은 뜬다
(코드값이 그대로 보이고 필터 목록만 빈다).

## 검증

API 왕복을 DB 로 확인했다.

| 보낸 것 | `company_usage_locations` |
|---|---|
| `['HELP_DESK','PROJECT_MANAGEMENT_SYSTEM']` | 2행 |
| `['HELP_DESK']` | 1행 (차이만 반영) |
| 필드 미전송 | **그대로 1행** |
| `[]` | 0행 |

`pnpm vite build` · AuthServer Release 빌드 통과.
`form.vue` 의 타입 오류 6건은 이 작업 **이전부터** 있던 것이다(`git stash` 로 대조 확인).

## 장례식장 관리시스템은 배정된 회사만 고른다 (2026-09-01)

장례식장 화면의 회사 목록에는 **사용처가 `FUNERAL_HOME_MANAGEMENT_SYSTEM` 인 회사만**
나온다. 화면마다 조건을 적지 않고 셀렉트 **타입을 하나 더** 두는 방식이다.

| 무엇 | 자리 |
|---|---|
| API 필터 | `GET /system/companies?usageLocation=…` (AuthServer `CompanyEndpoints`·`CompanyService`) |
| 셀렉트 타입 | `scom.biz_select_configs` 의 `funeralCompany` (`docs/sql/biz_select_funeral_company.sql`) |
| 화면 | 장례식장 9곳을 `<BizSelect type="funeralCompany">` 로 바꿨다 |
| 목록 직접 호출 | 정산 통계(`stat/billing-custom`)만 BizSelect 를 안 쓴다 — `getCompanyList(FUNERAL_USAGE_LOCATION)` |

**왜 타입을 하나 더 두나.** 조건을 화면마다 `params` 로 적으면 아홉 군데에 같은 문자열이
흩어지고, 규칙이 바뀔 때 하나를 빠뜨린다. 타입을 두면 조건이 `static_params` 한 곳에만
있어서 **DB 값 하나로 전 화면이 함께 바뀐다**(설정은 코드가 아니라 표에 둔다 —
`#/api/biz-select` 머리말과 같은 방침).

**기존 `company` 타입은 그대로 둔다.** 포털의 회사 관리·조직도·부서 관리는 전체 목록이 맞다.
`usageLocation` 을 안 주면 예전과 똑같이 전부 내려온다.

**새 장례식장 화면을 만들 때는 `type="funeralCompany"` 를 쓴다.** `type="company"` 로
쓰면 전체 목록이 나와 이 규칙에서 새어 나간다.

### 검증

| 확인 | 결과 |
|---|---|
| `?usageLocation=FUNERAL_HOME_MANAGEMENT_SYSTEM` | 3건 (InCom · 준 시스템 · 미르포토) — DB 배정과 일치 |
| `?usageLocation=HELP_DESK` | 2건 |
| 파라미터 없음 | 13건 (전체) |
| 건물정보 · 호실 화면 회사 필터 | 3건만 노출 |
| 포털 부서 관리 | 전체 목록 그대로 |
