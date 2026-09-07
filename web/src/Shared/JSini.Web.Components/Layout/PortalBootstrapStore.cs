using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 부트스트랩 응답을 <b>사용자별로</b> 잠깐 들고 있는다.
///
/// [무엇을 막는 것인가]
///
/// 레이아웃은 <b>업무 모듈을 넘나들 때마다 새로 만들어진다</b> — Piral 의
/// 모듈 컨테이너가 갈리면서 그 안의 scoped 서비스도 함께 새로 생긴다. 그래서
/// 장례식장에서 헬프데스크로 옮기기만 해도 권한표와 메뉴를 처음부터 다시
/// 읽었다. 부트스트랩으로 왕복을 넷에서 하나로 줄였지만 <b>그 하나가 화면
/// 전환마다 나는 것</b>은 그대로다.
///
/// 이 통은 그 하나마저 없앤다. 열쇠가 맞으면 게이트웨이를 아예 안 부른다.
///
/// [싱글턴이어야 하는 이유]
///
/// scoped 로 두면 모듈 컨테이너가 갈릴 때 이 통도 함께 사라져서 아무것도
/// 막지 못한다. <b>이 문제를 만드는 바로 그 수명</b>을 피해야 하므로 싱글턴이다.
/// 대신 사용자 자료를 담게 되므로 열쇠를 사람마다 갈라야 한다 — 아래 참고.
///
/// [열쇠에 토큰을 섞는다]
///
/// 사용자 이름만으로는 <b>다시 로그인해도 옛 값을 본다</b>. 역할이 바뀌어
/// 다시 로그인한 사람이 옛 권한표를 그대로 받는 것은 <i>권한이 없는데 보이는</i>
/// 쪽으로 틀리는 길이다. 접근 토큰의 지문을 섞으면 로그인이 새로 되는 순간
/// 열쇠가 달라져 저절로 갈린다. 토큰 자체는 담지 않는다(지문만 쓴다).
///
/// [그래도 오래 들고 있지 않는다]
///
/// <see cref="Ttl"/> 이 지나면 버린다. 권한을 관리자가 고쳤을 때 그 사람이
/// 다시 로그인하지 않아도 이만큼 뒤에는 반영된다. 실제 통제는 서버가 계속
/// 하므로 이 사이에 열리는 화면도 자료를 못 받는다 — 여기서 늦는 것은
/// <b>메뉴가 보이느냐</b>까지다.
/// </summary>
public sealed class PortalBootstrapStore(IMemoryCache cache, ILogger<PortalBootstrapStore> logger)
{
    /// <summary>
    /// 들고 있는 시간. 화면 전환을 몇 번 하든 이 안에서는 한 번만 읽는다.
    ///
    /// 짧게 잡은 것은 권한 변경이 늦게 반영되는 쪽이 더 나쁘기 때문이다.
    /// 모듈을 넘나드는 일은 몇 초 간격이라 2분이면 사실상 전부 걸린다.
    /// </summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public bool TryGet(string key, out PortalBootstrap.PortalBootstrapWire? data) =>
        cache.TryGetValue(Key(key), out data) && data is not null;

    public void Put(string key, PortalBootstrap.PortalBootstrapWire data) =>
        cache.Set(Key(key), data, Ttl);

    /// <summary>
    /// 이 사람 것을 지운다. 즐겨찾기를 담거나 뺀 뒤처럼 <b>스스로 바꾼 것을
    /// 아는 자리</b>에서 부른다 — 안 지우면 모듈을 옮겼을 때 옛 목록이 돌아온다.
    /// </summary>
    public void Forget(string key)
    {
        cache.Remove(Key(key));
        logger.LogDebug("부트스트랩 통을 비웠다.");
    }

    private static string Key(string key) => $"portal-bootstrap:{key}";

    /// <summary>
    /// 이 사람의 열쇠를 만든다. 이름 + 접근 토큰 지문.
    ///
    /// 토큰이 없으면(있을 수 없지만) 이름만으로 만든다. 그 경우에도 사람이
    /// 섞이지는 않는다 — 다시 로그인했을 때 갈리지 않을 뿐이다.
    /// </summary>
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
