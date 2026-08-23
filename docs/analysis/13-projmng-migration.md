# 프로젝트관리(ProjMng) 이식 — Blazor WASM → JSini 포털

작성: 2026-08-22
대상: `/home/quri/ProjMng`(원본) → `/home/quri/Funeralv2`(포털)

## 1. 무엇을 했나

독립적으로 돌던 프로젝트관리 시스템을 JSini 관리 포털의 MSA 로 편입했다.
헬프데스크(구 JinRestApi)를 이식할 때와 같은 길이다
([10-jsini-portal-unification.md](10-jsini-portal-unification.md)).

| 구분 | 이식 전 | 이식 후 |
|---|---|---|
| 백엔드 | `ProjMng/ProjMngServer` (독립, 인증 없음, CORS AllowAll) | `microservices/ProjMngServer` (게이트웨이 뒤, 루프백 :5450) |
| 모델 | `ProjMng/ProjModel` (별도 프로젝트, Fody 사용) | `microservices/ProjMngServer/Models/` 내장 (Fody 제거) |
| 프론트 | `ProjMng/ProjMngWasm` (Blazor WASM + Radzen, 33화면) | `fronts/apps/jsini-portal/src/views/projmng/` (Vue 3 + Vben, 32화면) |
| 인증 | 자체 로그인 `sp_proj_login` + 자체 메뉴/사용자그룹 | 포털 계정(AuthServer JWT) 단일화 |
| 메뉴·권한 | 자체 메뉴 테이블 | `scom.system_menus` / `scom.role_menus` |

**Blazor 프로젝트는 지우지 않았다.** 원본은 `/home/quri/ProjMng` 에 그대로 있다.
비교·확인이 끝난 뒤에 정리하면 된다.

## 2. 왜 프론트를 Vue 로 다시 썼나

Blazor 를 별도 SPA 로 유지하면 포털의 전제와 정면으로 부딪힌다.

- **인증이 이중화된다.** 포털은 게이트웨이가 JWT 를 검증하고 `X-User-Id` 를 붙이는 구조인데,
  Blazor 쪽은 자체 로그인 화면과 자체 `POST /api/Proj/login` 을 들고 있었다
  (그마저도 `Program.cs` 의 `AuthStateProvider` 등록이 주석 처리되어 사실상 배선이 없었다).
- **메뉴가 둘이 된다.** 포털 메뉴는 DB 주도 동적 라우터이고, Blazor 는 자체 `mnu_id/pgm_id` 트리였다.
  사용자에게 내비게이션이 두 개 보인다.
- **재사용이 0 이다.** 포털이 이미 가진 `request.ts`(토큰·응답 봉투), 접근 가드, i18n, 시간대 초기화를
  하나도 쓸 수 없다.
- **첫 로딩이 무겁다.** .NET 런타임 + BCL 로 수 MB 를 내려받는다.

그리고 **이식을 막는 Blazor 고유 기능이 없었다.** csproj 에 `Microsoft.CodeAnalysis.CSharp`,
`MetadataReferenceService.BlazorWasm`(브라우저 내 C# 컴파일)이 있어 걸림돌인가 확인했는데
실제 사용처는 `WasmShear/Services/DevService.cs:3` 의 주석 한 줄뿐이었다.

## 3. 이식이 가능했던 이유 — 메타 구동 구조

화면을 하나하나 다시 쓴 것이 아니다. 원본이 **메타데이터 구동**이었다.

```
화면  →  프로시저 이름 + 파라미터  →  ProjMngServer  →  PostgreSQL 저장 프로시저
                                                            ↓
화면  ←  { code, message, res, cols, data }  ←──────────────┘
                              ↑
                     컬럼 메타(cols)를 보고 그리드가 스스로 그린다
```

업무 로직은 전부 DB(`projmng` 스키마)의 저장 프로시저에 있고, 서버는 1,780줄짜리
범용 통로다. 그래서 이식의 실질은 **부품 네 개**였다.

| 이식 후 | 원본 | 역할 |
|---|---|---|
| `shared/dynamic-grid.vue` | `QuriDynamicGrid` (890줄) | `cols` 메타로 컬럼을 그리고, 행 편집·추가·복사·삭제·엑셀을 담당 |
| `shared/code-select.vue` | `QuriDropDown` (229줄) | `sp_projCommon` 공통코드 드롭다운 |
| `shared/code-editor.vue` | `QuriCodeEditor` (276줄) | 코드·SQL 편집기 |
| `shared/erd-diagram.vue` | `QuriDiagram` (mxgraph) | ERD·플로우 다이어그램 |
| `shared/split-pane.vue` | `RadzenSplitter` | 좌우/상하 분할 |
| `shared/use-proc-grid.ts` | (화면마다 반복되던 코드) | 조회·저장·삭제 한 묶음 |

부품이 서고 나니 화면 대부분은 50~150줄이다.

## 4. 만들어진 것

### 백엔드

```
microservices/ProjMngServer/
  Program.cs                        JSini MSA 규약 적용 (아래 참조)
  Controllers/                      원본 그대로 — Proj / Dev / Sys / Media
  Services/                         원본 그대로 — 저장 프로시저 호출 엔진
  Models/                           ProjModel 내장 (Fody 제거)
  Filters/UserIdentityActionFilter.cs   SSUserId ← X-User-Id 로 덮어쓴다
  Filters/RawSqlGuardMiddleware.cs      /api/Dev/sql 역할 확인
  appsettings.json                  루프백 :5450, DevTools 설정
  appsettings.Local.json.sample      DB 접속 문자열 견본
```

원본에서 달라진 점만 적는다.

- **루프백(`127.0.0.1:5450`) 에만 바인딩한다.** 원본은 인증이 아예 없었고 CORS 가
  `AllowAnyOrigin` 이었다. 내부 서비스는 게이트웨이가 붙여 주는 헤더를 신원으로 믿으므로
  외부에 열려 있으면 헤더 위조로 사칭이 된다.
- **`SSUserId` 를 클라이언트에서 받지 않는다.** 프론트가 무엇을 보내든 게이트웨이 신원으로
  갈아 끼운다. 저장 프로시저가 이 값을 `req_ss_user_id` 로 받아 감사·권한에 쓰기 때문이다.
- **`/api/Dev/sql` 에 역할 확인을 붙였다.** 이 경로는 요청 본문의 문자열을 그대로 실행한다 —
  사실상 임의 SQL 이다. 게이트웨이가 JWT 를 요구하는 것만으로는 "로그인한 모든 사용자"가
  임의 SQL 을 돌릴 수 있으므로 `DevTools:RawSqlRoles`(기본값: 관리자 역할)로 한 번 더 막는다.
  `DevTools:AllowRawSql=false` 로 두면 경로 자체가 닫힌다.
- 전역 예외 핸들러(`JSini.Shared.Infrastructure`)와 `/health` 를 붙였다.

### 게이트웨이

| 경로 | 클러스터 | 대상 | 인증 |
|---|---|---|---|
| `/api/projmng/**` | projmng-cluster | ProjMngServer :5450 | JWT 필요 |

`/api/projmng/Proj` → 서비스의 `/api/Proj` 로 간다(프리픽스를 떼고 `/api` 를 다시 붙인다).
헬프데스크와 같은 방식이다.

### 프론트

```
src/api/projmng/
  types.ts     요청·응답 봉투 타입
  request.ts   전용 클라이언트 (숫자 code 봉투를 흡수)
  proc.ts      dbCont / dbSave / dbDelete / jsCont / mdCont / rawSql / getCommon

src/views/projmng/
  shared/      공용 부품 6개 + 컴포저블
  comm/        3화면   기준정보 (공통코드 · 화면메뉴 · 사용자그룹)
  proj/        17화면  프로젝트 · 설계 · DB · 도구
  proj/modules/ 1화면  일정 편집 대화창
  develop/     5화면   DB 도구 · 소스 분석
  home/        2화면   할일
  sys/         2화면   DB 로직
  external/    2화면   외부 시스템
```

응답 봉투가 다른 이유로 전용 클라이언트를 뒀다.

| 시스템 | 봉투 |
|---|---|
| 포털 · 장례식장 | `{ code: 'S000', data }` |
| 헬프데스크 | `{ success: true, data }` |
| **프로젝트관리** | `{ code: 0, message, res, cols, data }` — 숫자 코드, 음수면 실패 |

`cols` 를 화면이 반드시 봐야 하므로 이 클라이언트는 `data` 만 꺼내지 않고 봉투 전체를 돌려준다.

### 메뉴

[docs/sql/projmng_menu_seed.sql](../sql/projmng_menu_seed.sql) — **DB 에 반영 완료**.
최상위 `프로젝트관리`(order_no 30) 아래 폴더 10개 + 화면 32개(보이는 것 30, 숨긴 것 2)다.

권한은 `ADMINISTRATOR` / `SYSTEM_ADMINISTRATOR` 두 역할에만 줬다. 개발자용 도구라
파트너 역할에는 주지 않았고, 필요해지면 역할 권한 화면에서 켜면 된다.
[DB 쿼리 테스터]만 `SYSTEM_ADMINISTRATOR` 단독이다.

## 5. 중복 기능의 메뉴 이름

포털에는 이미 겹치는 기능이 여럿 있다. 지우거나 합치지 않고 **이름으로 구분**했다.

| 겹치는 기능 | 기존 (다른 시스템) | 프로젝트관리 |
|---|---|---|
| 프로젝트 | 헬프데스크 "프로젝트 관리" | "프로젝트 목록" |
| WBS | 헬프데스크 "WBS" | "프로젝트 WBS" |
| 일정 | 헬프데스크 "전체 일정" | "프로젝트 일정표" |
| 공통코드 | 포털 "공통코드" | "프로젝트 공통코드" |
| 메뉴 관리 | 포털 "메뉴 관리" | "프로젝트 화면 메뉴" |
| 사용자·권한 | 포털 "역할 관리" | "프로젝트 사용자 그룹" |
| 다이어그램 | 헬프데스크 "다이어그램" | "프로젝트 ERD" / "프로젝트 업무 흐름" |
| 서버 상태 | 포털 "서버 상태" | "JSini 서버 모니터" |

**주의할 점이 있다.** "프로젝트 화면 메뉴"·"프로젝트 사용자 그룹"이 다루는 것은
**관리 대상 프로젝트의** 메뉴·권한이지 포털 것이 아니다. 포털의 인증·권한은 AuthServer
한 곳이 관장한다는 원칙은 그대로다. 화면 안에도 같은 설명을 적어 두었다.

## 6. 원본과 달라진 것

이식하면서 판단이 필요했던 것들이다. 되돌리려면 어디를 보면 되는지 함께 적었다.

| 항목 | 원본 | 이식 후 | 이유 |
|---|---|---|---|
| 코드 편집기 | Monaco (BlazorMonaco) | 자체 편집기 (`shared/code-editor.vue`) | Monaco 가 포털에 없는 의존성이다. 줄번호·고정폭·Tab 들여쓰기는 갖췄다. Monaco 로 바꾸려면 이 파일 하나만 교체하면 된다(인터페이스 동일) |
| 스케줄러 | Radzen Scheduler | 직접 그린 월 달력 (`proj/scheduler.vue`) | 포털에 그 부품이 없다. 원본이 실제로 쓰던 것은 월 뷰·상태별 색·드래그 이동뿐이었다 |
| 엑셀 시트 | Luckysheet (JS interop) | 표 편집 + JSON 편집 (`proj/sheet.vue`) | Luckysheet 는 포털에 없고 유지도 끊겼다. **저장 형식(`cont` 문자열)은 같아서 기존 자료가 열린다** |
| 다이어그램 엔진 | mxgraph (`wwwroot/lib`) | `@maxgraph/core` | mxgraph 의 후속이고 포털이 이미 쓰고 있다(헬프데스크 다이어그램). ERD 저장 형식(`ErdInfo` JSON)은 그대로라 배치가 살아난다 |
| 유즈케이스 저장 형식 | mxgraph XML | ERD 와 같은 JSON | ⚠️ **기존 XML 자료가 있으면 열리지 않는다.** 유즈케이스는 쌓인 자료가 없다고 보고 형식을 통일했다. 자료가 있으면 되돌려야 한다 |
| 사용자 설정 화면 | 사용자 정보 + 테마/글꼴 | 사용자 정보만 | 테마·글꼴은 포털 환경설정이 담당한다. 두 곳에서 관리하면 어긋난다 |
| 미리보기 쿼리 | `where rownum < 10` (오라클 문법) | DB 종류별 분기 | 실제 대상이 PostgreSQL·MSSQL 이라 원본 문법으로는 동작하지 않았다 (`develop/db-tools.vue`) |
| Fast 테스트 | `prj_rid` 가 `7` 로 박혀 있음 | 프로젝트 선택 + `IsFast=true` | 화면 이름이 뜻하는 대로 동작하게 맞췄다 |
| 부품 모음 | **빈 파일**(0 바이트) | 공용 부품 확인 화면 | 자리를 없애면 이식 목록이 어긋난다. 숨긴 메뉴로 등록했다 |
| 그리드 부품 테스트 조회 | `sp_projlist` (DB 에 없는 프로시저) | `sp_dev_proj_exec` | 원본에서도 열리지 않던 화면이다. 그리드 확인이 목적이라 같은 성격의 프로시저로 바꿨다 |
| 공통코드 조회 | 컴포넌트마다 매번 호출 | 한 번 읽고 캐시 | 화면 하나에 드롭다운이 여럿이라 같은 코드를 여러 번 읽었다. 코드를 편집하는 화면은 저장 후 캐시를 비운다 |
| 장례 프레임 빈소 현황 | `FuneralfrData.razor` | **제거** | 쓰지 않는 기능이라 확인 후 삭제했다. 아래 참조 |

### 제거한 것 — 장례 프레임 빈소 현황 (2026-08-22)

원본 `Pages/FuneralfrData.razor` 를 옮긴 화면인데, 쓰지 않는 기능으로 확인되어 걷어냈다.
같이 지운 것까지 적어 둔다 — 이 화면 하나만을 위해 만든 것들이다.

| 지운 것 | 위치 |
|---|---|
| 화면 | `views/projmng/external/funeral-frame-data.vue` |
| 게이트웨이 라우트 | `funeralfr-route` (`/api/funeralfr/**`) |
| 게이트웨이 클러스터 | `funeralfr-cluster` (`https://funeralfr.jsini.co.kr`) |
| 메뉴 | `PM_EXT_FRDATA` — `scom.system_menus` · `scom.role_menus` 에서 삭제 |

메뉴 시드 스크립트에 삭제문을 넣어 두었으므로 다시 실행해도 되살아나지 않는다.

**[장례 프레임 서버 모니터]**(`PM_EXT_FRMON`)는 남겨 두었다. 이름이 비슷하지만 다른 화면이다 —
장비 상태(Glances)를 iframe 으로 띄우는 것이라 게이트웨이를 거치지 않는다.

## 7. 확인한 것

```
dotnet build jsini.sln           → 오류 0
vue-tsc --noEmit                 → 신규 오류 0 (기존 47건은 이식 전과 동일)
oxlint (projmng 전체)            → 오류 0
vite build --mode production     → 성공, 화면들이 청크로 분리됨
scripts/smoke-test.sh            → 24 통과 · 0 실패
```

서비스를 띄워 확인한 것들이다.

- `/health` 200, 루프백 바인딩(외부 IP 로는 연결 불가)
- 게이트웨이 경유 `/api/projmng/**` 는 토큰 없으면 401
- 응답 봉투에 `req_ss_user_id` 가 **헤더 값으로** 채워진다 → 신원 덮어쓰기 동작 확인
- `/api/Dev/sql` : `PARTNER` 역할 403, `SYSTEM_ADMINISTRATOR` 통과 → 역할 가드 동작 확인

**실제 DB 를 붙여 돌려 보고 잡은 것** (2026-08-22):

- `IsProjDb` 플래그를 개발도구 화면 4곳에서 잘못 켜고 있었다
  (`erd.vue`, `flow.vue`, `db-tools.vue`, `table-manage.vue`).
  서버는 이 플래그로 쿼리를 어디서 찾을지 가른다 — 꺼짐이면 `projmng.devsqlresp`(DB 종류별
  시스템 쿼리), 켜짐이면 `projmng.dev_db_prop`(그 DB 전용 쿼리)다.
  `tablelist`·`proclist`·`columnsOftable` 은 앞쪽이라 켜면 `NullReferenceException` 이 났다.
  원본에서 켜는 곳은 `JsProcDbReturn` 하나(= `code_master`/`code_detail`)뿐이다.
  고친 뒤 정상 동작을 확인했고, `jsCont()` 주석에 판단 기준을 적어 두었다.

## 8. 남은 일

### DB 접속 — 연결 완료 (2026-08-22)

접속 문자열은 `microservices/ProjMngServer/appsettings.Local.json` 에 넣었다
(`.gitignore` 대상이라 커밋되지 않는다. 원본 ProjMng 도 `appsettings.json` 을 같은 방식으로 제외했다).

대상은 `jsini.co.kr:15432` 의 `jsini` DB, `projmng` 스키마다.
저장 프로시저 50개와 테이블 24개가 여기 있다.

> **`SearchPath` 는 붙여 써야 한다.** `ProjService` 가 접속 문자열을 손으로 파싱해
> `SearchPath` 키를 그대로 찾는다(`ProjService.cs:78`). Npgsql 정식 표기인
> `Search Path=`(공백)로 쓰면 스키마를 못 읽어 프로시저를 `.sp_xxx` 로 호출해 전부 실패한다.
> 포털의 `AuthServer/appsettings.Local.json` 은 공백 있는 표기라 따라 쓰면 안 된다.

동작 확인 결과다. 게이트웨이를 거치지 않고 서비스에 직접 넣어 확인했다.

| 경로 | 결과 |
|---|---|
| `sp_devcomm_exec`, `sp_dev_menu_exec`, `sp_dev_proj_exec`, `sp_dev_user_exec` | 정상 (12·38·7·9건) |
| `sp_dev_user_grp_exec`, `sp_projdblist`, `sp_projdbrspolist`, `sp_devsqlresp_base_exec` | 정상 (4·12·48·21건) |
| `sp_proj_wbs_exec` (prj_rid=3) | 정상 (95건, 30컬럼) |
| `sp_projCommon` (projlist·db·compstat·schedule_type·CODE_TYPE) | 정상 — 드롭다운 전부 채워진다 |
| `/Dev` 의 `tablelist`·`proclist`·`columnsOftable` | 정상 (21·40건) |
| `/Dev/sql` 직접 쿼리 | 정상, 역할 가드도 동작 (PARTNER 403) |
| `sp_dev_db_prop_exec`, `sp_dev_srcinfo_dtl_exec`, `sp_dev_activityinfo_exec`, `sp_dev_excel_exec`, `sp_home_todo_pay` | 정상 |
| `md_source_trace`, `md_blazor_scan` | **자료 문제로 실패/빈 결과** (아래 참조) |

### 🟠 판단이 필요한 것

- **DB 통합 여부.** 헬프데스크가 별도 DB(`jinrecept`)를 쓰는 문제가 이미 걸려 있고
  ([12-decisions-pending.md](12-decisions-pending.md) D2), 프로젝트관리가 세 번째 DB 가 된다.
  `funeralv2` DB 의 `projmng` 스키마로 합칠지 별도로 둘지 정해야 한다.
- **WBS·일정의 중복.** 헬프데스크와 프로젝트관리 양쪽에 있다. 지금은 메뉴 이름으로만
  구분해 둔 상태다. 합칠지 둘 다 둘지는 실제로 쓰는 쪽을 보고 정하는 편이 낫다.
- **`sp_dev_user_exec` 사용자 테이블.** 프로젝트관리가 자체 사용자 테이블을 들고 있다.
  헬프데스크는 `auth_user_links` 로 포털 계정과 연결했는데, 같은 방식으로 이을지 정해야 한다.
- **개발자 도구를 운영에 열어 둘지.** [DB 쿼리 테스터]·[DB 개체 탐색]·[테이블 설명 관리]는
  대상 DB 를 직접 건드린다. 운영 환경에서는 `DevTools:AllowRawSql=false` 로 닫는 편이 안전하다.

### 🟡 DB 자료 쪽에서 손봐야 하는 것

이식과 무관하게 **DB 에 등록된 값이 이 장비와 맞지 않아** 실패하는 것들이다.

| 대상 | 증상 | 원인 |
|---|---|---|
| 소스 추적 · Glue 추적 · 소스 스캐너 (`md_*`) | `DirectoryNotFoundException` / 빈 결과 | `projmng.dev_srcinfo.src_path` 가 전부 윈도우 경로다 (`c:/projects/projMng`, `c:\SmartFactoryMES\...`). 서버가 그 경로를 직접 훑으므로 ProjMngServer 가 도는 장비의 실제 경로로 다시 등록해야 한다 → [프로젝트 소스 정보] 화면 |
| `sp_dev_program_exec` | `42P01: relation "projmng.dev_program" does not exist` | 프로시저는 있는데 참조하는 테이블이 없다. **이식본은 이 프로시저를 부르지 않으므로** 화면에는 영향이 없다 |
| `sp_projlist` | 프로시저 자체가 없다 (`sp_projdblist` 만 있다) | 원본 `ProjComTest.razor` 가 이걸 불렀으니 **이식 전에도 그 화면은 열리지 않았다.** [그리드 부품 테스트] 화면의 조회를 `sp_dev_proj_exec` 로 바꿔 동작하게 했다 |

### ⚪ 확인만 하면 되는 것

- 공통코드 ID 몇 개는 원본 화면에서 쓰던 이름을 그대로 넘겼다(`srclist`, `srclang`,
  `todo_state`, `yn`, `user`). 해당 코드가 없으면 그 드롭다운만 빈다.
  주요 코드(`projlist`, `db`, `compstat`, `schedule_type`, `CODE_TYPE`, `projdb`)는 확인 완료다.
- 원본 Blazor 프로젝트(`/home/quri/ProjMng`)는 그대로 남겨 두었다.
  화면을 하나씩 대조해 확인이 끝나면 정리해도 된다.
