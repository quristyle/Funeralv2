# web/ — Blazor 업무 포털 (이행 중)

`fronts/apps/jsini-portal`(Vue3 + vben, 약 90,000줄)을 .NET 10 + Blazor +
DevExpress 로 옮기는 자리다. **업무별로 독립 배포되는 MFE 구조**로 다시 짠다.

지금은 골격만 서 있다 — 업무 앱 여섯 개가 껍데기이고 화면은 아직 옮기지 않았다.

## 구조

```
                        Browser
                           │
                   ┌───────┴────────┐
                   │  Nginx / LB    │
                   └───┬────────┬───┘
                       │        │
              ┌────────▼──┐  ┌──▼─────────────┐
              │Blazor Shell│  │  API Gateway   │
              │   :5557    │  │     :5265      │
              └─────┬──────┘  └──┬─────────────┘
         YARP 경로 프록시           │
    ┌────────┬──────┴───┬────────┐│  ┌────────┬─────────┐
    ▼        ▼          ▼        ▼│  ▼        ▼         ▼
 Funeral  HelpDesk   Admin    …  ││ Auth   funeralv2   AI
  :5561    :5562     :5563       ││ :5264   :5320    :5029
    │        │          │        ││
    └────────┴──────────┴────────┼┘
                                 └── 각 MFE 가 게이트웨이를 **직접** 부른다
                                     (셸을 거치지 않는다 — 병목이 된다)
```

각 MFE 는 **자기 프로세스, 자기 포트, 자기 컨테이너**다.
사용자는 `:5557` 하나만 본다.

```
web/
  JSini.Web.slnx
  .keys/                            Data Protection 키 링 (git 미포함)
  src/
    Shell/JSini.Web.Shell/          :5557  로그인 · YARP 라우팅 · 홈 · 오류
    Shared/
      JSini.Web.Abstractions/       계약 (IPortalModule · 권한 · 메뉴). 구현 없음
      JSini.Web.Models/             Shared Models — 여러 앱이 쓰는 DTO
      JSini.Web.Http/               게이트웨이 클라이언트 + BFF 토큰 처리
      JSini.Web.Components/         Blazor Common — 레이아웃 · DevExpress · 공통 등록
    Apps/
      JSini.Web.Funeral/   :5561  /funeral    ← views/funeral        (21,900줄)
      JSini.Web.HelpDesk/  :5562  /helpdesk   ← views/helpdesk       (18,900줄)
      JSini.Web.Admin/     :5563  /admin      ← views/portal/{system,release,notice,auth}
      JSini.Web.Site/      :5564  /site       ← views/portal/{site,ai}
      JSini.Web.LifeEnv/   :5565  /life       ← views/life           (5,500줄)
      JSini.Web.ProjMng/   :5566  /projmng    ← views/projmng        (4,600줄)
  tests/JSini.Web.Architecture.Tests/
```

## 대화형이 안 되는 두 가지 함정 (둘 다 실제로 밟음)

**증상이 같아서 특히 나쁘다** — 화면은 멀쩡히 그려지는데(프리렌더는 되니까)
버튼이 하나도 안 눌린다. `curl` 로는 정상으로 보이고 **브라우저 콘솔을 봐야**
원인이 보인다. 화면을 옮기다 "왜 안 눌리지" 가 되면 여기부터 의심하라.

### `UseRouting()` 을 명시적으로 불러야 한다

안 부르면 `WebApplication` 이 파이프라인 **맨 앞**에 자동으로 끼워 넣는데,
그건 앱의 `UsePathBase("/funeral")` 보다 앞이다. 라우팅이 접두사가 붙은 원래
경로로 매칭을 시도해 회로 협상이 **405** 가 된다.

### `FallbackPolicy` 를 쓰면 안 된다

`options.FallbackPolicy = options.DefaultPolicy` 는 **명시적 정책이 없는 모든
엔드포인트**에 걸린다. Blazor 회로(`_blazor`)도 예외가 아니라서 협상이 401 →
`/login` 리다이렉트 → 업무 앱에는 그런 화면이 없음 → 다시 → **ERR_TOO_MANY_REDIRECTS**.

같은 보호는 각 앱 `Components/_Imports.razor` 의 `@attribute [Authorize]` 로
얻는다. 컴포넌트에 걸리므로 회로를 건드리지 않고, 새 화면에서 빠뜨릴 수도 없다.

둘 다 `JSiniWebApp.UseJSiniWebApp()` 한 곳에서 처리한다.

## 프로세스가 갈라져서 생긴 규칙 세 가지

앱이 별도 프로세스가 되면서 **컴파일러가 잡아 주던 것들을 프로세스 경계가
가져갔다.** 아래 셋은 어겨도 빌드가 통과하고, 운영에서 그 업무만 조용히 깨진다.
그래서 전부 테스트로 못박았다.

### 1. `@page` 에 자기 접두사를 적지 않는다

`UsePathBase("/funeral")` 이 이미 접두사를 뗀다.

```razor
@page "/status"        ✅ 실제 주소 /funeral/status
@page "/funeral/status" ❌ 실제 주소 /funeral/funeral/status — 아무도 못 연다
```

RCL 이던 시절과 **정반대**다. 그때는 모두 한 라우터에 들어가서 접두사를 적어야 했다.

### 2. 셸의 `PortalApps` 설정과 앱의 `RoutePrefix` 가 같아야 한다

셸은 `appsettings.json` 의 `RoutePrefix` 로 넘기고, 앱은
`IPortalModule.RoutePrefix` 로 `UsePathBase` 를 잡는다. 어긋나면 셸은 넘기는데
앱이 "내 경로가 아니다" 며 404 를 낸다. 각 파일만 보면 둘 다 정상으로 보인다.

### 3. Data Protection 키 링과 응용프로그램 이름이 일곱 앱에서 같아야 한다

셸이 로그인 쿠키를 굽고, 업무 앱들이 그 쿠키에서 게이트웨이 토큰을 꺼내 쓴다.
어긋나면 증상은 **"로그인은 되는데 업무 화면을 누르면 다시 로그인"** 이다.

`SetApplicationName` 을 빼먹으면 기본값이 어셈블리 이름이라 반드시 이렇게 된다.

→ 셋 다 `JSiniWebApp.AddJSiniWebApp()` 한 곳에서 처리한다. **앱의 Program.cs 는
이 메서드를 부르는 것 말고 할 일이 없어야 한다.**

## 지켜야 하는 의존 규칙

1. 업무 앱 → 셸 참조 **금지** (앱이 아는 셸은 `Abstractions` 뿐)
2. 업무 앱 → 다른 업무 앱 참조 **금지**
3. `@page` 에 자기 접두사 중복 **금지**
4. `Components`(Blazor Common) → 업무 앱 참조 **금지**
5. 앱 `Key`·`RoutePrefix` 중복 **금지**
6. 셸 설정과 앱 선언 **일치**

전부 `tests/JSini.Web.Architecture.Tests` 가 검사한다(20건). **문서로 두지 않는
이유는 아무도 문서를 읽으면서 ProjectReference 를 추가하지 않기 때문이다.**

공유가 필요할 때: **두 앱이 쓰면 복제, 세 번째 앱부터 승격.**
화면 조각은 `Components`, DTO 는 `Models` 로. 이 규칙이 없으면 공용
라이브러리가 모든 앱의 결합 지점이 되어 앱을 나눈 의미가 사라진다.

## 라우팅 소유권이 뒤집혔다

| | Vue | Blazor |
|---|---|---|
| 라우트 정의 | DB `scom.system_menus.component` (Vue 파일 경로) | 앱의 `@page` |
| DB 역할 | 라우트 **생성원** | 메뉴 노출·권한 **테이블** |
| 연결 고리 | `component` | `path` |
| 불일치 발견 | 런타임 `console.warn` | 기동 로그 + 아키텍처 테스트 |

DB 의 `component` 컬럼은 **더 이상 읽지 않는다.** 권한 판정은 예전부터 `path`
기준이었으므로(`/auth/menu/permissions`) 그쪽은 그대로다.

`MenuProvider.ReportRouteMismatch` 가 DB 메뉴 경로와 앱의 `@page`(접두사를 붙인
전체 경로)를 대조해 로그로 남긴다. **이행이 끝나면 이 로그가 비어야 한다.**

부수 효과로 Vue 의 `refreshAccessMenus`(없어진 라우트를 `removeRoute` 로 걷어내던
동기화 로직)가 통째로 사라졌다. 라우트가 절대 바뀌지 않기 때문이다.

## 인증 — BFF

토큰이 브라우저로 내려가지 않는다. 브라우저에는 인증 쿠키만 있고, 게이트웨이용
JWT 는 그 쿠키의 클레임에 암호화되어 들어 있다. Vue 때 `accessToken` 이 브라우저
메모리에 있던 것과 다르다 — XSS 로 토큰이 새는 경로가 사라졌다.

**이 설계가 MFE 구조를 가능하게 한다.** 토큰이 쿠키 안에 있으므로 키 링만
공유되면 어느 앱이든 꺼내 쓸 수 있다. 별도 세션 저장소가 필요 없다.

로그인·로그아웃 화면만 정적 SSR 이다(`[ExcludeFromInteractiveRouting]`).
회로 안에서는 `Set-Cookie` 를 붙일 수 없기 때문이다. 그래서 **로그인은 셸에만
있고**, 업무 앱에서 인증이 풀리면 `OnRedirectToLogin` 이 PathBase 를 벗어난
절대 경로로 셸에 보낸다.

## DevExpress

버전은 `Directory.Packages.props` 의 `DevExpressVersion` 한 줄에서만 정한다.
현재 **26.1.4** — nuget.org 에서 그대로 받으므로 인증 피드를 등록할 필요가 없다.

**라이선스 파일은 장비마다 각자 넣어야 한다.** 없으면 빌드는 되지만
`DX1000`/`DX1001` 평가판 경고가 뜬다.

1. <https://devexpress.com/DX1001> 에서 개인 라이선스 키를 내려받는다
2. `%AppData%\DevExpress\DevExpress_License.txt` 에 둔다

CI(GitHub Actions)에서도 같은 파일이 필요하다 — 시크릿으로 넣고 그 경로에 쓴다.

25.1 대로 내리지 않는다: net8.0 타겟이고, 취약점이 있는
`System.Security.Cryptography.Xml` 8.0.2 를 끌고 온다(NU1903, 고위험 8건).

## 개발 명령

```
dev.bat mfe               Blazor 포털 전체 (셸 + 업무 MFE 6개)
dev.bat blazor uiprojmng  셸과 프로젝트관리만 (한 업무만 고칠 때)
dev.bat portal mfe        Vue(:5555)와 Blazor를 나란히 — 화면 대조용
dev.bat stop mfe          전체 중지
```

기본 `dev.bat`(=all)에는 Blazor 가 **들어 있지 않다** — 넣으면 창이 열아홉 개
뜬다. 일상 작업은 아직 Vue 포털이다. 컷오버 때 `SVC_KEYS_DEFAULT` 를 바꾼다.

빌드·테스트는 `web/` 에서 `dotnet build` / `dotnet test`.

## 화면 이관 — DevExpress 그리드에서 먼저 알아야 할 것

### `DataTable` 을 쓰면 `CustomizeEditModel` 이 **필수**다

DevExpress 는 자료 원본에서 편집 모델을 리플렉션으로 만드는데 DataTable 에는
그럴 타입이 없다. 없으면 편집을 여는 순간 죽는다 —
`Cannot create an edit model automatically.`

떼어 낸 행(`DataTable.NewRow()`)을 편집 모델로 주고, `EditModelSaving` 에서
`ItemArray` 로 표에 옮긴다. `DynamicGrid.razor` 가 그렇게 한다.

### 컬럼이 실행 시점에 정해지면 `DataTable` 을 쓴다

**DevExpress 그리드는 `FieldName` 을 리플렉션으로 <ins>속성</ins>에서 찾는다.**
`Dictionary<string, object?>` 를 넘기면 키를 컬럼으로 인식하지 못하고
`A property with the name 'cm_cd' is not found` 로 죽는다. 실제로 밟은 함정이다.

`System.Data.DataTable` 을 넘기면 DevExpress 가 정식으로 지원하고, 세 가지가
공짜로 따라온다.

- **타입별 정렬·필터** — 문자열로 뭉개면 숫자 10 이 2 보다 앞에 온다
- **타입별 편집기** — 날짜는 달력, 불리언은 체크박스가 알아서 뜬다
- **변경 추적** — `DataRowState` 가 Added·Modified 를 알려 준다.
  Vue 가 `quri_ischange` 를 손으로 붙이고 지우던 일이 통째로 사라졌다

### ProjMng — 부품과 진행 상황

프로젝트관리는 보통 CRUD 가 아니다. **저장 프로시저 이름을 실어 보내면 결과와
함께 컬럼 메타를 돌려주는 범용 통로**이고, 업무 로직은 전부 DB 에 있다.

```
Api/ProjMngClient.cs   DbCont · DbSave · DbDelete · JsCont · MdCont
Api/ProjMngTable.cs    프로시저 결과 → DataTable (타입 변환·변경 추적)
Api/ProcGrid.cs        조회·저장·삭제 묶음 (Vue 의 useProcGrid)
Api/CommonCodes.cs     공통코드 조회·캐시 (sp_projCommon 직접 호출)
Components/Shared/DynamicGrid.razor  메타 구동 그리드 (드롭다운 컬럼 포함)
Components/Shared/CodeSelect.razor   공통코드 드롭다운
Components/Shared/SearchBar.razor    조건줄
Components/Shared/SplitPane.razor    마스터-디테일 좌우 분할
Components/Shared/Notice.razor       화면 안내줄
```

이 부품들이 서면 화면은 **"어떤 프로시저를 어떤 파라미터로 부르는가"** 만 적으면
된다. `Wbs.razor`(약 60줄)와 `CommonCode.razor`(약 90줄)가 그 본보기다.

**이식 완료 7 / 27**

| 화면 | 라우트 | 프로시저 |
|---|---|---|
| 공통코드 | `/comm/common-code` | `sp_devcomm_exec` (마스터-디테일) |
| WBS | `/proj/wbs` | `sp_proj_wbs_exec` |
| 소스 정보 | `/proj/source` | `sp_dev_srcinfo_exec` (+상세) |
| 프로젝트 목록 | `/proj/manage` | `sp_dev_proj_exec` |
| 소스 스캐너 | `/proj/scaner` | `md_blazor_scan` |
| DB 로직 항목 | `/sys/db-logic-item` | `sp_devsqlresp_base_exec` |
| 서버 모니터 | `/external/jsini` | (iframe) |

**남은 20개는 부품이 하나씩 더 필요하다** — 화면이 어려운 게 아니라 부품이 없다.

| 막는 것 | 화면 수 | 대상 |
|---|---|---|
| 코드 편집기 (Vue 는 monaco) | 9 | db-tester · db-tools · glue-trace · source-trace · code · component · db · db-logic · sheet |
| 다이어그램 (Vue 는 maxgraph) | 3 | erd · flow · use-case |
| 포털 계정 셀렉트 (biz-select) | 4 | todo · todo-monitor · user · monitoring |
| 날짜 선택 + 탭 | 1 | scheduler |
| 없음 (바로 가능) | 3 | table-manage · funeral-monitor · com-test/fast-test |

원본이 Blazor 였다 (`C:\ProjMng\ProjMngWasm`, 51 razor / 약 9,900줄).
구조 참고로 쓸 만하지만 **정본은 현행 Vue** — 이식 뒤에 고쳐진 것이 있다.

## 남은 일

- [ ] ProjMng 나머지 20개 — 위 표의 부품부터 만든다.
      코드 편집기가 9개를 풀므로 그것이 우선이다 (Vue 는 monaco 를 JS interop 으로 썼다)
- [ ] 순차 이관: LifeEnv → Site → Admin → HelpDesk → Funeral
- [ ] 멀티탭 레이아웃 — **독립 앱 구조에서는 설계가 다시 필요하다.**
      업무를 옮길 때 문서가 새로 로드되므로 탭 상태를 앱이 들고 있을 수 없다.
- [ ] PWA·웹푸시 이식 (`push-sw.js` + 매니페스트. 오프라인 캐시는 포기)
- [ ] SignalR `DeviceHub` 연결 (Funeral 앱. 회로마다 연결하지 말고 공용 연결 + 팬아웃)
- [ ] `scom.system_menus.path` 가 앱 접두사 규칙과 맞는지 실측 — **백엔드 세션과 조율 필요**
- [ ] 운영 배포: 컨테이너 7개 + `.keys` 공유 볼륨 + nginx. `deploy/docker/` 갱신
- [ ] `fronts/apps/jsini-site` → 정적 SSR 별도 앱 (셸과 무관, 병행 가능)
