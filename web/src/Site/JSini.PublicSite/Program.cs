using JSini.PublicSite.Api;
using JSini.PublicSite.Components;
using JSini.PublicSite.Site;

var builder = WebApplication.CreateBuilder(args);

// 정적 SSR 전용. AddInteractiveServerComponents() 를 부르지 않는다. 회로·blazor.web.js·사용자별 상태가 없다(검색 봇과 링크 미리보기가 대부분인 트래픽이다).
builder.Services.AddRazorComponents();

// 방문자 주소를 게이트웨이로 넘긴다(ClientIpHandler). 없으면 문의 폼의 분당 3회 제한이 사이트 전체 공용 통이 된다.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ClientIpHandler>();

// 개인정보처리방침 · 이용약관에 적는 회사 정보(LegalInfo). 사람이 바뀌면 설정만 고친다.
builder.Services.Configure<LegalInfo>(builder.Configuration.GetSection("Legal"));

// SiteServer 공개 API. 인증이 없어 BFF 토큰 처리가 필요 없다.
builder.Services.AddHttpClient<SiteApi>(client =>
{
    var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

    // 백엔드가 느려도 소개 사이트가 함께 느려지면 안 된다. 짧게 끊고 빈 값으로 그린다.
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddHttpMessageHandler<ClientIpHandler>();

var app = builder.Build();

// ── HEAD 를 GET 처럼 받는다 ───────────────────────────────
// 화면 끝점은 HEAD 에 405 를 준다. 주소 검사기(구글 OAuth 브랜딩 인증)가 HEAD 부터 보내므로 GET 으로 바꿔 그리고 본문만 버린다. 헤더는 GET 과 같아야 한다.
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsHead(context.Request.Method))
    {
        await next();
        return;
    }

    context.Request.Method = HttpMethods.Get;
    var body = context.Response.Body;
    context.Response.Body = Stream.Null;

    try
    {
        await next();
    }
    finally
    {
        context.Response.Body = body;
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

// ── 언어 조각이 아닌 주소를 기본 언어로 보낸다 ──────────────
// 주소의 첫 조각이 언어가 아니면 /ko 를 붙여 다시 보낸다(/about → /ko/about). 안 그러면 아무 주소나 기본 언어 화면으로 200 이 나와 검색 봇이 같은 페이지를 주소마다 담는다.
// 정적 파일은 맨 끝 조각의 확장자로 가린다. 첫 조각만 보면 하위 폴더의 파일까지 옮겨져 404 가 된다.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var trimmed = path.Trim('/');
    var segments = trimmed.Split('/');

    var skip =
        trimmed.Length == 0
        || segments[^1].Contains('.')
        || segments[0].StartsWith('_')
        || SiteMessages.Locales.Contains(segments[0], StringComparer.OrdinalIgnoreCase);

    if (skip)
    {
        await next();
        return;
    }

    // 물음표 뒤는 그대로 옮긴다. 안 옮기면 UTM 같은 유입 경로가 사라진다.
    context.Response.Redirect($"/{SiteMessages.DefaultLocale}{path}{context.Request.QueryString}");
});

app.UseRouting();
app.MapStaticAssets();

// 문의 폼이 정적 SSR 폼(EditForm Method="post")이라 위조방지가 필요하다.
app.UseAntiforgery();

app.MapRazorComponents<App>();

// 언어 없이 들어오면 /ko 로 보낸다. 302 다. 301 이면 브라우저가 영구 캐시해 나중에 Accept-Language 로 고르는 변경이 먹지 않는다.
app.MapGet("/", () => Results.Redirect("/ko"));

app.Run();
