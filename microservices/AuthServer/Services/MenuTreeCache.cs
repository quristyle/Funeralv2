using AuthServer.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace AuthServer.Services;

/// <summary>
/// 사이드바 메뉴 트리를 들고 있는다.
///
/// [왜 캐시해도 되나 — 트리가 사용자마다 다르지 않다]
///
/// <c>/menu/all</c> 은 <b>활성 메뉴 전부</b>를 내려보낸다. 사용자별로 거르는
/// 일은 프론트가 권한표로 한다(<c>MenuFilter</c>). 그래서 같은 언어를 쓰는
/// 사람은 전원이 <b>글자 하나 다르지 않은 같은 응답</b>을 받는다 —
/// 열쇠가 <c>locale</c> 하나면 충분한 이유다.
///
/// <b>권한을 여기서 거르게 되면 이 캐시는 그 즉시 틀린다.</b> 그때는 열쇠에
/// 역할을 넣거나 캐시를 걷어내야 한다. 거르는 자리를 옮길 사람이 이 문단을
/// 보라고 여기 적어 둔다.
///
/// [무엇을 아끼나]
///
/// 요청마다 DB 왕복 두 번(메뉴 179행 + 다국어 사전)과 트리 만들기가 돌고
/// 있었다. 포털은 <b>업무 모듈을 넘나들 때마다</b> 메뉴를 다시 읽으므로
/// (Piral 모듈 컨테이너가 갈리면서 레이아웃이 새로 생긴다) 그 횟수가
/// 화면 전환 횟수만큼이다.
///
/// [한 프로세스 안에서만 유효하다]
///
/// AuthServer 를 여러 벌 띄우면 각자 자기 캐시를 든다. 메뉴를 고친 요청이
/// 간 인스턴스만 즉시 반영되고 나머지는 <see cref="Ttl"/> 만큼 늦는다.
/// 지금은 한 벌이라 문제가 없고, 늘릴 때는 이 문단이 확인할 자리다.
/// </summary>
public sealed class MenuTreeCache(IMemoryCache cache, ILogger<MenuTreeCache> logger)
{
    /// <summary>
    /// 안전망. 메뉴를 바꾸면 <see cref="Invalidate"/> 가 즉시 지우므로 평소에는
    /// 이 시간까지 가지 않는다. 무효화를 부르지 않는 경로(예: DB 를 직접 고친
    /// 경우)가 있어도 이만큼만 늦게 반영되도록 두는 값이다.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 캐시 열쇠 전부를 한 번에 끊는 줄. <see cref="IMemoryCache"/> 에는
    /// "이 접두사로 시작하는 것 모두 지우기" 가 없어서, 언어별 항목을 이
    /// 토큰에 함께 묶어 두고 토큰을 끊는다.
    /// </summary>
    private CancellationTokenSource _reset = new();

    /// <summary>
    /// 트리를 돌려준다. 없으면 <paramref name="load"/> 로 만들어 담는다.
    ///
    /// <b>돌려준 트리를 고치면 안 된다.</b> 다음 요청이 같은 객체를 받는다.
    /// 지금 부르는 곳은 그대로 직렬화만 하므로 안전하다.
    /// </summary>
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

    /// <summary>
    /// 담아 둔 트리를 전부 버린다. <b>메뉴나 다국어를 고친 뒤에 부른다.</b>
    ///
    /// 언어를 가리지 않고 통째로 버리는 이유는, 어느 언어가 영향을 받는지
    /// 정확히 아는 것보다 다시 읽는 편이 싸기 때문이다(왕복 두 번).
    /// 잘못 남기면 <i>메뉴를 고쳤는데 안 바뀐다</i>로 나타나고, 그건 원인을
    /// 짐작하기 어려운 종류의 고장이다.
    /// </summary>
    public void Invalidate()
    {
        var old = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();

        logger.LogInformation("메뉴 트리 캐시를 버렸다.");
    }
}
