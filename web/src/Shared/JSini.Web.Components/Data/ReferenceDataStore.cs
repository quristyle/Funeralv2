using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace JSini.Web.Components.Data;

/// <summary>거의 안 바뀌는 참조자료(공통코드 · 회사 목록 · 범용 셀렉트 설정)를 회로 바깥에서 든다. 모듈 안의 scoped 캐시는 회로마다 같은 표를 백 벌 읽고 업무를 넘나들면 통째로 사라졌다. 싱글턴 통은 모두에게 같은 자료만 담아야 한다 — 사람을 섞으면 남의 목록이 보이므로 열쇠를 만들지 않고 받는다.
/// 고친 것은 Invalidate 한 번으로 모두에게 반영된다. 통은 PortalBootstrapStore 와 같은 틀이다.</summary>
public sealed class ReferenceDataStore(IMemoryCache cache, ILogger<ReferenceDataStore> logger)
{
    /// <summary>들고 있는 시간. 부트스트랩 통(2분)보다 길다 — 여기 담기는 것은 고르개를 채우는 목록이라 늦어도 위험하지 않다. 고쳤을 때는 TTL 을 기다리지 않고 Invalidate 를 쓴다.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>묶음마다의 세대. Invalidate 가 올린다. IMemoryCache 는 접두사로 지우지 못하므로 세대를 열쇠에 섞어 지우는 일을 숫자 하나 올리는 것으로 만든다 — 옛 세대 값은 아무도 찾지 않고 TTL 이 지나면 사라진다.</summary>
    private readonly ConcurrentDictionary<string, int> _generations = new(StringComparer.Ordinal);

    /// <summary>통에 있으면 그것을, 없으면 load 로 읽어 담는다. group 은 Invalidate 의 단위이고 모듈 사이에 겹치지 않게 짓는다. load 가 null 을 돌려주면 담지 않는다 — 담아 버리면 서버가 돌아와도 TTL 동안 빈 고르개가 남는다.</summary>
    /// <param name="group">묶음 이름(projmng.codes · funeral.codes 처럼).</param>
    /// <param name="key">묶음 안의 열쇠. 사람마다 갈라야 하면 여기에 섞는다.</param>
    /// <param name="load">실제로 읽는 일. null 을 돌려주면 실패로 보고 담지 않는다.</param>
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

    /// <summary>이 묶음을 버린다. 그 자료를 고치는 화면이 저장 뒤에 부른다 — 안 부르면 방금 고친 값이 다른 화면의 고르개에 안 나와 사용자는 저장이 안 된 줄 안다.</summary>
    public void Invalidate(string group)
    {
        var next = _generations.AddOrUpdate(group, 1, (_, current) => current + 1);
        logger.LogDebug("참조자료 묶음 {Group} 을 버렸다 (세대 {Generation}).", group, next);
    }

    /// <summary>열쇠를 만든다. 담기는 형을 반드시 섞는다 — 같은 엔드포인트를 두 모듈이 각자 자기 DTO 로 받는데(범용 셀렉트 설정은 이름만 같고 타입이 다르다) 열쇠가 같으면 TryGetValue 가 형이 안 맞아 false 를 주고 두 모듈이 서로의 값을 번갈아 밀어낸다. 오류는 안 나지만 캐시를 안 둔 것보다 나쁘다. 형을 섞으면 값은 형마다 따로 담기고 세대는 공유되어 한쪽에서 버리면 양쪽이 함께 버려진다.</summary>
    private string Key(string group, string key, Type type)
    {
        var generation = _generations.TryGetValue(group, out var value) ? value : 0;
        return $"refdata:{group}:{generation}:{type.FullName}:{key}";
    }
}
