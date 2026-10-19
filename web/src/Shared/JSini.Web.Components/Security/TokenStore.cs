using System.Security.Claims;
using JSini.Web.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 한 사용자(회로)의 게이트웨이 토큰을 내놓는 곳. 토큰은 인증 쿠키 안에 있어, 서버를 다시 띄워도 로그인이 유지된다(키 링만 폴더에 있으면 옛 쿠키를 그대로 푼다).
/// 사용자는 스스로 알아내지 않는다. AuthenticationStateProvider 는 핸들러에서 부를 수 없어 레이아웃이 Initialize 로 넘겨 준다.
/// 갱신한 토큰은 이 인스턴스에 담지 않는다. 핸들러가 받는 인스턴스는 모든 사용자가 쓰는 공용 한 벌이라 남의 요청에 실리기 때문이다. 갱신분은 RefreshedTokens 에 쿠키 토큰을 열쇠로 담는다.
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

    /// <summary>넘겨받은 사용자에게 그 클레임이 없으면 HttpContext 쪽을 다시 본다. 레이아웃이 사용자를 넘기기 전에 게이트웨이를 부르면 토큰이 잠깐 비어 401 이 나고, 갱신 실패로 번지기 때문이다.</summary>
    private string? Claim(string type) =>
        _user?.FindFirstValue(type)
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue(type);
}
