using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components;
using JSini.Web.Components.Menu;
using JSini.Web.Shell.Components;
using JSini.Web.Shell.Routing;
using JSini.Web.Shell.Security;
using Piral.Blazor.Orchestrator;
using Piral.Blazor.Orchestrator.Loader;

var builder = WebApplication.CreateBuilder(args);

// appsettings.Local.json(git 제외)을 더한다. 개발용 로그인 미리 채우기(DevLogin)는 이 파일에만 둔다.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ── 업무 MFE 모듈 검색 ──
// AddJSiniWebApp 보다 먼저 해야 한다(라우트 인벤토리가 모듈 어셈블리를 받아야 한다). 셸은 모듈 타입을 모르고 출력 폴더의 JSini.Web.*.dll 에서 IPortalModule 을 찾는다. csproj 의 ProjectReference 가 빠지면 0개다.
using var discoveryLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();
});

var moduleRegistry = PortalModuleRegistry.DiscoverAndRegister(
    builder.Services,
    builder.Configuration,
    logger: discoveryLoggerFactory.CreateLogger<PortalModuleRegistry>());

// ── 셸 구성 ──
// 접두사를 비운다. 한 프로세스·한 라우터라 @page 가 곧 전체 경로다(/funeral/status).
builder.AddJSiniWebApp(
    routePrefix: string.Empty,
    routeAssemblies: [typeof(Program).Assembly, .. moduleRegistry.Assemblies]);

builder.Services.AddScoped<LoginService>();

// 로그인 전에 부르는 계정 경로(가입 신청 · 비밀번호 찾기). 토큰을 붙이지 않는 클라이언트를 쓴다.
builder.Services.AddScoped<AccountClient>();

// 셸이 기대하는 모듈 목록(appsettings 의 PortalApps). 진단 화면과 기동 대조에 쓴다.
var expectedApps = PortalApp.Read(builder.Configuration);
builder.Services.AddSingleton<IReadOnlyList<PortalApp>>(expectedApps);

// ── Piral.Blazor MFE 오케스트레이션 ──
// 모듈 컨테이너와 PageScripts/PageStyles 주입을 쓴다. 기본 MfDiscoveryLoaderService(feed.piral.cloud)는 기동 때마다 바깥으로 나가 아무것도 싣지 못하므로, 로컬 캐시 폴더(Microfrontends:CacheDir)만 보는 스냅샷 로더로 갈아 끼운다.
// 모듈은 빌드 시점에 합성되므로 그 폴더는 지금 비어 있다. 무중단 개별 배포가 필요해지면 모듈을 nupkg 파일럿으로 말아 이 폴더에 둔다.
builder.Services.AddMicrofrontends<MfSnapshotLoaderService>();
builder.Host.UseMicrofrontendContainers();

// 로그인 전용 클라이언트. AuthTokenHandler 를 거치지 않는다. 로그인 때는 토큰이 없고 Set-Cookie(리프레시 토큰)를 직접 읽어야 한다.
builder.Services.AddHttpClient(LoginService.HttpClientName, client =>
{
    var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // 쿠키를 직접 다룬다. 자동 수집하면 핸들러 재사용으로 남의 리프레시 쿠키가 섞인다.
    UseCookies = false,
})
// 로그인한 사람의 주소를 게이트웨이로 넘긴다. 빠지면 로그인 속도 제한(IP 당 분당 10회)이 포털 컨테이너 하나로 묶여 열한 번째 사람부터 429 가 된다.
// 익명 클라이언트는 둘(이 로그인용, JSini.Web.Http 의 AnonymousClientName)이라 한쪽만 고치면 그쪽만 조용히 바뀐다. 둘 다 이 핸들러를 붙인다.
.AddHttpMessageHandler<ClientIpHandler>();

var app = builder.Build();

// ── 기동 진단 ─────────────────────────────────────────────────────
// 모듈이 0개여도 셸은 뜨고 업무 메뉴만 404 가 된다. 눈에 띄게 남긴다.
{
    var modules = moduleRegistry.Modules;
    var routes = app.Services.GetRequiredService<RouteInventory>();

    app.Logger.LogInformation(
        "JSini Piral.Blazor 셸 기동 — 모듈 {Modules}개 ({Keys}), 라우트 {Routes}개",
        modules.Count,
        string.Join(", ", modules.Select(m => m.Key)),
        routes.Paths.Count);

    // 기대 목록과 대조. 어긋난 쪽이 곧 "그 업무만 통째로 404" 다.
    var found = modules.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missing = expectedApps.Where(a => !found.Contains(a.Key)).Select(a => a.Key).ToList();

    if (missing.Count > 0)
    {
        app.Logger.LogCritical(
            "셸이 기대한 업무 모듈이 실려 있지 않다: {Missing}. "
            + "그 업무 화면은 전부 404 가 된다 — 셸 csproj 의 '업무 MFE 모듈' "
            + "ProjectReference 와 모듈의 IPortalModule.Key 를 확인하라.",
            string.Join(", ", missing));
    }

    foreach (var app_ in expectedApps)
    {
        var module = modules.FirstOrDefault(m =>
            string.Equals(m.Key, app_.Key, StringComparison.OrdinalIgnoreCase));

        if (module is not null && module.RoutePrefix != app_.RoutePrefix)
        {
            app.Logger.LogCritical(
                "'{Key}' 접두사가 어긋난다: 셸 설정={Shell}, 모듈 선언={Module}",
                app_.Key, app_.RoutePrefix, module.RoutePrefix);
        }
    }
}

app.UseJSiniWebApp();
app.UseMicrofrontends();

// AddAdditionalAssemblies 가 없으면 업무 화면이 전부 404 다. Routes.razor 의 Router 만으로는 부족하다(그건 회로 안 라우팅).
// 첫 요청의 404 여부는 엔드포인트 라우팅이 정하므로 둘 다 적는다. 여기만 빠지면 주소 직접 입력이, Router 만 빠지면 메뉴 이동이 깨진다.
app.MapMicrofrontends<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. moduleRegistry.Assemblies]);

// ── 패스키 도전값 중계 ──
// 로그인 화면의 지문·얼굴 단추가 부른다. 익명이다. 나가는 값에 비밀은 없고, 자격 증명 목록은 아이디를 적었을 때만 채워진다(없는 아이디에도 빈 목록을 줘 계정 존재를 숨긴다).
// 브라우저가 게이트웨이를 직접 부르지 않는다(BFF). 시도 제한은 게이트웨이의 auth-attempts 가 건다.
app.MapPost("/passkey/options", async (
    LoginService auth, PasskeyOptionsRequest? request,
    CancellationToken cancellationToken) =>
{
    var options = await auth.GetPasskeyLoginOptionsAsync(request?.Username, cancellationToken);

    return options is { } value
        ? Results.Json(value)
        // 까닭을 나누지 않는다. 브라우저가 할 수 있는 일은 「지금은 안 된다」를 말하는 것뿐이다.
        : Results.Json(new { error = "지금은 기기 인증을 쓸 수 없습니다." }, statusCode: 503);
}).AllowAnonymous();

// ── 소셜 로그인 (구글 · 네이버 · 카카오) ──
// 브라우저가 지나가는 두 자리(공급자로 보내기 · 되돌아오기)를 셸에 건다. 공급자가 사람을 화면으로 돌려보내야 해서 게이트웨이가 아니다.
app.MapSocialLoginFlow();

// 로그아웃은 POST 다(GET 이면 이미지 태그 하나로 로그아웃시킬 수 있다). ?noauto=1 을 붙여 자동 로그인을 한 번만 멈춘다. 맨 /login 이면 바로 다시 들어가고, 패스키 자동 열기도 같은 표시로 멈춘다.
app.MapPost("/logout", async (HttpContext context) =>
{
    await LoginService.SignOutAsync(context);
    return Results.Redirect("/login?noauto=1");
}).RequireAuthorization();

app.Run();

/// <summary>패스키 도전값 요청. 아이디는 있어도 되고 없어도 된다(없으면 기기가 열쇠를 고른다).</summary>
/// <param name="Username">로그인 아이디. 비워도 된다.</param>
internal sealed record PasskeyOptionsRequest(string? Username);
