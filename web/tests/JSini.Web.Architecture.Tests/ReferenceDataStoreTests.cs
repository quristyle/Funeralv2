using JSini.Web.Components.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 참조자료 통이 무엇을 나눠 쓰고 무엇을 갈라 담는지.
///
/// [왜 시험으로 못 박나]
///
/// 이 통은 <b>틀려도 오류가 나지 않는</b> 종류다. 잘못되면 캐시가 안 먹어
/// 조용히 느려지거나, 반대로 안 갈라야 할 것이 갈리지 않아 <b>남의 목록이
/// 보인다.</b> 둘 다 화면에는 아무 표시가 안 난다.
///
/// 특히 <b>형을 열쇠에 섞는 것</b>은 지우기 쉽다. 없어도 컴파일되고 시험도
/// 통과하고 화면도 뜬다 — 두 모듈이 번갈아 서로의 값을 밀어내는 것만 남는다.
/// </summary>
public class ReferenceDataStoreTests
{
    private static ReferenceDataStore NewStore() =>
        new(new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReferenceDataStore>.Instance);

    /// <summary>담은 뒤에는 다시 읽지 않는다.</summary>
    [Fact]
    public async Task 같은_열쇠는_한_번만_읽는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return Task.FromResult<List<string>?>(["가"]);
        }

        Assert.Equal(["가"], await store.GetOrAddAsync("묶음", "열쇠", Load));
        Assert.Equal(["가"], await store.GetOrAddAsync("묶음", "열쇠", Load));

        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task 열쇠가_다르면_따로_읽는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return Task.FromResult<List<string>?>(["가"]);
        }

        await store.GetOrAddAsync("묶음", "하나", Load);
        await store.GetOrAddAsync("묶음", "둘", Load);

        Assert.Equal(2, reads);
    }

    /// <summary>
    /// <b>형이 다르면 서로 덮어쓰지 않는다.</b>
    ///
    /// <para>
    /// 같은 엔드포인트를 두 모듈이 <b>각자 자기 DTO</b>로 받는 일이 있다
    /// (범용 셀렉트 설정 — 프로젝트관리와 헬프데스크의 <c>BizSelectConfig</c> 는
    /// 이름만 같고 다른 타입이다). 모듈끼리 참조가 금지라 그것이 정상이다.
    /// </para>
    ///
    /// <para>
    /// 열쇠에 형이 안 섞이면 <c>IMemoryCache.TryGetValue&lt;T&gt;</c> 가 형이
    /// 안 맞아 <c>false</c> 를 주고, 부르는 쪽은 「없다」고 읽어 다시 담는다.
    /// 결과가 <b>번갈아 서로를 밀어내는 것</b>이고 오류는 안 난다 —
    /// 왕복은 그대로인데 통이 메모리만 쓰는 상태다.
    /// </para>
    /// </summary>
    [Fact]
    public async Task 형이_다르면_서로_밀어내지_않는다()
    {
        var store = NewStore();
        var 글자읽기 = 0;
        var 숫자읽기 = 0;

        Task<List<string>?> 글자()
        {
            글자읽기++;
            return Task.FromResult<List<string>?>(["가"]);
        }

        Task<List<int>?> 숫자()
        {
            숫자읽기++;
            return Task.FromResult<List<int>?>([1]);
        }

        // 같은 묶음 · 같은 열쇠 · 다른 형으로 번갈아 부른다.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(["가"], await store.GetOrAddAsync("묶음", "같은열쇠", 글자));
            Assert.Equal([1], await store.GetOrAddAsync("묶음", "같은열쇠", 숫자));
        }

        // 형을 섞지 않으면 여섯 번 다 읽는다.
        Assert.Equal(1, 글자읽기);
        Assert.Equal(1, 숫자읽기);
    }

    /// <summary>
    /// 버린 뒤에는 다시 읽는다. <b>고친 값이 고르개에 반영되는 유일한 길이다.</b>
    /// </summary>
    [Fact]
    public async Task 버리면_다시_읽는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return Task.FromResult<List<string>?>([$"{reads}번"]);
        }

        Assert.Equal(["1번"], await store.GetOrAddAsync("묶음", "열쇠", Load));

        store.Invalidate("묶음");

        Assert.Equal(["2번"], await store.GetOrAddAsync("묶음", "열쇠", Load));
        Assert.Equal(2, reads);
    }

    /// <summary>버리는 것은 그 묶음뿐이다.</summary>
    [Fact]
    public async Task 다른_묶음은_함께_버려지지_않는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return Task.FromResult<List<string>?>(["가"]);
        }

        await store.GetOrAddAsync("이쪽", "열쇠", Load);
        await store.GetOrAddAsync("저쪽", "열쇠", Load);
        Assert.Equal(2, reads);

        store.Invalidate("이쪽");

        await store.GetOrAddAsync("이쪽", "열쇠", Load);   // 다시 읽는다
        await store.GetOrAddAsync("저쪽", "열쇠", Load);   // 그대로 있다

        Assert.Equal(3, reads);
    }

    /// <summary>
    /// <b><c>null</c> 은 담지 않는다.</b> 읽기가 실패했다는 뜻이고, 담아 버리면
    /// 서버가 돌아와도 TTL 동안 빈 고르개가 남는다.
    /// </summary>
    [Fact]
    public async Task 실패는_담지_않는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return Task.FromResult<List<string>?>(reads == 1 ? null : ["돌아왔다"]);
        }

        Assert.Null(await store.GetOrAddAsync("묶음", "열쇠", Load));

        // 실패를 담았다면 여기서도 null 이 나온다.
        Assert.Equal(["돌아왔다"], await store.GetOrAddAsync("묶음", "열쇠", Load));
        Assert.Equal(2, reads);
    }

    /// <summary>
    /// 예외도 담지 않는다. <c>cache.Set</c> 에 닿기 전에 밖으로 나간다 —
    /// 부르는 자리 몇 곳이 <c>null</c> 대신 예외로 실패를 알린다.
    /// </summary>
    [Fact]
    public async Task 예외가_지나가면_담지_않는다()
    {
        var store = NewStore();
        var reads = 0;

        Task<List<string>?> Load()
        {
            reads++;
            return reads == 1
                ? throw new InvalidOperationException("서버가 죽었다")
                : Task.FromResult<List<string>?>(["돌아왔다"]);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetOrAddAsync("묶음", "열쇠", Load));

        Assert.Equal(["돌아왔다"], await store.GetOrAddAsync("묶음", "열쇠", Load));
    }
}
