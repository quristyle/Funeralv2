using System.Globalization;
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
/// 셸의 서비스 등록을 한 곳에 모은다. 쿠키 이름과 DataProtection 응용 이름이 여기서만 정해져야 앱 사이에 로그인이 이어진다.
/// </summary>
public static class JSiniWebApp
{
/// <summary>인증 쿠키 이름. 이 값을 읽는 곳은 모두 같아야 한다.</summary>
    public const string AuthCookieName = "jsini.portal";

/// <summary>Data Protection 응용 이름. 앱마다 다르면 쿠키를 서로 풀지 못한다.</summary>
    private const string DataProtectionAppName = "JSini.Portal";

    /// <summary>
    /// 업무 앱과 셸이 공통으로 쓰는 것을 모두 등록한다.
    /// </summary>
    /// <param name="builder">호스트 빌더</param>
    /// <param name="routePrefix">앱이 사는 경로 접두사(<c>/funeral</c>). 셸은 빈 문자열. 권한표의 열쇠(전체 경로)와 맞추려고 받는다.</param>
    /// <param name="routeAssemblies">@page 를 담은 어셈블리. 라우트 대조에 쓴다.</param>
    public static WebApplicationBuilder AddJSiniWebApp(
        this WebApplicationBuilder builder,
        string routePrefix,
        params Assembly[] routeAssemblies)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        UseDashedDates();

        // ── 화면 ─────────────────────────────────────────────────
        services.AddDevExpressBlazor();
        services
            .AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                // 연결이 잠깐 끊겨도 회로가 3분 뒤 폐기되지 않도록 15분으로 늘린다.
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(15);
                options.DisconnectedCircuitMaxRetained = 200;
            })
            .AddHubOptions(options =>
            {
                // 다이어그램 저장본(그림이 data URL 로 들어간다)이 기본 32KB 를 넘으면 회로가 끊긴다.
                // 연결 하나가 한 번에 받는 양이라 4MB 를 넘겨 크게 두지 않는다.
                options.MaximumReceiveMessageSize = 4 * 1024 * 1024;

                // 일시적인 패킷 지연이나 모바일 망 전환 시 섣부른 연결 끊김을 방지한다.
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);

                // 켜지 않으면 SignalR 이 byte[] 인자를 DI 서비스로 보고 빼 버려 파일 조각 인자가 모자란다(Autofac).
                // 명시적 [FromServices] 인자만 서비스로 본다. Blazor 허브는 그런 인자가 없어 잃는 것이 없다.
                options.DisableImplicitFromServicesParameters = true;
            });

        // ── 응답 압축 ────────────────────────────────────────────
        // 화면 HTML 은 기본 MimeTypes 에 없어 압축되지 않았다. 여기서 더하고, Brotli 를 gzip 보다 먼저 등록한다(협상은 등록 순서).
        // nginx 가 이미 압축된 응답은 건너뛰므로 어느 쪽이 해도 결과는 같다.
        services.AddResponseCompression(options =>
        {
            // 운영은 nginx 뒤 HTTPS 라 여기서도 켠다. 위조방지 토큰은 매 요청 달라 BREACH 대상이 아니다.
            options.EnableForHttps = true;

            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            [
                "text/html",

                // 첨부 중계는 이미 압축된 그림·압축파일이라 목록에 넣지 않는다.
                "application/json",
                "image/svg+xml",
            ]);
        });

        // Optimal 을 쓴다. SmallestSize 는 CPU 를 몇 배 쓰고 몇 % 더 줄이는데, 응답이 사용자마다 달라 캐시로 회수할 수 없다.
        services.Configure<BrotliCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Optimal);
        services.Configure<GzipCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Optimal);

        // ── 앱 사이에 쿠키를 공유하기 위한 키 링 ─────────────────
        // 키 링을 파일로 공유한다. 상대 경로는 ContentRoot 기준으로 풀어야 실행 방식(dotnet run · 컨테이너)에 따라 갈라지지 않는다.
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

                // 업무 앱이 하위 경로(/funeral 등)에 있으므로 Path 는 루트여야 한다.
                options.Cookie.Path = "/";

                // 개발은 http, 운영은 nginx 뒤 https.
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.None
                    : CookieSecurePolicy.Always;

                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/forbidden";

                // LoginPath 만 두면 업무 앱의 PathBase 가 붙어 /funeral/login 으로 가 404 가 된다. 로그인은 셸에만 있으므로 루트 절대 경로로 보내고, 돌아올 주소에만 PathBase 를 붙인다.
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

                // 하루 근무를 덮는 8시간. 미끄럼 만료라 쓰는 동안에는 풀리지 않는다.
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        // FallbackPolicy 는 쓰지 않는다. 명시 정책 없는 모든 엔드포인트(Blazor 회로 _blazor 포함)에 걸려 협상이 401 → /login 을 돌며 ERR_TOO_MANY_REDIRECTS 가 난다.
        // 대신 각 앱 Components/_Imports.razor 의 @attribute [Authorize] 로 컴포넌트에 건다.
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();

        // ── 게이트웨이 · 권한 · 메뉴 ─────────────────────────────
        // 업무 앱이 게이트웨이를 직접 부른다(BFF). 토큰은 인증 쿠키 클레임에 실려 키 링만 공유하면 어느 앱이든 꺼내 쓴다.
        // TokenStore 는 회로 전(정적 SSR)에는 HttpContext 로 사용자를 본다.
        services.AddHttpContextAccessor();
        // 갱신 토큰은 사용자별 TokenStore 가 아니라 이 싱글턴에 담는다(AuthTokenHandler 는 공용이다).
        services.AddSingleton<RefreshedTokens>();
        services.AddScoped<ITokenStore, TokenStore>();
        services.AddJSiniGateway(configuration);
        // 부트스트랩이 구현체의 Apply 를 부른다. 인터페이스와 구현은 같은 인스턴스여야 한다.
        services.AddScoped<PermissionContext>();
        services.AddScoped<IPermissionContext>(sp => sp.GetRequiredService<PermissionContext>());
        services.AddScoped<MenuProvider>();
        services.AddScoped<IMenuProvider>(sp => sp.GetRequiredService<MenuProvider>());

        // 아이디로 이름·얼굴을 푸는 창구. 모듈끼리 참조할 수 없어 여럿이 쓰는 이 자리에 둔다.
        services.AddScoped<UserFaceClient>();

        // 공지 조회. 레이아웃과 로그인 화면이 함께 쓰므로 셸에 둔다(셸은 모듈을 이름으로 알지 못한다).
        services.AddScoped<NoticeClient>();

        // 메뉴 사용기록. 회로 안 라우팅은 HTTP 로그에 안 남아 레이아웃이 직접 적는다.
        services.AddScoped<MenuUsageRecorder>();

        // 화면 접근 판정의 단일 지점과 차단 기록. 사이드바·탭·레이아웃·라우터 앞 문(ScreenGate)이 모두 여기에 묻는다.
        services.AddScoped<ScreenAccess>();
        services.AddScoped<BlockedAccessRecorder>();

        // 내 알림 설정·웹푸시 구독. 환경설정 화면이 쓴다.
        services.AddScoped<Settings.NotificationClient>();

        // 웹푸시 구독 절차. 순서가 틀리면 조용히 깨지므로 한 벌만 둔다(PushEnroll).
        services.AddScoped<Settings.PushEnroll>();

        // 위치 저장 절차. 순서를 틀리면 좌표가 날아가거나 다른 탭의 스위치를 되돌린다(GeoLocator).
        services.AddScoped<Settings.GeoLocator>();

        // 남에게 알림을 보낸다(앱 푸시 · 메일). 창은 Settings/NotifySendPopup 이 그린다.
        services.AddScoped<Settings.NotifySender>();

        // 쪽지 송신. 헤더 띠에 쓰므로 셸(레이아웃)에 둔다.
        services.AddScoped<Settings.NoteClient>();

        // 로그인 뒤 첫 화면 설정. 쓰는 자리가 셸 홈이라 여기 둔다(규칙은 Layout/PortalHome).
        services.AddScoped<Settings.HomePathClient>();

        // 공개 공지 통. 로그인 화면 HTML 과 함께 쓰이므로 싱글턴이다(요청마다 게이트웨이를 부르지 않는다).
        services.AddSingleton<PublicNoticeStore>();

        // ── 셸 상태 ──────────────────────────────────────────────
        // scoped: 회로 하나가 사용자 한 명의 창이다. 싱글턴이면 탭과 즐겨찾기가 모두에게 공유된다.
        services.AddScoped<MenuFavorites>();
        services.AddScoped<PortalTabs>();

        // 화면을 옮기는 동안의 표시. 레이아웃이 켜고 `DataPage` 가 끈다.
        services.AddScoped<PageTransition>();

        // 떠난 화면의 상태(조건·목록·표 모습)를 맡겨 두는 통. 회로마다 하나다(ScreenState).
        services.AddScoped<ScreenState>();

        // 토스트 통. 회로마다 하나여야 한다(싱글턴이면 남의 알림이 뜬다).
        services.AddScoped<Toasts>();

        // 브레드크럼이 메뉴 펼침을 사이드바에 요청하는 통(MenuReveal).
        services.AddScoped<MenuReveal>();

        // 휴대폰에서 브레드크럼이 탭 메뉴 열기를 요청하는 통(TabMenuRequest).
        services.AddScoped<TabMenuRequest>();

        // 헤더 사용자 단추의 얼굴·이름. 쿠키 클레임에는 사진이 없어 게이트웨이에 묻는다.
        services.AddScoped<CurrentUser>();

        // 레이아웃이 뜰 때 한 번에 읽는 부트스트랩(PortalBootstrap).
        services.AddScoped<PortalBootstrap>();

        // 레이아웃 상단 도구 아이콘 관리자
        services.AddScoped<Layout.HeaderIconManager>();

        // 계정에 담긴 화면 설정(휴대폰 띠 · 바로가기 · 토스트 자리). 브라우저가 아니라 계정에 붙는다. scoped 다.
        services.AddScoped<Settings.PortalPrefs>();

        // 브라우저 저장소 값(잠금·고정 탭 등)을 한 왕복으로 읽는다. 탭의 것이라 싱글턴이 아니다(scoped).
        services.AddScoped<PortalBoot>();

        // 참조자료(공통코드·회사·셀렉트 설정) 통. 모듈 컨테이너가 갈려도 살아야 하므로 싱글턴이다.
        // 손잡이 ReferenceData 는 사용자별 ITokenStore 를 보므로 scoped 다.
        services.AddSingleton<ReferenceDataStore>();
        services.AddScoped<ReferenceData>();

        // 부트스트랩 응답을 사용자별로 잠깐 들고 있는 통. 모듈 컨테이너가 갈려도 살아야 하므로 싱글턴이다.
        services.AddMemoryCache();
        services.AddSingleton<PortalBootstrapStore>();

        // 오른쪽 서랍 넷 중 하나만 펴는 묶음. 손잡이보다 먼저 등록해야 손잡이가 접는 길을 걸 수 있다.
        services.AddScoped<RightDrawerGroup>();

        // 테마 서랍을 사용자 메뉴에서도 열 수 있게 하는 손잡이.
        services.AddScoped<ThemeDrawer>();
        services.AddScoped<UserMenuDrawer>();

        // 알림함 서랍. 종은 헤더, 판은 셸 바깥에 있어 이 손잡이가 둘을 잇는다(헤더가 쌓임 맥락을 만든다).
        services.AddScoped<NotificationDrawer>();

        // 빠른 지시 서랍 손잡이. 안의 알맹이는 업무 모듈이 등록한다(QuickAskContent).
        services.AddScoped<QuickAskReveal>();

        // AI 대화 서랍을 헤더 단추에서 여닫는 손잡이. 같은 이유로 scoped 다.
        services.AddScoped<AiChatReveal>();

        // 창마다 다른 접속 주소. 값은 SidebarFooter 가 채운다.
        services.AddScoped<ClientAddress>();

        // 잠금화면도 scoped 다 — 한 사람이 잠갔다고 모두의 화면이 덮이면 안 된다.
        services.AddScoped<ScreenLock>();

        // 그림 미리보기 창. 여는 자리가 많아 손잡이 하나로 모으고, 사용자별로 scoped 다.
        services.AddScoped<ImagePreview>();

        // 아바타를 누르면 뜨는 사람 카드 손잡이.
        services.AddScoped<UserCard>();

        // 글자 크기 단계(사람마다 다르다). SizeModeScope 가 흘리는 값을 읽는 자리다.
        services.AddScoped<ThemeSize>();

        // ── 오류 기록 ────────────────────────────────────────────
        //
        // 오류 기록: 추적 번호로 찾도록 표(portal_error_logs)에 적는다. 보고 통·배경 작업·처리기는 같은 인스턴스를 공유해야 한다.
        services.AddHttpClient(Diagnostics.PortalErrorReporter.HttpClientName, client =>
            {
                var gateway = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
                client.BaseAddress = new Uri(gateway.EndsWith('/') ? gateway : gateway + "/");

                // 게이트웨이가 죽어 있을 때 큐가 막히지 않도록 짧게 끊는다.
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar);

        services.AddSingleton<Diagnostics.PortalErrorReporter>();
        services.AddHostedService(sp => sp.GetRequiredService<Diagnostics.PortalErrorReporter>());
        services.AddExceptionHandler<Diagnostics.PortalErrorHandler>();

        var assemblies = routeAssemblies.Length > 0
            ? routeAssemblies
            : [Assembly.GetEntryAssembly()!];

        services.AddSingleton(RouteInventory.Build(routePrefix, assemblies));

        // 화면마다 권한이 어디서 오는지(RouteInventory 와 같은 어셈블리를 훑는다).
        services.AddSingleton(ScreenAccessCatalog.Build(assemblies));

        return builder;
    }

    /// <summary>
    /// 날짜 기본 표기를 <c>yyyy-MM-dd</c> 로 맞춘다. 적지 않은 칸은 CurrentCulture 의 짧은 날짜(ko-KR 은 <c>yyyy. M. d.</c>)로 떨어진다.
    /// ko-KR 복제본의 날짜 서식만 바꾼다. 프로세스 전역이며 지역화 미들웨어가 없어 회로에도 그대로 걸린다.
    /// </summary>
    private static void UseDashedDates()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ko-KR").Clone();

        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        culture.DateTimeFormat.DateSeparator = "-";

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // DI 를 세우는 이 스레드에도 걸어야 기동 중 찍는 날짜가 같은 서식이 된다.
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// 요청 파이프라인을 공통 순서로 세운다.
    /// 순서가 틀리면 조용히 잘못 동작한다(UseAuthentication 이 UseAntiforgery 뒤면 폼 제출이 익명 처리된다).
    /// </summary>
    public static WebApplication UseJSiniWebApp(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        // 정적 자원은 MapStaticAssets 다(UseStaticFiles 는 RCL 의 _content 를 못 찾는다).
        // 그 뒤의 AllowAnonymous 는 필수다 — 빼면 정책에 걸려 로그인 화면 CSS 가 302 로 튕긴다.
        // UseRouting 을 명시적으로 부른다. 안 부르면 자동 라우팅이 UsePathBase 앞에 끼어 회로 협상이 405 가 된다.
        // 압축은 응답을 만드는 미들웨어보다 앞에 둔다(스트림을 갈아 끼우므로 뒤에 두면 아무 일도 없고 오류도 없다).
        // 정적 자원은 이미 Content-Encoding 이 붙어 있어 이 미들웨어가 건드리지 않는다.
        app.UseResponseCompression();

        app.UseRouting();

        app.MapStaticAssets().AllowAnonymous();

        // 인증 → 인가 → 위조방지 순서. 위조방지가 인증보다 앞이면 폼 제출이 익명으로 처리된다.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // 첨부 내려받기 중계. 요청의 신원을 그대로 게이트웨이로 흘리므로 UseAuthentication 뒤에 연다.
        app.MapJSiniFileDownload();

        // 프로필 사진 올리기 중계. 인증 뒤, 위조방지(UseAntiforgery) 뒤에 연다 — DisableAntiforgery() 가 그 미들웨어에 기댄다.
        app.MapJSiniProfilePhotoUpload();

        return app;
    }
}
