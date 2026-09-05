using JSini.Web.Abstractions;
using JSini.Web.Components;
using JSini.Web.Components.Menu;
using JSini.Web.Shell.Components;
using JSini.Web.Shell.Routing;
using JSini.Web.Shell.Security;
using Piral.Blazor.Orchestrator;
using Piral.Blazor.Orchestrator.Loader;

var builder = WebApplication.CreateBuilder(args);

// ── 업무 MFE 모듈 검색 ────────────────────────────────────────────
//
// **AddJSiniWebApp 보다 먼저 해야 한다.** 라우트 인벤토리(RouteInventory)가
// 모듈 어셈블리를 받아야 하는데, 그걸 만드는 것이 AddJSiniWebApp 이기 때문이다.
// 순서가 뒤바뀌면 인벤토리에 셸의 라우트(/login 등)만 담기고, DB 메뉴 대조가
// "화면이 하나도 없다" 로 잘못 보고된다.
//
// 셸은 모듈 타입을 이름으로 알지 못한다 — 출력 폴더의 JSini.Web.*.dll 을 훑어
// IPortalModule 구현을 찾을 뿐이다. 그 DLL 이 거기 있는 것은 csproj 의
// ProjectReference 덕분이고, 그 참조가 빠지면 여기서 0개가 나온다.
using var discoveryLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();
});

var moduleRegistry = PortalModuleRegistry.DiscoverAndRegister(
    builder.Services,
    builder.Configuration,
    logger: discoveryLoggerFactory.CreateLogger<PortalModuleRegistry>());

// ── 셸 구성 ───────────────────────────────────────────────────────
//
// 접두사가 빈 문자열인 것이 요점이다. 모듈이 각자 프로세스이던 시절에는
// UsePathBase 가 접두사를 떼어 주어 @page 가 상대 경로였지만, 지금은 한
// 프로세스·한 라우터라 @page 가 곧 전체 경로다(/funeral/status).
builder.AddJSiniWebApp(
    routePrefix: string.Empty,
    routeAssemblies: [typeof(Program).Assembly, .. moduleRegistry.Assemblies]);

builder.Services.AddScoped<LoginService>();

// 셸이 기대하는 모듈 목록(appsettings 의 PortalApps). 진단 화면과 기동 대조에 쓴다.
var expectedApps = PortalApp.Read(builder.Configuration);
builder.Services.AddSingleton<IReadOnlyList<PortalApp>>(expectedApps);

// ── Piral.Blazor MFE 오케스트레이션 ───────────────────────────────
//
// 모듈 컨테이너(모듈별 DI 격리)와 PageScripts/PageStyles 주입이 여기서 온다.
//
// **로더를 갈아 끼운 이유.** 기본 MfDiscoveryLoaderService 는 설정이 없으면
// feed.piral.cloud 를 본다 — 기동할 때마다 바깥으로 나가고, 그 피드에 우리
// 파일럿은 없으니 아무것도 싣지 못하면서 기동만 늦어진다. 스냅샷 로더는
// 로컬 캐시 폴더(Microfrontends:CacheDir)만 본다.
//
// 지금 그 폴더는 비어 있다. 모듈은 빌드 시점에 합성되기 때문이다(셸 csproj).
// 무중단 개별 배포가 필요해지면 각 모듈을 nupkg 파일럿으로 말아 이 폴더에
// 떨어뜨리는 것이 다음 단계고, **그때 고칠 곳이 이 한 줄이다.**
builder.Services.AddMicrofrontends<MfSnapshotLoaderService>();
builder.Host.UseMicrofrontendContainers();

// 로그인 전용 클라이언트. AuthTokenHandler 를 거치지 않는다 — 로그인 시점에는
// 붙일 토큰이 없고, 응답의 Set-Cookie(리프레시 토큰)를 직접 읽어야 한다.
builder.Services.AddHttpClient(LoginService.HttpClientName, client =>
{
    var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // 쿠키를 우리가 직접 다룬다. HttpClient 가 자동으로 모아 두면 그 통이
    // 모든 사용자에게 공유되어(핸들러는 재사용된다) 남의 리프레시 쿠키가 섞인다.
    UseCookies = false,
});

var app = builder.Build();

// ── 기동 진단 ─────────────────────────────────────────────────────
//
// 모듈이 0개여도 셸은 멀쩡히 뜬다. 로그인도 되고 첫 화면도 나온다 —
// 업무 메뉴를 누를 때만 404 다. 실제로 그 상태로 한동안 굴러갔으므로,
// 눈에 띄게 남긴다.
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

// **AddAdditionalAssemblies 가 없으면 업무 화면이 전부 404 다.**
//
// Routes.razor 의 <Router AdditionalAssemblies="..."> 만으로는 안 된다. 그건
// 회로가 붙은 뒤 브라우저 안에서 도는 라우팅이고, 첫 요청이 404 냐 아니냐를
// 정하는 것은 **엔드포인트 라우팅**이다. 엔드포인트 쪽은 여기 적은 어셈블리만
// 훑는다. 둘 다 적어야 하고, 한쪽만 적으면 증상이 갈린다.
//   · 여기만 빠짐  → 주소를 직접 쳐도 404 (지금 고친 것)
//   · Router 만 빠짐 → 첫 화면은 뜨는데 메뉴로 이동하면 못 찾는다
app.MapMicrofrontends<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. moduleRegistry.Assemblies]);

// 로그아웃은 POST 다. GET 으로 두면 이미지 태그 하나로 남을 로그아웃시킬 수 있고
// (CSRF), 브라우저가 미리 읽어 보는 것만으로도 로그아웃된다.
app.MapPost("/logout", async (HttpContext context) =>
{
    await LoginService.SignOutAsync(context);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.Run();
