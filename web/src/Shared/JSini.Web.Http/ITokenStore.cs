namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이로 나갈 때 쓰는 토큰을 내놓는 곳. 인스턴스가 곧 사용자라고 믿으면 안 된다. AuthTokenHandler 가 받는 것은 모든 사용자가 쓰는 공용 한 벌이라, 누구의 토큰인지는 지금 요청의 인증 쿠키로 갈라야 한다.
/// BFF 라 토큰은 브라우저로 내려가지 않는다. 브라우저에는 인증 쿠키만 있고 게이트웨이용 JWT 는 서버가 든다. XSS 로 토큰이 새는 경로가 없다.
/// 비동기인 까닭은 인증 쿠키를 풀려면 AuthenticationStateProvider 를 물어야 하고, 그것이 비동기라서다. 동기로 두면 구현이 .Result 로 기다리게 되어 회로에서 교착된다.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// 이 회로의 사용자를 알려 준다. <b>레이아웃이 가장 먼저 부른다.</b>
    ///
    /// <summary>이 회로의 사용자를 알려 준다. 레이아웃이 가장 먼저 부른다. 토큰은 인증 쿠키의 클레임에 있고, 그것을 꺼내려면 AuthenticationStateProvider 를 물어야 하는데 그것은 Razor 컴포넌트의 DI 스코프 안에서만 부를 수 있다(핸들러에서 부르면 예외). 그래서 컴포넌트인 레이아웃이 대신 물어 여기에 넘긴다. 넘기기 전에는 IHttpContextAccessor 로 본다(정적 SSR).</summary>
    /// </summary>
    void Initialize(System.Security.Claims.ClaimsPrincipal user);

    /// <summary>게이트웨이로 보낼 access token. 로그인 전에는 <c>null</c>.</summary>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>AuthServer 가 심어 준 리프레시 쿠키(<c>이름=값</c>). 브라우저가 아니라 셸 서버가 갱신할 때 직접 싣는다.</summary>
    ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default);

    /// <summary>갱신으로 access token 만 바뀌었을 때. 리프레시 쿠키는 그대로 둔다.</summary>
    void UpdateAccessToken(string accessToken);

    /// <summary>갱신까지 실패했을 때. 들고 있던 것을 버린다.</summary>
    void Clear();
}
