# 프론트 체감 속도 점검 — 메뉴 클릭에서 자료가 보일 때까지

2026-09-07 · 대상은 업무 포털(:5557)과 그 뒤의 게이트웨이·서비스다.

측정은 이 장비(개발)에서 떠 있는 서비스에 대고 했다. **개발 장비의 DB 는
`jin114.co.kr:31015` 라 왕복이 28.8ms** 다(`psql -c "select 1"` 실측).
운영은 DB 가 같은 호스트라 이 값이 1ms 아래로 떨어진다 — 그래서 아래에서
"왕복 수" 로 적은 항목은 **운영에서는 브라우저↔서버 왕복이 비용의 몸통**이고
개발에서는 DB 왕복이 몸통이다. 둘 다 줄이는 방향은 같다.

## 진행 상태 (2026-09-07)

**우선순위 1~5 를 고쳤다.** 재서 확인한 값은 각 항목에 적어 두었다.

| | 항목 | 상태 | 잰 값 |
|---|---|---|---|
| 1 | D-1 그림 캐시 | **고침** | 판정은 시험 28건으로 못 박음. 실물은 개발 장비에서 못 잰다(아래) |
| 2 | C-1 JS interop | **고침** | 왕복 6~7 → **1** |
| 3 | A-2 자원 지문 | **고침** | 아홉 개가 `no-cache` → `max-age=31536000, immutable` |
| 4 | A-1 HTML 압축 | **고침** | 184,455 → **28,845 bytes** (brotli, 6.4배) |
| 5 | C-2 트리 재생성 | **고침** | 두 번째 업무 전환부터 179노드를 다시 만들지 않는다 |
| 6~11 | | 손대지 않음 | |

고치면서 **이 문서가 두 군데 틀렸던 것**을 알았다. 해당 항목에 적어 두었다.

- **C-2** — `Reapply` 가 업무 전환마다 두 번 돈다고 적었는데 사실이 아니다.
  `PermissionContext.Apply` 는 알림을 내지 않으므로 한 번이다. 실제 비용은
  다른 데 있었고, 고친 것도 그쪽이다.
- **A-2** — theme.js 가 꽂는 DevExpress 테마 CSS 도 `@Assets` 로 고칠 수 있다고
  적었는데 안 된다. 그 패키지 정적자원에는 **지문이 붙은 변형이 아예 없다**
  (테마 CSS 44개 중 0개).

---

체감 시간은 세 토막으로 갈린다. 토막마다 원인이 다르고 고치는 자리도 다르다.

| 토막 | 언제 | 지금 무엇이 무거운가 |
|---|---|---|
| A. 첫 진입 | 로그인 직후 · F5 | 자원 재검증, 무압축 HTML, 프리렌더 이중 조회 |
| B. 같은 업무 안에서 메뉴 클릭 | 대부분의 클릭 | 순차 왕복, 전량 조회, 참조자료 캐시 수명 |
| C. 업무를 넘는 메뉴 클릭 | 사이드바에서 다른 업무 | **레이아웃 통째 재생성** — 여기가 제일 비싸다 |

---

## A. 첫 진입

### A-1. HTML 응답이 압축되지 않는다 〔**고침**〕

```
$ curl -sD- -H "Accept-Encoding: br, gzip" http://localhost:5557/login | grep -i content-encoding
(없음)
```

`/login` 이 71KB 다. 업무 화면은 프리렌더된 DevExpress 그리드가 실려 그보다
훨씬 크다. 정적 자원은 `MapStaticAssets` 가 미리 압축해 두지만 **HTML 은
아무도 압축하지 않는다.** 저장소에 nginx 설정이 없어 앞단에서 압축하는지도
확인할 수 없다.

**고치는 방법** — `UseJSiniWebApp()` 한 곳에 응답 압축을 넣는다.

```csharp
services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/html"]);
});
// UseRouting 앞
app.UseResponseCompression();
```

**고친 결과** — `JSiniWebApp` 에 Brotli·Gzip 을 등록하고 `UseRouting` **앞**에
세웠다(뒤에 두면 아무 일도 안 일어나고 오류도 안 난다). 재서 확인한 값이다.

| | 크기 |
|---|---|
| 압축 없음 | 184,455 bytes |
| gzip | 30,102 bytes |
| **brotli** | **28,845 bytes** (6.4배) |

세기는 `Optimal` 이다. `SmallestSize` 는 CPU 를 몇 배 쓰면서 몇 %만 더 줄이는데,
이 응답은 **사용자마다 매번 새로 만들어지는 것**이라 그 값을 캐시로 회수할 수 없다.

앞단 nginx 에 `gzip_types text/html` 이 이미 있어도 겹치지 않는다 — 이미
`Content-Encoding` 이 붙은 응답에 nginx 가 또 압축하지는 않는다. 그리고 nginx
설정은 이 저장소에 없으므로(서버에서 관리한다) **여기서 하는 것이 확실한 쪽**이다.

### A-2. `<head>` 자원 아홉 개가 지문 없는 주소라 매번 재검증한다 〔**고침**〕

빌드 매니페스트에는 자원마다 주소가 **두 개** 들어 있다.

```
$ python3 (JSini.Web.Shell.staticwebassets.endpoints.json)
immutable(지문 있음): 1002    no-cache(지문 없음): 1353
_content/JSini.Web.Components/app.css | ['no-cache']
```

`JSiniHead.razor` 는 지문 없는 쪽을 손으로 적어 두었다. 그래서 페이지를 새로
열 때마다 `app.css`(91KB) · `theme.js`(27KB) · 모듈 CSS 여섯 장(73KB) ·
파비콘까지 **아홉 번 304 왕복**을 한다. 내용은 안 받아도 왕복은 한다.

`@Assets[...]` 로 적으면 지문이 붙은 주소가 나가고 `max-age=31536000,
immutable` 이 걸린다. 이 저장소는 이미 그 문법을 쓰고 있다 —
`ScopedCssBundle` 한 줄만 그렇게 되어 있다.

**고치는 방법** — `JSiniHead.razor` 의 `href` 를 전부 감싼다.

```razor
<link rel="stylesheet" href="@Assets["_content/JSini.Web.Components/app.css"]" />
<script src="@Assets["_content/JSini.Web.Components/theme.js"]"></script>
```

**고친 결과** — `JSiniHead.razor` 의 주소 여덟 개와 `MainLayout` 의 로고를 감쌌다.
모듈 스타일시트는 `IPortalModule.StyleSheet` 로 오는 값이라 미리 알 수 없는데,
`Assets[...]` 는 **없는 열쇠를 넣으면 넣은 값을 그대로 돌려주므로** 안심하고
감쌀 수 있다(던지지 않는다 — 이 문서가 처음에 반대로 적어 두었다).

배포본(Release publish)으로 재서 확인한 값이다.

| 주소 | 전 | 후 |
|---|---|---|
| `app.css` → `app.limmmnbyv5.css` | `no-cache` | `max-age=31536000, immutable` |
| `theme.js` → `theme.y1udfsjjn9.js` | `no-cache` | `max-age=31536000, immutable` |
| 모듈 CSS 여섯 장 | `no-cache` | `max-age=31536000, immutable` |
| 파비콘 | `no-cache` | `max-age=31536000, immutable` |

> **개발 환경에서는 확인되지 않는다.** `MapStaticAssets` 가 개발일 때
> 지문이 붙은 주소에도 `no-cache` 를 씌운다(고친 것이 바로 보이게 하려고).
> 그래서 이 표는 `dotnet publish` 산출물을 `ASPNETCORE_ENVIRONMENT=Production`
> 으로 띄워 잰 것이다. Debug 빌드에 환경변수만 바꿔 띄우면 **RCL 정적자원이
> 아예 안 실려**(`_content/…` 가 0바이트) 엉뚱한 결론이 나온다.

**theme.js 가 꽂는 DevExpress 테마 CSS 는 이 방법이 통하지 않는다.**
이 문서가 처음에 「지문 표를 실어 주면 된다」고 적었는데, 그 패키지 정적자원에는
**지문이 붙은 변형이 아예 없다** — 테마 CSS 44개 전부 `no-cache` 한 벌뿐이다.
가리킬 immutable 주소가 없으므로 클라이언트 쪽에서 할 수 있는 일이 없다.

고치려면 우리가 그 경로의 헤더를 덮어써야 하고, 그때 **DevExpress 를 올렸을 때
옛 테마가 남는 문제**를 같이 풀어야 한다(지금은 파일 이름이 버전과 무관하다).
넷이 합쳐 155KB 이고 그중 149KB 가 `core.min.css` 한 장이다.

### A-3. `theme.js` 가 동기로 막고, 테마 CSS 는 그 **뒤에** 직렬로 실린다

FOUC 를 막으려고 일부러 동기로 둔 것이라 그 자체는 맞는 선택이다. 다만 지금은
`27KB 스크립트 받기 → 실행 → 그때 비로소 테마 CSS 4장 요청` 이 직렬이다.
첫 그림이 나오기까지 왕복이 두 겹 쌓인다.

**고치는 방법** — 기본 테마(Fluent Light Blue)의 네 장에 `<link rel=preload>`
를 `<head>` 맨 위에 박아 둔다. 브라우저가 스크립트를 받는 동안 함께 받는다.
theme.js 가 나중에 같은 주소로 `<link>` 를 꽂으면 캐시에서 즉시 나온다.
저장된 선택이 다른 사용자는 그 네 장을 헛되게 받는 셈이지만, `preload` 는
렌더를 막지 않으므로 손해가 왕복이 아니라 대역폭뿐이다.

### A-4. `dx-blazor-all.js` 323KB — 전 부품 번들

DevExpress 부품 전체가 한 장에 들어 있다. 지금 쓰는 부품은 그리드·편집기·
트리뷰·팝업 정도다.

**고치는 방법** — DevExpress 가 부품별 모듈 로딩을 지원하는 버전이면 그쪽으로
바꾼다. 아니면 `preload-script.js` 가 하는 일을 확인해 **`type=module` 을
`defer` 뒤로 미룬다** — 첫 그림은 프리렌더된 HTML 로 이미 나와 있고, 이 번들이
필요한 시점은 회로가 붙은 뒤다. 우선순위는 낮다(압축하면 ~90KB).

### A-5. 프리렌더가 켜져 있어 `OnInitializedAsync` 가 두 번 돈다 〔구조적 · 손대지 않음〕

`App.razor` 가 `RenderMode.InteractiveServer` 를 쓰고 이 값은 `prerender: true`
다. 그래서 **첫 진입·F5 마다 화면의 조회가 두 번 나간다** — 정적 SSR 로 한 번,
회로가 붙고 또 한 번. 게이트웨이·서비스·DB 를 두 벌 태우고, 사용자는 자료가
한 번 떴다 사라졌다 다시 뜨는 것을 본다.

메뉴 클릭(회로 안 라우팅)에는 해당하지 않는다. 첫 진입에만 걸린다.

**고치는 방법** 은 둘 중 하나다.

- **프리렌더를 끈다** — `@rendermode="new InteractiveServerRenderMode(prerender: false)"`.
  첫 그림이 늦어지는 대신 조회가 한 번이다. 이 포털은 첫 화면이 로그인 뒤의
  업무 화면이고 프리렌더된 그리드가 어차피 눌리지 않으므로 손해가 적다.
- **`PersistentComponentState` 로 넘긴다** — 프리렌더가 읽은 자료를 회로가
  물려받는다. 이미 `ThemeSize` 가 그 방식을 쓰고 있어 본보기가 있다.
  화면마다 손으로 적으면 갈라지므로 **`DataPage` 에 넣는다** — 이 뼈대가
  `LoadAsync` 를 감싸고 있으므로 그 안에서 한 번만 처리하면 161개 화면이 함께 받는다.

권하는 쪽은 **`DataPage` 에 `PersistentComponentState`** 다. 프리렌더를 끄면
첫 그림이 흰 화면이 되어 "느려졌다" 로 읽힌다.

---

## B. 같은 업무 안에서 메뉴 클릭

### B-1. 화면 초기화의 왕복이 직렬이다 〔범위 넓음 · 손대지 않음〕

`Task.WhenAll` 을 쓰는 곳이 26곳인데 화면은 161개다. 초기화에서 API 를 넷 이상
부르는 화면 열아홉 개를 찾았다.

| 화면 | 초기화 왕복 |
|---|---|
| `Admin/ServerStatus.razor` | 10 |
| `Admin/UserRoleMap.razor` | 8 |
| `ProjMng/TableManage.razor` · `Admin/UserList.razor` | 6 |
| `Admin/ReleaseNotes` · `Profile` · `CompanyUserList` | 5 |
| 그 밖 열두 화면 | 4 |

`Admin/UserList.razor:238` 이 전형이다.

```csharp
_roles = await Api.GetRolesAsync();
_depts = Flatten(await Api.GetDeptsAsync());
_all   = await Api.GetAccountsAsync();
```

셋이 서로를 기다릴 이유가 없다. 개발 장비에서 이 셋은 왕복 셋 × (게이트웨이 +
DB 28.8ms) 다.

**고치는 방법** — 서로 의존하지 않는 조회는 `Task.WhenAll` 로 묶는다.
`LifeEnv/BirthdayList.razor:221` 이 이미 그렇게 하고 있어 본보기가 있다.

```csharp
var roles = Api.GetRolesAsync();
var depts = Api.GetDeptsAsync();
var all   = Api.GetAccountsAsync();
await Task.WhenAll(roles, depts, all);
_roles = roles.Result; _depts = Flatten(depts.Result); _all = all.Result;
```

> **주의** — 이것은 프론트에서만 안전하다. 백엔드에서 같은 짓을 하면
> `DbContext` 동시 사용으로 죽는다(`PortalBootstrapEndpoints` 주석 참고).

### B-2. 참조자료 캐시가 `scoped` 다 — 사람마다, 업무를 옮길 때마다 다시 읽는다

공통코드·회사·부서 목록처럼 **모두에게 같고 거의 안 바뀌는 자료**의 캐시가
전부 scoped 로 등록돼 있다.

```
Apps/JSini.Web.ProjMng/ProjMngModule.cs:34   AddScoped<CommonCodes>()
Apps/JSini.Web.ProjMng/ProjMngModule.cs:35   AddScoped<BizOptions>()
Apps/JSini.Web.Funeral/Api/CommonCodeClient.cs:40   private readonly Dictionary<...> _cache
Apps/JSini.Web.Admin/Api/OrgOptions.cs
Apps/JSini.Web.HelpDesk/Api/BizOptionService.cs
```

scoped 는 회로 하나(=사용자 창 하나)다. 그래서 접속자가 백 명이면 같은 공통코드
표를 백 벌 읽는다. 게다가 **업무를 넘나들면 Piral 모듈 컨테이너가 갈리면서
이 캐시도 함께 사라진다** — 장례식장 → 헬프데스크 → 장례식장 이면 세 번 읽는다.

**고치는 방법** — `PortalBootstrapStore` 가 이미 이 문제를 정확히 풀어 놓았다.
같은 틀을 쓴다: **싱글턴 + `IMemoryCache` + TTL**. 사용자와 무관한 자료라
부트스트랩과 달리 열쇠에 사람을 섞을 필요도 없다.

```csharp
services.AddSingleton<CommonCodeStore>();   // IMemoryCache, TTL 10분
services.AddScoped<CommonCodes>();          // 얇은 껍데기만 남긴다
```

TTL 은 부트스트랩(2분)보다 길게 잡아도 된다 — 권한과 달리 늦게 반영돼도
틀리는 방향이 위험하지 않다.

### B-3. 서버 페이징이 사실상 없다 〔구조적 · 손대지 않음〕

```
GetPageAsync (서버 페이징) :   1곳
GetListAsync (전량 조회)   : 102곳
GridDevExtremeDataSource / CustomData : 0곳
```

`CommGrd` 는 `PageSize="15"` 로 **받아 둔 목록을 메모리에서 나눈다.** 그래서
클릭 한 번의 값이 (1) 서비스가 전량을 만들고 (2) 게이트웨이가 전량을 옮기고
(3) 회로가 전량을 들고 있고 (4) DevExpress 가 전량으로 정렬·필터 색인을 만드는 것이다.

지금 자료가 작아서 아직 아프지 않다. 실측한 최대 테이블이 `scom.role_menus`
829행, `projmng.dev_activityinfo` 3,947행, `jinrecept.jsini.pushnotificationlog`
31,751행이다. **마지막 것이 화면에 붙는 날 그 화면은 못 쓴다.**

**고치는 방법** — 전면 개조가 아니라 **큰 표만 골라** `CommGrd` 에 서버 페이징
길을 하나 낸다.

- `CommGrd` 에 `DataProvider` 파라미터를 하나 더 받아, 주면
  `DxGrid` 의 서버측 데이터 소스로 붙이고 안 주면 지금대로 동작하게 한다.
  (`GetPageAsync` 가 이미 `page.total` 을 읽으므로 프론트 계약은 서 있다.)
- 백엔드는 `Take`/`Skip` 을 쓰는 곳이 16곳뿐이다. 페이징이 필요한 엔드포인트를
  먼저 고른다 — 알림 로그 · 로그인 로그 · 활동 이력이 그 후보다.
- **당장 할 수 있는 것**: 로그성 화면의 조회 조건에 기간 기본값을 넣어
  전량이 나오지 않게 한다. 코드 한 줄이고 효과가 가장 크다.

### B-4. N+1 〔손대지 않음〕

확인한 것 중 나쁜 쪽은 `Admin/UserRoleMap.razor` 다.

```csharp
// :257 LoadScopesAsync — 회사 수만큼 왕복
foreach (var company in companies)
    var tree = await Api.GetRoleScopeTreeAsync(company.Id);

// :335 DirectRolesAsync — **똑같은 것을 다시** 한다. 사용자를 고를 때마다.
```

주석에 "회사가 많지 않으므로 그만큼 왕복해도 무겁지 않다" 고 적혀 있다.
왕복 하나가 무겁지 않은 것은 맞지만 `DirectRolesAsync` 는 **선택이 바뀔 때마다**
회사 목록과 모든 트리를 다시 읽는다 — 초기화 때 이미 읽은 것이다.

**고치는 방법** — 두 가지가 다 필요하다.

1. `LoadScopesAsync` 가 읽은 트리를 화면 필드에 들고 있고 `DirectRolesAsync` 는
   그것을 본다. 왕복이 0 이 된다.
2. 회사 목록을 도는 부분은 `Task.WhenAll` 로 묶는다.

같은 모양이 `Admin/OrgChart.razor`(:294 · :501 · :823),
`Admin/MenuRoleMap.razor:150`, `Funeral/ArchiveListPage.razor`(:187 · :319),
`ProjMng/SourceTrace.razor:171` 에 있다.

---

## C. 업무를 넘는 메뉴 클릭 — **여기가 제일 비싸다**

`web/CLAUDE.md` 에 이미 적혀 있는 사실이다.

> 모듈을 넘나들 때마다 메뉴·권한·즐겨찾기를 다시 읽는다. 공지와 별개의
> 문제이고 아직 손대지 않았다.

부트스트랩 왕복은 `PortalBootstrapStore`(2분 TTL)가 막아 놨다. 하지만
**레이아웃이 통째로 다시 만들어지는 것 자체**는 그대로고, 거기 딸린 값이 남아 있다.

### C-1. JS interop 이 여섯~일곱 번 직렬로 나간다 〔**고침** — 여섯이 하나가 됐다〕

레이아웃이 새로 생기면 딸린 부품의 `OnAfterRenderAsync(firstRender: true)` 가
모두 다시 참이 된다. 그 안에서 부르는 것들이다.

| 부품 | 부르는 것 | 회 |
|---|---|---|
| `MainLayout` | `jsiniWatermark.show` | 1 |
| `ScreenLock.RestoreAsync` | `sessionStorage.getItem` | 1 |
| `NoticeAutoPopup` | `sessionStorage.getItem` → `localStorage.getItem` | 1~2 |
| `TabBar` | `localStorage.getItem`(고정 탭) | 1 |
| `ThemeToggle` | `jsiniTheme.catalog` → `jsiniTheme.current` | 2 |

Blazor Server 에서 JS interop 한 번은 **브라우저까지 갔다 오는 왕복 한 번**이다.
여섯 번이 직렬이면 왕복 50ms 환경에서 300ms 가 그냥 붙는다. 그리고
`jsiniTheme.catalog` 는 테마 스물둘의 목록 전체를 JSON 으로 실어 온다 —
서랍을 열지도 않았는데.

**고친 결과**

1. **한 번에 받는다.** `theme.js` 에 `jsiniBoot.read(요청)` 을 냈다 — 열쇠 목록을
   받아 사전 하나로 돌려주고, 워터마크와 지우기까지 같은 왕복에 태운다.
   서버 쪽은 `PortalBoot`(scoped)가 조율한다. **먼저 부른 사람이 왕복을 내고
   나머지는 같은 `Task` 를 기다린다** — Blazor 가 `OnAfterRenderAsync` 를
   자식부터 부르므로 순서를 우리가 정할 수 없기 때문이다.
2. `jsiniTheme.catalog` 는 **서랍을 처음 열 때** 부른다. 테마 스물둘의 이름과
   색이 전부 실려 오는데 서랍을 열지 않으면 한 줄도 안 쓰는 값이었다.
   지금 고른 것(`current`)만 부트 왕복에 함께 태운다 — 그쪽은 서랍과 무관하게
   필요하다(쿠키를 막아 둔 브라우저에서 크기를 맞추는 유일한 자리다).
3. 브라우저 저장소 열쇠 다섯을 `PortalBoot` 한 곳으로 모았다. 전에는 네 파일에
   흩어져 있었고 읽는 자리와 쓰는 자리가 다른 파일에 있는 열쇠도 있었다.

로그인 화면에서 끝까지 태워 확인했다 — C# `ForgetUserNoticeClosed()` →
`jsiniBoot.read` 의 `forget` → 브라우저에서 그 열쇠가 사라짐. 공개 공지 팝업이
정상적으로 뜨고, 닫으면 `:public` 에만 표시가 남고(`:user` 는 그대로), 다시
열면 안 뜬다.

**하나를 0 으로 만드는 것은 하지 않았다.** 처음에 「싱글턴 통에 회로 아이디로
담자」고 적었는데, **Blazor 는 부품에게 자기 회로 아이디를 알려 주지 않는다.**
사용자로 열쇠를 만들면 담기는 값의 임자가 어긋난다 — 부트스트랩(사용자의 것,
어느 탭에서 봐도 같다)과 달리 이 값들은 **탭의 것**이다. 탭 하나를 잠그면 그 탭만
잠겨야 하고 고정 탭도 창마다 다르다. 그래서 여섯을 하나로 줄이는 데서 멈췄다 —
**남은 하나는 왕복 하나이고, 탭이 섞이는 위험을 안고 갈 값이 아니다.**
뿌리(C-4)를 잡으면 이 고민 자체가 없어진다.

### C-2. 사이드바 트리 179노드가 전환마다 새로 만들어진다 〔**고침**〕

> **이 문서가 처음에 틀리게 적었다.** 「`Reapply` 가 전환 한 번에 두 번 돈다」고
> 했는데 아니다 — `PermissionContext.Apply` 는 알림을 내지 않으므로(낼 수도
> 없다, `MenuProvider` 를 모른다) 한 번이다. 실제 비용은 아래에 있고, 고친 것도
> 그쪽이다.

사이드바에 들어가는 트리를 만드는 데 두 단계가 있고 **둘 다 179노드짜리 새 객체
그래프**를 만든다.

1. `WithHref` — 메뉴마다 링크 주소를 풀어 채운다
2. `MenuFilter.Filter` — 권한과 화면 크기로 거른다 (`menu with { … }`)

왕복도 아니고 조회도 아니라 눈에 안 띄는데, 포털은 **업무를 넘나들 때마다
레이아웃을 새로 만들기 때문에** 그 둘이 화면 전환마다 다시 돌았다. 그리고
아끼려는 것이 계산보다 **그 뒤에 오는 것**이다 — 결과가 새 객체라
`DxTreeView` 가 179노드를 통째로 다시 그렸다. 값이 한 글자도 안 달라졌는데.

**고친 결과** — 만든 트리를 기억하는 통(`MenuTree`)을 두고 그것을
**부트스트랩 응답 안에** 넣었다(`PortalBootstrapWire.MenuTree`).

그 자리가 요점이다. **통의 수명이 곧 메뉴·권한표의 수명**이라 열쇠를 만들 필요가
없다 — 통이 살아 있다는 것 자체가 「그때 그대로다」라는 뜻이다. 권한을 열쇠에
넣거나 TTL 을 따로 두면 권한이 바뀐 뒤에도 옛 트리를 보여 줄 길이 생기고,
그 틀림은 **「권한이 없는데 메뉴가 보인다」** 쪽이다. 응답에 매달아 두면
응답이 새로 오는 순간 통도 새것이라 그 길이 아예 없다.

거른 결과는 화면 크기마다 하나씩 담는다 — 그것이 거르기의 나머지 입력이다.

`MenuNode` 가 `init` 전용 `record` 라 창을 둘 열어도 한 그래프를 함께 봐도 된다.
그리고 「메뉴를 읽었다」 로그도 **실제로 만들었을 때만** 남긴다 — 전에는 업무를
옮길 때마다 같은 줄이 쌓여 정작 처음 읽은 시점이 묻혔다.

### C-3. 공지 팝업이 업무 전환마다 API 를 다시 부른다 〔JS 왕복만 줄었다〕

`NoticeAutoPopup` 은 sessionStorage 에 닫은 표시가 없으면 `GetPopupAsync()` 를
부른다. 표시가 있어도 그것을 읽으려고 JS 왕복을 한다(C-1).

**지금 상태** — 표시를 읽는 JS 왕복은 C-1 으로 없어졌다(부트 왕복에 함께 실린다).
**API 왕복은 그대로다** — 닫았으면 안 부르고, 안 닫았으면 전환마다 부른다.

이것을 0 으로 만들려면 C-1 에서 접은 것과 같은 벽에 부딪힌다(탭을 가르는 열쇠).
C-4 를 잡는 편이 낫다.

### C-4. 근본 — 레이아웃이 왜 다시 만들어지는가

C-1~C-3 은 증상을 각각 막는 것이다. 뿌리는 **Piral 모듈 컨테이너가 갈릴 때
레이아웃까지 함께 새로 만들어지는 것**이다.

여기는 조사가 더 필요하다. `Routes.razor` 는 평범한
`AuthorizeRouteView DefaultLayout="MainLayout"` 이라 원래는 레이아웃 인스턴스가
유지되어야 한다. `Piral.Blazor.Orchestrator` 가 모듈 컨테이너를 어떻게 끼우는지
읽어야 답이 나온다(패키지는 `net8.0` 타겟이다).

**당장은 C-1·C-2 로 막고**, 뿌리를 잡으면 그 셋이 다 필요 없어진다. 순서를
이렇게 두는 이유는 뿌리 쪽이 Piral 을 걷어내거나 갈아 끼우는 결정으로 번질 수
있어서다 — 지금 얻을 수 있는 것을 먼저 얻는다.

---

## D. 그림과 첨부 — 중계가 캐시를 전부 끈다 〔**고침**〕

`FileDownload.cs:319` 한 줄이 중계하는 **모든** 것에 걸린다.

```csharp
http.Response.Headers.CacheControl = "private, no-store";
```

주석의 이유는 맞다 — 첨부는 공개 여부가 바뀔 수 있고 중간 캐시에 남으면
비공개로 되돌린 파일이 계속 나간다. 문제는 이 경로로 **그림도 지나간다**는 것이다.

| 지나가는 것 | `no-store` 의 결과 |
|---|---|
| 헤더의 사용자 얼굴 | **화면을 열 때마다** 셸→게이트웨이→FileServer |
| 영정 사진 | 목록을 다시 그릴 때마다 전부 |
| 장비 미리보기(`DevicePreviewList`) | 카드 수만큼, 매 조회 |
| 미디어 썸네일 | 목록 수만큼 |
| 공지 본문의 `<img>`(`NoticeHtml`) | 팝업을 띄울 때마다 |

`no-store` 는 메모리 캐시까지 금지한다. 같은 그림을 같은 페이지에서 두 번
쓰면 두 번 받는다.

**고친 결과** — 첨부와 그림을 갈랐다. 가르는 기준은 **위쪽이 준 형식**이다.

```csharp
// 그림(화면에 박히는 것)
http.Response.Headers.ETag = $"\"{id}\"";
http.Response.Headers.CacheControl = "private, max-age=300";

// 첨부(사람이 눌러 받는 것) — 지금 그대로
http.Response.Headers.CacheControl = "private, no-store";
```

형식으로 가르는 이유는 그것이 **브라우저가 이 응답을 어떻게 다루는지와 정확히
같은 기준**이기 때문이다. 이름(`?name=`)이 있느냐로 가르지 않았다 — 그쪽은
부르는 화면이 넘겨 주기로 한 값이라 빠뜨리면 조용히 판정이 뒤집힌다.

정한 값들의 근거:

- **`private`** — 중간 캐시에는 안 남고 브라우저에만 남는다.
- **`max-age=300`** — **바이트가 낡을 걱정은 없다.** 주소의 열쇠가 파일
  아이디이고 FileServer 는 덧쓰지 않는다(사진을 바꾸면 아이디가 새로 생긴다).
  그래서 이 값을 정하는 기준은 **「볼 자격이 없어졌는데 얼마나 더 보이느냐」**
  하나다. 5분이면 화면을 옮겨 다니는 동안은 한 번도 다시 안 받고, 남는 창은
  그 사람 브라우저 안에서 5분인데 그 사람은 방금까지 볼 자격이 있던 사람이다.
- **`ETag` 를 우리가 만든다** — 위쪽(FileServer)이 안 주기 때문이고, 그래도
  되는 이유는 위와 같다(그 아이디의 바이트는 안 바뀐다). `max-age` 가 지난
  뒤의 재검증이 바이트 전송 없이 304 로 끝난다.
- **`If-None-Match` 는 위쪽을 부른 뒤에 따진다.** 앞에서 끊으면 판정
  (FileServer)을 건너뛰게 되어, 비공개로 되돌린 파일에 계속 304 를 주고
  브라우저가 캐시본을 계속 쓴다. 여기서 아끼는 것은 판정이 아니라 전송뿐이다.
- **자료실 갈래는 그림이어도 캐시하지 않는다.** 그 경로의 목적 절반이
  **내려받은 횟수를 세는 것**이라, 브라우저가 캐시본을 쓰면 요청이 서버에 닿지
  않아 숫자가 멈춘다. 화면에 아무 표시도 안 나므로 **세다가 멈춘 것을
  알아차릴 방법이 없다.**

**개발 장비에서는 실물로 못 잰다.** FileServer 는 로컬에 바이트가 없으면
운영 호스트로 302 를 주는데 그 호스트가 개발망에서 안 풀린다(루트 CLAUDE.md).
그래서 중계가 502 를 돌려주고 성공 경로의 헤더를 볼 수가 없다. 대신 판정을
시험으로 못 박았다 — `FileDownloadCacheTests` 28건(형식 가르기 · 자료실 예외 ·
`If-None-Match` 의 약한 검증표·별표·여럿·다른 파일).

얼굴 사진은 더 나아갈 수 있다 — 주소에 아이디가 있어 `max-age` 를 하루로 잡아도
안전하다. 다만 중계가 「얼굴이냐」를 알 방법이 없어 지금은 한 값으로 두었다.

---

## E. 백엔드

### E-1. 권한표 조회가 DB 왕복 넷을 직렬로 쓴다 〔손대지 않음〕

`MenuService.GetMenuPermissionsAsync` 는 계정 → 역할 → 부여 → 메뉴 를 차례로 읽는다.
개발 장비에서 28.8ms × 4 = **115ms**. 메뉴 트리는 `MenuTreeCache` 가 있는데
**권한표에는 캐시가 없다.**

`PortalBootstrapEndpoints` 는 그 위에 즐겨찾기·내정보를 더해 왕복 여섯~일곱이다.
`DbContext` 를 공유해서 병렬로 못 돈다고 주석에 적혀 있고, 그 말이 맞다.

**고치는 방법**

1. **한 쿼리로 줄인다.** 계정→역할→부여→메뉴는 조인 하나로 쓸 수 있다.
   왕복 넷이 하나가 된다. 프론트 계약은 안 바뀐다.
2. **`DbContextFactory` 를 쓰면 병렬이 열린다.** `AddDbContextFactory` 로
   바꾸고 넷이 각자 컨텍스트를 받으면 `Task.WhenAll` 이 가능해진다.
   1번을 먼저 하고, 그래도 모자라면 이쪽이다.
3. 권한표에도 캐시를 둔다. 열쇠는 계정, TTL 은 짧게 —
   **틀리는 방향이 「권한이 없는데 보인다」 쪽**이라 부트스트랩의 2분보다 짧게 잡는다.

### E-2. `AsNoTracking` 이 절반에만 붙어 있다 〔손대지 않음〕

```
ToListAsync()  : 268곳
AsNoTracking   : 108곳
```

읽기만 하는 조회에 변경 추적이 붙으면 EF 가 엔티티마다 스냅숏을 만든다.
행이 적으면 안 아프지만 공짜로 얻을 수 있는 것이다.

**고치는 방법** — 화면에 내려보내는 조회에 `AsNoTracking()` 을 붙인다.
서비스마다 `DbContext` 기본값을 `QueryTrackingBehavior.NoTracking` 으로 두고
**저장하는 곳에서만** 추적을 켜는 편이 빠뜨림이 없다.

```csharp
options.UseNpgsql(cs).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

다만 이 방식은 **기존 저장 경로가 조용히 안 먹게 될 위험**이 있다 —
`_context.Update(...)` 없이 필드만 바꾸고 `SaveChanges` 하는 코드가 있으면
그렇게 된다. 서비스 하나씩 옮기고 저장 경로를 확인한다.

### E-3. Npgsql 풀·타임아웃이 기본값이다 〔손대지 않음〕

`AddDbContext` 일곱 곳 전부 연결 문자열 그대로다. 풀 크기, 명령 타임아웃,
`EnableRetryOnFailure` 가 하나도 지정돼 있지 않다. 지금 규모에서 기본값이
문제는 아니지만 **개발 장비가 원격 DB 를 쓰는 동안에는 재시도가 있는 편이 낫다.**

---

## 손대는 순서

| | 무엇 | 상태 | 값 | 품 |
|---|---|---|---|---|
| 1 | D — 그림에 `private, max-age` + `ETag` | **고침** | 그림 많은 화면이 눈에 띄게 | 파일 한 곳 |
| 2 | C-1 — JS interop 여섯을 하나로 | **고침** | 업무 전환마다 왕복 수백 ms | 작다 |
| 3 | A-2 — `@Assets[]` 로 지문 붙이기 | **고침** | 첫 진입 왕복 아홉 제거 | 작다 |
| 4 | A-1 — HTML 압축 | **고침** | 첫 진입 전송량 6.4배 | 작다 |
| 5 | C-2 — 트리 재생성 + 사이드바 재렌더 | **고침** | 사이드바 179노드 | 작다 |
| 6 | B-2 — 참조자료 캐시를 싱글턴으로 | 남음 | 사람 수·전환 수만큼 곱해지던 것 | 중간 |
| 7 | E-1 — 권한표 한 쿼리 + 캐시 | 남음 | 부트스트랩 절반 | 중간 |
| 8 | B-1 — 순차 왕복을 `Task.WhenAll` 로 | 남음 | 화면 열아홉 곳 | 화면 수만큼 |
| 9 | A-5 — `DataPage` 에 `PersistentComponentState` | 남음 | 첫 진입 조회 절반 | 중간 |
| 10 | B-3 — 큰 표에 서버 페이징 | 남음 | **지금은 안 아프고 나중에 못 쓰게 된다** | 크다 |
| 11 | C-4 — 레이아웃 재생성의 뿌리 | 남음 | C 전체가 사라진다 | 조사 필요 |

1~5 가 끝났다. 서로 얽히지 않아 따로 되돌릴 수 있고, 건드린 것은 셸 공용
(`JSini.Web.Components`)뿐이라 업무 모듈 여섯은 한 줄도 안 바뀌었다.

### 1~5 로 건드린 파일

```
새로 만든 것
  Layout/PortalBoot.cs                      브라우저 상태를 한 왕복으로 (C-1)
  tests/…/FileDownloadCacheTests.cs         캐시 판정 시험 28건 (D-1)

고친 것
  Data/FileDownload.cs                      그림과 첨부를 가른다 (D-1)
  wwwroot/theme.js                          jsiniBoot.read (C-1)
  Layout/MainLayout.razor                   워터마크를 부트 왕복에 (C-1)
  Layout/NoticeAutoPopup.razor              읽기 둘 제거 (C-1)
  Layout/TabBar.razor                       읽기 하나 제거 (C-1)
  Layout/ThemeToggle.razor                  catalog 를 서랍 열 때로 (C-1)
  Security/ScreenLock.cs                    읽기 하나 제거 (C-1)
  Layout/JSiniHead.razor                    @Assets[] (A-2)
  JSiniWebApp.cs                            응답 압축 (A-1) · PortalBoot 등록
  Menu/MenuProvider.cs                      MenuTree 통 (C-2)
  Layout/PortalBootstrap.cs                 그 통을 응답에 매단다 (C-2)
  JSini.Web.Components.csproj               InternalsVisibleTo
```

아키텍처 테스트는 134 → **162건**(캐시 판정 28건 추가) 전부 통과한다.

---

## 재는 방법이 없는 것이 문제다

이 문서의 숫자는 **바깥에서 잰 것**이다. 로그인이 필요한 화면은 재지 못했다.
지금 저장소에는 "메뉴를 눌러 자료가 보일 때까지" 를 재는 수단이 없어서,
고친 뒤에 나아졌는지 말할 방법도 없다.

**먼저 세울 것** — 이 감사보다 이것이 먼저일 수도 있다.

1. **서버 쪽**: `DataPage.LoadAsync` 에 스톱워치를 걸고 화면 열쇠(`RouteKey`)와
   함께 로그로 남긴다. 뼈대가 하나라 **한 곳만 고치면 161개 화면이 다 찍힌다.**
   그 자리에 게이트웨이 왕복 수도 같이 센다.
2. **브라우저 쪽**: `theme.js` 옆에 `performance.getEntriesByType('navigation')`
   을 읽어 한 번 보내는 스크립트를 둔다. 첫 진입만 재면 된다.
3. `Admin/ServerStatus.razor` 가 이미 상황판 자리라 **그 숫자를 띄울 화면이
   이미 있다.**
