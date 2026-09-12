# web/ — .NET 10 Blazor 프론트 (Piral.Blazor MFE)

옛 Vue3 + vben 포털(약 90,000줄)을 .NET 10 + Blazor + DevExpress 로 옮기는 자리다.
원본은 2026-09-05 에 저장소에서 걷어냈다 — 필요하면 git 이력에서 꺼낸다
(`git log --diff-filter=D -- fronts`).

## 구조

```
                        Browser
                           │
                   ┌───────┴────────┐
                   │  Nginx / LB    │
                   └───┬────────┬───┘
                       │        │
     ┌─────────────────▼──┐  ┌──▼─────────────┐
     │ Blazor 업무 포털   │  │  API Gateway   │
     │      :5557         │  │     :5265      │
     │ ┌────────────────┐ │  └──┬─────────────┘
     │ │ 셸             │ │     │
     │ │  로그인·레이아웃 │ │     │  ┌────────┬─────────┐
     │ ├────────────────┤ │     ▼  ▼        ▼         ▼
     │ │ Funeral        │ │   Auth   funeralv2   AI  …
     │ │ HelpDesk       │─┼─────┘   :5320    :5029
     │ │ Admin          │ │   :5264
     │ │ Site           │ │
     │ │ LifeEnv        │ │  각 모듈이 게이트웨이를 **직접** 부른다
     │ │ ProjMng        │ │  (BFF — 토큰은 브라우저로 안 내려간다)
     │ └────────────────┘ │
     └────────────────────┘
     ┌────────────────────┐
     │ 회사 소개 사이트    │  포털과 **무관하다.** 인증도, 공유 프로젝트 참조도 없다.
     │      :5556         │  정적 SSR 전용.
     └────────────────────┘
```

```
web/
  JSini.Web.slnx
  src/
    Shell/JSini.Web.Shell/          :5557  셸 (로그인 · 레이아웃 · 모듈 등록)
    Shared/
      JSini.Web.Abstractions/       계약 (IPortalModule · 권한 · 메뉴). 구현 없음
      JSini.Web.Models/             여러 모듈이 쓰는 DTO
      JSini.Web.Http/               게이트웨이 클라이언트 + BFF 토큰 처리
      JSini.Web.Components/         Blazor Common — 레이아웃 · 메뉴 · DevExpress 래퍼
    Apps/
      JSini.Web.Funeral/            /funeral
      JSini.Web.HelpDesk/           /helpdesk
      JSini.Web.Admin/              /admin
      JSini.Web.Site/               /site
      JSini.Web.LifeEnv/            /life
      JSini.Web.ProjMng/            /projmng
    Site/JSini.PublicSite/          :5556  회사 소개 사이트 (정적 SSR)
  docs/
    menu-route-map.md               DB 메뉴 179건 ↔ Blazor 라우트 정본 표
    menu-path-cutover.sql           DB path 를 새 경로로 바꾸는 SQL (아직 안 돌렸다)
  tests/JSini.Web.Architecture.Tests/
```

## 모듈은 어떻게 실리는가 — **빌드 시점 합성**

셸 코드에는 `using JSini.Web.Funeral` 같은 줄이 **한 곳도 없다.** 모듈은
출력 폴더의 `JSini.Web.*.dll` 을 훑어 `IPortalModule` 구현으로 찾는다
(`PortalModuleRegistry`). 그 DLL 이 거기 있는 것은 셸 csproj 의
`ProjectReference` 덕분이고, 그게 전부다.

### 이 구조에서 실제로 한 번 크게 밟은 것

한동안 **셸 출력 폴더에 모듈 DLL 이 하나도 없었다.** 모듈이 각자 프로세스이던
시절에 참조를 뗐고, 단일 셸로 합치면서 다시 붙이지 않았다. 증상은
**"메뉴를 눌러도 화면이 안 열린다"** 하나였고, 그런데

- 빌드가 통과했다 (셸은 모듈 타입을 안 쓰니까)
- 아키텍처 테스트도 통과했다 (테스트 프로젝트는 여섯 모듈을 직접 참조한다)
- 셸은 멀쩡히 떴고 로그인도 됐다

그래서 세 겹으로 막아 두었다.

1. `appsettings.json` 의 `PortalApps` 에 **기대 목록**을 적어 두고 기동 때 대조한다.
   어긋나면 `LogCritical`.
2. 첫 화면(`/`)이 기대·실제를 나란히 보여 준다.
3. `ShellCompositionTests` 가 셸 csproj 를 읽어 참조 여부를 빌드 때 검사한다.

### `AddAdditionalAssemblies` 를 빠뜨리면 같은 증상이 난다

`Routes.razor` 의 `<Router AdditionalAssemblies>` 만으로는 부족하다. 그건
회로가 붙은 뒤 브라우저 안에서 도는 라우팅이고, **첫 요청이 404 냐 아니냐를
정하는 것은 엔드포인트 라우팅**이다. 둘 다 적어야 한다.

```csharp
app.MapMicrofrontends<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. moduleRegistry.Assemblies]);   // ← 이것
```

### Piral.Blazor 는 어디까지 쓰고 있나

모듈 컨테이너(모듈별 DI 격리)와 `PageScripts`/`PageStyles` 주입을 쓴다.
**런타임 파일럿 로딩은 아직 쓰지 않는다** — 모듈이 빌드 시점에 합성되기 때문이다.

기본 `MfDiscoveryLoaderService` 는 설정이 없으면 `feed.piral.cloud` 를 본다.
기동할 때마다 바깥으로 나가고 우리 파일럿은 거기 없으므로, 로컬 캐시만 보는
`MfSnapshotLoaderService` 로 갈아 끼웠다(`Microfrontends:CacheDir`).

무중단 개별 배포가 필요해지면 각 모듈을 nupkg 파일럿으로 말아 그 폴더에
떨어뜨리는 것이 다음 단계고, **그때 고칠 곳은 `Program.cs` 의 그 한 줄과
셸 csproj 의 `업무 MFE 모듈` ItemGroup 뿐**이다.

## 대화형이 안 되는 함정 (실제로 밟음)

**증상이 같아서 특히 나쁘다** — 화면은 멀쩡히 그려지는데(프리렌더는 되니까)
버튼이 하나도 안 눌린다. `curl` 로는 정상으로 보이고 **브라우저 콘솔을 봐야**
원인이 보인다.

### `FallbackPolicy` 를 쓰면 안 된다

`options.FallbackPolicy = options.DefaultPolicy` 는 **명시적 정책이 없는 모든
엔드포인트**에 걸린다. Blazor 회로(`_blazor`)도 예외가 아니라서 협상이 401 →
`/login` 리다이렉트 → 다시 → **ERR_TOO_MANY_REDIRECTS**.

같은 보호는 각 모듈 `Components/_Imports.razor` 의 `@attribute [Authorize]` 로
얻는다. 컴포넌트에 걸리므로 회로를 건드리지 않고, 새 화면에서 빠뜨릴 수도 없다.

### 미들웨어 순서

`UseRouting` → `MapStaticAssets` → `UseAuthentication` → `UseAuthorization`
→ `UseAntiforgery`. 위조방지가 인증보다 앞에 있으면 폼 제출이 익명으로 처리된다.

전부 `JSiniWebApp.UseJSiniWebApp()` 한 곳에서 처리한다.

## 지켜야 하는 의존 규칙

1. 업무 모듈 → 셸 참조 **금지** (모듈이 아는 셸은 `Abstractions` 뿐)
2. 업무 모듈 → 다른 업무 모듈 참조 **금지**
3. `@page` 는 **자기 접두사로 시작**한다
4. `Components`(Blazor Common) → 업무 모듈 참조 **금지**
5. 모듈 `Key`·`RoutePrefix` 중복 **금지**
6. 셸 `PortalApps` 설정과 모듈 선언 **일치**
7. 셸은 **모든** 업무 모듈을 `ProjectReference` 로 참조한다
8. 모듈마다 포괄 라우트(`_Pending.razor`)가 **정확히 하나**
9. 화면 이름이 같은 모듈의 자료 타입 이름을 **가리지 않는다**

전부 `tests/JSini.Web.Architecture.Tests` 가 검사한다(65건). **문서로 두지 않는
이유는 아무도 문서를 읽으면서 ProjectReference 를 추가하지 않기 때문이다.**

공유가 필요할 때: **두 모듈이 쓰면 복제, 세 번째부터 승격.**
화면 조각은 `Components`, DTO 는 `Models` 로.

## 라우팅 소유권이 뒤집혔다

| | Vue | Blazor |
|---|---|---|
| 라우트 정의 | DB `scom.system_menus.component` (Vue 파일 경로) | 모듈의 `@page` |
| DB 역할 | 라우트 **생성원** | 메뉴 노출·권한 **테이블** |
| 연결 고리 | `component` | **`route_key`** (2026-09-07 이후) |
| 불일치 발견 | 런타임 `console.warn` | 기동 로그 + 아키텍처 테스트 |

DB 의 `component` 컬럼은 **더 이상 읽지 않는다.**

## 연결 고리는 URL 이 아니라 **열쇠**다

화면마다 `@page` 아래에 열쇠를 하나 적는다.

```razor
@page "/funeral/room-status"
@attribute [RouteKey("funeral.room-status")]
```

DB 는 그 열쇠(`scom.system_menus.route_key`)를 들고, 사이드바가 링크를 걸 때
기동 때 만들어 둔 표(`RouteInventory.Catalog`)에서 주소를 푼다.
**사전 조회 한 번이고 DB 를 타지 않는다** — 라우팅 속도는 `@page` 그대로다.

### 왜 URL 을 열쇠로 쓰면 안 됐나

`path` 는 **역할-메뉴 권한표와 즐겨찾기의 열쇠**이기도 하다. 그래서 URL 을
한 글자만 고쳐도 그 메뉴의 권한이 조용히 끊기고, 끊기는 방향이 *권한이 없는데
메뉴가 보이는* 쪽이라 특히 나쁘다. 결과로 URL 이 사실상 못 바꾸는 값이 되었고
Vue 시절 경로 69건이 그대로 남아 있었다.

열쇠를 떼어 놓으면 그 매듭이 풀린다.

| | 소유자 | 바꿔도 되나 |
|---|---|---|
| URL (`@page`) | 코드 | **된다.** DB 는 URL 을 모른다 |
| 열쇠 (`RouteKey`) | 코드가 정하고 DB 가 가리킨다 | 한 번 정하면 안 바꾼다 |
| `path` | DB | 권한·즐겨찾기가 걸려 있어 안 바꾼다 |

### **가장 자주 밟던 함정이 없어졌다**

화면을 다 만들어 놓고도 메뉴로 열리지 않는 일이 열몇 건 있었다 —
`DbTester.razor` 가 `/projmng/develop/db-tester` 인데 DB 메뉴는
`/projmng/db/tester` 인 식이다. **각 파일만 보면 둘 다 정상으로 보였다.**

이제 메뉴 관리 화면(`/admin/system/menu`)의 「화면」 칸이 **실려 있는 화면
목록에서 고르게** 한다. 없는 화면을 가리킬 방법이 아예 없다.

빠뜨림은 `RouteKeyTests` 넷이 빌드 때 막는다 — 화면마다 열쇠가 정확히 하나,
저장소 전체에서 유일, 자기 모듈 이름으로 시작, 포괄 라우트에는 붙이지 않기.

### 이행 상태 (2026-09-07)

운영 DB 179건 중 **153건에 열쇠가 채워져 있다**(`docs/menu-route-key-backfill.sql`).
남은 26건은 묶음(CATALOG) 24 + 갈 화면이 없는 vben 대시보드 잔재 2
(`/analytics` · `/workspace`)라 채울 것이 없다.

열쇠가 없는 메뉴는 아래 `RouteAliases` 로 떨어진다. **그 길이 안 쓰이게 되면
`RouteAliases` 와 `menu-path-cutover.sql` 을 함께 지운다.**

### 옛 경로는 `RouteAliases` 가 흡수한다

DB 의 `path` 69건(장례식장·포털관리·소개사이트)은 아직 Vue 시절 경로다
(`/room_status`, `/portal/notice`). 바꾸는 SQL 은
[docs/menu-path-cutover.sql](docs/menu-path-cutover.sql) 에 준비돼 있지만
**아직 돌리지 않았다.**

대신 `JSini.Web.Components/Menu/RouteAliases.cs` 가 옛 경로를 새 경로로 옮긴다.
그래서 DB 를 안 바꿔도 메뉴가 열리고, 나중에 SQL 을 돌려도 그대로 동작한다(멱등).

**`MenuNode.Path` 는 건드리지 않는다.** 권한표와 즐겨찾기의 열쇠가 그 값이라,
바꾸면 *권한이 없는데 메뉴가 보이는* 쪽으로 틀린다. 링크 주소(`Href`)만 옮긴다.

### 메뉴 제목의 다국어는 **서버가** 옮긴다

DB 의 `title` 179건 중 16건이 번역 키다(`system.menu.title` · `role_management`).
사이드바가 그 키를 그대로 보여 주던 것을 고쳤다 — **화면이 옮기지 않는다.**

화면이 제목마다 번역 함수를 부르는 방식은 쓰지 않는다. 대부분이 키가 아니라서
"그런 키는 없다" 경고만 수백 줄 쏟아진다(Vue 에서 실제로 그랬고, 그게 사이드바가
늦게 뜨던 이유였다). 서버가 `scom.i18n_resources` 를 한 번 읽어 **찾았을 때만**
`meta.titleText` 에 담아 준다. 못 찾으면 `null` 이고, 그때는 저장된 제목이 이미
사람이 읽는 글자라는 뜻이다.

메뉴를 내려보내는 곳이 둘이라(`MenuService` · `SystemMenuService`) 한동안
**뒤엣것만** 번역을 붙였다. 그래서 메뉴 관리 화면에서는 "메뉴 관리" 로 보이는
항목이 사이드바에서는 `system.menu.title` 로 보였다. 지금은 둘 다
`MenuTitleTranslator` 한 벌을 쓴다.

값을 고치는 곳은 `/admin/system/i18n` 이다. 고치면 메뉴를 다시 읽는 시점부터 바뀐다.

### 화면 이름이 자료 타입 이름을 가리면 안 된다

Razor 가 만드는 클래스는 `{모듈}.Components.Pages` 에 들어가고 자료 타입은
`{모듈}.Api` 에 있다. 이름이 같으면 **화면 안에서는 화면 자신이 이긴다** —
같은 네임스페이스라 더 가깝기 때문이다.

```csharp
@* MyInfo.razor 안 *@
private MyInfo? _info;                // ← 자료가 아니라 화면 자신
_info = await Api.GetMyInfoAsync();   // ← 형식이 안 맞는다
```

오류 문구가 `'MyInfo' 에는 'UserId' 에 대한 정의가 없습니다` 라서 **자료 타입을
잘못 만든 것처럼 읽힌다.** 네 화면에서 같은 길로 헤맸다.
화면 쪽에 `Page` · `List` · `Board` 를 붙인다. `ComponentNamingTests` 가 막는다.

### 아직 안 옮긴 화면은 "준비 중" 으로 받는다

모듈마다 포괄 라우트가 하나 있다(`Components/Pages/_Pending.razor` →
`@page "/funeral/{*rest}"`). 라우트 우선순위가 리터럴 > 매개변수 > 포괄이라
실제 화면이 있으면 언제나 그쪽이 이긴다.

빈 404 로 두면 **"아직 안 옮긴 화면"** 과 **"주소를 잘못 친 것"** 이 구분되지
않는다. 앞엣것은 기다리면 되는 일이고 뒤엣것은 신고할 일인데, 화면이 같으면
둘 다 신고가 들어온다.

**이행이 끝나면 이 여섯 파일을 지운다. 남아 있다는 것 자체가 표시다.**

## 인증 — BFF

토큰이 브라우저로 내려가지 않는다. 브라우저에는 인증 쿠키만 있고, 게이트웨이용
JWT 는 그 쿠키의 클레임에 암호화되어 들어 있다. Vue 때 `accessToken` 이 브라우저
메모리에 있던 것과 다르다 — XSS 로 토큰이 새는 경로가 사라졌다.

로그인·로그아웃 화면만 정적 SSR 이다(`[ExcludeFromInteractiveRouting]`).
회로 안에서는 `Set-Cookie` 를 붙일 수 없기 때문이다.

Data Protection 키 링을 폴더에 두는 것은 이제 **재기동 때 로그인이 풀리지 않게**
하려는 것이다. 프로세스가 일곱이던 시절에는 그 일곱이 쿠키를 함께 풀어야 해서
필수였고, 어긋나면 "로그인은 되는데 업무 화면을 누르면 다시 로그인" 이 났다.

### 게이트웨이로 나가는 `HttpClient` 는 **쿠키 통을 꺼 둔다** (실제로 밟음)

`HttpClientFactory` 는 기본 핸들러를 **사용자와 무관하게 돌려 쓴다.** 그 핸들러가
쿠키 통을 들고 있으면(기본값이 그렇다) 어느 한 사람의 로그인 응답에 실려 온
쿠키가 통에 남아 **그 뒤의 모든 요청**에 딸려 나간다 — 다른 사용자의 요청에도,
로그인하지 않은 요청에도.

AuthServer 는 로그인할 때 `jsini_file_at` 쿠키를 심고 게이트웨이는 **파일 읽기
경로에서 그것을 신원으로 받는다.** 그래서 첨부 중계를 붙이자
**로그인하지 않은 요청이 비공개 공지의 첨부를 받아 갔다.** 로그인 화면에서
재현했다. 셸의 로그인 전용 클라이언트는 같은 이유로 이미 꺼 두고 있었는데
게이트웨이 클라이언트에만 빠져 있었다(`ServiceCollectionExtensions.NoCookieJar`).

꺼도 잃는 것이 없다 — 우리가 쿠키를 쓰는 곳은 토큰 갱신 하나뿐이고 거기서는
`ITokenStore` 가 사용자별로 들고 있는 값을 `Cookie` 헤더에 직접 싣는다.

### 첨부 내려받기는 셸이 중계한다 (`FileDownload`)

백엔드가 주는 `/api/file/download/...` 는 **브라우저가 게이트웨이와 같은
오리진이던 Vue 시절 주소**다. 지금 브라우저가 보는 것은 포털(:5557)이고 거기에는
`/api` 가 없다 — 그대로 쓰면 404 다. 셸이 중계한다.

| 쓸 것 | 어디에 |
|---|---|
| `FileDownload.UrlFor(fileId, fileName)` | 공지 첨부처럼 파일 아이디를 아는 자리 |
| `FileDownload.ArchiveUrlFor(archiveId, fileId, fileName)` | 자료실 · 플레이어 다운로드 — **내려받은 횟수를 센다** |
| `FileDownload.RelayUrl(저장된값)` | DB 에 `/api/...` 로 박혀 있는 값 (영정 사진 · 미디어 썸네일) |

자료실이 갈래가 다른 이유는 FileServer 로 바로 가지 않기 때문이다.
`auth/help/archives/…/download` 를 거쳐야 횟수가 올라가고(자료실의 「내려받기」
칸이 그 숫자다) 그쪽이 다시 FileServer 로 302 로 넘긴다.

`RelayUrl` 은 파일 주소가 아니면 그대로 돌려주므로 아무 값에나 씌워도 안전하다.
운영 DB 에 그런 상대경로가 66건이고 **절대 URL 은 한 건도 없다.** DB 는
건드리지 않는다 — 절대 URL 로 박아 넣으면 환경을 옮길 때 다시 깨진다.

**익명과 로그인을 셸이 가르지 않는다.** 지금 요청의 신원을 그대로 흘려보내면
FileServer 의 `PublicFileAccessFilter` 가 판정한다 — 로그인 요청이면 통과,
익명이면 `is_public` 인 파일만. 공지 첨부의 `is_public` 은 AuthServer 가 공지를
저장할 때 맞춘다(`PublicFileSyncService` · D-S10). **가르는 코드를 프론트에 또
두면 언젠가 백엔드와 어긋나고, 어긋나는 쪽은 늘 「열려서는 안 되는데 열린」 쪽이다.**

> 옛 공지 본문에 박힌 `<img src="/api/file/download/{id}">` 도 같은 이유로 깨진다.
> `NoticeHtml` 이 **보여 줄 때만** 중계 경로로 옮긴다 — DB 는 건드리지 않는다.

### `AddHttpClient<T>` 의 타입 이름은 저장소 전체에서 유일해야 한다 (실제로 밟음)

클라이언트 이름을 **네임스페이스를 빼고 타입 이름만으로** 짓는다. 그래서 두
모듈에 같은 이름의 타입이 있으면 나중 등록이 던진다. 그런데 그 예외가
**어디에도 드러나지 않는다** — `PortalModuleRegistry` 가 잡아 삼키고, 모듈은
이미 목록에 들어간 뒤라 기동 대조(`PortalApps`)도 통과한다. 결과는 **그 모듈의
서비스 절반만 등록된 채 뜨는 것**이고, 화면은 열리고 그 서비스를 쓰는 동작에서만
죽는다. 장례식장·포털관리에 `FileUploadClient` 가 둘 생기면서 실제로 그랬다.
`HttpClientNamingTests` 가 이제 빌드 때 막는다.

## 공지 팝업

공지는 포털관리가 관리하지만 **보이는 것은 모든 화면**이다. 그래서 DTO 는
`Models`, 조회는 `Components/Layout/NoticeClient`, 화면은
`Components/Layout/NoticePopup` 에 있다 — 셸은 업무 모듈을 이름으로 알지 못하므로
포털관리 모듈에 두면 레이아웃이 못 쓴다.

| 자리 | 무엇을 띄우나 | 회로 |
|---|---|---|
| `MainLayout` (`NoticeAutoPopup`) | 로그인한 사용자. 공개 공지까지 함께 | 쓴다 |
| `Login` 화면 (`PublicNoticePopup`) | `is_public` 인 것만 | **안 쓴다** |
| 공지 관리의 「미리보기」 (`Preview="true"`) | 고른 한 건. **같은 화면을 그대로 쓴다** | 쓴다 |

### 로그인 화면 팝업은 첫 HTML 에 실려 나간다 — 회로를 기다리지 않는다

한동안 로그인 화면이 **부품 하나만 대화형 섬**으로 올려 같은 부품을 썼다
(`<NoticeAutoPopup Public="true" @rendermode="InteractiveServer" />`). 그 값이
컸다 — 팝업이 뜨기까지 브라우저가 이 순서를 모두 지나야 했다.

1. `blazor.web.js` (200KB)
2. DevExpress 모듈 `dx-blazor-all.js` **1.4MB** 를 받아 해석 —
   Blazor 는 `DOMContentLoaded` 를 기다리므로 이것이 끝나야 시작한다
3. `/_blazor/initializers` → `negotiate` → 웹소켓 → 회로 시작
4. 섬이 그려지고 저장소를 읽는다 (왕복 1)
5. **그 다음에** 공개 공지를 읽는다 (게이트웨이 → AuthServer → DB)

개발 장비에서 잰 값 — 로그인 폼이 그려진 뒤 팝업이 뜨기까지 **캐시가 다 찬
상태에서 460ms**, 처음 들어온 사람은 **800ms**. 운영은 왕복마다 실제 지연이
붙어 3·4번에서 더 벌어진다.

지금은 서버가 공지를 읽어 팝업 마크업을 **첫 HTML 에 함께 실어 보낸다**
(`PublicNoticePopup`). 재어 보니 로그인 화면에서 **DevExpress 스크립트와
회로가 통째로 사라졌다** — 그 화면에 대화형 부품이 하나도 없어서 DevExpress
가 스크립트를 꽂지 않고 Blazor 도 회로를 열지 않는다. `DOMContentLoaded` 가
265ms → 177ms 이고, 팝업은 로그인 폼과 **같은 그림에** 뜬다.

| | 옛 방식 | 지금 |
|---|---|---|
| 팝업이 보이기까지 | HTML 후 460ms (캐시 warm) | 첫 그림 |
| 로그인 화면이 받는 것 | + `dx-blazor-all.js` 1.4MB · `blazor.web.js` 회로 | theme.js · CSS 뿐 |
| 공지 조회 | 사람마다 게이트웨이 → DB | `PublicNoticeStore` 통 (30초) |

**`CommPopup`(DxPopup)을 쓸 수 없다.** 정적 렌더에서 껍데기만 내놓고
(`<dxbl-modal class="dxbl-popup-hidden" inert>`) 내용은 브라우저에서 만든다 —
첫 HTML 에 실을 수가 없다. 그래서 골격만 순수 HTML·CSS 로 다시 세웠다
(`.jsini-snotice` — 치수는 떠 있는 DevExpress 팝업에서 잰 값이다).

**갈라지는 것은 골격뿐이다.** 본문 거르개·첨부 주소·본문/첨부/점의 CSS
클래스·게시일과 파일 크기 서식은 로그인 뒤 팝업의 것 그대로이고, `CommPopup`
이 정해 둔 규칙도 값까지 같게 옮겼다.

| | `CommPopup` | 로그인 화면 (`.jsini-snotice`) |
|---|---|---|
| 끌기 | `AllowDrag` + `AllowDragByHeaderOnly` | 머리에서만 (theme.js `dragByHead`) |
| 잡는 표시 | `jsini-drag-head` | **같은 클래스** (app.css 한 곳) |
| 끄는 동안 | `.dxbl-popup-dragging` | `.jsini-snotice--dragging` (같은 규칙 목록) |
| 바깥 클릭 | 닫지 않는다 | 닫지 않는다 |
| Esc | 닫는다 | 닫는다 |
| 최대 높이 | `86vh` · 본문만 구른다 | 같다 |

**머리로만 잡는 이유가 같다** — 본문까지 잡히면 글을 끌어 고르려는 동작이 창
옮기기로 먹혀 공지를 복사할 수 없다. 끌어 옮긴 창은 `transform` 으로 **옮긴
거리만** 얹는다(`left`/`top` 을 주면 flex 가운데 정렬을 걷어내야 해서, 공지를
넘겨 본문 길이가 달라질 때 자리가 튄다). 머리가 화면 위로 나가면 다시 잡을 수
없으므로 사방 40px 은 남게 묶는다.

넘기기·닫기·「오늘 하루 보지 않기」는 theme.js 의 `jsiniNotice` 가 받는다.
그 파일은 `<head>` 에서 동기로 돌고, 팝업 바로 뒤의 인라인 `<script>` 가
파싱되는 동안 `init` 을 부른다 — `DOMContentLoaded` 를 기다리면 회로를
걷어낸 이유가 없어진다. 향상된 이동으로 들어온 경우만 `scanNotices` 가 받는다.

### 「한 번만 뜬다」를 부품 수명에 기대면 안 된다 (실제로 밟음)

`OnAfterRenderAsync(firstRender)` 만으로는 모자랐다. **업무를 옮길 때 레이아웃이
통째로 다시 만들어지기 때문**이다 — 화면이 다른 모듈에 있으면 Piral 의 모듈
컨테이너가 갈리면서 `MainLayout` 이 새로 생기고 `firstRender` 가 또 참이 된다.
첫 화면에서 장례식장 도움말로 옮기자 공지가 다시 떴다.

> 같은 이유로 **모듈을 넘나들 때마다 메뉴·권한·즐겨찾기를 다시 읽는다.**
> 공지와 별개의 문제이고 아직 손대지 않았다.

그래서 닫았다는 사실을 브라우저에 적어 둔다.

| 저장소 | 열쇠 | 무엇을 기억하나 | 언제까지 |
|---|---|---|---|
| sessionStorage | `jsini-notice-closed:user` | 이 탭에서 닫았다 | 탭을 닫을 때까지 |
| sessionStorage | `jsini-notice-closed:public` | 이 탭에서 **방금** 닫았다 | **5분** |
| localStorage | `jsini-notice-dismissed` | 이 공지는 오늘 안 본다 | 날짜가 바뀔 때까지 |

**`:public` 만 시한이 있다.** 그 표시가 막으려는 것은 하나뿐이다 — 비밀번호를
틀려 폼이 다시 올라올 때(정적 SSR 이라 문서가 새로 로드된다) 방금 닫은 공지가
또 뜨는 것. 그런데 값을 `'1'` 로 두었더니 **탭을 닫을 때까지** 안 떴고, 증상이
「전체공개 공지가 로그인 전에 안 보인다」로만 보였다(실제로 신고를 받았다) —
브라우저를 새로 열면 뜨고 그 탭에서만 안 뜨니 원인이 표시로 보이지 않는다.
닫은 **시각**을 적고 5분만 인정한다. 옛 `'1'` 이 남은 탭은 저절로 풀린다.

**공개용과 로그인용 열쇠가 따로다.** 하나로 두면 로그인 화면에서 공개 공지를
닫은 사람이 로그인한 뒤 사내 공지를 못 본다. 그리고 로그인 화면이 뜨면
`:user` 를 지운다 — 곧 로그인할 참이니 다시 띄워야 한다.

**읽는 쪽이 둘이다.** `:user` 와 `jsini-notice-dismissed` 는 `PortalBoot` 의
공용 왕복이 읽고, `:public` 은 **theme.js 가 읽는다**(그 값을 보는 로그인
화면에 회로가 없다). 열쇠 글자는 그래도 `PortalBoot` 하나에만 있다 —
로그인 화면이 `data-` 속성으로 JS 에 넘긴다. 「오늘」의 기준도 서버 날짜
하나다(마크업의 `data-today`). 양쪽이 어긋나면 오류가 아니라 **「오늘 하루
보지 않기가 한쪽에서만 듣는다」** 로 나온다.

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

### 테마 — `<head>` 에 테마 `<link>` 를 적지 않는다

고를 수 있는 것이 스물둘이고(Fluent 22조합 · Classic 넷 · Bootstrap 여섯)
Classic 은 한 장이 2.8MB 다. 다 적으면 첫 방문에 십몇 MB 를 받는다.

전부 `JSini.Web.Components/wwwroot/theme.js` 가 한다 — 저장된 선택을 읽어
필요한 것만 꽂고, 한 번 꽂은 것은 지우지 않고 `disabled` 로 껐다 켠다.

**붙박이 `<link>` 를 하나라도 두면 그것만 끌 수 없다.** theme.js 는 자기가
만든 것만 관리하기 때문이다. 한동안 `office-white` 가 그렇게 남아 있어서
어떤 테마를 골라도 늘 함께 켜져 있었다.

두 가지가 순서에 걸려 있다.

- `theme.js` 는 **`app.css` 보다 앞**에 있어야 한다. 꽂는 자리가 스크립트
  자리라, 뒤에 두면 테마가 우리 CSS 를 덮어 사이드바 폭과 헤더 높이가
  테마마다 달라진다. 자리표(`meta[name=jsini-theme-boundary]`)로 지킨다.
- Bootstrap 을 고르면 스타일시트가 **두 장**이다 — Bootstrap 본체가 먼저,
  DevExpress 의 `bootstrap-external.bs5.min.css` 가 나중. 뒤집히면 DevExpress
  부품만 옛 색으로 남는다. `priorityOf` 가 지킨다.

Bootstrap · Bootswatch 파일은 `wwwroot/bootstrap/` 에 커밋해 두었다
(MIT, 1.2MB). **런타임에 CDN 을 부르지 않는다.**

#### 화면에 Bootstrap 유틸리티 클래스를 쓰지 않는다 (실제로 밟음)

`d-flex` · `gap-1` · `text-muted` 같은 것들이다. Bootstrap 이 실려 있을 때만
있는 클래스인데, **기본 테마인 Fluent 에는 Bootstrap 이 없다.** 그래서
Classic 이나 Bootstrap 테마로 보면 멀쩡하고 기본 테마에서만 어긋난다 —
고른 테마에 따라 달라지는 것이라 재현 조건을 찾기 나쁘다.

떠 있는 화면에서 잰 값:

| 테마 | `.d-flex` |
|---|---|
| **Fluent Light (기본값)** | `display: block` |
| Classic Blazing Berry | `display: flex` |
| Bootstrap Flatly | `display: flex` |

공통코드 화면의 「수정·삭제」 단추가 그래서 붙어 나왔다. 표 안의 단추 묶음은
`jsini-actions`(app.css)를 쓴다. **razor 파일 스물몇 개가 아직 같은 클래스를
쓰고 있다** — 눈에 띄면 그때 바꾼다.

#### 꺼 둔 `<link>` 는 브라우저가 안 받는다 (실제로 밟음)

증상은 **"테마를 골라도 아무 일이 없는데 F5 하면 적용돼 있다"** 였다.

theme.js 는 새 스타일시트가 다 실린 뒤에 옛 것을 끈다(그 사이가 비면 화면이
하얗게 번쩍인다). 그래서 새 `<link>` 를 **꺼 둔 채로** 만들고 `load` 를
기다렸는데, `link.disabled = true` 로 만든 `<link>` 는 브라우저가 아예
내려받지 않는다 — 요청도, `load` 도, `sheet` 도 없다. 기다리던 콜백이 영영
안 와서 갈아 끼우는 코드가 돌지 않았다. 새로고침하면 그때는 **첫 적용**이라
켠 채로 만들어져 정상 동작했고, 그것이 "F5 하면 된다" 의 정체다.

대신 `media = 'not all'` 로 만든다. 정상으로 받아 오고 `load` 도 오는데
적용만 안 된다. 다 받은 뒤 `media` 를 `all` 로 되돌리면서 켠다(`setActive`).
**켤 때 `disabled` 만 만지면 안 된다** — `media` 가 `not all` 로 남아 있으면
켜도 적용되지 않는다.

한 장이 끝내 안 오면 4초 뒤에 넘어간다. 안 예쁜 것보다 안 바뀌는 것이 나쁘다.

#### 테마 창은 오른쪽 서랍이다

떠 있는 창이 아니라 헤더 아래부터 바닥까지 오른쪽을 채우는 판이다
(데모와 같다). 항목이 스물이 넘어서 떠 있는 창으로 두면 그 안에서 또
굴려야 하고 창 밖을 스치기만 해도 닫힌다.

닫혀 있을 때 `display: none` 이 아니라 **화면 밖으로 밀어 둔다**(`transform`).
그래야 여닫는 것이 미끄러져 보인다. `position: fixed` 라 자리를 차지해도
가로 스크롤이 생기지 않고, `visibility: hidden` 이 함께 걸려 있어 안 보이는
단추로 탭 이동이 들어가지 않는다.

덮개는 **헤더를 덮지 않는다.** 덮으면 방금 누른 테마 아이콘으로 다시 닫을 수 없다.

#### 서랍의 생김새는 [docs/테마캡쳐.png](docs/테마캡쳐.png) 가 정본이다

데모의 테마 창을 찍어 둔 그림이다. **치수와 색을 눈대중으로 맞추지 않는다** —
그 그림의 픽셀에서 읽는다. 실제로 그렇게 뽑은 값들이다.

| | 값 |
|---|---|
| 색 네모 | 20×20, 모서리 0.3rem |
| 목록 | 두 칸, 줄 높이 ~37px |
| 밝기·크기 칸 | 한 덩어리(segmented), 칸 하나 ~83×35 |
| 묶음 제목 | 굵게 + **아래**에 밑줄 |
| 고른 것 | 옅은 면 (`#f4f4f3` 언저리) |

Fluent 강조색 열한 개와 Classic·Bootstrap 열 개의 스와치 색도 전부 그
그림에서 읽었다(`theme.js`). 한동안 비슷해 보이는 색을 손으로 적어 두었는데,
**고르기 전 네모와 고른 뒤 화면 색이 서로 달랐다.**

고른 것은 **면으로** 칠한다. 테두리를 두르면 그 줄만 키가 커져 목록이 들썩인다.
그 면 색은 테마 변수가 아니라 글자색을 옅게 섞어 만든다
(`--jsini-theme-picked`) — `--jsini-surface` 는 판 배경과 같아지는 테마가 있어
그런 테마에서는 고른 항목이 표시가 안 났다.

#### 세로 flex 안의 줄은 **눌린다** (실제로 밟음)

서랍 속(`.jsini-theme`)이 세로 flex 인데, 그 자식은 기본으로 줄어들 수 있다
(`flex-shrink: 1`). 항목이 스물하나가 되어 판보다 길어지자 **굴리는 대신 전부
납작해졌다** — 34px 이어야 할 밝기 칸이 12px 로 찌그러져 글자가 잘렸다.

목록은 grid 라 멀쩡해 보여서 더 찾기 나빴다. `.jsini-theme > * { flex: 0 0 auto }`
한 줄로 막는다.

### 크기 — **화면에 `SizeMode` 를 적지 않는다**

서랍 맨 위에서 고르고, 기본값은 DevExpress 기본과 같은 **보통(Medium)** 이다.

#### 고르는 단계는 여섯, DevExpress 모드는 셋이다

DevExpress 가 주는 크기는 셋뿐이다(`SizeMode.Small` · `Medium` · `Large`).
**그 넷째를 만들지 않는다** — 없는 값을 흘리면 그 크기에서만 그리드·달력·팝업이
따라오지 않아 한 화면에 두 크기가 된다.

그런데 사람이 고치고 싶어 하는 것은 대개 **글자 크기**이고, 그것은 우리 사다리
(`--jsini-fs-*`)가 따로 갖고 있다. 그래서 **단계를 여섯으로 늘리고 부품 크기는
가까운 DevExpress 모드로 접는다.**

| 고르는 단계 | `--jsini-fs-base` | DevExpress |
|---|---|---|
| 가장작게 `xxsmall` | 0.625rem (10px) | Small |
| 아주작게 `xsmall` | 0.6875rem (11px) | Small |
| 작게 `small` | 0.75rem (12px) | Small |
| 조금작게 `compact` | 0.8125rem (13px) | Small |
| 보통 `medium` | 0.875rem (14px) | **Medium** (기본) |
| 크게 `large` | 1rem (16px) | Large |

우리가 넣은 셋(`xxsmall` · `xsmall` · `compact`)은 **줄 높이·단추 높이가
「작게」와 같고 글자만 다르다.** 그것이 그 단계들의 뜻이다 — 같은 밀도에서
글자만 조절한다.

접는 표는 **`ThemeSize.Steps` 한 곳에만** 있다. theme.js 는 아이디와 이름만 안다 —
두 곳에 같은 표를 두면 어긋나도 예외가 안 나고, 증상이 「글자는 바뀌는데 부품이
안 따라온다」로만 보인다.

**그래서 `ThemeSize` 가 담는 값이 `SizeMode` 가 아니라 단계 이름이다.**
`SizeMode` 로 담으면 여섯이 셋으로 뭉개져 서랍이 어느 칸을 고른 것인지 알 수
없다 — 아주작게를 골라도 「작게」에 표시가 붙는다. 쿠키와
`PersistentComponentState` 도 같은 이유로 단계 이름을 실어 나른다.

크기 칸은 **판 너비를 채운다**(`jsini-theme__segmented--fill`). 밝기(Light/Dark)는
둘이라 `width: max-content` 로 두어도 되지만, 여섯을 그렇게 두면 판을 넘어가
`overflow: hidden` 에 잘린다 — 마지막 칸이 사라져도 오류가 안 난다.
칸이 좁아 글자는 사다리에서 두 단 내린다(`--jsini-fs-2xs`).

#### 껍데기는 **작아질 때만** 따라가고 「조금작게」에서 멈춘다 (`--jsini-chrome-scale`)

헤더의 아이콘·로고·세로선·띠 높이와 **`CommGrd` 아래 띠의 아이콘**이 이 배율을
곱해 쓴다. 값은 `--jsini-fs-base / 0.875rem` 이고 **0.929 를 넘지 않는다.**

**멈추는 장치가 둘이다** — 그림 **크기**는 이 배율이, 그 그림을 담은 단추의
**간격**은 `SizeMode` 캡이 멈춘다(아래 「크기만 멈추면 간격이 계속 벌어진다」).
한쪽만 있으면 반만 멈춘다.

| 단계 | 배율 | 띠 | 로고 | 아이콘 (헤더·그리드) |
|---|---|---|---|---|
| 가장작게 | 0.714 | 40px | 27×19 | 15 |
| 아주작게 | 0.786 | 44px | 30×21 | 17 |
| 작게 | 0.857 | 48px | 33×23 | 19 |
| **조금작게** | **0.929** | **52px** | **35×25** | **20** ← 최대 |
| 보통 | 0.929 | 52px | 35×25 | 20 |
| 크게 | 0.929 | 52px | 35×25 | 20 |

**「보통」·「크게」가 사다리(1 · 1.143)를 따라가지 않는다.** 글자만 커지고
껍데기는 「조금작게」에서 멈춘다 — 아이콘까지 커지면 띠에 꽉 차 답답해진다.
한동안 캡이 「보통」이었는데 그 둘이 여전히 크게 느껄졌다.

**글자 사다리(`--jsini-fs-*`)를 여기에 직접 쓰지 않는다.** 헤더와 그리드 띠는
본문보다 한 단계 큰 아이콘을 쓰는데(1.35rem), 사다리를 그대로 쓰면 그
「한 단계 크게」가 없어진다. 배율만 받아 자기 기준값에 곱한다.

띠 높이까지 함께 낮추는 이유는, 아이콘과 로고만 줄이면 **띠는 그대로 높아서**
작은 아이콘이 빈 띠 가운데 떠 보이기 때문이다. `--jsini-header-height` 한 곳만
고치면 격자·모바일 사이드바·테마 서랍·AI 서랍이 다 따라온다(쓰는 곳 일곱).

##### 로고는 CSS 로 덮어야 한다 (실제로 밟음)

`BrandMark` 는 크기를 **SVG 속성**(`width` · `height`)으로 준다. 그래서
아이콘만 줄고 **로고는 꿈쩍하지 않았다** — 클래스(`.jsini-header__logo`)를
고쳤는데 헤더가 그 클래스를 더 이상 쓰지 않는 것도 겹쳤다.

`.jsini-header .jsini-brand-mark` 에 `width` 를 주고 `height: auto` 로 둔다
(`viewBox` 가 비율을 지킨다). **38 은 `BrandMark.Size` 의 기본값**이라 그것을
고치면 이 줄도 같이 고친다. 헤더로 좁혀 둔 것은 다른 자리에서 `Size` 를 주고
쓸 때 그 값이 이기게 하려는 것이다.

배율 값은 손으로 적어 둔다 — CSS 는 길이를 길이로 나눌 수 없다.
**사다리를 고치면 그 줄도 같이 고친다.**

##### 크기만 멈추면 **간격이 계속 벌어진다** (실제로 밟음)

배율만으로는 반만 멈춘다. 아이콘 **그림**은 우리 CSS 가 재지만 그것을 담은
DevExpress 단추의 **좌우 여백**은 `SizeMode` 가 정하기 때문이다. 재어 보면
이렇다.

| 단계 | dx 클래스 | 좌우 여백 | 단추 폭 |
|---|---|---|---|
| 작게 | `dxbl-sm` | 3px | 27 |
| 조금작게 | `dxbl-sm` | 3px | 28 |
| 보통 | (기본=Medium) | **5px** | **32** |
| 크게 | `dxbl-lg` | **7px** | **36** |

그래서 「아이콘은 안 커지는데 사이가 계속 벌어진다」가 됐다. 우리가 둔 간격
(`gap`)은 고정 `rem` 이라 범인이 아니다 — **단추 자신이 넓어진 것**이다.

멈추는 방법은 그 자리에만 **`SizeMode` 를 덮어씌우는 것**이다. `ThemeSize.Chrome`
이 고른 단계를 `ChromeCapStep`(`compact`)에서 잘라 돌려주고, 껍데기 네 곳이
그것을 캐스케이드로 흘린다.

```razor
<CascadingValue Name="@ThemeSize.CascadeName" TValue="SizeMode?" Value="@Size.Chrome">
    … 아이콘 단추만 …
</CascadingValue>
```

| 감싸는 곳 | 무엇만 |
|---|---|
| `HeaderTools` | 자기 단추 넷 |
| `ThemeToggle` | `.jsini-theme-anchor` 만 (**서랍 속은 아니다**) |
| `AiChatDrawer` | 여는 단추만 (**`<aside>` 판은 아니다**) |
| `CommGrd` | `commgrd__footer` 만 (**표 자신은 아니다**) |

**헤더를 통째로 감싸지 않는다.** 테마 서랍·AI 서랍·사용자 판이 DOM 에서
`.jsini-header` 의 **자손**이라, 통째로 감싸면 그 판들의 내용까지 「조금작게」로
눌린다. 그것들은 껍데기가 아니라 **내용**이고 사용자가 고른 크기를 따라야 한다.

CSS 로 여백을 덮는 길도 있었지만 쓰지 않았다. DevExpress 여백 값이
**테마 스물둘마다 다르고**, 덮으면 테마를 올릴 때 조용히 어긋난다.

> **`ThemeSize.ChromeCapStep` 과 `app.css` 의 `--jsini-chrome-scale` 캡이 같은
> 단계를 가리켜야 한다.** 어긋나도 예외가 안 나고, 증상이 「어느 단계에서
> 그림은 멈췄는데 간격은 한 단 더 간다」로만 보인다.

한동안 화면마다 `SizeMode="SizeMode.Small"` 을 손으로 박아 두었다 — **145곳**
이었다. 사실상 Small 이 기본이었고, 그래서 "내용이 좀 작게 느껴진다" 는 말이
나왔다. 전부 걷어냈다. **새 화면에서도 적지 않는다** — 적는 순간 그 부품만
사용자의 선택을 안 따른다.

같은 이유로 **글자 크기를 `rem` 으로 박지 않는다.** 아래 사다리 절 참고 —
박으면 그 글자만 다섯 단계를 안 따라온다.

값이 내려가는 길은 `Routes.razor` 를 감싼 `SizeModeScope` 다. DevExpress 부품은
크기를 `[CascadingParameter(Name = "ParentSizeMode")]` 로 받으므로, 라우터를
한 번 감싸면 그리드·편집기·단추·팝업이 모두 받아 간다.

> 그 이름은 `DxComponentBase.ParentSizeModeCascadeName` 에 상수로 있지만
> **internal 이라 참조할 수 없다.** `ThemeSize.CascadeName` 에 글자를 다시
> 적어 두었다. 어긋나도 예외가 나지 않고 **크기만 안 먹는다.**

#### 크기는 테마와 달리 **서버가 알아야 한다**

테마는 스타일시트라 브라우저가 `<link>` 를 갈아 끼우면 끝이다. 크기는 다르다 —
`dxbl-sm`/`dxbl-lg` 는 **서버가 HTML 을 만들 때** 부품 뿌리에 붙인다.
`<html>` 에 클래스를 하나 붙여 두는 식으로는 아무 일도 일어나지 않는다
(테마 CSS 가 `.dxbl-btn.dxbl-sm` 처럼 **부품 자신**을 겨눈다).

그래서 첫 그림이 이미 맞으려면 값을 두 곳에서 얻어야 한다.

| 시점 | 어디서 | 없으면 |
|---|---|---|
| 프리렌더 | 요청에 실려 온 `jsini.size` 쿠키 (theme.js 가 굽는다) | 늘 Medium 으로 그려진다 |
| 회로 시작 | 프리렌더가 남긴 `PersistentComponentState` | 회로가 붙는 순간 Medium 으로 되돌아간다 |

회로에는 HttpContext 가 없어 쿠키를 다시 읽을 수 없다 — 두 번째 칸이 그래서 있다.

#### 글자 크기를 `rem` 으로 박지 않는다 — `--jsini-fs-*` 사다리를 쓴다

`rem` 은 `<html>`(16px)에 묶이는데 **본문은 14px 이다**(테마가 body 에
`--dxds-font-size-base-md` 를 건다). 그래서 `font-size: 0.8rem` 라벨은 옆에 선
DevExpress 편집기보다 작고, 크기를 Large 로 바꿔도 꿈쩍하지 않는다.

사다리의 뿌리(`--jsini-fs-base`)가 고른 단계를 따라간다 — 0.625 · 0.6875 ·
0.75 · 0.8125 · 0.875 · 1rem. 그중 **0.75 · 0.875 · 1 은 DevExpress 가 정한 값을
그대로 옮긴 것**이고 나머지 둘은 그 사이·아래로 우리가 넣었다(위 표 참고). `data-dx-size` 는 theme.js 가 `<head>` 안에서 동기로
세우므로 글자 크기는 스타일시트를 기다리지 않는다.

단은 `2xs · xs · sm · md · lg · xl · 2xl · 3xl · 4xl` 아홉이고, Medium 에서
옛 하드코딩 값과 거의 같은 픽셀이 되도록 배수를 잡았다 — **Medium 화면은
이 작업 전과 같고, Small·Large 를 골랐을 때 비로소 따라온다.**

#### Fluent 의 입력칸은 **아래 선을 따로 그린다** (실제로 밟음)

테두리를 지우려고 `--dxbl-text-edit-border-color: transparent` 를 줘도
**Fluent 에서는 칸 아래에 선 하나가 남는다.** 그 선은 다른 변수로 그린다 —

```css
.dxbl-text-edit { border-bottom-color: var(--dxbl-text-edit-underline-color) }
```

Bootstrap 계열(Classic)에는 그 개념이 없어서 **같은 CSS 가 Classic 에서는
깔끔하고 Fluent 에서만 선이 남는다.** 사이드바의 메뉴 검색 칸이 그랬다.

상태마다 변수가 따로이고, 테마가 상태 규칙에서 그 값을 갈아 끼운다 —
마우스를 올리면 `-hovered`, 누르면 `-active`, 고르면 `-focused` 다. 그래서
**두 집안을 다 비워야** 한다.

| 집안 | 비울 것 |
|---|---|
| 아래 선(Fluent) | `--dxbl-text-edit-underline-color` · `-hovered-color` · `-active-color` · `-focused-color` |
| 네 변 테두리 | `--dxbl-text-edit-border-color` · `-hovered-color` · `-active-color` |

하나라도 남기면 그 상태에서만 선이 되돌아온다 — 실제로 평소만 비웠을 때는
**마우스를 올릴 때 네 변이** 나타났고, 밑줄만 비웠을 때는 **누르는 순간 아래
선이** 나타났다.

갈아 끼우는 규칙은 `:not()` 이 넷 달려 자릿수가 다섯이라 이길 수 없다. 대신
**그 규칙이 읽어 가는 변수**를 비운다 — 변수를 정하는 쪽은 `.dxbl-text-edit`
하나뿐이고 app.css 가 테마보다 뒤에 꽂히므로 같은 자릿수로도 우리 값이 남는다.

**고른 상태에도 아무것도 그리지 않는다.** 선 대신 옅은 바탕(강조색 8%)을
깔아 봤는데 그 바탕이 둥근 네모로 보여 **테두리가 되돌아온 것처럼** 읽혔다.
남는 단서는 깜박이는 캐럿과 지우기 단추뿐이다 — 사이드바 맨 위에 칸이 하나뿐인
자리라 그것으로 충분하다. **표 안의 편집칸처럼 칸이 여러 개 나란한 자리에서는
이 방식을 그대로 쓰지 않는다**(어느 칸에 있는지 알 수 없게 된다).

### 레이아웃 치수는 DevExpress 데모에서 가져왔다

헤더 3.5rem · 사이드바 330px · 검색 3rem · 본문 여백 `1.1rem 1.5rem`.
눈대중이 아니라 데모의 `dx-demo.css` 에서 읽은 값이다. 고칠 일이 있으면
거기를 먼저 본다.

**브레드크럼만 데모를 따르지 않는다.** 데모는 본문 위에 제 줄(3rem)로 두는데
우리는 그 위에 탭 줄(2.5rem)이 하나 더 있어서, 헤더까지 합치면 본문이
시작되기 전에 **9rem 이 사라졌다.** 헤더 오른쪽은 도구 단추가 끝에 붙어
가운데가 늘 비어 있으므로 거기 얹었다 — 제 줄도, 배경도, 아래 선도 없다.

```
[☰][로고] │ 시스템관리자 │ 시스템 › 메뉴 관리        [도구][테마][사용자]
```

그래서 헤더가 좁아지면 브레드크럼이 **줄지 않고 자기 안에서 가로로 구른다**
(`flex: 0 1 auto` + `min-width: 0` + `overflow-x: auto`, 칸마다 `flex: 0 0 auto`).
칸이 눌리게 두면 글자가 잘리고, 안 줄게 두면 도구 단추가 헤더 밖으로
밀려난다. 휴대폰(≤767px)에서는 통째로 감춘다 — 지금 어느 화면인지는 바로
아래 탭 줄이 이미 말해 준다.

**홈 아이콘도 없앴다.** 바로 왼쪽의 로고가 이미 `/` 로 가는 링크라 같은
자리에 같은 링크가 둘이 된다.

사이드바 검색은 우리가 만든 칸이 아니라 `DxTreeView` 의 `ShowFilterPanel`
이다(돋보기 아이콘까지 DevExpress 가 그린다). 겉껍데기가 세 겹이라
`.dxbl-treeview` · `.dxbl-treeview-container` · `.dxbl-scroll-viewer` 에
**모두** flex 를 걸어야 검색 칸이 고정되고 트리만 구른다.

## 체감 속도 — 이미 고친 것과 그 이유

감사 문서가 [docs/frontend-performance-audit.md](docs/frontend-performance-audit.md)
에 있다(읽기용 한 장은 같은 이름의 `.html` 이고 `build-audit-html.sh` 가 뽑는다 —
**손으로 고치지 않는다**). 여기 적는 것은 그중 **되돌리기 쉬운 결정들**이다.

### 응답을 압축한다 — `UseResponseCompression` 은 `UseRouting` **앞**

`MapStaticAssets` 는 정적 자원을 빌드 때 미리 압축해 두지만 **화면 HTML 은
그 대상이 아니다.** 아무도 압축하지 않고 있었고, 배포본 로그인 화면이
184KB → 28.8KB(brotli)로 줄었다.

**순서가 전부다.** 이 미들웨어는 응답 스트림을 갈아 끼우는 방식이라 응답을
만드는 쪽보다 앞에 서야 한다. 뒤에 두면 **아무 일도 일어나지 않고 오류도 안
난다** — 증상이 「압축을 넣었는데 `Content-Encoding` 이 없다」 하나라 원인이
순서로 보이지 않는다.

Brotli 를 gzip 보다 먼저 등록한다(협상이 등록 순서를 따른다). 세기는
`Optimal` 이다 — `SmallestSize` 는 CPU 를 몇 배 쓰면서 몇 %만 더 줄이는데,
이 응답은 **사용자마다 매번 새로 만들어지는 것**이라 그 값을 캐시로 회수할 수 없다.

앞단 nginx 와 겹쳐도 문제없다 — 이미 `Content-Encoding` 이 붙은 응답에 nginx 가
또 압축하지는 않는다.

### `<head>` 의 주소는 **반드시 `@Assets[...]` 를 거친다**

매니페스트에는 자원마다 주소가 **두 개** 들어 있다. 지문이 붙은 쪽만
`max-age=31536000, immutable` 이고 안 붙은 쪽은 `no-cache` 라 **페이지를 새로
열 때마다 304 왕복을 한다.** 손으로 적어 두어서 아홉 개가 전부 그러고 있었다.

없는 열쇠를 넣어도 던지지 않는다 — 넣은 값을 그대로 돌려준다. 그래서 모듈이
선언한 주소(`IPortalModule.StyleSheet`)처럼 미리 알 수 없는 값도 감쌀 수 있다.

> **개발 환경에서는 확인되지 않는다.** `MapStaticAssets` 가 개발일 때 지문이
> 붙은 주소에도 `no-cache` 를 씌운다. 확인은 `dotnet publish` 산출물을
> `ASPNETCORE_ENVIRONMENT=Production` 으로 띄워서 한다. **Debug 빌드에
> 환경변수만 바꿔 띄우면 안 된다** — RCL 정적자원이 아예 안 실려
> (`_content/…` 가 0바이트) 엉뚱한 결론이 나온다.

**DevExpress 테마 CSS 는 이 방법이 통하지 않는다.** 그 패키지 정적자원에는
지문이 붙은 변형이 아예 없다(테마 CSS 44개 중 0개). 가리킬 immutable 주소가
없으므로 클라이언트 쪽에서 할 수 있는 일이 없다.

### 브라우저에서 읽을 것은 `PortalBoot` 한 곳으로 모은다

레이아웃이 뜰 때 부품 다섯이 저마다 저장소를 읽고 워터마크 호출이 하나 더
있었다 — **여섯이 직렬**이었고, 포털은 업무를 넘나들 때마다 레이아웃을 새로
만들기 때문에 그것이 화면 전환마다 났다.

Blazor Server 에서 **JS 호출 하나는 브라우저까지 갔다 오는 왕복 하나**다.
`theme.js` 의 `jsiniBoot.read(요청)` 이 열쇠 목록을 받아 사전 하나로 돌려주고,
워터마크와 지우기까지 같은 왕복에 태운다.

**부품이 새로 생길 때 왕복을 늘리지 않으려면 두 가지를 지킨다.**

1. **열쇠는 `PortalBoot` 에만 적는다.** 새 값이 필요하면 그쪽 목록에 한 줄
   더한다. `theme.js` 에는 적지 않는다 — 양쪽에 적으면 한쪽만 고치는 날이 오고,
   증상은 「그 표시가 조용히 안 읽힌다」다.
2. **`ReadAsync()` 를 부르고 순서에 기대지 않는다.** Blazor 는
   `OnAfterRenderAsync` 를 **자식부터** 부르므로 누가 먼저인지 우리가 정하지
   못한다. 먼저 부른 사람이 왕복을 내고 나머지는 같은 `Task` 를 기다린다.

`jsiniTheme.catalog`(테마 스물둘의 이름과 색)는 **서랍을 처음 열 때** 부른다.
단추를 그릴 때 부르면 서랍을 열지도 않았는데 그 짐이 전환마다 실린다.

> **왕복을 0 으로 만들지 않았다.** `PortalBootstrapStore` 처럼 싱글턴 통에
> 담으려면 **탭을 가르는 열쇠**가 필요한데 Blazor 는 부품에게 회로 아이디를
> 알려 주지 않는다. 사용자로 가르면 임자가 어긋난다 — 부트스트랩은 사용자의
> 것이고 이 값들은 **탭의 것**이다(탭 하나를 잠그면 그 탭만 잠겨야 한다).

### 걸러 둔 메뉴 트리는 **부트스트랩 응답 안에** 기억한다

사이드바 트리를 만드는 데 두 단계가 있고 둘 다 **179노드짜리 새 객체 그래프**를
만든다(`WithHref` · `MenuFilter.Filter`). 왕복도 조회도 아니라 눈에 안 띄는데,
전환마다 다시 돌았고 **결과가 새 객체라 `DxTreeView` 가 179노드를 통째로 다시
그렸다** — 값이 한 글자도 안 달라졌는데. 아끼려는 것이 계산보다 그 재렌더다.

`MenuTree` 통을 `PortalBootstrapWire` **안에** 둔 것이 요점이다. 통의 수명이
곧 메뉴·권한표의 수명이라 **열쇠를 만들 필요가 없다** — 통이 살아 있다는 것
자체가 「그때 그대로다」라는 뜻이다.

**권한을 열쇠에 넣거나 TTL 을 따로 두지 않는다.** 그러면 권한이 바뀐 뒤에도 옛
트리를 보여 줄 길이 생기고, 그 틀림은 **「권한이 없는데 메뉴가 보인다」** 쪽이다.

### 중계하는 그림은 캐시하고 첨부는 안 한다 (`FileDownload`)

한동안 중계하는 **모든 것**에 `private, no-store` 가 걸려 있었다. 첨부에는 맞는
값인데 **이 경로로 그림도 지나간다** — 헤더의 얼굴 · 영정 사진 · 장비 미리보기 ·
미디어 썸네일 · 공지 본문의 `<img>`. `no-store` 는 메모리 캐시까지 금지하므로
같은 그림을 한 페이지에서 두 번 쓰면 두 번 받았다.

가르는 기준은 **위쪽이 준 형식**이다(`image/*`). 브라우저가 이 응답을 어떻게
다루는지와 정확히 같은 기준이기 때문이다. **이름(`?name=`)으로 가르지 않는다** —
그쪽은 부르는 화면이 넘겨 주기로 한 값이라 빠뜨리면 조용히 판정이 뒤집힌다.

| | 값 | 왜 |
|---|---|---|
| 그림 | `private, max-age=300` + 우리가 만든 `ETag` | 바이트는 안 바뀐다(아이디가 열쇠고 덧쓰지 않는다). 정하는 기준은 **볼 자격이 없어졌을 때 얼마나 더 보이나** 하나다 |
| 첨부 | `private, no-store` | 한 번 받으면 끝이라 얻을 것이 없다 |
| **자료실 갈래** | `private, no-store` (그림이어도) | **내려받은 횟수를 세는 경로**다. 캐시본을 쓰면 요청이 서버에 닿지 않아 숫자가 멈추고, **화면에 표시가 안 나서 멈춘 것을 알 수 없다** |

`If-None-Match` 는 **위쪽을 부른 뒤에** 따진다. 앞에서 끊으면 판정(FileServer)을
건너뛰게 되어 비공개로 되돌린 파일에 계속 304 를 준다. 여기서 아끼는 것은
판정이 아니라 **바이트 전송**뿐이다.

**개발 장비에서는 실물로 못 잰다** — FileServer 가 운영 호스트로 302 를 주고
그 호스트가 개발망에서 안 풀려 중계가 502 다. 판정은 `FileDownloadCacheTests`
28건이 지킨다.

## 개발 명령

```
dev.bat blazor            업무 포털 (:5557)
dev.bat web               회사 소개 사이트 (:5556)
dev.bat stop blazor       중지
```

`front` · `portal` · `mfe` 는 `blazor` 의 옛 이름이라 그대로 받아 준다.

빌드·테스트는 `web/` 에서 `dotnet build` / `dotnet test`.

**포털이 떠 있으면 빌드가 실패한다** — 실행 중인 프로세스가 DLL 을 물고 있어
복사가 안 된다(MSB3027). `dev.bat stop blazor` 를 먼저 한다.

## 화면 이관 — DevExpress 그리드에서 먼저 알아야 할 것

### `DataTable` 에는 **편집 창을 열지 않는다**

DevExpress 는 자료 원본에서 편집 모델을 리플렉션으로 만드는데 DataTable 에는
그럴 타입이 없다. 그대로 편집을 열면 죽는다 —
`Cannot create an edit model automatically.` (떼어 낸 행을 편집 모델로 주고
`EditModelSaving` 에서 `ItemArray` 로 옮기는 길이 있고, 지워진 `DynamicGrid`
가 그렇게 했다.)

지금은 그 길을 쓰지 않는다. **칸을 미리 알 수 없는 화면은 읽기 전용이거나,
고칠 칸 하나를 셀 안에서 바로 고친다**(`RuntimeColumns` 의 `Editable`).
고친 줄은 `DataRow` 가 스스로 기억하므로(`DataRowState`) 화면은
`ChangedRows()` 만 보면 된다. 아래 「ProjMng — 부품」 참고.

### 컬럼이 실행 시점에 정해지면 `DataTable` 을 쓴다

**DevExpress 그리드는 `FieldName` 을 리플렉션으로 <ins>속성</ins>에서 찾는다.**
`Dictionary<string, object?>` 를 넘기면 키를 컬럼으로 인식하지 못하고
`A property with the name 'cm_cd' is not found` 로 죽는다. 실제로 밟은 함정이다.

`System.Data.DataTable` 을 넘기면 DevExpress 가 정식으로 지원하고, 세 가지가
공짜로 따라온다.

- **타입별 정렬·필터** — 문자열로 뭉개면 숫자 10 이 2 보다 앞에 온다
- **타입별 편집기** — 날짜는 달력, 불리언은 체크박스가 알아서 뜬다
- **변경 추적** — `DataRowState` 가 Added·Modified 를 알려 준다

### ProjMng — 부품

프로젝트관리는 한동안 **보통 CRUD 가 아니었다.** 저장 프로시저 이름을 실어
보내면 결과와 컬럼 메타를 돌려주는 범용 통로가 있었고, 업무 로직이 전부 DB 에
있었다. **2026-09-12 에 그 프로시저 23개를 백엔드로 옮기고 DB 에서 지웠다**
(경위와 그때 드러난 결함 스물몇 가지는
[docs/commgrd-dynamicgrid-merge.md](docs/commgrd-dynamicgrid-merge.md)).

```
Api/ProjMngClient.cs   Js · Md · RawSql — **DB 가 아니라 「서버가 하는 일」**
Api/ProjMngTable.cs    그 결과 → DataTable (타입 변환·변경 추적)
Api/*Client.cs         업무 자료. 자기 이름의 REST 통로로 간다
                       (ProjectClient · WbsClient · HomeTodoClient · …)
Api/CommonCodes.cs     드롭다운 목록 조회·캐시 (projmng/proj-codes)
Api/BizOptions.cs      포털 계정 목록 조회·캐시
Components/Shared/RuntimeColumns.razor 실행 시점에 정해지는 칸 (CommGrd 안에 넣는다)
Components/Shared/WbsEditForm.razor    WBS·일정표가 같이 쓰는 편집 창
Components/Shared/ParticipationBoard.razor  참여 배정 — 참여자·투입 관리 두 메뉴가 이것 하나를 연다
Components/Shared/ProjectGrid.razor    읽기 전용 프로젝트 표
Components/Shared/CodeSelect.razor     공통코드 드롭다운
Components/Shared/BizSelect.razor      포털 계정 드롭다운
Components/Shared/SourceSelect.razor   소스 드롭다운
Components/Shared/CodeEditor.razor     monaco 편집기 (JS interop · 늦게 불러온다)
Components/Shared/DiagramViewer.razor  다이어그램
Components/Shared/DateRangeTabs.razor  기간 선택 + 탭
Components/Shared/SearchBar.razor      조건줄
Components/Shared/SplitPane.razor      마스터-디테일 좌우 분할
```

**표는 `CommGrd` 하나다.** `DynamicGrid` 라는 두 번째 그리드가 있었는데
2026-09-12 에 지웠다 — 표 부품이 둘이면 아래 띠·줄무늬·쪽나누기·엑셀이 두 벌이
되고 한쪽만 고치는 날이 온다(실제로 갈라져 있었다).

#### 옛 도구(draw.io)로 그린 그림은 옮겨야 읽힌다

유즈케이스 화면(`/projmng/design/use-case`)이 읽는 `projmng.dev_proj_prop`
(`prop_type = 'USE_CASE'`)에 **20건이 전부 `<mxGraphModel>` XML** 로 들어
있었다. 지금 화면이 쓰는 `ErdModel` JSON 과 다른 형식이라 파서가 **조용히 빈
그림**을 돌려주고, 화면은 「저장된 그림이 없습니다」로 보였다. 그것을
「아직 안 그렸구나」로 읽은 사람이 새로 그려 저장하면 **옛 그림이 덮어써진다.**

한동안 `ErdModel.IsLegacyDrawing` 으로 **저장만 막아** 두었는데, 막는 것은
잃지 않게 할 뿐 보이게 하지는 않는다. 2026-09-13 에
`scripts/projmng-drawio-to-diagram.py` 로 실제로 옮겼다 — 도형 281 · 관계선 123.
같은 스크립트가 DB 접속 속성(`dev_db_prop`)도 본다(아래).

**원본은 지우지 않았다.** 같은 줄이 `prop_type = 'USE_CASE_MXGRAPH'` 로 한 벌
남아 있고 화면은 그 갈래를 읽지 않는다. 되돌리기는 `UPDATE` 한 줄이다
(스크립트 머리말).

| draw.io | 지금 형식 |
|---|---|
| `mxCell[vertex=1]` | `entities[]` (`manual: true`) |
| `value` (HTML) | `name` — 태그를 털어 낸 글자 |
| `mxGeometry x/y/width/height` | `x/y/w/h` |
| `mxCell[edge=1]` (source·target 있음) | `relations[] {from,to,label}` |
| 끝점이 좌표뿐인 선 · 그림 · 색·모양 | **버린다**(줄마다 몇 개인지 찍는다) |

##### 크기가 적힌 도형은 기본값으로 밀어 올리지 않는다 (실제로 밟음)

`diagram-viewer.js` 가 상자 크기를 `Math.max(entity.w, DEFAULT_W)` 로 잡고
있었다. 기본값(180×60)은 **크기가 안 적힌 새 도형**을 위한 것인데, 적혀
있는 것까지 그 바닥으로 올리면 작게 그려 둔 도형이 전부 같은 크기가 된다.

옮겨 온 유즈케이스에서 그대로 드러났다 — 도형 281개 중 **247개**가 그 바닥보다
작아서, 배치를 정확히 옮겨도 상자들이 겹쳐 덩어리로 보였다. 거실 배치도의
책장·의자, 화살표, 글자 조각처럼 **작아야 뜻이 통하는** 도형이 많다.
칸이 있는 표 상자는 예전 그대로다(줄이 늘면 상자가 커져야 해서 바닥이 뜻을 가진다).

##### 손으로 만든 도형의 라벨은 **저절로 접히지 않는다**

`whiteSpace: 'wrap'` 이 있어도 그렇다. maxgraph 는 라벨을 **HTML 로 그릴
때만** 접고, 그 판정은 값이 DOM 노드이거나 `strictHtml` 일 때만 참이다.
손으로 만든 도형은 더블클릭해 이름을 고치는 대상이라 글자 그대로 둔다
(HTML 을 넣으면 편집기에 태그가 보인다).

대신 **줄바꿈은 들어 있으면 그대로 그려진다.** 그래서 옮기는 쪽이 상자 폭에
맞춰 줄을 끊어 담는다 — draw.io 가 눈으로 보여 주던 접힘이 실제 줄바꿈이 된다.
**띄어쓰기에서만, 눈에 띄게 넘칠 때만**(1.35배) 끊는다. 글자 사이에서 끊으면
표 이름이 `t_mg_srt_target_res` / `ult_dtl` 로 갈라지고, 아슬아슬한 줄까지
손대면 원래 한 줄이던 것이 두 줄이 된다. 20건 중 손댄 것은 **8줄**이다.

##### DB 접속 속성 8건도 옮겼다 — 그리고 열 수 있게 했다

`projmng.dev_db_prop` 에도 draw.io 가 8건 있었다(`ER Diagram` 2 · `flow` 1 ·
`공통` 2 · `인터페이스` 1 · `DB 이중화` 2). 도형 53 · 관계선 32 를 옮겼고,
원본은 **같은 이름의 새 줄**로 남겼다(`db_ptype = 'MXGRAPH'`, 설명 칸에
`원본: db_prid=N`).

**줄마다 `db_prid` 로 집는다.** 이 표에는 제약도 인덱스도 없어서 `(db_rid,
db_pkey)` 가 겹치는 줄이 실제로 있다 — `(4, 'ER Diagram')` 이 둘이고
**내용이 서로 다르다**. 그 짝으로 고치면 한 번에 두 줄이 같은 값으로 덮여
한쪽이 사라진다.

###### 그 8건은 어느 화면도 읽지 않고 있었다

ERD 화면과 업무 흐름 화면은 **`db_pkey='erd'` 한 줄만** 읽는다. 그래서 형식을
옮겨도 여전히 안 보였다. 두 화면에 **「그림」 고르개**를 붙였다 — 그 접속에
저장된 그림들을 골라 연다. 항목을 만드는 규칙은 `Api/DrawingOption.cs` 한
곳에 있다(두 화면이 같은 줄들을 나눠 보므로 갈라 두면 목록이 어긋난다).

| 거르는 것 | 왜 |
|---|---|
| 그림이 아닌 속성 | 같은 표에 공통코드 질의·프로시저 서식이 있다. 열면 빈 캔버스만 뜬다 |
| 보관본(`MXGRAPH`) | 같은 이름이 두 벌 뜨고, 골라 봐야 「옛 도구라 못 연다」만 나온다 |
| 겹치는 이름 | 지우지 않고 **번호를 붙인다**(`ER Diagram #7`) — 겹칠 때만 |

###### ERD 가 아닌 그림에는 표를 얹지 않는다

이 두 화면은 저장본 위에 **대상 DB 의 표 목록**을 얹어 그린다. 그런데 같은
접속에 구성도·인터페이스 같은 그림도 저장돼 있다. 거기에 표 상자 일흔 개를
얹으면 그림이 파묻히고, 그 상태로 **「저장」을 누르면 그 표들이 눌러앉는다** —
되돌리려면 하나씩 지워야 한다. `db_pkey='erd'` 일 때만 얹는다.

대상 DB 에 묻는 것 자체는 그대로 나간다. 네 조회를 나란히 띄우고 나서야 어느
그림을 열지 알게 되기 때문이고, 순서를 뒤집으면 **ERD 를 열 때 왕복이 둘로
늘어난다** — 그쪽이 훨씬 잦다.

#### 참여 배정은 화면 하나다 — 축만 뒤집는다 (`ParticipationBoard`)

프로젝트 참여자(`/projmng/proj/user`)와 투입 관리(`/projmng/proj/appointment`,
메뉴 제목은 「일정 편집」)는 **각도만 다른 같은 표**였다. 그쪽이
「이 프로젝트에 누가」, 이쪽이 「이 사람이 어디에」다. 2026-09-12 에 화면
실체를 하나로 합치고 **메뉴 두 건은 그대로 두었다** — 시작 축만 다르게 연다.

갈라 두었을 때 실제로 어긋나 있었다. 참여자 화면은 체크할 때마다 사람 목록을
**통째로 다시 읽었고** 투입 관리는 안 읽었다. 같은 저장인데 왕복이 두 배
다른 것을 **파일만 보면 둘 다 정상으로 보인다.**

| | 합치기 전 | 지금 |
|---|---|---|
| 프로젝트 기준으로 사람 넣기 | **못 한다**(아래) | 왼쪽에서 프로젝트를 고른다 |
| 기준을 고를 때 | 왕복 1~2 | **0** — 세 번에 다 받아 놓는다 |
| 체크 10개 연타 | 왕복 20 | 첫 건 + 나머지 묶음 |
| 되돌리기 | 없다 | 방금 한 묶음 전체 |

**「프로젝트 기준으로 넣기」가 왜 막혀 있었나.** 옛 참여자 화면의 「프로젝트」
조건은 왼쪽 사람 목록을 *그 프로젝트의 참여자만*으로 좁혔다. 확인에는 맞는
동작인데 그 순간 **아직 안 들어간 사람이 목록에서 사라진다** — 넣을 사람이
화면에 없다. 그래서 조건을 「전체」로 되돌리고 사람을 한 명씩 골라 오른쪽의
프로젝트 전부에서 같은 프로젝트를 열 번 찾아야 했다.

지키는 규칙 넷은 부품 머리말에 있다. 여기 적을 것은 **밟은 것 셋**이다.

##### `@@bind-Text:event="oninput"` 은 DevExpress 편집기에 안 듣는다 (실제로 밟음)

값은 칸에 들어가는데 **바인딩이 안 걸려서** 찾기가 조용히 아무 일도 하지
않는다. 오류가 없고 글자는 보이므로 「검색이 안 된다」로만 보인다.
사이드바 메뉴 검색과 같은 방식을 쓴다 — `BindValueMode="BindValueMode.OnInput"`
+ `InputDelay`.

##### 대기줄 앞에서 기다리면 묶이지 않는다 (실제로 밟음)

연달아 누른 것을 묶으려고 대기줄을 두었는데, 그 앞에 「보내는 중이면
기다린다」를 안전판으로 넣었더니 **두 번째 클릭이 첫 번째가 끝날 때까지
서 있다가 자기 묶음을 따로 보냈다.** 열 번 누르면 열 번 나갔고, 증상이
「되돌리기를 눌렀는데 한 건만 되돌아온다」로만 보였다.

기준을 옮기는 길 셋(`PickAsync` · `SetAxis` · `Drill`)이 보내던 것을 기다리면
충분하다 — 기준이 섞일 자리는 거기뿐이다.

##### 되돌리기는 「보낸 것」이 아니라 「보내기 전 상태」를 담는다

연타가 1+3+6 처럼 여러 묶음으로 나뉘어 나가면, 보낸 것을 뒤집는 방식은
**마지막 묶음만 되돌린다.** 한 번 보내기 시작해서 대기줄이 빌 때까지를 한
단위로 보고, 그 사이 손댄 줄들의 **손대기 전 상태**를 담는다.

##### 체크할 때 목록을 다시 정렬하지 않는다

「참여한 것 위로」는 기준을 고르거나 찾기를 바꿀 때만 매긴다. 체크할 때마다
매기면 방금 켠 줄이 손가락 밑에서 맨 위로 튀어 올라가고 다음 줄이 밀린다.

##### 저장은 일괄 통로 하나다

`POST projmng/project-users/assignments/bulk` — `{ prjRid?, userId?, add[], remove[] }`
를 한 트랜잭션으로. **한 건짜리도 이 길로 간다.** 한 줄짜리 통로가 따로
있었는데 없앴다 — 갈래를 둘로 두면 한쪽에만 걸리는 버그가 생긴다(넣기가
한쪽은 `WHERE NOT EXISTS`, 다른 쪽은 `ON CONFLICT` 로 갈릴 뻔했다).

기준은 프로젝트나 사람 **둘 중 하나**여야 한다. 둘 다 주면 `add[]` 에 담긴
것이 아이디인지 프로젝트 번호인지 알 수 없어 서버가 거절한다 — 조용히 한쪽을
고르면 **엉뚱한 짝이 들어가고도 200 이 나간다.**

`projmng.dev_proj_user_map` 에 **기본키를 걸었다**(`(prj_rid, user_id)`,
`deploy/sql/projmng-proj-user-map-pk-2026-09-12.sql`). 제약이 하나도 없던
표라 같은 짝이 두 줄 들어갈 수 있었고, 한 번에 여러 건 보내게 되면서 그 틈이
넓어졌다. **운영 DB 에 이미 반영했다.**

#### 칸을 미리 알 수 없는 화면은 `RuntimeColumns`

다섯 화면이 그렇다 — 쿼리 테스터(사람이 친 SQL) · DB 도구 · 테이블 관리 ·
소스 추적 · 소스 스캐너. 자료를 `DataTable` 로 받고 칸을 **받고 나서** 세운다.

```razor
<CommGrd TItem="DataRow" Data="@_data.Table" …>
    <Columns>
        <RuntimeColumns Table="@_data.Table" Order="@_data.ColumnOrder"
                        Hidden="@BodyColumns" Editable="@DescColumns" />
    </Columns>
</CommGrd>
```

**`Dictionary<string, object?>` 를 넘기면 안 된다.** DevExpress 그리드는
`FieldName` 을 리플렉션으로 <ins>속성</ins>에서 찾아서 `A property with the
name 'cm_cd' is not found` 로 죽는다. `DataTable` 은 정식으로 지원되고 타입별
정렬·필터와 변경 추적(`DataRowState`)이 따라온다.

**줄은 `DataRow` 가 아니라 `DataRowView` 로 온다** (실제로 밟음). 표를 주면
DevExpress 가 `DataView` 로 감싸기 때문이다. 그래서 `TItem="DataRowView"` 로
받고 줄은 `RuntimeCell.Row(...)` 로 꺼낸다 — 바로 캐스팅하면 줄을 그리다
던지고 **회로가 끊겨** 화면이 통째로 멎는다(증상은 「조회를 눌러도 아무 일이
없다」).

**칸 안에서 고치는 부품은 `IHandleEvent` 로 자동 렌더를 꺼야 한다**
(실제로 밟음). 입력칸이 값을 돌려줄 때 Blazor 가 부르는 `StateHasChanged` 가
DevExpress 의 칸 설정 렌더 중에 걸려 `Async rendering is not allowed here` 로
던지고, 역시 회로가 끊긴다.

#### 실패를 빈 표로 말하지 않는다 (실제로 밟음)

프로시저가 실패해도 화면에는 **줄이 0개인 표**가 갔다. 그러면 화면은
「조회 결과가 없습니다」라고 말하는데 **그건 거짓말이다** — 자료가 없는 것과
못 읽은 것은 고쳐야 할 곳이 서로 다르다(자료 등록 ↔ DB 연결).

프로젝트 목록 화면이 그래서 계속 「조회 결과가 없습니다」였고, 실제 원인은
**ProjMng DB 에 붙지 못한 것**이었다(포트가 바뀌어 있었다). 지금은 REST 라
실패가 `ApiException` 으로 올라오고 `DataPage` 가 서버 문구를 그대로 띄운다.

> **ProjMng DB 는 포털·장례식장과 다른 곳에 있다**(`jin114.co.kr:31015/projmng`).
> 개발 장비에서 그 호스트가 안 풀리면 프로젝트관리 화면 전부가 이 안내를 낸다.
> 화면 문제가 아니다.

#### 다이어그램(ERD·흐름도·유즈케이스)에서 밟은 것 셋

세 화면이 `DiagramViewer`(maxgraph 0.24, 로컬 정적 자산) 하나를 쓴다.
**그림을 그리고 관리하는 화면**이다 — 도형을 만들고(「도형 추가」), 끌어
옮기고, 크기를 바꾸고, 도형끼리 선으로 잇고, 선과 **손으로 만든 도형**의
이름을 더블클릭해 고치고, 고른 것을 지운다(<kbd>Delete</kbd> 또는
「선택 지우기」).

**표에서 온 도형의 이름·설명은 캔버스에서 못 고친다.** 그 값의 정본은 대상
DB 의 테이블 이름과 코멘트라, 고쳐 봐야 다음 불러오기에 되돌아간다 — 고칠 수
있게 두면 「고쳤는데 사라진다」가 된다. 그쪽은 [테이블·컬럼 설명 관리]에서
코멘트를 고친다. 손으로 만든 도형(`manual`)은 캔버스가 정본이라 고칠 수 있고,
옅은 파랑으로 갈라 보인다.

1. **JS 모듈을 상대 경로로 부르면 404 다.** `./js/diagram-viewer.js` 로
   적혀 있었는데, 모듈이 각자 프로세스이던 시절(:5566)의 잔재다. 지금은 셸
   하나라 그 경로가 문서 주소 기준으로 풀려 `/projmng/design/erd` 에서는
   `/js/…` 가 된다. import 가 던지고 **회로가 끊긴다** — 그림이 안 그려지는
   것이 아니라 그때부터 화면의 아무 단추도 안 눌렸다. RCL 경로로 부른다
   (`./_content/JSini.Web.ProjMng/js/diagram-viewer.js`).
2. **`graph.getCellGeometry(cell)` 은 0.24 에 없다.** 저장할 때만 부르는
   이름이라 **저장이 언제나 터졌고**(회로까지 끊겼다), 화면에는 「저장하지
   않은 변경」만 남았다. 좌표는 셀이 들고 있다 — `cell.getGeometry()`.
3. **ERD 와 흐름도는 같은 저장본을 본다**(`db_pkey='erd'`). 옛 소스가 그랬고,
   다른 점은 흐름도가 **사라진 표**를 `(삭제됨)` 과 흐린 도형으로 표시한다는
   것뿐이다. 그 표시는 **저장하지 않는다** — 조회 결과지 그림의 성질이 아니고,
   남기면 ERD 화면이 물려받는다. 한동안 흐름도만 `db_pkey='flow'` 를 읽어
   **어느 DB 를 골라도 빈 그림**이었다(그 속성은 운영에 한 줄뿐이고 그마저
   draw.io XML 이다).
4. **운영 자료에 draw.io XML 이 섞여 있다.** 유즈케이스 8건은 **전부**
   `<mxGraphModel …>` 이고 DB 속성의 `ER Diagram` 도 그렇다. `ErdModel.Parse`
   는 그것을 **빈 모델**로 돌려주므로 화면은 「저장된 그림이 없습니다」로
   보이고, 그것을 「아직 안 그렸다」로 읽은 사람이 새로 그려 저장하면
   **옛 그림이 덮어써진다.** `ErdModel.IsLegacyDrawing` 으로 가르고 저장을
   막는다(여는 꺾쇠 하나로 판정한다 — JSON 은 언제나 `{` 로 시작한다).

**저장 안 한 변경을 화면이 말해 준다.** 캔버스가 처음 바뀌는 순간
`OnChanged` 가 한 번 올라오고(끌 때마다 수십 번 나는 이벤트를 그대로 올리면
회로 왕복이 그만큼이다) 화면이 경고 줄을 띄운다. 불러오기·저장이 내린다.

**도구상자의 도형은 끌어다 놓고, 클립보드의 그림은 붙여넣는다.**
도구 칸에서 캔버스로 끌면 그 자리에 생기고(`dragover` 에서 기본 동작을 막지
않으면 `drop` 이 아예 오지 않는다), 캔버스 위에서 <kbd>Ctrl</kbd>+<kbd>V</kbd>
하면 클립보드의 그림이 들어간다(그림 파일을 끌어다 놓아도 같다). 여기서 밟은
것 넷:

5. **그림은 넣기 전에 줄인다.** 저장은 캔버스 → 회로 → 서버로 가는데,
   화면 캡처 한 장이 base64 로 수 MB 다. 긴 변 1100px · 한 장 420KB 로 맞추고
   (`diagram-viewer.js` 의 `IMAGE_MAX_*`), PNG 로 안 눌리면 흰 바탕을 깔아
   JPEG 로 바꾼다(바탕을 안 깔면 투명한 곳이 **검게** 나온다). 저장본에는
   `ErdEntity.Image` 에 data URL 로 담는다 — 파일 서버에 올려 주소로 두면
   **그림을 그리는 SVG 가 인증 없이 파일 서버를 부르게 된다.**
6. **회로의 수신 한도를 올려 두었다**(`JSiniWebApp` 에서 4MB, 기본값 32KB).
   `SaveAsync` 는 **브라우저 → 서버** 방향이라 이 한도에 걸리고, 넘으면 오류가
   아니라 **회로가 그냥 끊긴다**. 그림 없이도 이미 넘고 있었다 — 운영 저장본에
   66KB·46KB·40KB 짜리 ERD 가 있다(그 그림들은 여태 저장이 안 됐다는 뜻이다).
7. **손으로 만든 도형은 `kind` 를 저장본에 남긴다.** 안 남기던 동안에는
   불러오기가 전부 `MANUAL_STYLE` 로 그려서, **저장 한 번에 육각형도 구름도
   네모가 됐다.** 그림은 `image`, 도형은 `kind` — 둘 다 그림의 성질이라 남긴다
   (칸 목록 `fields` 와 외래키 선 `auto` 는 조회 결과라 안 남긴다).
8. **화면 좌표는 「그때」 재야 한다.** 놓은 자리를 그래프 좌표로 옮기는 셈이
   `getBoundingClientRect()` 에 기대는데, 「그림을 넣는 중입니다」 한마디가
   알림 자리를 늘려 **캔버스를 아래로 민다.** 알림을 띄우고 나서 재면 놓은
   자리보다 50px 쯤 아래에 생긴다. 붙여넣기는 마우스가 있던 그 순간 재어 둔다.

**부품은 한 번만 만든다.** `DiagramViewer.EnsureAsync` 가 만드는 중인 일감을
들고 있다(`_creating`). `if (_instance is null)` 이던 동안에는 그 안에 `await`
가 셋이라 **끝나기 전에 다시 들어왔고** — 화면이 열릴 때 그리기·도구 목록·
미리보기가 거의 동시에 부른다 — 같은 `<div>` 안에 그래프가 두세 개 생겼다.
보이는 것은 맨 위 하나뿐이라 한동안 아무도 몰랐는데, 붙여넣기를 붙이자
**Ctrl+V 한 번에 그림이 세 장** 생기면서 드러났다.

#### 코드 편집기는 **필요할 때** monaco 를 받는다 (실제로 밟음)

BlazorMonaco 는 스크립트 세 장이 전역에 있기를 기대한다. 없으면
`StandaloneCodeEditor` 가 자기 첫 렌더에서 `window.monaco` 를 찾다 실패하고
**회로가 끊긴다** — 그 화면만 깨지는 것이 아니라 그때부터 아무 단추도 안
눌린다. 편집기를 쓰는 화면 넷이 실제로 그렇게 죽어 있었다.

`<head>` 에 적으면 `editor.main.js` 3MB 를 **편집기가 없는 화면까지** 받는다.
그래서 `CodeEditor` 가 필요할 때 받고(`wwwroot/js/monaco-loader.js`)
**다 받은 뒤에야** 편집기를 그린다. 감싸개가 나중에 넣어 주는 방식으로는
언제나 늦다 — Blazor 는 `OnAfterRender` 를 **자식부터** 부른다.

기다리는 것은 `setTimeout` 으로 한다. `requestAnimationFrame` 은 **보이지 않는
탭에서 콜백이 오지 않아** 「불러오는 중」에 멈춘 채로 남는다.

#### 고르개를 편집 창에 넣으면 던진다 (실제로 밟음)

`CodeSelect` · `BizSelect` · `SourceSelect` 는 `Value`/`ValueChanged` 로 값을
주고받아 DevExpress 가 기대하는 `ValueExpression` 이 없다. `EditForm`
(표의 편집 창) 안에 들어가면 그것을 필수로 보고 던지고, **그 예외가 회로를
끊는다.** 셋 다 `ValidationEnabled="false"` 로 껐다 — 우리는 DevExpress
검증을 쓰지 않는다.

## 회사 소개 사이트 (`src/Site/JSini.PublicSite`)

**정적 SSR 전용이다.** `AddInteractiveServerComponents()` 를 부르지 않는다 —
회로도, `blazor.web.js` 도, 사용자별 서버 상태도 없다. 검색 봇과 링크 미리보기가
대부분인 트래픽에 회로를 열어 줄 이유가 없다.

- 공유 프로젝트(Abstractions·Components·Http·Models)를 **하나도 참조하지 않는다.**
  참조가 생기는 순간 공개 사이트가 업무 포털의 배포 일정에 묶인다.
- DevExpress 도 쓰지 않는다. 화면 전부가 순수 HTML/CSS 로 충분하다.
- Tailwind 를 가져오지 않았다. .NET 빌드 옆에 node 빌드를 붙이면 Vue 를
  걷어낸 의미가 없다. 원본의 유틸리티를 이름 있는 클래스로 옮겨 적었다(`wwwroot/site.css`).
- 회로가 없어서 달라진 것 셋:
  - 자료실 분류 거르기 → `?category=` 질의 문자열 (링크로 보낼 수 있고 뒤로 가기가 된다)
  - 모바일 차림표 → `<details>` (브라우저가 여닫으므로 JS 가 필요 없다)
  - 문의 본문 → 서식 편집기 대신 여러 줄 입력 (서버가 어차피 태그를 걷어낸다)
- 히어로 배경 모션은 Blazor 와 무관한 평범한 JS 한 장이다(`wwwroot/shard-motion.js`).

## 화면을 새로 만들 때

모듈마다 뼈대가 같다. **화면이 백 개가 넘어서 그 열 줄을 손으로 적으면 반드시
갈라진다** — 갈라지는 곳은 늘 실패 처리다.

- `DataPage` 를 상속한다 (`@inherits DataPage`). 조회·빈 결과·실패를 한 곳에서 처리한다.
  `LoadAsync` 는 건수를 돌려주고, 0 이면 "없습니다" 를 띄운다. `RunAsync` 는 저장·삭제용이다.
- **제목 줄을 두지 않는다.** 어느 화면인지는 사이드바와 브레드크럼이 이미
  말해 준다 — 세 곳에 같은 글자가 있으면 본문만 좁아진다. `PageHeading` 은
  2026-09-06 에 147개 화면에서 걷어내고 부품도 지웠다.
- 화면은 **조회 영역(`CommSch`) → 자료 영역(`CommCont` + `CommGrd`)** 으로 쌓는다.
  조건 칸은 `CommSchItem Label="…"` 로 **라벨을 붙인다**. 조회 단추는 판이
  그리므로 화면이 따로 두지 않는다(`OnSearch` · 이름은 `SearchText`).
  오래 걸리는 조회는 `Busy` 로 그 단추를 잠근다.
  조건을 표 안 도구줄에 두지 않는다 — 표가 넓어질수록 조건이 가로 스크롤
  저편으로 밀려나고, 화면마다 조건 자리가 달라진다. `CommCont` 로 감싸야
  표가 본문 높이를 꽉 채우고 쪽 스크롤이 안 생긴다.
  **대시보드처럼 키 큰 조각이 여럿 쌓인 화면은 감싸지 않는다** — 감싸면
  남은 높이를 그 판이 다 먹어 아래가 잘린다.
- 안내 줄은 `PageNotice`, 묶음 길잡이는 `GroupLinks`.
- 편집 창은 `CommPopup` 이다(`DxPopup` 을 감싼 것). 머리를 잡아 옮길 수 있고,
  바깥을 눌러도 닫히지 않고, 폼이 화면보다 길면 본문만 구른다 — 셋 다
  기본값이라 화면에서 적지 않는다. **묻는 창은 `ConfirmDialog`** 다.
  `CommPopup` 은 splat 을 열어 두지 않으므로, 필요한 파라미터가 없으면
  그 부품에 선언한다(빠뜨리면 빌드가 그 자리에서 막힌다).
- 계층 자료는 **`CommTree`** 다(`DxTreeList` 를 감싼 것). 표의 `CommGrd` 와 같은
  자리에 놓고 같은 아래 띠를 갖는다 — 다른 것 넷은 아래에 적어 두었다.
  **맨 `DxTreeList` 를 쓰지 않는다.** 그러면 관리 칸·아래 띠·줄무늬·가운데
  정렬을 화면마다 손으로 그리게 되고, 실제로 세 화면이 그렇게 갈라져 있었다.
- 좌우 분할(`ad-split`)의 **판 둘은 `CommCont` 여야 한다** —
  `<CommCont CssClass="ad-split__side">`. 높이를 채우는 규칙이
  `.ad-split > .commcont` 를 겨누므로 맨 `<div>` 로 두면 **하나도 안 걸린다.**
  메뉴롤 화면이 그래서 나무 안쪽 높이가 92px 이 되고 **뿌리 14개 중 한 줄만**
  그려지고 있었다. 오류는 없고 트리 껍데기는 보이므로 증상이 「메뉴가 안
  나온다」 하나뿐이라 원인이 판의 태그로 보이지 않는다.
- **폭을 사용자가 정해야 하면 `ad-vsplit`** 다 — 셸 사이드바와 같은
  `DxSplitter` 를 쓴다(admin.css). 양쪽이 필요로 하는 폭이 자료에 따라 다른
  화면(사람롤의 이름·부서·회사)에서 26%·50% 고정은 늘 한쪽이 아쉽다.
  둘 다 **부모가 높이를 줘야 하고**, 안 주면 오류 없이 판이 무너진다.

  | | 폭 | 쓰는 곳 |
  |---|---|---|
  | `ad-split` | 격자 고정(26%) | 메뉴롤 · 공통코드 |
  | `ad-vsplit` | 끌어서 정한다 | 사람롤 |

- **판 안에서 `CommTree`·`CommGrd` 의 높이를 값으로 주지 않는다.** 아래 띠
  (펼치기·엑셀)가 판 밖으로 밀려나고, 판이 `overflow: hidden` 이라 **눌러야
  할 단추가 사라진다.** `max-height: 100%` 도 `height: 100%` 도 「부품 전체
  높이」를 가리켜서 표가 판을 꽉 채운다 — 사람롤에서 두 번 밟았다.
  표에 `flex: 1; min-height: 0` 을 주고 높이는 flex 에 맡긴다.
- **`DxTabs` 안에 표·나무를 넣으면 그 부품의 상자 둘을 함께 묶어야 한다.**
  `dxbl-tabs-content-panel` 과 `dxbl-tabs-content` 는 `min-height: auto` 인
  flex 항목이라 **안쪽 내용보다 작아지지 않는다.** 역할 관리에서 나무를
  펼치자 탭 뿌리는 562px 인데 그 둘이 **8579px** 로 늘어나 「권한 저장」이
  화면 밖으로 나갔다. 나무에 `flex: 1; min-height: 0` 을 줘도 안 듣는다 —
  **끊기는 자리가 그보다 위**다. 탭 뿌리를 세로 flex 로 만들고 두 상자에
  `flex: 1; min-height: 0; overflow: hidden` 을 준다(admin.css `.ad-roletabs`).
- 표의 **자료 칸은 가운데 정렬이 기본**이다. 오른쪽·왼쪽으로 두고 싶은 칸만
  `TextAlignment` 를 적는다 — 적으면 그 값이 이긴다. CSS 로 하지 않는 이유는
  DevExpress 가 **우리가 정한 것과 자기가 자료형을 보고 정한 것에 같은 클래스**를
  붙여서, CSS 만으로는 둘을 구분할 수 없기 때문이다.
- 조건줄은 `jsini-toolbar`, 통계 타일은 `jsini-stats`, 상태 표시는 `jsini-badge`.
- 모듈 전용 스타일은 그 모듈 `wwwroot/*.css` 에 두고
  `IPortalModule.StyleSheet` 로 알린다. 셸은 모듈 이름을 알지 못한다.

### `CommGrd` 에 `EventCallback` 을 splat 하면 화면이 500 으로 죽는다

**실제로 두 화면이 그래서 안 열리고 있었다**(공통코드 · 기기관리).

`CommGrd` 는 선언하지 않은 파라미터를 전부 `DxGrid` 로 흘려 보낸다. 편한
대신 **Razor 가 그 값의 형을 모른다.** 문자열이 어긋나는 것은 `Coerce` 가
맞춰 주지만 **대리자는 맞춰 줄 수 없다** — 아래처럼 적으면 `EventCallback`
으로 감싸이지 않은 맨 `Func<object, Task>` 가 넘어간다.

```razor
@* 틀렸다 — CommGrd 가 선언하지 않은 이름이라 splat 으로 흘러간다 *@
SelectedDataItemChanged="@((object? item) => OnGroupChangedAsync(item))"
```

DevExpress 가 대입할 때 형변환에 실패하고 화면은 **그리기도 전에** 500 이
된다(``Unable to cast … Func`2 … to EventCallback`1``). 빌드도 다른
테스트도 전부 통과하고 파일만 보면 정상으로 보인다.

고른 줄은 `CommGrd` 가 선언한 이름으로 받는다.

```razor
SelectedItem="@_group"
SelectedItemChanged="@OnGroupChangedAsync"
```

**둘을 같이 준다.** 값 없이 알림만 받으면 화면이 고른 줄을 보관하지 않는다는
뜻이 되어 강조가 곧 풀린다. 그리고 대리자를 `CommGrd` 안에서 감싸는 것으로는
안 된다 — 그러면 알림 받는이가 화면이 아니라 `CommGrd` 가 되어 **표만** 다시
그려지고, 고른 줄에 딸린 칸이 안 바뀐다.

`DxGrid` 의 다른 `EventCallback` 도 같다. 필요해지면 **`CommGrd` 에 먼저
선언한다.** `CommGrdSplatTests` 가 남은 splat 을 찾아 막는다 — 금지 목록은
`DxGrid` 를 반사로 훑어 만들므로 DevExpress 를 올려도 따라온다.

### 나무는 `CommTree` 다 — 표와 다른 것 넷

`CommGrd` 를 아는 사람이 헷갈릴 만한 자리만 적는다. 나머지(splat · Coerce ·
선택 파라미터 · 기본값을 다시 적지 않기)는 표와 **똑같다** — `CommTreeTests`
둘이 표와 같은 검사를 나무에도 걸어 둔다.

| | 표(`CommGrd`) | 나무(`CommTree`) |
|---|---|---|
| 순번 칸 | 기본으로 붙는다(`ShowRowNumber`) | **없다** |
| 쪽나누기 | 15줄씩 | **안 쓴다**(`ShowAllRows`) |
| 새 줄 | `OnNew(item)` | `OnNew(item, parent)` |
| 저장 | `OnSave((item, isNew))` | `OnSave((item, isNew, parent))` |

**순번 칸이 없는 것은 자리가 없어서다.** 나무의 첫 자료 칸이 계층 칸이라
DevExpress 가 그 셀 안에 들여쓰기와 펼침 화살표를 그린다. 앞에 순번 칸을
끼우면 **그 둘이 순번 칸으로 옮겨 가고** 제목 칸은 계층을 잃는다 — 화면에
그려 확인했다. 번호가 꼭 필요하면 **계층 칸 다음에** 자기 칸을 두고
`@(cell.VisibleIndex + 1)` 을 적는다.

**쪽나누기를 쓰지 않는 것은 뿌리 줄만 세기 때문이다.** DevExpress 기본값이
한 쪽에 10줄인데 그 10이 **뿌리의 수**다. 메뉴 관리 화면은 뿌리가 34개라
두 쪽으로 갈려 있었고, 2쪽에 무엇이 있는지 알 단서가 아무것도 없었다.
나무는 접었다 펴는 것으로 크기를 다스린다(아래 띠의 펼치기·접기).

**「나무에는 팝업 편집이 없다」는 말은 사실이 아니다.** 화면 주석 두 곳에
그렇게 적혀 있는데 `TreeListEditMode.PopupEditForm` 이 있다. 그래서
등록·하위 추가·수정·삭제가 표와 같은 모양으로 붙어 있고, 새 줄에는 부모가
함께 온다(`OnNew` · `OnSave` 의 `parent`).

끌어 옮기기를 켜는 것은 화면 몫이다(`AllowDragRows`). 받는 쪽
(`ItemsDropped`)은 `CommTree` 가 **선언해 두었다** — `EventCallback` 이라
splat 으로 넘기면 화면이 500 으로 죽는다(위 절과 같은 이유다).

### 사이드바와 본문 사이는 `DxSplitter` 다

구분선을 끌어 사이드바 폭을 바꾼다(90~640px). 끌어 둔 폭은 **브라우저에**
남는다(`jsini-sidebar-width`) — 알맞은 폭은 화면 크기에 딸린 것이라 사용자가
아니라 기기에 붙는 값이다. 읽는 것은 `PortalBoot` 의 공용 왕복에 실려 오므로
왕복이 늘지 않는다(열쇠 목록에 한 줄 더한 것이 전부다).

#### 구분선은 자리를 차지하지 않는다 — 잡는 자리만 10px

DevExpress 기본 구분선이 12px 인데 그만큼 **본문이 오른쪽으로 밀린다.** 본문에
이미 좌우 여백(1.5rem)이 있어 사이드바와 내용 사이에 36px 짜리 빈 띠가 생기고,
탭 줄·브레드크럼도 함께 밀려 헤더·사이드바와 세로줄이 어긋났다.

2px 로 줄여도 여전히 벌어져 보였다 — 사이드바에 이미 1px 테두리가 있어서 그
옆의 빈 2px 이 **선 두 개**로 읽힌다. 그래서 `SeparatorSize="0"` 이다. 경계에
보이는 선은 사이드바 테두리 하나뿐이고 본문이 그 선에 바로 붙는다(재어 보니
사이드바 0~330, 본문 330~1280).

**폭이 0 이어도 끌 수 있다.** 잡는 일은 상자가 아니라 `::before` 가 하고, 그것이
좌우 5px씩 상자 밖으로 나가 있다(잡이 10px — 좌표별로 확인했다). 그 의사요소는
본문 판보다 위에 있어야 한다 — 본문 판이 DOM 에서 뒤라서 층을 올리지 않으면
잡이의 오른쪽 절반이 본문에 먹힌다(`z-index: 1`).

마우스를 올렸을 때의 단서는 **경계에 뜨는 2px 강조선**이다(잡이 가운데만
칠하는 그라디언트). 잡이를 통째로 칠하면 방금 없앤 「벌어져 보임」이 마우스를
올릴 때마다 되돌아온다.

**본문을 구분선 아래로 겹치지 않는다.** 판을 음수 여백으로 당겨 겹치면 구분선이
본문의 첫 12px 위에 놓이고, 그 띠는 화면 왼쪽을 따라 끝까지 내려간다 — 표의 첫
칸과 트리의 펼침 화살표가 그 자리에 있어서 「누른 것이 안 눌린다」가 된다.
겹치는 값이 판 크기와 별개로 관리되는 두 번째 값이 되는 것도 문제다.

접기는 **구분선 가운데의 화살표와 헤더의 ☰ 가 같은 상태**를 나눠 쓴다
(`Collapsed` · `CollapsedChanged`). 따로 두면 화살표로 접은 뒤 ☰ 를 눌렀을 때
아무 일도 안 일어난 것처럼 보인다.

**접힘은 폭으로 기억하지 않는다.** 접으면 부품이 크기를 0 으로 알려 주는데
그것을 저장하면 다음에 열 때 폭 0 인 사이드바가 나온다 — 그래서 1px 미만은
저장하지 않고 거른다.

#### 좁은 화면에서는 판의 **상자만** 없앤다

휴대폰에서 사이드바는 본문을 덮어야 한다(360px 화면에서 판을 나란히 두면
본문이 24px 남는다). 판을 나눠 갖는 부품으로는 덮을 수 없으므로, 좁은 화면에서
판과 판안쪽을 `display: contents` 로 만들어 **상자를 지운다.** 그러면 그 안의
사이드바·본문이 다시 격자 칸이 되어 예전 규칙(고정 덮개 · 본문 첫 칸)이 그대로
걸리고, 구분선은 감춘다.

**마크업을 화면 크기로 갈아 끼우지 않는 이유가 그것이다.** `@if (_isPhone)` 로
갈랐더니 경계(767px)를 넘을 때마다 사이드바 트리와 열려 있던 화면이 통째로 다시
만들어졌다 — 펼쳐 둔 가지가 접히고 화면이 조회를 다시 한다.

> 판 안쪽의 여백(테마마다 8~16px)은 걷어낸다. 문서 안의 분할 화면에는 맞는
> 값이지만 여기 들어오는 것은 셸의 판 둘이라, 그 여백이 사이드바를 안쪽으로
> 밀어 헤더와 세로줄이 어긋난다. 여백을 쓰는 규칙과 다투지 않고
> `--dxbl-splitter-pane-padding-x/y` 를 덮는다.

### 사이드바는 검색 · 탭 · 트리 셋이다

위에서부터 **메뉴 검색 → 탭(메뉴 · 즐겨찾기) → 고른 탭의 트리** 다.
검색이 탭보다 위에 있어서 칸 하나가 두 탭에 다 걸린다.

**검색 칸은 우리가 그리고, 거르는 일은 DevExpress 가 한다.** 한동안
`DxTreeView.ShowFilterPanel` 이 그린 칸을 썼는데 그것은 트리 **안쪽** 맨 위에
붙어서 탭보다 아래로 내려간다. 그래서 칸만 우리가 두고 `FilterString` 으로
넘긴다 — 걸린 가지를 펼치는 것까지 DevExpress 몫으로 남는다. **직접 거르지
않는다.**

즐겨찾기도 트리다(`Menu/FavoriteTree.cs`). 메뉴 트리에서 담은 화면과 그 위
묶음만 남긴다. 평평한 목록이던 때는 담아 둔 「목록」이 어느 업무 것인지
알 수 없었다 — 메뉴 제목은 묶음 안에서만 유일하다.

#### 메뉴 아이콘은 DB 값이고, 펼침 표시는 **오른쪽 끝**이다

옛 vben 사이드바가 `[아이콘][제목][화살표]` 였다. 그 모양으로 되돌린 것이다.

아이콘 이름은 DB 에 있다(`scom.system_menus.icon` — 179건 중 178건에 값이 있고
139가지다). **iconify 이름**이다(`lucide:calendar-days` · `carbon:building`).
옛 포털은 iconify 런타임이 그 이름으로 SVG 를 받아 왔는데, 여기서는 필요한
139가지를 CSS mask 로 굳혀 두었다.

| | 어디에 |
|---|---|
| 생성기 | `scripts/build-menu-icons.py` (DB 를 읽어 iconify CSS API 로 받는다) |
| 생성물 | `Components/wwwroot/menu-icons.css` (61KB, 커밋한다) |
| 이름 → 클래스 | `Components/Menu/MenuIcons.cs` |
| 그리는 곳 | `SidebarMenu.NodeText` (DxTreeView 의 `NodeTextTemplate`) |

**아이콘 때문에 바깥으로 나가는 요청이 없다.** 색은 `currentColor` 라 테마
스물둘을 그대로 따라온다 — app.css 의 아이콘 묶음과 같은 방식이다.

메뉴 관리 화면에서 CSS 에 없는 이름을 넣으면 **동그라미**가 나온다(빈 사각형이
아니다 — `.jsini-mi` 가 `--svg` 기본값을 들고 있다). 그러니 급하지 않고,
생성기를 다시 돌려 커밋하면 제 그림이 나온다. 이름 목록을 C# 쪽에 두지 않는
이유가 그것이다 — 두 곳을 맞춰야 하고, 어긋나면 아이콘이 사라지는 쪽으로 틀린다.

펼침 표시는 **DevExpress 가 제목 앞에 그린 단추를 CSS 가 오른쪽으로 옮긴 것**
이다(글리프는 그대로 쓴다 — 그 스프라이트가 이미 `>` · `v` 다). 자리만 옮기므로
눌러서 펴는 것도 그대로 된다. `order` 가 아니라 **절대 자리**로 옮긴다 —
`order` 로 보내면 그 단추가 강조 알약(`…item-container`) 바깥에 남아 마우스를
올렸을 때 색이 화살표 앞에서 끊긴다.

> 그 규칙에는 클래스가 하나 더 붙어 있다(`.dxbl-btn.dxbl-btn-tool`).
> DevExpress 도 같은 단추에 `position: relative` 를 주고 **자릿수가 같은데**,
> 테마 CSS 가 app.css 뒤에 꽂히므로 그냥 적으면 그쪽이 이긴다. 실제로 규칙은
> 실려 있는데 화살표가 안 움직였다.

**메뉴 글자는 크기 여섯 단계를 그대로 따라간다.** 서랍의 단계는 여섯인데
DevExpress 모드는 셋이고 앞의 넷이 Small 하나로 접힌다(`ThemeSize.Steps`).
그래서 나무 글자가 DevExpress 의 단 값에 묶여 있는 동안은 **앞의 네 단계가
전부 12px 로 같았다.** 여섯을 아는 것은 우리 사다리뿐이라 그 값으로 덮는다 —
`--dxbl-treeview-font-size: var(--jsini-fs-base)`. 재어 확인한 값:

| | 가장작게 | 아주작게 | 작게 | 조금작게 | 보통 | 크게 |
|---|---|---|---|---|---|---|
| 전 | 12 | 12 | 12 | 12 | 14 | 16 |
| 후 | **10** | **11** | 12 | **13** | 14 | 16 |

**줄 높이는 건드리지 않는다.** 우리가 끼운 네 단계는 「작게」의 밀도에서
글자만 조절하는 것이 뜻이다(사다리 머리말). 줄 높이까지 비율로 바꾸면 서랍의
단계가 사실상 여섯 개의 밀도가 되어 그리드·달력과 어긋나는 자리가 다시 생긴다.

같은 이유로 사이드바 안에 `rem` 으로 박아 둔 글자(탭 · 즐겨찾기 수 · 빈 목록
안내)도 사다리 단으로 바꿨다 — 트리만 따라가고 탭은 그대로면 그 어긋남이
바로 눈에 띈다.

**화살표 크기는 그 줄의 글자 크기를 따라간다.** DevExpress 는 그 그림을 여백
토큰으로 재는데 작게·보통의 값이 **같아서**(둘 다 12px) 크기를 바꿔도 제목만
커지고 화살표는 그대로였다. 그림 규칙과 다투는 대신 그 규칙이 읽는 변수를
나무 뿌리에서 덮는다 — `--dxbl-treeview-btn-icon-size: var(--dxbl-treeview-font-size)`.
화살표가 앉을 여백도 `em` 이라 함께 커진다.

> **블록 주석 안에 `/* */` 를 또 쓰면 뒤 규칙이 조용히 죽는다.** CSS 주석은
> 겹치지 않아서 안쪽 `*/` 가 주석을 먼저 닫고, 그 뒤 몇 줄이 값으로 읽히다가
> 규칙 하나가 통째로 사라진다. 이 자리에서 실제로 밟았다 — 규칙은 파일에
> 있는데 브라우저에서는 없었다. 설명에 값을 적을 때는 `→ 12px` 처럼 적는다.

#### DxTreeView 의 `Data` 를 렌더마다 새로 만들면 펼침이 풀린다

**실제로 밟았다.** 좁히기를 렌더 본문에서 하면 매번 새 목록이 되고, DxTreeView
는 `Data` 가 다른 것으로 바뀌면 트리를 새로 지으면서 **펼침 상태를 버린다.**
즐겨찾기 트리를 펼쳐 열어도 다음 렌더에 도로 접혔다.

화면을 옮길 때마다 레이아웃이 다시 그려지며 `Nodes` 가 **같은 값으로** 다시
들어오는 것도 같은 함정이다. 그래서 좁힌 결과를 들고 있다가 메뉴나 즐겨찾기가
**실제로** 바뀔 때만 다시 좁힌다(`ReferenceEquals` 로 본다).

펼치기는 `ExpandAll()` 을 `OnAfterRender` 에서 한 번만 부른다. 렌더마다 부르면
사용자가 접어 둔 묶음이 다시 펴진다. 메뉴 탭은 접힌 채로 둔다 — 179개를
다 펴면 오히려 못 찾는다.

### 화면에 Bootstrap 유틸리티 클래스를 쓰지 않는다

`d-flex` · `gap-1` · `me-1` · `text-muted` · `card` · `row` · `col-md-6` 같은
것들이다. **기본 테마인 DevExpress Fluent 에는 Bootstrap 이 실려 있지 않다.**

이 함정이 나쁜 이유는 **아무 오류도 나지 않는다**는 것이다. 클래스가 없으면
브라우저는 그냥 무시한다. Fluent 에서 실측한 값이 이렇다.

| 적은 것 | Fluent 에서 실제 값 |
|---|---|
| `d-flex` | `display: block` |
| `gap-1` | `gap: normal` |
| `me-1` · `ms-2` | 여백 없음 |
| `row` · `col-*` | 그냥 `div` — 칸이 세로로 쌓인다 |

더 나쁜 것은 **Classic `.bs5` 와 Bootstrap 계열 테마에서는 멀쩡히 보인다**는
점이다. 그 테마로 개발하면 끝까지 모르고, 기본 테마로 보는 사용자에게만
배치가 어긋난다. 기상 현황판이 실제로 그랬다 — 지역 카드가 가로로 늘어서야
하는데 한 줄에 하나씩 쌓여서, 지역이 열이면 열 번을 굴려야 했다.

대신 **이름 있는 클래스**를 쓴다. 유틸리티를 새로 만들어 모으지 않는다.

| 자리 | 이름 |
|---|---|
| 표 안 단추 묶음 | `jsini-actions` |
| 이름 앞 딱지 묶음 | `jsini-tags` |
| 한 칸 안에 조작을 나란히 | `jsini-inline` |
| 왼쪽 설명 · 오른쪽 조작 | `jsini-split` |
| 테두리 상자 | `jsini-card` |
| 흐린 글씨 · 곁들이는 한 줄 | `jsini-text-muted` · `jsini-hint` |
| 차트 툴팁 안쪽 | `jsini-tip` |

여기 없는 모양이면 **한 모듈만 쓰는 동안은 그 모듈 `wwwroot/*.css` 에**
(`le-obs` · `hd-form` · `ad-checks` 처럼), 둘 이상이 쓰게 되면 `app.css` 로
올린다.

정의 없는 이름을 쓰는 것도 같은 증상을 낸다. `jsini-text-muted` 는 세 화면이
쓰고 있었는데 어느 CSS 에도 없어서 흐려지지 않고 있었고, `pm-frame` 은
`ServerMonitor.razor.css` 안에만 있어서 같은 모듈의 다른 화면이 못 썼다.
**이름을 새로 쓸 때 정의부터 찾는다.**

**칸을 손으로 적을지 말지**는 화면이 얼마나 자주 보이느냐로 정한다.
매일 보는 화면은 DTO 와 컬럼을 적고, 서버가 준 대로 보면 되는 화면
(보고서·수집 로그)은 `AutoGrid` + `JsonTable` 을 쓴다. 헬프데스크가 그렇다 —
그쪽은 엔드포인트가 마흔 개라 DTO 를 마흔 개 만들면 백엔드가 칸을 더할 때마다
화면이 아니라 DTO 를 고치러 가게 된다. **어느 쪽인지는 화면 주석에 적는다.**

## 남은 일

- [ ] **화면 다듬기** — DB 메뉴 179건이 모두 열린다(묶음 34 제외 145건 전부).
      다만 헬프데스크 35화면은 `AutoGrid` 로 서버가 준 대로 보여 주는 상태다.
      매일 보는 것부터 칸을 손으로 적어 간다.
- [ ] **아직 안 옮긴 기능** — 화면 주석에 그 이유와 함께 적어 두었다:
      담당자 재배정 · 플레이어 릴리스 걸기 · 문의 답장 보내기 (D2 결정 대기) ·
      푸시 구독 등록 (D10 결정 대기).
      **되돌리기 어렵거나 라이브러리를 하나 더 얹어야 하는 것들**이다.
      (2026-09-06 에 옮긴 것 — 간트 둘은 DxScheduler 타임라인으로(D4),
      첨부 업로드는 여섯 화면에(D5), 잠금화면(D7) · 헤더 AI 대화창(D11) ·
      이미지 정리 단추(D17).)

- [ ] **첨부는 `FilePicker` 한 벌로 올린다** (`Components/Data/FilePicker.razor`).
      **DevExpress `DxUpload` 을 쓰면 안 된다** — 그것은 브라우저가 직접
      POST 하는데 BFF 라 브라우저에 게이트웨이 토큰이 없다. 표준 `InputFile`
      로 회로(서버)에 받아 서버가 게이트웨이로 올린다. 새 화면에 첨부를
      붙일 때 이 부품을 쓴다.
- [ ] `docs/menu-path-cutover.sql` 실행 — **운영 DB 를 바꾼다.** 안 돌려도
      `RouteAliases` 가 흡수하므로 급하지 않다. 돌리면 별칭표를 지울 수 있다.
- [ ] 멀티탭 레이아웃 — 한 프로세스·한 회로가 되면서 오히려 쉬워졌다.
      문서가 새로 로드되지 않으므로 탭 상태를 레이아웃이 들고 있을 수 있다.
- [ ] PWA·웹푸시 이관 (`push-sw.js` + 매니페스트. 오프라인 캐시는 포기)
- [ ] SignalR `DeviceHub` 연결 (Funeral 모듈. 회로마다 연결하지 말고 공용 연결 + 팬아웃)
- [ ] **운영 배포 파이프라인** — 옛 `deploy-portal.yml` · `deploy-site.yml` 은
      vite 정적 빌드를 `/srv/jsini/{portal,site}` 로 rsync 하는 것이었고,
      프론트가 .NET 프로세스가 되면서 성립하지 않아 지웠다. 컨테이너 두 개 +
      nginx 리버스 프록시로 다시 짜야 한다. `deploy/docker/` 갱신 필요.
