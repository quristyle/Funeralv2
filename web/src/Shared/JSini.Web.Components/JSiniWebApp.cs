using System.Reflection;
using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Security;
using JSini.Web.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
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
        services.AddRazorComponents().AddInteractiveServerComponents();

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
        services.AddScoped<IPermissionContext, PermissionContext>();
        services.AddScoped<MenuProvider>();
        services.AddScoped<IMenuProvider>(sp => sp.GetRequiredService<MenuProvider>());

        // ── 셸 상태 ──────────────────────────────────────────────
        //
        // 셋 다 scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다.
        // 싱글턴으로 두면 열어 둔 탭과 즐겨찾기가 모든 사용자에게 공유된다.
        services.AddScoped<MenuFavorites>();
        services.AddScoped<PortalTabs>();

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

        return app;
    }
}
