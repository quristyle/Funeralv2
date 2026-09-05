using JSini.PublicSite.Api;
using JSini.PublicSite.Components;

var builder = WebApplication.CreateBuilder(args);

// **정적 SSR 전용이다.** AddInteractiveServerComponents() 를 부르지 않는다.
//
// 회로가 없다는 것은 blazor.web.js 도, 웹소켓도, 서버가 들고 있는 사용자별
// 상태도 없다는 뜻이다. 공개 사이트에는 그 셋이 다 부담이다 — 검색 봇과
// 링크 미리보기가 대부분인 트래픽에 회로를 열어 줄 이유가 없다.
//
// 원본(vite-ssg)이 정적 프리렌더를 골랐던 이유(첫 화면 속도·검색 노출)를
// 서버 렌더로 그대로 잇는다. 대신 프리렌더와 달리 DB 문구가 늘 최신이다.
builder.Services.AddRazorComponents();

// SiteServer 공개 API. 인증이 없다 — 공개 사이트라 BFF 토큰 처리가 필요 없다.
builder.Services.AddHttpClient<SiteApi>(client =>
{
    var baseUrl = builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

    // 소개 사이트는 백엔드가 느리다고 함께 느려지면 안 된다. SiteApi 는 실패를
    // 빈 값으로 바꾸므로, 짧게 끊고 화면을 그리는 편이 낫다.
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseRouting();
app.MapStaticAssets();

// 문의 폼이 정적 SSR 폼(EditForm Method="post")이라 위조방지가 필요하다.
app.UseAntiforgery();

app.MapRazorComponents<App>();

// 언어 없이 들어오면 기본 언어로 보낸다. 원본 라우터의 `{ path: '/', redirect: '/ko' }` 다.
//
// 301 이 아니라 302 다 — 나중에 Accept-Language 를 보고 고르게 바꿀 수 있는데,
// 301 로 내보내면 브라우저가 영구 캐시해서 그 변경이 먹지 않는다.
app.MapGet("/", () => Results.Redirect("/ko"));

app.Run();
