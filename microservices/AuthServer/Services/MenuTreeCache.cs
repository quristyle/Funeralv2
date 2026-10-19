using AuthServer.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace AuthServer.Services;

/// <summary>사이드바 메뉴 트리를 들고 있는다. 트리는 사용자마다 같아서 언어 열쇠 하나로 캐시한다.</summary>
/// 권한으로 거르게 되면 이 캐시는 즉시 틀리므로 거르는 자리를 옮기면 캐시를 걷어내야 한다. 한 프로세스 안에서만 유효하다.
public sealed class MenuTreeCache(IMemoryCache cache, ILogger<MenuTreeCache> logger)
{
    /// <summary>안전망. 메뉴를 바꾸면 Invalidate 가 즉시 지우므로 평소에는 이 시간까지 가지 않는다.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>캐시 열쇠 전부를 한 번에 끊는다. IMemoryCache 에는 접두사 삭제가 없어 토큰으로 묶어 끊는다.</summary>
    private CancellationTokenSource _reset = new();

    /// <summary>트리를 돌려준다. 없으면 load 로 만들어 담는다.</summary>
    /// 돌려준 트리는 고치면 안 된다 — 다음 요청이 같은 객체를 받는다.
    public async Task<List<MenuDto>> GetOrLoadAsync(string? locale, Func<Task<List<MenuDto>>> load)
    {
        var key = $"menu-tree:{locale ?? "ko"}";

        if (cache.TryGetValue(key, out List<MenuDto>? cached) && cached is not null)
        {
            return cached;
        }

        var tree = await load();

        cache.Set(key, tree, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
        }.AddExpirationToken(new CancellationChangeToken(_reset.Token)));

        return tree;
    }

    /// <summary>담아 둔 트리를 전부 버린다. 메뉴나 다국어를 고친 뒤에 부른다.</summary>
    /// 언어를 가리지 않고 버리는 이유는 다시 읽는 편이 싸기 때문이다. 잘못 남기면 「고쳤는데 안 바뀐다」로 나타난다.
    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();

        logger.LogInformation("메뉴 트리 캐시를 버렸다.");
    }
}
