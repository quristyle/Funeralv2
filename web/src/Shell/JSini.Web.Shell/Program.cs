using JSini.Web.Components;
using JSini.Web.Shell.Components;
using JSini.Web.Shell.Routing;
using JSini.Web.Shell.Security;

var builder = WebApplication.CreateBuilder(args);

// ── 업무 MFE 목록 ────────────────────────────────────────────────
//
// 업무 앱은 각자 독립 프로세스라 셸의 출력 폴더에 DLL 이 없다. 어셈블리를
// 훑어 찾을 방법이 없으므로 설정에서 읽는다 — 주소는 환경마다 다르니
// (개발은 localhost 포트, 운영은 컨테이너 이름) 원래 설정이 정할 일이다.
var apps = builder.Configuration.GetSection("PortalApps").Get<PortalApp[]>() ?? [];

// ── 셸도 업무 앱과 똑같이 구성한다 ───────────────────────────────
//
// 쿠키 이름·만료·Data Protection 키 링이 일곱 앱에서 모두 같아야 한다.
// 하나만 어긋나면 업무를 옮길 때마다 로그인 화면으로 튕긴다.
builder.AddJSiniWebApp(routePrefix: string.Empty, typeof(Program).Assembly);

builder.Services.AddSingleton<IReadOnlyList<PortalApp>>(apps);
builder.Services.AddPortalProxy(apps);
builder.Services.AddScoped<LoginService>();

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

app.Logger.LogInformation(
    "업무 MFE {Count}개: {Apps}",
    apps.Length,
    string.Join(", ", apps.Select(a => $"{a.RoutePrefix}→{a.Address}")));

app.UseJSiniWebApp();

// ── 업무 경로를 MFE 로 넘긴다 ────────────────────────────────────
//
// **셸의 라우팅보다 먼저 와야 한다.** 뒤에 두면 /funeral 이 셸의 라우터에
// 먼저 걸려 NotFound 가 되고, 프록시까지 오지 않는다.
//
// 인증을 요구하는 이유: 로그인하지 않은 요청을 MFE 까지 보낼 이유가 없다.
// 여기서 막으면 셸의 /login 으로 곧바로 나가고, MFE 는 익명 트래픽을 아예 안 본다.
app.MapReverseProxy().RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 로그아웃은 POST 다. GET 으로 두면 이미지 태그 하나로 남을 로그아웃시킬 수 있고
// (CSRF), 브라우저가 미리 읽어 보는 것만으로도 로그아웃된다.
app.MapPost("/logout", async (HttpContext context) =>
{
    await LoginService.SignOutAsync(context);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.Run();
