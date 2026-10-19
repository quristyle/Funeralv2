using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace JSini.Web.Components.Layout;

/// <summary>부트스트랩 응답을 사용자별로 잠깐 들고 있는다. 레이아웃은 업무를 넘나들 때마다 새로 만들어져 그 안의 scoped 서비스도 새로 생기므로, 부트스트랩 하나가 화면 전환마다 나는 것을 이 통이 없앤다 — 열쇠가 맞으면 게이트웨이를 아예 안 부른다. 싱글턴이어야 하는 것은 scoped 면 모듈 컨테이너가 갈릴 때 함께 사라져 막지 못하기 때문이다.
/// 사용자 자료를 담으므로 열쇠를 사람마다 가른다 — 이름만으로는 역할이 바뀌어 다시 로그인한 사람이 옛 권한표를 받는다. 접근 토큰의 지문을 섞으면 다시 로그인할 때 열쇠가 달라진다(토큰 자체는 담지 않는다). 열쇠가 바뀌지 않는 것은 틀린 쪽이라 Ttl(2분)이 지나면 버린다 — 실제 통제는 서버가 하고, 여기서 늦는 것은 메뉴가 보이느냐까지다.</summary>
public sealed class PortalBootstrapStore(IMemoryCache cache, ILogger<PortalBootstrapStore> logger)
{
    /// <summary>들고 있는 시간. 화면 전환을 몇 번 하든 이 안에서는 한 번만 읽는다. 짧게 잡은 것은 권한 변경이 늦게 반영되는 쪽이 더 나쁘기 때문이다.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public bool TryGet(string key, out PortalBootstrap.PortalBootstrapWire? data) =>
        cache.TryGetValue(Key(key), out data) && data is not null;

    public void Put(string key, PortalBootstrap.PortalBootstrapWire data) =>
        cache.Set(Key(key), data, Ttl);

    /// <summary>이 사람 것을 지운다. 즐겨찾기를 담거나 뺀 뒤처럼 스스로 바꾼 것을 아는 자리에서 부른다 — 안 지우면 모듈을 옮겼을 때 옛 목록이 돌아온다.</summary>
    public void Forget(string key)
    {
        cache.Remove(Key(key));
        logger.LogDebug("부트스트랩 통을 비웠다.");
    }

    private static string Key(string key) => $"portal-bootstrap:{key}";

    /// <summary>이 사람의 열쇠를 만든다. 이름과 접근 토큰 지문을 섞는다. 토큰이 없으면 이름만으로 만든다 — 그 경우에도 사람이 섞이지는 않는다.</summary>
    public static string KeyFor(ClaimsPrincipal user)
    {
        var name = user.Identity?.Name ?? "?";
        var token = user.FindFirst(Security.TokenStore.AccessTokenClaim)?.Value;

        if (string.IsNullOrEmpty(token))
        {
            return name;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"{name}:{Convert.ToHexString(digest.AsSpan(0, 8))}";
    }
}
