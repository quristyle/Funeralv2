using JSini.PublicSite.Api;
using JSini.PublicSite.Components;
using JSini.PublicSite.Site;

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

// ── 언어 조각이 아닌 주소를 기본 언어로 보낸다 ──────────────
//
// 화면 주소가 `/{Locale}/...` 라 **첫 조각이 무엇이든 맞아 버린다.**
// `/about` 은 `Locale = "about"` 로 잡히고, 언어를 못 알아보면 기본값(ko)으로
// 좁히므로 **한국어 첫 화면이 200 으로 그려진다.**
//
// 공개 사이트에서 그것은 그냥 오타 처리 문제가 아니다 — `/about` · `/services` ·
// 무엇이든 같은 내용이 200 으로 나오니, 검색 봇이 같은 페이지를 주소마다
// 따로 담는다(중복 문서). 이 사이트의 트래픽 대부분이 검색 봇이다.
//
// 그래서 첫 조각이 아는 언어가 아니면 `/ko` 를 붙여 다시 보낸다.
// `/about` → `/ko/about`(실제 화면), `/xyzzy` → `/ko/xyzzy`(404).
// 사람이 친 주소는 살리고, 없는 주소는 없다고 답한다.
//
// **정적 파일과 프레임워크 경로는 건드리지 않는다.** 파일은 확장자로 가리고
// (`/site.css`), 프레임워크 경로는 접두사로 가린다.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var first = path.Trim('/').Split('/', 2)[0];

    var skip =
        first.Length == 0
        || first.Contains('.')
        || first.StartsWith('_')
        || SiteMessages.Locales.Contains(first, StringComparer.OrdinalIgnoreCase);

    if (skip)
    {
        await next();
        return;
    }

    // 물음표 뒤는 그대로 옮긴다. 안 옮기면 UTM 같은 것이 사라져
    // 유입 경로를 잃는다.
    context.Response.Redirect($"/{SiteMessages.DefaultLocale}{path}{context.Request.QueryString}");
});

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
