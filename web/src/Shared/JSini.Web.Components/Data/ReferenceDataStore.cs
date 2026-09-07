using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace JSini.Web.Components.Data;

/// <summary>
/// 거의 안 바뀌는 참조자료를 <b>회로 바깥에서</b> 들고 있는다.
///
/// [무엇을 막는 것인가]
///
/// 공통코드 · 회사 목록 · 범용 셀렉트 설정처럼 <b>모두에게 같고 몇 달에 한 번
/// 바뀌는 자료</b>의 캐시가 전부 모듈 안의 <c>scoped</c> 서비스에 있었다.
/// scoped 는 회로 하나(=사용자 창 하나)라 두 가지가 겹쳤다.
///
/// <list type="number">
///   <item>접속자가 백 명이면 같은 표를 <b>백 벌</b> 읽는다.</item>
///   <item><b>업무를 넘나들면 그 캐시가 통째로 사라진다</b> — Piral 모듈
///         컨테이너가 갈리면서 안의 scoped 서비스도 함께 새로 생긴다.
///         장례식장 → 헬프데스크 → 장례식장 이면 세 번 읽었다.</item>
/// </list>
///
/// 2번이 이 통을 만든 이유다. <see cref="Layout.PortalBootstrapStore"/> 가 부트스트랩
/// 응답에 대해 이미 같은 일을 하고 있고, 여기는 그 틀을 참조자료로 넓힌 것이다.
///
/// [싱글턴이어야 하는 이유]
///
/// scoped 로 두면 <b>이 문제를 만드는 바로 그 수명</b>을 그대로 물려받아
/// 아무것도 막지 못한다.
///
/// [사람을 섞지 않는 것이 이 클래스의 유일한 위험이다]
///
/// 싱글턴 통에 사용자 자료가 들어가면 <b>남의 목록이 보인다.</b> 그래서 통은
/// 열쇠를 만들지 않고 <b>받는다</b> — 무엇을 나눠 써도 되는지는 그 자료를
/// 읽는 사람만 알 수 있기 때문이다. 판단은 <see cref="ReferenceData"/> 를
/// 부르는 자리에서 하고, 근거를 거기 주석으로 남긴다.
///
/// [고친 것이 모두에게 반영된다 — 이게 오히려 나아진 점이다]
///
/// 옛 방식에서는 코드를 고친 사람의 화면에서만 캐시가 비었고 다른 사용자는
/// 옛 값을 계속 봤다(그 걱정이 scoped 를 고른 이유로 적혀 있었다). 통이
/// 하나면 <see cref="Invalidate"/> 한 번으로 <b>모두가</b> 새 값을 본다.
/// </summary>
public sealed class ReferenceDataStore(IMemoryCache cache, ILogger<ReferenceDataStore> logger)
{
    /// <summary>
    /// 들고 있는 시간.
    ///
    /// <para>
    /// 부트스트랩 통(2분)보다 길게 잡았다. 그쪽은 <b>권한표</b>를 담아서 늦게
    /// 반영되는 방향이 「권한이 없는데 메뉴가 보인다」 쪽인데, 여기 담기는
    /// 것은 고르개를 채우는 목록이라 늦어도 위험하지 않다. 그리고 실제로
    /// 고쳤을 때는 TTL 을 기다리지 않는다 — <see cref="Invalidate"/> 가 있다.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 묶음마다의 세대. <see cref="Invalidate"/> 가 올린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>IMemoryCache</c> 는 「접두사로 지우기」를 못 한다.</b> 담은 열쇠를
    /// 따로 들고 있다가 훑어 지우는 방법도 있지만, 그 목록 자체가 또 다른
    /// 동시성 문제가 된다(지우는 중에 담기는 것).
    /// </para>
    ///
    /// <para>
    /// 세대를 열쇠에 섞으면 <b>지우는 일이 숫자 하나 올리는 것</b>으로 끝난다.
    /// 옛 세대의 값은 아무도 찾지 않게 되고 TTL 이 지나면 저절로 사라진다.
    /// </para>
    /// </remarks>
    private readonly ConcurrentDictionary<string, int> _generations = new(StringComparer.Ordinal);

    /// <summary>
    /// 통에 있으면 그것을, 없으면 <paramref name="load"/> 로 읽어 담는다.
    /// </summary>
    /// <param name="group">
    /// 묶음 이름. <see cref="Invalidate"/> 의 단위이고 모듈 사이에 겹치지 않게
    /// 짓는다(<c>projmng.codes</c> · <c>funeral.codes</c>).
    /// </param>
    /// <param name="key">묶음 안에서의 열쇠. 사람마다 갈라야 하면 여기에 섞는다.</param>
    /// <param name="load">
    /// 실제로 읽는 일. <b><c>null</c> 을 돌려주면 담지 않는다</b> — 실패를
    /// 그렇게 알린다. 담아 버리면 서버가 돌아와도 TTL 동안 빈 고르개가 남는다.
    /// </param>
    public async Task<T?> GetOrAddAsync<T>(string group, string key, Func<Task<T?>> load)
        where T : class
    {
        var full = Key(group, key, typeof(T));

        if (cache.TryGetValue(full, out T? cached) && cached is not null)
        {
            return cached;
        }

        // **여기서 겹쳐 읽는 것을 막지 않는다.** 두 회로가 같은 순간에 물으면
        // 둘 다 읽는다. GET 이고 결과가 같으므로 틀리지 않고, 막으려면 묶음마다
        // 자물쇠를 둬야 하는데 그 자물쇠가 회로를 붙잡는 쪽이 더 나쁘다.
        var loaded = await load();

        if (loaded is null)
        {
            return null;
        }

        cache.Set(full, loaded, Ttl);
        return loaded;
    }

    /// <summary>
    /// 이 묶음을 버린다. <b>그 자료를 고치는 화면이 저장 뒤에 부른다.</b>
    /// </summary>
    /// <remarks>
    /// 안 부르면 방금 고친 값이 다른 화면의 고르개에 반영되지 않는다. 사용자는
    /// 저장이 안 된 줄 알고 같은 일을 반복한다.
    /// </remarks>
    public void Invalidate(string group)
    {
        var next = _generations.AddOrUpdate(group, 1, (_, current) => current + 1);
        logger.LogDebug("참조자료 묶음 {Group} 을 버렸다 (세대 {Generation}).", group, next);
    }

    /// <summary>
    /// 열쇠를 만든다. <b>담기는 형을 반드시 섞는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// [형을 안 섞으면 두 모듈이 서로를 덮어쓴다 — 조용히]
    /// </para>
    ///
    /// <para>
    /// 같은 엔드포인트를 두 모듈이 읽으면서 <b>각자 자기 DTO</b>로 받는 일이
    /// 있다(범용 셀렉트 설정이 그렇다 — 프로젝트관리와 헬프데스크의
    /// <c>BizSelectConfig</c> 는 이름만 같고 다른 타입이다). 모듈은 서로를
    /// 참조하지 못하니(규칙 2) 그것이 정상이다.
    /// </para>
    ///
    /// <para>
    /// 그때 열쇠가 같으면 <c>IMemoryCache.TryGetValue&lt;T&gt;</c> 가 형이 안
    /// 맞아 <c>false</c> 를 주고, 부르는 쪽은 「없다」고 읽어 다시 담는다.
    /// 결과는 <b>두 모듈이 번갈아 서로의 값을 밀어내는 것</b>이고 —
    /// 오류가 나지 않는다. 캐시를 안 둔 것보다 나쁘다(왕복은 그대로인데
    /// 통이 메모리만 쓴다).
    /// </para>
    ///
    /// <para>
    /// 형을 섞으면 값은 형마다 따로 담기고, <b>묶음(세대)은 그대로 공유</b>된다 —
    /// 한쪽에서 버리면 양쪽이 함께 버려진다. 그것이 맞는 동작이다.
    /// </para>
    /// </remarks>
    private string Key(string group, string key, Type type)
    {
        var generation = _generations.TryGetValue(group, out var value) ? value : 0;
        return $"refdata:{group}:{generation}:{type.FullName}:{key}";
    }
}
