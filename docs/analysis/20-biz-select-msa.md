# 20. BizSelect 메타데이터에 MSA 를 붙이고 헬프데스크·프로젝트관리를 태웠다

작업일 2026-08-25.

## 무엇이 문제였나

`scom.biz_select_configs` 는 셀렉트 하나를 "어느 API 를 어떻게 읽어 무엇을 라벨/값으로 쓰는가"
한 줄로 적어 두는 표다. 화면은 `<BizSelect type="company">` 만 쓰고 나머지는 DB 가 정한다.

그런데 등록된 8건이 전부 포털(`auth`)과 장례식장(`funeral`) 것이었다.
헬프데스크·프로젝트관리는 이식해 붙였는데 셀렉트는 각자 방식대로 남아 있었다.

- 헬프데스크 — `store/helpdesk.ts` 가 `/companys` · `/admins` · `/customers` 를 코드에 박아 두고
  `companyOptions` 같은 computed 로 내려 줬다. 팀·프로젝트·사용자 목록은 화면마다 따로 불러
  `list.map(t => ({label: t.name, value: t.id}))` 를 반복했다.
- 프로젝트관리 — `code-select.vue` 가 `getCommon(codeId)` 로 프로시저 `sp_projCommon` 을 직접 불렀다.

## 왜 URL 프리픽스만으로는 안 되는가

`api_url` 앞에 `/helpdesk` 를 붙이는 것으로 끝나지 않는다. **서비스마다 응답 봉투가 다르고,
그 차이를 프론트의 서로 다른 요청 클라이언트가 흡수하고 있기 때문이다.**

| MSA | 봉투 | 클라이언트 | 클라이언트가 돌려주는 것 | `result_path` |
|---|---|---|---|---|
| auth · funeral · file · ai | `{ code:'S000', data }` | `requestClient` | `data` | `result` |
| helpdesk | `{ success:true, data }` | `helpdeskClient` | `data` (이미 배열) | (비움) |
| projmng | `{ code:0, message, cols, data }` | `projmngClient` | 봉투 전체 (`cols` 를 화면이 쓴다) | `data` |

그래서 새로 넣은 `service_code` 는 **프리픽스이자 봉투를 벗길 클라이언트를 고르는 키**다.

## 표에 추가한 컬럼

| 컬럼 | 뜻 |
|---|---|
| `service_code` | 호출 대상 MSA. NOT NULL, 기본 `auth` |
| `static_params` | 호출할 때 항상 함께 보내는 값 (JSON 객체) |
| `param_path` | 화면이 넘긴 파라미터를 본문의 어느 자리에 넣을지 (점 표기). 비면 최상위 |

`api_url` 의 뜻이 바뀌었다 — **MSA 프리픽스를 뺀 서비스 내부 경로**다.
기존 8건은 마이그레이션이 첫 세그먼트를 떼어 `service_code` 로 옮겼다
(`/auth/system/companies` → `auth` + `/system/companies`).

`biz_type` UNIQUE 도 복원했다. 최초 마이그레이션 SQL 에는 있었는데 EF 가 만든 실제 테이블에는 빠져 있었다.

### `static_params` · `param_path` 는 왜 필요했나

프로젝트관리는 조회 대상을 URL 이 아니라 **본문**으로 지정한다.

```json
{ "ProcName": "sp_projCommon", "ProcType": "srch", "MainParam": { "code_id": "projlist", "etc0": "" } }
```

프로시저 이름은 고정이고 조회 조건만 화면이 정한다. 이 둘을 갈라 두어야
`code_id` 마다 메타데이터를 만들지 않고 **한 행으로 프로젝트관리의 모든 드롭다운을 태울 수 있다**
(projlist · projdb · schedule_type · user · srclist · compstat · yn · todo_state · srclang · db).

## 넣은 메타데이터 (`docs/sql/biz_select_msa.sql`, 반복 실행 안전)

| biz_type | MSA | 경로 | 라벨/값 |
|---|---|---|---|
| `helpdesk_company` | helpdesk | GET `/companys` | name / id |
| `helpdesk_team` | helpdesk | GET `/teams` | name / id |
| `helpdesk_admin` | helpdesk | GET `/admins` | userName / id |
| `helpdesk_customer` | helpdesk | GET `/customers` | userName / id |
| `helpdesk_user` | helpdesk | GET `/users/` | userName / userId |
| `helpdesk_project` | helpdesk | GET `/project` | name / id |
| `projmng_common` | projmng | POST `/Proj` | name / code |

## 코드 쪽

- `src/api/biz-select.ts` — **새로 만든 단일 통로.** `fetchBizOptions(bizType, params)` 가
  설정을 찾아 MSA 클라이언트를 고르고, 파라미터를 조립하고, 목록을 뽑아 `{items, options}` 를 준다.
  `items` 는 원본 행이다 — 라벨·값 말고 다른 컬럼이 필요한 곳(계정 대조 화면,
  프로젝트관리의 `db_nick`/`db_schema`)이 쓴다.
- `BizSelect.vue` — 이 통로를 쓰도록 다시 썼다. `change` 가 값과 함께 원본 행을 넘기고,
  `loaded` 로 목록 전체를 넘긴다. 상위 선택을 기다리는 조건은 `requiredParams` 로 명시할 수 있다
  (부서·건물·층의 기존 이름 기반 규칙은 그대로 지킨다).
- `store/helpdesk.ts` — `loadOrganizations()` 가 메타데이터를 탄다. `companyOptions` 등
  기존 computed 는 그대로라 쓰던 화면 5개는 손대지 않았다.
- `api/projmng/proc.ts` 의 `getCommon()` — 메타데이터를 탄다. `code-select.vue` 와
  그것을 쓰는 화면 23개는 손대지 않았다. **캐시를 유지하려고 컴포넌트를 그대로 뒀다** —
  `BizSelect` 로 갈아끼우면 화면마다 매번 프로시저를 다시 부르게 된다.
- 화면 전환: 프로젝트 셀렉트 3곳(`project/info`·`wbs`·`wbs-gantt`) 을 `<BizSelect>` 로 바꾸고,
  팀 셀렉트 2곳(`org/admin`·`project/manage`) 과 사용자 셀렉트 1곳(`push/history`) 을
  `fetchBizOptions` 로 바꿨다.

관리 화면(`/system/metadata_manager`)에 MSA · 고정 파라미터 · 파라미터 경로 컬럼과 입력을 붙였다.

## 확인한 것

- `dotnet build microservices/AuthServer` · `pnpm vite build` 통과.
  `vue-tsc` 오류 수는 49로 손대기 전(50)보다 줄었다 (전부 기존 것).
- 게이트웨이를 통해 15건 전부 실제로 호출해 건수와 라벨/값을 확인했다.
- 브라우저에서 `/system/metadata_manager` · `/projmng/todo/list` · `/helpdesk/project/wbs`
  · `/helpdesk/request/manage` · `/system/dept` 를 열어 셀렉트가 예전대로 도는 것을 확인했다.

## 남은 것

- 헬프데스크 요청·일정 화면의 회사/담당자 셀렉트는 여전히 스토어의 `companyOptions` 를 쓴다.
  데이터 출처는 메타데이터로 통일됐지만 컴포넌트는 `<Select>` 그대로다. `<BizSelect>` 로 바꾸면
  같은 화면에서 같은 목록을 두 번 부르게 되어(스토어가 이미 받아 둔다) 지금은 두지 않았다.
  스토어 캐시를 `fetchBizOptions` 쪽으로 옮기면 정리할 수 있다.
- 프로젝트관리 `code-select.vue` 를 `<BizSelect>` 로 흡수하려면 목록 캐시를 공용 통로로
  올려야 한다. 위와 같은 이야기다.
