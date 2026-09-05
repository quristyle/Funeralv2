using System.Security.Claims;
using JSini.Web.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 한 사용자(회로)의 게이트웨이 토큰을 내놓는 곳.
///
/// [토큰을 인증 쿠키 안에 넣어 두는 이유]
///
/// 셸이 로그인 쿠키를 굽고, 업무 앱들이 그 쿠키에서 토큰을 꺼내 쓴다. 앱이
/// 각자 프로세스라 메모리로는 이어지지 않는데, 쿠키는 브라우저가 모두에게
/// 실어 보내므로 이어진다. Data Protection 키 링만 공유되면 어느 앱이든 푼다.
/// 별도 세션 저장소가 필요 없고, 서버를 다시 띄워도 로그인이 유지된다.
///
/// [사용자를 스스로 알아내지 않는다]
///
/// <c>AuthenticationStateProvider</c> 는 Razor 컴포넌트의 DI 스코프 안에서만
/// 부를 수 있다. 이 클래스는 HTTP 메시지 핸들러에서도 불리므로 거기서 물으면
/// 예외로 죽는다. 그래서 레이아웃이 대신 물어 <see cref="Initialize"/> 로 넘겨 준다.
///
/// [갱신한 토큰은 쿠키에 다시 쓰지 않는다]
///
/// 회로 안에서는 HTTP 응답이 없어서 쿠키를 다시 구울 수 없다. 갱신한 토큰은
/// 이 회로의 메모리에만 남는다. 새로고침하면 쿠키의 옛 토큰으로 시작해 401 을
/// 한 번 받고 다시 갱신한다 — 왕복 한 번이 더 들지만 리프레시 쿠키의 수명이
/// 훨씬 길어서 사용자에게는 보이지 않는다.
/// </summary>
public sealed class TokenStore(IHttpContextAccessor httpContextAccessor) : ITokenStore
{
    /// <summary>인증 쿠키 안에서 access token 이 들어 있는 클레임 이름.</summary>
    public const string AccessTokenClaim = "jsini:access_token";

    /// <summary>인증 쿠키 안에서 리프레시 쿠키가 들어 있는 클레임 이름.</summary>
    public const string RefreshCookieClaim = "jsini:refresh_cookie";

    /// <summary>레이아웃이 넘겨 준 사용자. 회로에서는 이쪽이 유일한 출처다.</summary>
    private ClaimsPrincipal? _user;

    /// <summary>갱신으로 새로 받은 토큰. 쿠키의 것보다 이쪽이 우선한다.</summary>
    private string? _refreshed;

    private bool _cleared;

    /// <summary>
    /// 회로가 붙기 전(정적 SSR·로그인 직후 리다이렉트)에는 HttpContext 가 유효하다.
    /// 붙은 뒤에는 <c>null</c> 이므로 <see cref="Initialize"/> 로 받은 것을 쓴다.
    /// </summary>
    private ClaimsPrincipal? User => _user ?? httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// 사용자를 넘겨받는다. <b>버림 표시도 함께 되돌린다.</b>
    ///
    /// 새 사용자를 받았다는 것은 새 토큰이 왔다는 뜻이다. 되돌리지 않으면
    /// 앞선 401 한 번 때문에 세워진 표시가 남아 계속 토큰 없이 나간다.
    /// </summary>
    public void Initialize(ClaimsPrincipal user)
    {
        _user = user;
        _cleared = false;
    }

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cleared)
        {
            return ValueTask.FromResult<string?>(null);
        }

        // 넘겨받은 사용자에게 토큰 클레임이 없으면 HttpContext 쪽을 다시 본다.
        //
        // 레이아웃이 사용자를 넘기기 전에 화면이 먼저 게이트웨이를 부르는
        // 순간이 있다. 그때 한쪽만 보면 토큰이 잠깐 비어 401 을 한 번 맞고,
        // 그 401 이 갱신 실패 → 토큰 버림으로 번졌다.
        return ValueTask.FromResult(
            _refreshed
            ?? _user?.FindFirstValue(AccessTokenClaim)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(AccessTokenClaim));
    }

    public ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default)
    {
        if (_cleared)
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult(
            _user?.FindFirstValue(RefreshCookieClaim)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(RefreshCookieClaim));
    }

    public void UpdateAccessToken(string accessToken)
    {
        _refreshed = accessToken;
        _cleared = false;
    }

    public void Clear()
    {
        _refreshed = null;
        _cleared = true;
    }
}
