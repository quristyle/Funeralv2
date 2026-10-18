using System.Security.Claims;
using JSini.Web.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 한 사용자(회로)의 게이트웨이 토큰을 내놓는 곳.
///
/// [토큰을 인증 쿠키 안에 넣어 두는 이유]
///
/// 셸이 로그인 쿠키를 굽고, 업무 화면이 그 쿠키에서 토큰을 꺼내 쓴다. 앱이
/// 각자 프로세스이던 시절에는 메모리로 이어지지 않아 쿠키 말고는 길이 없었다.
/// 단일 셸이 된 지금도 그대로 두는 것은 <b>서버를 다시 띄워도 로그인이 유지되기</b>
/// 때문이다 — 토큰을 프로세스 메모리에만 두면 재기동마다 전원 로그아웃이고,
/// 그러지 않으려면 별도 세션 저장소를 하나 세워야 한다. 키 링만 폴더에 있으면
/// 다시 띄운 프로세스가 옛 쿠키를 그대로 푼다.
///
/// [사용자를 스스로 알아내지 않는다]
///
/// <c>AuthenticationStateProvider</c> 는 Razor 컴포넌트의 DI 스코프 안에서만
/// 부를 수 있다. 이 클래스는 HTTP 메시지 핸들러에서도 불리므로 거기서 물으면
/// 예외로 죽는다. 그래서 레이아웃이 대신 물어 <see cref="Initialize"/> 로 넘겨 준다.
///
/// [갱신한 토큰은 쿠키에 다시 쓰지 않는다 — 이 인스턴스에도 담지 않는다]
///
/// 회로 안에서는 HTTP 응답이 없어서 쿠키를 다시 구울 수 없다. 갱신한 토큰과
/// 「세션 거절」 표시는 <see cref="RefreshedTokens"/> 에 <b>쿠키의 토큰을 열쇠로</b>
/// 담는다. 이 인스턴스의 칸에 두면 안 된다 — <c>AuthTokenHandler</c> 가 받는
/// 인스턴스는 <c>HttpClientFactory</c> 의 스코프에서 나온 <b>모든 사용자 공용</b>
/// 한 벌이라, 한 사람이 갱신한 토큰으로 남의 요청이 나간다(그 머리말 참고).
/// 열쇠가 쿠키의 토큰이므로 새로고침해도 갱신한 토큰을 이어 쓴다.
/// </summary>
public sealed class TokenStore(
    IHttpContextAccessor httpContextAccessor,
    RefreshedTokens refreshedTokens) : ITokenStore
{
    /// <summary>인증 쿠키 안에서 access token 이 들어 있는 클레임 이름.</summary>
    public const string AccessTokenClaim = "jsini:access_token";

    /// <summary>인증 쿠키 안에서 리프레시 쿠키가 들어 있는 클레임 이름.</summary>
    public const string RefreshCookieClaim = "jsini:refresh_cookie";

    /// <summary>레이아웃이 넘겨 준 사용자. 회로에서는 이쪽이 우선이다.</summary>
    private ClaimsPrincipal? _user;

    public void Initialize(ClaimsPrincipal user) => _user = user;

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var original = Claim(AccessTokenClaim);
        if (original is null)
        {
            return ValueTask.FromResult<string?>(null);
        }

        if (refreshedTokens.TryGet(original, out var refreshed, out var rejected))
        {
            return ValueTask.FromResult(rejected ? null : refreshed);
        }

        return ValueTask.FromResult<string?>(original);
    }

    public ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default)
    {
        if (Claim(AccessTokenClaim) is { } original
            && refreshedTokens.TryGet(original, out _, out var rejected)
            && rejected)
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult(Claim(RefreshCookieClaim));
    }

    public void UpdateAccessToken(string accessToken)
    {
        if (Claim(AccessTokenClaim) is { } original)
        {
            refreshedTokens.SetRefreshed(original, accessToken);
        }
    }

    public void Clear()
    {
        if (Claim(AccessTokenClaim) is { } original)
        {
            refreshedTokens.Reject(original);
        }
    }

    /// <summary>
    /// 넘겨받은 사용자에게 그 클레임이 없으면 HttpContext 쪽을 다시 본다.
    ///
    /// 레이아웃이 사용자를 넘기기 전에 화면이 먼저 게이트웨이를 부르는
    /// 순간이 있다. 그때 한쪽만 보면 토큰이 잠깐 비어 401 을 한 번 맞고,
    /// 그 401 이 갱신 실패 → 토큰 버림으로 번졌다.
    /// </summary>
    private string? Claim(string type) =>
        _user?.FindFirstValue(type)
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue(type);
}
