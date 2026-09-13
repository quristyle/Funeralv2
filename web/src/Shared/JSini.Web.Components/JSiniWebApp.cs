using System.Reflection;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Security;
using JSini.Web.Http;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JSini.Web.Components;

/// <summary>
/// 셸과 업무 앱 <b>일곱 개가 똑같이</b> 구성되도록 등록을 한 곳에 모은 것.
///
/// [이게 없으면 이 구조는 못 버틴다]
///
/// 앱이 각자 프로세스라 Program.cs 도 일곱 개다. 쿠키 이름 하나, 인증 만료 시간
/// 하나만 어긋나도 "장례식장에서 헬프데스크로 넘어가면 로그아웃된다" 가 된다.
/// 그런 버그는 각 파일만 보면 전부 정상으로 보이고, 일곱 개를 나란히 놓고
/// 비교해야만 보인다.
///
/// 그래서 앱의 Program.cs 는 이 메서드를 부르는 것 말고는 거의 할 일이 없어야 한다.
/// 앱마다 다른 것은 <b>base path 와 자기 업무 서비스 등록뿐</b>이다.
/// </summary>
public static class JSiniWebApp
{
    /// <summary>
    /// 인증 쿠키 이름. <b>일곱 앱이 모두 같아야 한다.</b>
    /// 같은 오리진(nginx 뒤)이므로 이름이 같으면 브라우저가 모두에게 실어 보낸다.
    /// </summary>
    public const string AuthCookieName = "jsini.portal";

    /// <summary>
    /// Data Protection 응용프로그램 이름.
    ///
    /// <b>이 값이 다르면 앱마다 다른 키로 쿠키를 암호화한다.</b> 그러면 셸이 구운
    /// 쿠키를 장례식장 앱이 풀지 못하고, 사용자는 업무를 옮길 때마다 로그인
    /// 화면으로 튕긴다. 기본값이 어셈블리 이름이라 아무것도 안 하면 반드시 이렇게 된다.
    /// </summary>
    private const string DataProtectionAppName = "JSini.Portal";

    /// <summary>
    /// 업무 앱과 셸이 공통으로 쓰는 것을 모두 등록한다.
    /// </summary>
    /// <param name="builder">호스트 빌더</param>
    /// <param name="routePrefix">
    /// 이 앱이 사는 경로 접두사 (<c>/funeral</c>). 셸은 빈 문자열이다.
    ///
    /// <c>UsePathBase</c> 가 이 접두사를 떼고 넘기므로 앱 안의 <c>@page</c> 는
    /// 접두사 없는 <b>상대 경로</b>다. 그런데 DB 메뉴와 권한표의 열쇠는 접두사가
    /// 붙은 전체 경로다 — 그 둘을 맞추려고 여기서 접두사를 받는다.
    /// </param>
    /// <param name="routeAssemblies">
    /// 이 앱의 <c>@page</c> 를 담고 있는 어셈블리들. 라우트 대조에 쓴다.
    /// 보통 자기 어셈블리 하나다.
    /// </param>
    public static WebApplicationBuilder AddJSiniWebApp(
        this WebApplicationBuilder builder,
        string routePrefix,
        params Assembly[] routeAssemblies)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // ── 화면 ─────────────────────────────────────────────────
        services.AddDevExpressBlazor();
        services
            .AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(options =>
            {
                // **회로의 수신 한도를 올린다.** 기본값은 32KB 다.
                //
                // 화면이 서버로 돌려보내는 것 중에 그만한 것이 있다 —
                // 다이어그램 저장이다. 붙여넣은 그림이 저장본 안에 data URL 로
                // 들어가서(ProjMng 의 ErdEntity.Image) 한 장만 있어도 32KB 를
                // 훌쩍 넘는다. 넘으면 오류가 아니라 **회로가 그냥 끊긴다** —
                // 화면이 멈추고, 사용자는 저장을 눌렀는데 아무 일도 안 일어난
                // 것으로 본다.
                //
                // 그림 쪽은 브라우저에서 먼저 줄이고(한 장 420KB) 들어온다.
                // 여기 4MB 는 그런 그림 여러 장이 든 그림 한 장분의 여유다.
                // 무한정 올리지 않는 이유는 이 값이 **연결 하나가 한 번에 물
                // 수 있는 양**이라, 크게 두면 접속 수만큼 메모리가 열린다.
                options.MaximumReceiveMessageSize = 4 * 1024 * 1024;
            });

        // ── 응답 압축 ────────────────────────────────────────────
        //
        // **HTML 을 아무도 압축하지 않고 있었다.** 정적 자원은 MapStaticAssets 가
        // 빌드 때 미리 압축해 두지만(.gz/.br 를 함께 만든다) 화면 HTML 은 그
        // 대상이 아니다. 로그인 화면이 71KB 였고, 업무 화면은 프리렌더된
        // DevExpress 그리드가 실려 그보다 훨씬 크다.
        //
        // 기본 MimeTypes 에 text/html 이 **없다**. 브라우저가 처음 받는 것이
        // 그것이라 여기서 더한다.
        //
        // [앞단 nginx 와 겹쳐도 문제가 없다]
        //
        // 이미 압축해서 준 응답에 nginx 가 또 압축하지는 않는다 — Content-Encoding
        // 이 붙어 있으면 건너뛴다. 그래서 어느 쪽이 하든 결과가 같고, 둘 중
        // 한쪽만 설정돼 있어도 압축이 된다. nginx 설정은 이 저장소에 없으므로
        // (서버에서 관리한다) **여기서 하는 것이 확실한 쪽**이다.
        //
        // [Brotli 를 먼저 등록한다]
        //
        // 협상은 브라우저가 보낸 Accept-Encoding 과 **등록 순서**로 정해진다.
        // Brotli 가 gzip 보다 20% 가까이 작고 요즘 브라우저는 전부 받는다.
        // 못 받는 브라우저에는 gzip 이 남는다.
        services.AddResponseCompression(options =>
        {
            // 개발은 http, 운영은 nginx 뒤 https 다. 켜 두지 않으면 운영에서만
            // 압축이 안 되고, 그 차이는 "운영이 느리다" 로만 보인다.
            //
            // BREACH 를 걱정할 자리가 아니다 — 그 공격은 **공격자가 넣은 글자와
            // 비밀값이 같은 응답에 함께 실릴 때** 성립한다. 여기서 압축하는 것은
            // 화면 HTML 이고, 위조방지 토큰은 매 요청 달라지며 세션 값은 쿠키에
            // 있다(본문에 없다).
            options.EnableForHttps = true;

            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            [
                "text/html",

                // 프리렌더된 화면 말고도 우리가 직접 내려주는 것들이다.
                // 첨부 중계(FileDownload)는 여기 걸리지 않는다 — 그림과
                // 압축파일은 이미 압축된 형식이라 다시 압축하면 커지고,
                // 형식이 목록에 없으면 미들웨어가 건드리지 않는다.
                "application/json",
                "image/svg+xml",
            ]);
        });

        // 압축 세기. 기본값(Fastest)은 HTML 에서 눈에 띄게 덜 줄인다.
        //
        // **SmallestSize 로 두지 않는다.** 그쪽은 CPU 를 몇 배 쓰면서 몇 %만 더
        // 줄이는데, 이 응답은 **사용자마다 매번 새로 만들어지는 것**이라 그 값을
        // 캐시로 회수할 수 없다. 정적 자원과 다른 점이 그것이다.
        services.Configure<BrotliCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Optimal);
        services.Configure<GzipCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Optimal);

        // ── 앱 사이에 쿠키를 공유하기 위한 키 링 ─────────────────
        //
        // 개발은 파일 폴더를 함께 본다. 운영(docker compose)은 같은 볼륨을
        // 일곱 컨테이너에 마운트한다. 여러 대로 늘릴 때는 Redis 로 옮긴다.
        //
        // 상대 경로는 **ContentRoot 기준**으로 푼다. 그냥 두면 프로세스의 현재
        // 디렉터리가 기준이 되는데, 그건 어떻게 띄우느냐에 따라 달라진다 —
        // `dotnet run` 은 프로젝트 폴더, 컨테이너는 /app, IDE 는 또 다르다.
        // 앱마다 다른 폴더를 보게 되면 키가 갈라지고, 증상은 "로그인은 되는데
        // 업무 화면을 누르면 다시 로그인" 이다.
        var configured = configuration["DataProtection:KeyRingPath"];
        var keyRing = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "jsini-portal-keys")
            : Path.GetFullPath(configured, builder.Environment.ContentRootPath);

        Directory.CreateDirectory(keyRing);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRing))
            .SetApplicationName(DataProtectionAppName);

        // ── 인증 ─────────────────────────────────────────────────
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = AuthCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;

                // 쿠키가 모든 앱 경로로 실려야 한다. 업무 앱이 하위 경로
                // (/funeral 등)에 있으므로 Path 를 좁히면 그 앱에만 안 간다.
                options.Cookie.Path = "/";

                // 개발은 http, 운영은 nginx 뒤 https.
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.None
                    : CookieSecurePolicy.Always;

                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/forbidden";

                // **LoginPath 만으로는 안 된다.**
                //
                // 업무 앱은 UsePathBase("/funeral") 아래에서 산다. 기본 동작은
                // LoginPath 앞에 PathBase 를 붙이므로 /funeral/login 으로 나가는데,
                // 그런 화면은 없다 — 로그인은 셸에만 있다. 사용자는 404 를 본다.
                //
                // 그래서 리다이렉트를 직접 쓴다. 사이트 루트 기준 절대 경로로 보내야
                // 셸에 닿는다. 돌아올 주소에는 PathBase 를 붙여야 원래 업무 화면으로
                // 돌아온다 — 안 붙이면 /status 만 남아 셸에서 404 가 된다.
                options.Events.OnRedirectToLogin = context =>
                {
                    var returnUrl = context.Request.PathBase
                        + context.Request.Path
                        + context.Request.QueryString;

                    context.Response.Redirect(
                        $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.Redirect("/forbidden");
                    return Task.CompletedTask;
                };

                // 오래 켜 두는 업무 화면이 많다. 8시간이면 하루 근무를 덮고,
                // 미끄럼 만료라 쓰는 동안에는 풀리지 않는다.
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        // **FallbackPolicy 를 쓰지 않는다.**
        //
        // 한때 `options.FallbackPolicy = options.DefaultPolicy` 로 두었다. 뜻은
        // 좋았다 — 화면마다 [Authorize] 를 붙이는 방식이면 새 화면에서 빠뜨리는
        // 순간 조용히 공개되니까.
        //
        // 그런데 그 정책은 <b>명시적 정책이 없는 모든 엔드포인트</b>에 걸린다.
        // Blazor 회로(`_blazor`)도 예외가 아니라서, 회로 협상이 401 이 되고
        // OnRedirectToLogin 이 /login 으로 보내고, 업무 앱에는 그런 화면이 없어
        // 다시 돌고… ERR_TOO_MANY_REDIRECTS 로 끝난다.
        //
        // 증상이 지독하다: 화면은 정상으로 그려진다(프리렌더는 되니까). 다만
        // 아무 버튼도 안 눌린다. curl 로는 보이지 않고 브라우저 콘솔을 봐야 안다.
        //
        // 같은 보호는 각 앱의 Components/_Imports.razor 에 `@attribute [Authorize]`
        // 를 두어 얻는다 — 그 폴더의 모든 컴포넌트에 걸리므로 새 화면에서
        // 빠뜨릴 수 없고, 엔드포인트가 아니라 컴포넌트에 걸리므로 회로를 건드리지 않는다.
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();

        // ── 게이트웨이 · 권한 · 메뉴 ─────────────────────────────
        //
        // 업무 앱도 자기 힘으로 게이트웨이를 부른다. 셸을 거치지 않는다 —
        // 거치면 셸이 모든 트래픽의 병목이 되고, 앱을 나눈 의미가 없어진다.
        // 토큰은 인증 쿠키 클레임에 실려 있으므로(TokenStore) 위의 키 링만
        // 공유되면 어느 앱이든 꺼내 쓸 수 있다. 별도 세션 저장소가 필요 없다.
        // TokenStore 가 회로 전(정적 SSR) 에는 HttpContext 로 사용자를 본다.
        services.AddHttpContextAccessor();
        services.AddScoped<ITokenStore, TokenStore>();
        services.AddJSiniGateway(configuration);
        // 부트스트랩이 구현체의 Apply 를 부르므로 구현체도 함께 올린다.
        // 둘이 같은 인스턴스여야 한다 — 따로 등록하면 부트스트랩이 채운
        // 권한표와 사이드바가 보는 권한표가 다른 물건이 된다.
        services.AddScoped<PermissionContext>();
        services.AddScoped<IPermissionContext>(sp => sp.GetRequiredService<PermissionContext>());
        services.AddScoped<MenuProvider>();
        services.AddScoped<IMenuProvider>(sp => sp.GetRequiredService<MenuProvider>());

        // 팝업 공지. **레이아웃과 로그인 화면이 함께 쓴다** — 포털관리 모듈에
        // 두면 레이아웃이 못 쓴다(셸은 모듈을 이름으로 알지 못한다).
        services.AddScoped<NoticeClient>();

        // 그중 **공개 공지만** 회로 바깥에서 잠깐 들고 있는 통.
        //
        // **싱글턴이어야 한다.** 로그인 화면 HTML 을 만드는 길 위에 있는
        // 왕복이라(`PublicNoticePopup`), scoped 로 두면 요청마다 게이트웨이를
        // 다녀온다. 담기는 것이 로그인 전에도 보이는 값이라 사람을 섞을
        // 위험도 없다(PublicNoticeStore 머리말).
        services.AddSingleton<PublicNoticeStore>();

        // ── 셸 상태 ──────────────────────────────────────────────
        //
        // 셋 다 scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다.
        // 싱글턴으로 두면 열어 둔 탭과 즐겨찾기가 모든 사용자에게 공유된다.
        services.AddScoped<MenuFavorites>();
        services.AddScoped<PortalTabs>();

        // 화면을 옮기는 동안의 표시. 레이아웃이 켜고 `DataPage` 가 끈다.
        services.AddScoped<PageTransition>();

        // 동작의 결과를 알리는 토스트. **회로마다 하나** — 싱글턴으로 두면
        // 남이 저장한 것이 내 화면에 뜬다(Toasts 머리말).
        services.AddScoped<Toasts>();

        // 헤더의 브레드크럼이 「이 메뉴를 사이드바에서 보여 달라」고 하는 통.
        // 그 둘은 형제도 부모 자식도 아니라 파라미터로 잇지 못한다(MenuReveal 머리말).
        services.AddScoped<MenuReveal>();

        // 헤더의 사용자 단추가 얼굴과 이름을 여기서 얻는다. 쿠키 클레임에는
        // 사진이 없어 게이트웨이에 한 번 물어야 한다(CurrentUser 머리말).
        services.AddScoped<CurrentUser>();

        // 레이아웃이 뜰 때 넷을 한 번에 읽는 자리(PortalBootstrap 머리말).
        services.AddScoped<PortalBootstrap>();

        // 브라우저에서 읽어 올 것을 한 왕복으로 읽는 자리(PortalBoot 머리말).
        //
        // **scoped 다.** 담는 값이 「이 탭의 것」이고(잠금 · 고정 탭) 저장소
        // 왕복 한 번이 값의 전부라, 부트스트랩처럼 싱글턴 통으로 올릴 이유가
        // 없다 — 그렇게 하면 탭을 가르는 열쇠가 필요해지는데 Blazor 는
        // 부품에게 회로 아이디를 알려 주지 않는다.
        services.AddScoped<PortalBoot>();

        // 참조자료(공통코드 · 회사 목록 · 범용 셀렉트 설정)를 회로 바깥에서
        // 들고 있는 통. **싱글턴이어야 하는 이유는 아래 부트스트랩 통과 같다** —
        // 모듈 컨테이너가 갈릴 때 함께 사라지면 아무것도 막지 못한다.
        //
        // 손잡이(ReferenceData)는 scoped 다. 사람을 가르는 열쇠를 만들려면
        // ITokenStore 를 봐야 하고 그것이 scoped 라서 그렇다.
        services.AddSingleton<ReferenceDataStore>();
        services.AddScoped<ReferenceData>();

        // 그 응답을 사용자별로 잠깐 들고 있는 통.
        //
        // **싱글턴이어야 한다.** scoped 로 두면 모듈 컨테이너가 갈릴 때 통도
        // 함께 사라져서, 막으려던 바로 그 재조회를 하나도 못 막는다
        // (PortalBootstrapStore 머리말).
        services.AddMemoryCache();
        services.AddSingleton<PortalBootstrapStore>();

        // 테마 서랍을 사용자 메뉴에서도 열 수 있게 하는 손잡이.
        services.AddScoped<ThemeDrawer>();

        // 이 창이 어디서 접속했는지. scoped 인 이유가 잠금과 같다 — 창마다 다르다.
        // 값을 채우는 곳은 SidebarFooter 이고, 왜 그쪽인지는 ClientAddress 머리말에 있다.
        services.AddScoped<ClientAddress>();

        // 잠금화면(D7)도 같은 이유로 scoped 다 — 한 사람이 잠갔다고 모두의
        // 화면이 덮이면 안 된다.
        services.AddScoped<ScreenLock>();

        // DevExpress 크기 모드(Small · Medium · Large)도 사람마다 다르다.
        // 값을 흘리는 것은 SizeModeScope 가 하고, 여기는 그것이 읽을 자리다.
        services.AddScoped<ThemeSize>();

        services.AddSingleton(RouteInventory.Build(
            routePrefix,
            routeAssemblies.Length > 0 ? routeAssemblies : [Assembly.GetEntryAssembly()!]));

        return builder;
    }

    /// <summary>
    /// 요청 파이프라인을 공통 순서로 세운다.
    ///
    /// 순서가 틀리면 조용히 잘못 동작한다 — 예컨대 UseAuthentication 이
    /// UseAntiforgery 뒤에 오면 폼 제출이 익명으로 처리된다. 일곱 곳에서
    /// 각자 순서를 적으면 언젠가 하나가 어긋난다.
    /// </summary>
    public static WebApplication UseJSiniWebApp(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        // .NET 9 부터는 UseStaticFiles 가 아니라 MapStaticAssets 다.
        //
        // UseStaticFiles 는 RCL 의 _content/... 를 못 찾는다 — 우리 레이아웃과
        // 테마 CSS 가 전부 JSini.Web.Components 에 있으므로 그러면 화면이
        // 스타일 없이 뜬다. MapStaticAssets 는 빌드 때 만들어진 매니페스트를
        // 읽어 RCL 자원까지 함께 서비스하고, 압축·캐시 헤더도 붙여 준다.
        //
        // **AllowAnonymous 가 반드시 있어야 한다.**
        //
        // 위에서 FallbackPolicy 를 "로그인해야 함" 으로 세웠는데, 그 정책은
        // 명시적 정책이 없는 <b>모든 엔드포인트</b>에 걸린다 — 정적 자원도
        // 예외가 아니다. 빼먹으면 CSS·JS 요청이 전부 302 로 로그인으로 튕기고,
        // 그 결과 <b>로그인 화면 자신이 스타일 없이</b> 뜬다.
        // 화면은 뜨니까 오류로 보이지 않고, "왜 이렇게 못생겼지" 로만 보인다.
        // **UseRouting 을 여기서 명시적으로 부른다.**
        //
        // 안 부르면 WebApplication 이 파이프라인 맨 앞에 자동으로 끼워 넣는데,
        // 그건 앱의 UsePathBase 보다 앞이다. 그러면 라우팅이 접두사가 붙은
        // 원래 경로(/projmng/_blazor)로 매칭을 시도해 회로 협상이 405 가 된다.
        //
        // 증상이 고약하다: 화면은 멀쩡히 그려지고(프리렌더는 되니까) 버튼만
        // 안 눌린다. 브라우저 콘솔을 봐야 "Failed to complete negotiation" 이 보인다.
        // **압축은 UseRouting 보다 앞이어야 한다.**
        //
        // 이 미들웨어는 응답 스트림을 갈아 끼우는 방식으로 동작하므로, 압축할
        // 응답을 만드는 미들웨어보다 **앞에** 서 있어야 한다. 뒤에 두면 아무
        // 일도 일어나지 않고 — 오류도 안 난다. 증상이 「압축을 넣었는데 헤더에
        // Content-Encoding 이 없다」 하나라 원인이 순서로 보이지 않는다.
        //
        // 정적 자원(MapStaticAssets)은 이 미들웨어를 거치지 않는 편이 낫지만
        // 그렇게 갈라 둘 필요가 없다 — 그쪽은 빌드 때 만들어 둔 .br/.gz 를
        // 그대로 내려주면서 Content-Encoding 을 이미 붙이고, 이 미들웨어는
        // 그 헤더가 있는 응답을 건드리지 않는다.
        app.UseResponseCompression();

        app.UseRouting();

        app.MapStaticAssets().AllowAnonymous();

        // **순서가 이 셋의 전부다.** 인증 → 인가 → 위조방지.
        //
        // 한때 UseAntiforgery 가 맨 앞에 있었다. 그러면 위조방지 미들웨어가
        // 아직 익명인 요청을 검사하게 되어, 로그인 폼 제출 같은 것이 조용히
        // 익명으로 처리된다. ASP.NET Core 가 문서로 정해 둔 순서가 이쪽이다.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // 첨부 내려받기 중계 (D5). **인증 뒤에 열어야 한다** — 이 경로는
        // 지금 요청의 신원을 그대로 게이트웨이로 흘려보내는 것이 전부이고,
        // 그 신원이 서는 곳이 UseAuthentication 이다. 앞에 두면 로그인한
        // 사람의 요청도 익명으로 나가서 공개 파일만 내려간다.
        app.MapJSiniFileDownload();

        // 프로필 사진 올리기 중계. 같은 이유로 인증 뒤에 열고, **위조방지
        // (UseAntiforgery) 뒤**여야 한다 — 그 미들웨어가 서 있어야
        // `DisableAntiforgery()` 가 뜻을 갖는다. 무엇을 대신 검사하는지는
        // ProfilePhotoUpload 머리말에 있다.
        app.MapJSiniProfilePhotoUpload();

        return app;
    }
}
