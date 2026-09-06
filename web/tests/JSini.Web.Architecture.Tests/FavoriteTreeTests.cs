using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 즐겨찾기 트리 좁히기. 사이드바의 「즐겨찾기」 탭이 이 결과를 그린다.
///
/// 평평한 목록이던 것을 트리로 바꾸면서 생긴 판정이라, <see cref="MenuFilterTests"/>
/// 와 같은 자리에서 같은 방식으로 지킨다 — 특히 <b>조상은 남기고 형제는 뺀다</b>
/// 는 것과 <b>원본을 바꾸지 않는다</b> 는 것.
/// </summary>
public sealed class FavoriteTreeTests
{
    private static MenuNode Catalog(string title, params MenuNode[] children) => new()
    {
        Path = $"/{title}",
        Title = title,
        IsCatalog = true,
        Children = children,
    };

    private static MenuNode Screen(string path, params MenuNode[] children) => new()
    {
        Path = path,
        Title = path,
        IsCatalog = false,
        Children = children,
    };

    private static Func<string, bool> Favorites(params string[] paths) =>
        path => paths.Contains(path);

    [Fact]
    public void 담지_않은_화면은_빠진다()
    {
        var menus = new[] { Screen("/a"), Screen("/b") };

        var result = FavoriteTree.Prune(menus, Favorites("/a"));

        Assert.Single(result);
        Assert.Equal("/a", result[0].Path);
    }

    /// <summary>
    /// <b>이 화면을 만든 이유.</b> 담아 둔 화면의 위 묶음이 함께 남아야 한다.
    ///
    /// 메뉴 제목은 묶음 안에서만 유일하다 — 「목록」이 업무마다 있어서
    /// 평평하게 늘어놓으면 담아 둔 것이 어느 업무 것인지 알 수 없다.
    /// </summary>
    [Fact]
    public void 담은_화면의_조상_묶음은_함께_남는다()
    {
        var menus = new[]
        {
            Catalog("장례식장", Catalog("현황", Screen("/funeral/room-status"))),
        };

        var result = FavoriteTree.Prune(menus, Favorites("/funeral/room-status"));

        var 장례식장 = Assert.Single(result);
        var 현황 = Assert.Single(장례식장.Children);
        var 화면 = Assert.Single(현황.Children);

        Assert.Equal("장례식장", 장례식장.Title);
        Assert.Equal("현황", 현황.Title);
        Assert.Equal("/funeral/room-status", 화면.Path);
    }

    /// <summary>
    /// 조상은 남기되 <b>형제는 뺀다.</b> 조상을 남기려고 그 아래를 통째로
    /// 남기면 즐겨찾기 탭이 메뉴 탭과 같아진다.
    /// </summary>
    [Fact]
    public void 담은_화면의_형제는_빠진다()
    {
        var menus = new[] { Catalog("업무", Screen("/a"), Screen("/b"), Screen("/c")) };

        var result = FavoriteTree.Prune(menus, Favorites("/b"));

        var 업무 = Assert.Single(result);
        var 남은것 = Assert.Single(업무.Children);
        Assert.Equal("/b", 남은것.Path);
    }

    [Fact]
    public void 담은_것이_하나도_없으면_비어_있다()
    {
        var menus = new[] { Catalog("업무", Screen("/a"), Screen("/b")) };

        Assert.Empty(FavoriteTree.Prune(menus, Favorites()));
    }

    /// <summary>
    /// 묶음은 담을 수 없다. 묶음의 <see cref="MenuNode.NavigateUrl"/> 은
    /// <c>null</c> 이라, 담긴 것으로 다루면 눌러도 아무 데도 가지 않는 항목이
    /// 즐겨찾기에 남는다.
    /// </summary>
    [Fact]
    public void 묶음은_경로가_담겨_있어도_자기로서_남지_않는다()
    {
        var menus = new[] { Catalog("업무", Screen("/a")) };

        Assert.Empty(FavoriteTree.Prune(menus, Favorites("/업무")));
        Assert.Equal(0, FavoriteTree.CountScreens(menus, Favorites("/업무")));
    }

    /// <summary>
    /// 자식이 있는 <b>화면</b>을 담으면 자기만 남고 자식은 빠진다.
    /// <c>/funeral/status</c>(현황관리)처럼 자식이 있는 화면이 실제로 있다.
    /// </summary>
    [Fact]
    public void 자식있는_화면을_담으면_자기만_남는다()
    {
        var menus = new[]
        {
            Screen("/funeral/status",
                Screen("/funeral/status/room"),
                Screen("/funeral/status/device")),
        };

        var result = FavoriteTree.Prune(menus, Favorites("/funeral/status"));

        var 남은것 = Assert.Single(result);
        Assert.Equal("/funeral/status", 남은것.Path);
        Assert.Empty(남은것.Children);
    }

    [Fact]
    public void 자식있는_화면과_그_자식을_함께_담으면_둘_다_남는다()
    {
        var menus = new[]
        {
            Screen("/funeral/status",
                Screen("/funeral/status/room"),
                Screen("/funeral/status/device")),
        };

        var result = FavoriteTree.Prune(menus,
            Favorites("/funeral/status", "/funeral/status/room"));

        var 부모 = Assert.Single(result);
        var 자식 = Assert.Single(부모.Children);
        Assert.Equal("/funeral/status/room", 자식.Path);
        Assert.Equal(2, FavoriteTree.CountScreens(result, Favorites("/funeral/status", "/funeral/status/room")));
    }

    /// <summary>
    /// 사이드바가 이 값을 서버가 준 개수와 견줘, 메뉴에 없는 즐겨찾기를 알린다.
    /// 묶음은 세지 않는다(위 사례 참고).
    /// </summary>
    [Fact]
    public void 담긴_화면_개수를_센다()
    {
        var menus = new[]
        {
            Catalog("업무", Screen("/a"), Screen("/b")),
            Catalog("다른업무", Screen("/c")),
        };

        Assert.Equal(2, FavoriteTree.CountScreens(menus, Favorites("/a", "/c")));

        // 메뉴에 없는 경로는 세지 않는다 — 그 차이가 곧 "안 보이는 즐겨찾기" 다.
        Assert.Equal(1, FavoriteTree.CountScreens(menus, Favorites("/a", "/없는화면")));
    }

    /// <summary>
    /// 원본을 바꾸지 않는다. 즐겨찾기를 담고 뺄 때마다 같은 원본을 다시
    /// 좁히므로, 좁히기가 원본을 건드리면 두 번째부터 결과가 달라진다.
    /// </summary>
    [Fact]
    public void 좁히기는_원본을_바꾸지_않는다()
    {
        var 담은것 = Screen("/a/1");
        var 부모 = Catalog("업무", 담은것, Screen("/a/2"));
        var menus = new[] { 부모 };

        FavoriteTree.Prune(menus, Favorites("/a/1"));

        Assert.Equal(2, 부모.Children.Count);
    }
}
