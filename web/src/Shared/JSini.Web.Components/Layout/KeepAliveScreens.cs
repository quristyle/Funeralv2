using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 메뉴에서 「화면 유지」(<c>keep_alive</c>)를 켠 화면을 탭이 열려 있는 동안 살려 둔다(vben 의 <c>keep-alive</c>).
/// 레이아웃이 이 목록을 <c>@Body</c> 옆에 그리고, 떠난 화면은 숨기기만 해서 입력하던 값·구른 자리·펼친 줄이 그대로 남는다.
/// 켜지 않은 화면은 예전처럼 떠나면 버려진다 — 살려 둔 화면은 보이지 않는 동안에도 타이머와 조회가 돈다.
/// scoped — 회로 하나가 곧 창 하나다.
/// </summary>
public sealed class KeepAliveScreens
{
    private readonly List<KeptScreen> _items = [];

    /// <summary>살려 둔 화면들. 레이아웃이 <c>@key</c> 로 그린다.</summary>
    public IReadOnlyList<KeptScreen> Items => _items;

    /// <summary>지금 보이는 것이 살려 둔 화면인가. 참이면 레이아웃은 <c>@Body</c> 를 그리지 않는다 — 둘 다 그리면 같은 화면이 두 벌 만들어진다.</summary>
    public bool ActiveKept => _items.Any(s => s.Active);

    /// <summary>라우터가 방금 고른 화면. 레이아웃은 RouteData 를 못 받으므로 Routes 의 KeepAliveRoute 가 레이아웃보다 먼저 적는다.</summary>
    public RouteData? Route { get; set; }

    /// <summary>
    /// 화면이 갈렸다. 지금 화면이 「화면 유지」면 살려 둔 것을 켜거나 새로 담고, 아니면 모두 숨긴다.
    /// </summary>
    /// <param name="href">질의 문자열을 뗀 지금 주소. 탭의 신원과 같다.</param>
    /// <param name="keepAlive">지금 화면의 메뉴가 「화면 유지」이고 볼 권한도 있는가.</param>
    /// <param name="layout">이 화면들을 그리는 레이아웃. 화면이 다른 레이아웃을 쓰면 여기서 그릴 수 없다.</param>
    public void Show(string href, bool keepAlive, Type layout)
    {
        foreach (var item in _items)
        {
            item.Active = false;
        }

        if (!keepAlive || Route is not { } route || !UsesLayout(route.PageType, layout))
        {
            return;
        }

        var existing = _items.FirstOrDefault(s => Same(s.Href, href));

        if (existing is not null && existing.PageType == route.PageType)
        {
            existing.Active = true;
            return;
        }

        if (existing is not null)
        {
            _items.Remove(existing);
        }

        _items.Add(new KeptScreen
        {
            Href = href,
            PageType = route.PageType,
            Parameters = new Dictionary<string, object?>(route.RouteValues),
            Active = true,
        });
    }

    /// <summary>탭에서 닫힌 화면을 놓아 준다 — 놓으면 부품이 Dispose 된다. 보이고 있는 것은 놓지 않는다(탭이 아직 안 열렸을 수 있다).</summary>
    /// <returns>하나라도 놓았는가.</returns>
    public bool Retain(IEnumerable<string> openHrefs)
    {
        var open = openHrefs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _items.RemoveAll(s => !s.Active && !open.Contains(s.Href)) > 0;
    }

    /// <summary>레이아웃이 새로 생겼다. 옛 레이아웃과 함께 부품도 사라졌으므로 숨은 것까지 한꺼번에 다시 만들지 않게 비운다.</summary>
    public void Reset() => _items.Clear();

    private static bool UsesLayout(Type page, Type layout)
    {
        var declared = page.GetCustomAttribute<LayoutAttribute>()?.LayoutType;
        return declared is null || declared == layout;
    }

    private static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>살려 둔 화면 하나.</summary>
public sealed class KeptScreen
{
    /// <summary>탭 주소. 신원이자 <c>@key</c> 다.</summary>
    public required string Href { get; init; }

    /// <summary>라우터가 고른 화면 클래스.</summary>
    public required Type PageType { get; init; }

    /// <summary>주소에서 뽑은 라우트 값(<c>{id}</c> 따위). 담을 때 한 번 베낀다 — 다음 이동의 값이 섞이지 않게.</summary>
    public required IDictionary<string, object?> Parameters { get; init; }

    /// <summary>지금 보이는가.</summary>
    public bool Active { get; set; }
}
