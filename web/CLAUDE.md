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
| 연결 고리 | `component` | `path` |
| 불일치 발견 | 런타임 `console.warn` | 기동 로그 + 아키텍처 테스트 |

DB 의 `component` 컬럼은 **더 이상 읽지 않는다.**

### **`@page` 를 DB 메뉴 경로에 맞춘다** — 가장 자주 밟는 함정

화면을 다 만들어 놓고도 메뉴로 열리지 않는 일이 실제로 열몇 건 있었다.
`DbTester.razor` 가 `/projmng/develop/db-tester` 인데 DB 메뉴는
`/projmng/db/tester` 인 식이다. **각 파일만 보면 둘 다 정상으로 보인다.**

정본은 [docs/menu-route-map.md](docs/menu-route-map.md) 다. 화면을 옮기기 전에
그 표에서 목적지 경로를 먼저 확인한다.

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

### 레이아웃 치수는 DevExpress 데모에서 가져왔다

헤더 3.5rem · 사이드바 330px · 검색 3rem · 브레드크럼 3rem ·
본문 여백 `1.1rem 1.5rem`. 눈대중이 아니라 데모의 `dx-demo.css` 에서 읽은
값이다. 고칠 일이 있으면 거기를 먼저 본다.

사이드바 검색은 우리가 만든 칸이 아니라 `DxTreeView` 의 `ShowFilterPanel`
이다(돋보기 아이콘까지 DevExpress 가 그린다). 겉껍데기가 세 겹이라
`.dxbl-treeview` · `.dxbl-treeview-container` · `.dxbl-scroll-viewer` 에
**모두** flex 를 걸어야 검색 칸이 고정되고 트리만 구른다.

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
- **변경 추적** — `DataRowState` 가 Added·Modified 를 알려 준다

### ProjMng — 부품

프로젝트관리는 보통 CRUD 가 아니다. **저장 프로시저 이름을 실어 보내면 결과와
함께 컬럼 메타를 돌려주는 범용 통로**이고, 업무 로직은 전부 DB 에 있다.

```
Api/ProjMngClient.cs   DbCont · DbSave · DbDelete · JsCont · MdCont
Api/ProjMngTable.cs    프로시저 결과 → DataTable (타입 변환·변경 추적)
Api/ProcGrid.cs        조회·저장·삭제 묶음 (Vue 의 useProcGrid)
Api/CommonCodes.cs     공통코드 조회·캐시
Api/BizOptions.cs      포털 계정 목록 조회·캐시
Components/Shared/DynamicGrid.razor   메타 구동 그리드 (드롭다운 컬럼 포함)
Components/Shared/CodeSelect.razor    공통코드 드롭다운
Components/Shared/BizSelect.razor     포털 계정 드롭다운
Components/Shared/CodeEditor.razor    monaco 편집기 (JS interop)
Components/Shared/DiagramViewer.razor 다이어그램
Components/Shared/DateRangeTabs.razor 기간 선택 + 탭
Components/Shared/SearchBar.razor     조건줄
Components/Shared/SplitPane.razor     마스터-디테일 좌우 분할
Components/Shared/Notice.razor        화면 안내줄
```

이 부품들이 서면 화면은 **"어떤 프로시저를 어떤 파라미터로 부르는가"** 만 적으면
된다. `Wbs.razor`(약 60줄)와 `CommonCode.razor`(약 90줄)가 그 본보기다.

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
- 제목 줄은 `PageHeading`, 안내 줄은 `PageNotice`, 묶음 길잡이는 `GroupLinks`.
- 조건줄은 `jsini-toolbar`, 통계 타일은 `jsini-stats`, 상태 표시는 `jsini-badge`.
- 모듈 전용 스타일은 그 모듈 `wwwroot/*.css` 에 두고
  `IPortalModule.StyleSheet` 로 알린다. 셸은 모듈 이름을 알지 못한다.

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
