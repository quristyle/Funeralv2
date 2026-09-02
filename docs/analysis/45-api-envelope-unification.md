# 45. 응답 봉투 하나 · 사용법 하나

2026-09-02.

게이트웨이를 지나 오는 응답은 **모양이 같다.** 그런데 프론트에서는 그 봉투를
**60곳 넘게 손으로, 조금씩 다른 방식으로** 벗기고 있었다. 벗기는 것을 잊은 곳은
정상 응답을 받고도 오류 토스트를 띄웠다. 그 기준을 한 곳으로 모은 기록이다.

## 1. 어쩌다 이렇게 됐나

발단은 사용자 신고였다. `/building/music-build` 에 들어가거나 작업 칸의
`건물 배정` 을 누르면 `건물 목록을 불러오지 못했습니다.` 가 떴다.

API 는 **200 으로 건물 2건을 정상 반환**하고 있었다.

```json
{ "success": true, "code": "S000",
  "data": { "result": [ {…}, {…} ], "page": { "total": 2 } } }
```

화면 코드는 이랬다.

```ts
const list = (await getBuildingsForMusic(row.id)) || [];
buildings.value = list;
checked.value = Object.fromEntries(list.map((b) => [b.buildingId, b.mapped]));
//                                      ^^^ list.map is not a function
```

`getBuildingsForMusic` 은 `BuildingMapping[]` 로 선언돼 있었지만 실제로 넘어온 것은
`{ result, page }` **객체**였다. `TypeError` 가 나고, 바로 아래 `catch` 가 그것을
삼켜 실패 토스트로 바꿨다. 타입 선언이 실제와 달라 `vue-tsc` 도 잡지 못했다.

## 2. 봉투 규칙 (백엔드)

`JSini.Shared.DTOs.ApiResponse<T>` 의 `SerializedData` 가 **모든** 응답을 감싼다
(`BuildSerializedData`). 엔드포인트가 `AddApiResponseWrapper()` 를 쓰든
`ApiResponse<T>.Ok(...)` 를 직접 돌려주든 결과는 같다.

| 핸들러가 돌려준 것 | 나가는 `data` |
|---|---|
| `null` | `null` |
| `Result` · `TotalCount` 를 가진 페이징 DTO | `{ result: <Result>, page: { total: <TotalCount> } }` |
| `IEnumerable` (문자열 제외) | `{ result: [ … ], page: { total: n } }` |
| **그 밖의 모든 것** (단일 객체 · 숫자 · 불리언) | `{ result: [ 값 ], page: { total: 1 } }` |

**핵심은 마지막 줄이다.** 객체 하나도 `result` 배열에 담겨 온다. 그래서 봉투만 보고는
'1건짜리 목록' 과 '객체 하나' 를 **구분할 수 없다.**

이 규칙을 쓰는 서비스는 일곱이다 — AuthServer · funeralv2Api · FileServer ·
LifeEnvServer · NotificationServer · SiteServer · AIAgentServer.

### 봉투가 다른 둘 (일부러 그대로 뒀다)

| 서비스 | 봉투 | 왜 안 맞추나 |
|---|---|---|
| HelpDeskServer | `{ success, message, data, meta, totalcount }` (자체 `ApiResponseBuilder`) | JinRestApi 를 그대로 이식했다. 봉투를 바꾸면 **살아 있는 JinReception** 과 어긋난다 |
| ProjMngServer | `{ code: 0, message, res, cols, data }` (숫자 코드) | ProjMngWasm 시절 규약이다. 바꾸면 **DB 프로시저 규약**까지 건드려야 한다 |

둘은 **전용 클라이언트가 차이를 흡수한다** — `api/helpdesk/request.ts` ·
`api/projmng/request.ts`. 이번 정리의 대상이 아니다.

## 3. 프론트가 받는 값

`requestClient` 는 `dataField: 'data'` 라 **`data` 까지만** 벗긴다.
그래서 API 함수가 손에 쥐는 값은 늘 `{ result, page }` 다.

`requestListClient`(`dataField: 'data.result'`)를 쓰면 될 것 같지만 **동작하지 않는다.**
인터셉터가 `responseData[dataField]` 로 **한 단계만** 찾기 때문에 점이 든 경로는
`undefined` 가 된다(`preset-interceptors.ts`). 여러 모듈의 주석이 각자 이 사실을
발견해 적어 두고 있었다.

## 4. 무엇을 했나

### 4.1 기준을 한 파일로 (`src/api/envelope.ts`)

객체 하나와 1건짜리 목록을 봉투가 구분해 주지 않으므로 **자동으로 벗기는 장치를
클라이언트에 둘 수 없다.** 구분은 엔드포인트를 아는 사람만 할 수 있다.
그래서 그 선택을 **이름으로** 드러냈다.

| 쓸 곳 | 함수 | 결과 |
|---|---|---|
| 목록을 기대한다 | `unwrapList<T>` | 항상 배열 |
| 객체 하나를 기대한다 | `unwrapOne<T>` | 객체 또는 `undefined` |
| 총건수까지 필요하다 | `unwrapPage<T>` | `{ items, total }` |

셋 다 **봉투 · 맨배열 · 맨객체를 모두 받아 준다.** 그래서 아직 손으로 벗기는 옛
코드나 봉투를 쓰지 않는 서비스와 섞여도 터지지 않는다.

**API 모듈 밖에서는 부르지 않는다.** 화면은 이미 벗겨진 값을 받는다.

### 4.2 그리드도 중앙에서 흡수 (`src/adapter/vxe-table.ts`)

예전 규칙은 이랬다 — *"봉투를 그대로 돌려주거나, 배열을 돌려주려면 페이저를 꺼라."*
vxe 가 페이저 상태에 따라 **서로 다른 자리**를 읽기 때문이다
(켜져 있으면 `result` · `page.total`, 꺼져 있으면 응답 전체).

`proxyConfig.response` 의 셋을 다 함수로 지정해 어느 쪽이든 배열과 봉투를 모두 받게 했다.

```ts
response: {
  list:   (params) => gridRows(params),   // 페이저 꺼짐
  result: (params) => gridRows(params),   // 페이저 켜짐
  total:  (params) => page.total ?? gridRows(params).length,
}
```

`total` 은 **`page.total` 이 정본이고 없으면 받은 건수**로 본다. 그래서 서버 페이징
화면은 서버 총건수를 그대로 쓰고(계정 관리에서 `전체 1037 건` 유지 확인),
전량 조회 화면은 벗긴 배열을 줘도 맞는 숫자가 나온다(건물 `전체 2 건` 확인).

### 4.3 모듈 정리

- **고장 고침** — `music-build`(3함수) · `funeral/setting`(3함수).
  두 화면 모두 정상 응답을 받고도 오류 토스트를 띄우고 목록이 비어 있었다.
- **봉투를 안 벗기던 GET 을 벗기게** — `funeral/building`(건물 · 층 · 호실 · 장비 ·
  장비설정 · 미디어 · 고인 · 리본 · 오버레이) · `funeral/info`(알림 · 호실히스토리 ·
  고인조회 · 나의정보 · 미리보기) · `portal/system/i18n`.
- **손수 벗기기 20곳을 공통 함수로** — `core/user` · `life/birthday` · `life/weather` ·
  `portal/{ai,faq,gateway,help-archive,notice,notification,qna,release,site}` ·
  `portal/system/{account,i18n,menu-favorite,menu-role,msa-users}`.
  같은 논리를 스무 번 쓴 것을 한 번으로 줄였다.

곁따라 드러난 것들.

- `cancelDeceasedDeparture` 는 `boolean` 선언인데 봉투(항상 truthy 객체)를 돌려주고
  있었다. 서버가 `false` 를 줘도 성공으로 읽혔다.
- `portal/system/i18n.ts` 의 `getI18nListByLocale` 은 `I18nResource[]` 로 선언한 값에
  `.result` 를 붙여 읽고 있었다 — 런타임은 맞고 타입만 틀린, 이 문제의 표본이다.
- `core/user.ts` 의 `requestListClient` 는 쓰이지 않는 죽은 import 였다.

## 5. 확인한 것

- `vue-tsc` — **변경 영역에 새 오류 0.** `unwrapOne` 이 `| undefined` 를 정직하게
  드러내면서 두 곳이 걸렸고(장비 상세 · 나의 정보) 둘 다 실제로 값이 없을 수 있는
  자리라 호출처를 맞췄다.
- `vitest` — 61개 파일 471개 통과.
- 브라우저 — 화면 **33개**를 돌며 오류 토스트 0건, 콘솔 오류 0건.
  그리드 행 수와 페이저 총건수가 유지되는 것도 함께 봤다.
- `music-build` · `work-options` 는 고장 전후를 직접 대조했다
  (건물 0건 → 2건, 설정 0개 → 4개, 오류 토스트 사라짐).

## 6. 남은 것

### D-A1. 백엔드가 단일 객체를 배열로 감싸는 것을 그대로 둘까

`{ result: [obj], page: { total: 1 } }` 는 이번 문제의 **뿌리**다. 이것 때문에
클라이언트가 자동으로 벗길 수 없고, 호출처가 `unwrapList` · `unwrapOne` 을
골라야 한다.

단일 객체를 감싸지 않으면 프론트는 껍데기 하나만 벗기면 되고 고를 것이 없어진다.
다만 **와이어 포맷이 바뀐다** — 포털만 쓰는 API 가 아니다(장비 · 플레이어 ·
회사 소개 사이트 · 옛 시스템 연동). 영향 범위를 이 자리에서 확인할 수 없어
손대지 않았다. 결정이 필요하다.

### D-A2. 화면에 남은 손수 벗기기 40곳

`views/` 에는 아직 `(res as any)?.result ?? res` 류가 40곳쯤 있다. API 모듈이
벗겨서 주기 시작했으므로 이제 **전부 무해한 no-op** 이다(`.result` 가 없으니
`?? res` 로 떨어진다). 그래서 급하지 않다.

다만 남겨 두면 "화면에서도 봉투를 더듬는다" 는 잘못된 본보기가 된다.
걷어낼 때는 **안전장치 없는 것**을 먼저 봐야 한다 — 예를 들어
`deceased-photo-form.vue` 의 `res.result || res.data` 는 `?? res` 가 없다.
(이 자리는 `requestClient` 가 아니라 antd 업로드의 **원본 응답**을 읽는 곳이라
지금은 정상이다.)

### D-A3. `requestListClient` 를 없앨까

점 경로가 동작하지 않아 **쓸 수 없는 클라이언트**다. 지금 실제로 부르는 곳은
**한 곳도 없다** — `request.ts` 의 정의만 남아 있고, 네 모듈에는 "이걸 쓰면 될 것
같지만 안 된다" 는 주석만 있다(`life/birthday` · `life/weather` ·
`portal/notification` · `portal/system/menu-favorite`).

`unwrapList` 가 그 자리를 대신하므로 정의를 지우는 것이 맞아 보인다. 남겨 두면
다음 사람이 또 시도한다. 다만 `request.ts` 는 공용 파일이라 지우는 결정은 따로 둔다.
그 네 주석도 이제 `envelope.ts` 를 가리키는 한 줄로 줄일 수 있다.
