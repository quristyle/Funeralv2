namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이로 나갈 때 쓰는 토큰을 내놓는 곳. <b>사용자(회로)마다 하나다.</b>
///
/// [토큰이 브라우저로 내려가지 않는다 — BFF]
///
/// Vue 때는 accessToken 이 브라우저 메모리에 있었고 화면이 직접
/// <c>Authorization</c> 헤더를 붙였다(<c>api/request.ts</c>). Blazor Server 에서는
/// HTTP 를 부르는 주체가 브라우저가 아니라 <b>셸 서버</b>다. 그러니 토큰을
/// 브라우저로 내려보낼 이유가 없다 — 브라우저에는 셸의 인증 쿠키만 있고,
/// 게이트웨이용 JWT 는 서버가 들고 있는다. XSS 로 토큰이 새는 경로가 사라진다.
///
/// [왜 비동기인가]
///
/// 토큰을 어디서 꺼내는지가 상황마다 다르다. 로그인 요청 중에는 방금 받은 값을
/// 손에 들고 있지만, 회로에서는 인증 쿠키를 풀어 봐야 알 수 있고 그 일이
/// 비동기다(<c>AuthenticationStateProvider</c>). 계약을 동기로 두면 구현이
/// <c>.Result</c> 로 기다리게 되고, 그건 Blazor 회로에서 교착으로 이어진다.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// 이 회로의 사용자를 알려 준다. <b>레이아웃이 가장 먼저 부른다.</b>
    ///
    /// [왜 스스로 알아내지 않나]
    ///
    /// 토큰은 인증 쿠키의 클레임에 있고, 그것을 꺼내려면
    /// <c>AuthenticationStateProvider</c> 를 물어야 한다. 그런데 그건
    /// <b>Razor 컴포넌트의 DI 스코프 안에서만</b> 부를 수 있다 — HTTP 메시지
    /// 핸들러에서 부르면 예외로 죽는다("Do not call GetAuthenticationStateAsync
    /// outside of the DI scope for a Razor component").
    ///
    /// 그래서 컴포넌트인 레이아웃이 대신 물어보고 여기에 넘겨 준다.
    /// 넘기기 전에는 <c>IHttpContextAccessor</c> 로 대신 본다 —
    /// 회로가 붙기 전(정적 SSR)에는 그쪽이 유효하다.
    /// </summary>
    void Initialize(System.Security.Claims.ClaimsPrincipal user);

    /// <summary>게이트웨이로 보낼 access token. 로그인 전에는 <c>null</c>.</summary>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// AuthServer 가 심어 준 리프레시 쿠키 (<c>이름=값</c> 형태).
    ///
    /// 예전에는 브라우저가 이 쿠키를 보관하고 <c>withCredentials</c> 로 자동으로
    /// 실어 보냈다. 이제 셸 서버가 그 자리를 대신하므로 갱신할 때 직접 싣는다.
    /// </summary>
    ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default);

    /// <summary>갱신으로 access token 만 바뀌었을 때. 리프레시 쿠키는 그대로 둔다.</summary>
    void UpdateAccessToken(string accessToken);

    /// <summary>갱신까지 실패했을 때. 들고 있던 것을 버린다.</summary>
    void Clear();
}
