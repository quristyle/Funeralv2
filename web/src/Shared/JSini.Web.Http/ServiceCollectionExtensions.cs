using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이 클라이언트를 DI 에 올린다. 셸의 <c>Program.cs</c> 가 한 번 부른다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// <b>토큰을 붙이지 않는</b> 게이트웨이 클라이언트의 이름.
    ///
    /// <para>
    /// 로그인 전에도 부를 수 있는 몇 안 되는 경로가 쓴다(공개 공지). Vue 의
    /// <c>baseRequestClient</c> 자리다 — 그쪽도 인터셉터를 떼어 둔 이유가
    /// 같았다. 토큰을 붙이는 클라이언트로 부르면 만료된 토큰 하나 때문에
    /// 401 → 갱신 → 로그인 화면에서 다시 로그인으로 튕긴다.
    /// </para>
    /// </summary>
    public const string AnonymousClientName = "JSini.Gateway.Anonymous";

    /// <summary>
    /// <see cref="GatewayClient"/> 와 토큰 처리를 등록한다.
    ///
    /// [클라이언트를 하나만 두는 이유]
    ///
    /// Vue 에는 서비스마다 클라이언트가 있었지만(<c>requestClient</c> ·
    /// <c>baseRequestClient</c> · 헬프데스크 전용 · 프로젝트관리 전용), 실제로 다른
    /// 것은 <b>경로 접두사뿐</b>이었다. 게이트웨이가 <c>/api/{서비스}/…</c> 로 갈라
    /// 주므로 클라이언트를 나눌 이유가 없다. 모듈이 자기 경로만 부르는지는
    /// 아키텍처 테스트가 본다.
    ///
    /// [수명]
    ///
    /// <see cref="ITokenStore"/> 는 <b>scoped</b> 여야 한다 — Blazor Server 에서
    /// scoped 는 회로(사용자) 하나에 대응한다. 싱글턴으로 두면 모든 사용자가 토큰
    /// 하나를 공유해서, 먼저 로그인한 사람의 권한으로 남의 요청이 나간다.
    /// </summary>
    /// <param name="services">서비스 모음</param>
    /// <param name="configuration">
    /// <c>Gateway:BaseUrl</c> 을 읽는다 (기본 <c>http://localhost:5265/api/</c>).
    /// 운영에서는 컨테이너 이름으로 바꾼다.
    /// </param>
    public static IServiceCollection AddJSiniGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";

        // 끝의 / 가 없으면 HttpClient 가 BaseAddress 의 마지막 칸을 상대 경로로
        // 덮어써서 /api 가 통째로 사라진다. 조용히 404 가 나는 종류의 실수라 막아 둔다.
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        services.AddScoped<AuthTokenHandler>();

        services.AddHttpClient<GatewayClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 파일 내려받기·엑셀 만들기가 오래 걸리는 화면이 있다.
                // 기본 100초는 짧아서 큰 목록에서 끊긴다.
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        // ── 로그인 전에도 부르는 경로 ─────────────────────────
        //
        // 토큰 핸들러를 붙이지 않는다. 이유는 AnonymousClientName 주석에 있다.
        services.AddHttpClient(AnonymousClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar);

        // ── AI 대화 (D11) ────────────────────────────────────
        //
        // 봉투를 벗기는 GatewayClient 로는 `text/event-stream` 을 흘려 읽을 수
        // 없어 클라이언트가 따로다. 여기서 등록하는 이유는 **헤더의 대화창**이
        // 레이아웃(공용)에 있기 때문이다 — 모듈이 등록하면 레이아웃이 못 쓴다.
        //
        // 스트리밍이라 시간 제한을 길게 둔다. 기본 100초면 긴 답의 중간에서
        // 끊기고, 증상은 「답이 하다 말았다」라 원인이 시간 제한으로 안 보인다.
        services.AddHttpClient<AiChatClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        return services;
    }

    /// <summary>
    /// 쿠키 통을 끈다. <b>게이트웨이로 나가는 모든 클라이언트에 붙인다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// [실제로 밟았다 — 익명 요청이 남의 신원으로 나갔다]
    /// </para>
    ///
    /// <para>
    /// <c>HttpClientFactory</c> 는 <b>기본 핸들러를 사용자와 무관하게 돌려 쓴다.</b>
    /// 그 핸들러가 쿠키 통을 들고 있으면(기본값이 그렇다) 어느 한 사람의
    /// 로그인 응답에 실려 온 쿠키가 통에 남아 <b>그 뒤의 모든 요청</b>에
    /// 딸려 나간다 — 다른 사용자의 요청에도, 로그인하지 않은 요청에도.
    /// </para>
    ///
    /// <para>
    /// AuthServer 는 로그인할 때 <c>jsini_file_at</c> 을 심고, 게이트웨이는
    /// 파일 읽기 경로에서 <b>그 쿠키를 신원으로 받는다</b>. 그래서 첨부 중계
    /// (<c>FileDownload</c>)를 붙이자 <b>로그인하지 않은 요청이 비공개 공지의
    /// 첨부를 받아 갔다.</b> 로그인 화면에서 재현했다.
    /// </para>
    ///
    /// <para>
    /// 끄더라도 잃는 것이 없다. 우리가 쿠키를 쓰는 곳은 토큰 갱신 한 군데뿐이고,
    /// 거기서는 <c>ITokenStore</c> 가 사용자별로 들고 있는 값을
    /// <c>Cookie</c> 헤더에 <b>직접</b> 싣는다(<c>AuthTokenHandler</c>).
    /// 셸의 로그인 전용 클라이언트가 같은 이유로 이미 이렇게 하고 있었는데,
    /// 정작 게이트웨이 클라이언트에는 빠져 있었다.
    /// </para>
    ///
    /// <para>
    /// [모듈이 등록하는 클라이언트도 붙여야 해서 <c>public</c> 이다]
    /// </para>
    ///
    /// <para>
    /// 멀티파트 업로드는 <c>GatewayClient</c> 로 못 해서 모듈마다 자기
    /// <c>HttpClient</c> 를 등록한다(<c>NoticeUploadClient</c> ·
    /// <c>ProfileImageClient</c> · 장례식장의 <c>FileUploadClient</c>).
    /// 그것들도 게이트웨이로 나가므로 같은 함정에 걸린다. 여기가
    /// <c>private</c> 이던 동안 그 셋에는 쿠키 통이 켜져 있었다.
    /// </para>
    /// </remarks>
    public static HttpMessageHandler NoCookieJar() => new HttpClientHandler
    {
        UseCookies = false,
    };
}
