using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Menu;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 휴대폰 아래 띠에 놓을 칸(<see cref="BottomNav"/>).
///
/// <para>
/// 값의 출처가 <b>브라우저 저장소</b>다 — 사람이 고칠 수 있고, 옛 모양이
/// 남아 있을 수 있고, 아예 없을 수도 있다. 그 셋 중 무엇이 와도 화면 아래에
/// <b>빈 띠</b>가 남지 않아야 한다. 빈 띠는 오류가 아니라 고장으로 읽힌다.
/// </para>
/// </summary>
public sealed class BottomNavTests
{
    private static readonly IReadOnlyList<MenuNode> Menus =
    [
        new()
        {
            Path = "/room_status",
            Href = "/funeral/room-status",
            RouteKey = "funeral.room-status",
            Title = "빈소 현황",
            Icon = "lucide:church",
        },
        new()
        {
            Path = "/funeral/setting",
            Title = "설정",
            IsCatalog = true,
            Children =
            [
                new()
                {
                    Path = "/funeral/setting/environment",
                    RouteKey = "funeral.setting.environment",
                    Title = "환경설정",
                },
            ],
        },
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("그냥 글자")]
    [InlineData("{\"p\":\"/\"}")]
    [InlineData("[{\"p\":\"\",\"t\":\"\"}]")]
    public void 읽을_것이_없으면_기본_다섯이다(string? json) =>
        Assert.Equal(BottomNav.Defaults, BottomNav.Parse(json));

    /// <summary>
    /// 다섯을 넘겨 적어 두어도 다섯까지만 쓴다. 저장하는 쪽도 같이 자르지만,
    /// <b>읽는 쪽에서 한 번 더</b> 자른다 — 저장소는 사람이 고칠 수 있다.
    /// </summary>
    [Fact]
    public void 다섯을_넘으면_앞에서_자른다()
    {
        var many = Enumerable.Range(0, 9)
            .Select(i => new BottomNavItem { Path = $"/x{i}", Title = $"칸{i}" })
            .ToArray();

        Assert.Equal(BottomNav.MaxItems, BottomNav.Parse(BottomNav.Serialize(many)).Count);
    }

    [Fact]
    public void 적어_둔_것을_그대로_읽어_온다()
    {
        var saved = new BottomNavItem[]
        {
            new() { Path = "/funeral/room-status", RouteKey = "funeral.room-status", Title = "빈소" },
        };

        var read = BottomNav.Parse(BottomNav.Serialize(saved));

        Assert.Equal(saved, read);
    }

    /// <summary>
    /// 이름이 빠진 칸은 버린다. 남겨 두면 띠에 <b>글자 없는 단추</b>가 서고,
    /// 그것은 눌러 보기 전에는 무엇인지 알 수 없다.
    /// </summary>
    [Fact]
    public void 이름이_없는_칸은_버린다()
    {
        var read = BottomNav.Parse(
            """[{"p":"/a","t":"가"},{"p":"/b","t":""},{"p":"","t":"다"}]""");

        Assert.Single(read);
        Assert.Equal("/a", read[0].Path);
    }

    // ── 아이콘 ──────────────────────────────────────────────

    [Fact]
    public void 박아_둔_아이콘이_가장_세다()
    {
        var item = new BottomNavItem { Path = "/funeral/room-status", Title = "빈소", Icon = "jsini-icon-home" };

        Assert.Equal("jsini-icon-home", BottomNav.IconClass(item, Menus));
    }

    /// <summary>
    /// 저장소에서 온 값이라 <b>클래스 이름 꼴이 아니면 버린다.</b> 그대로
    /// 붙이면 공백 하나로 남의 클래스를 켤 수 있다
    /// (<see cref="MenuIcons.CssClass"/> 가 같은 이유로 같은 일을 한다).
    /// </summary>
    [Theory]
    [InlineData("jsini-icon-home jsini-shell__fab")]
    [InlineData("jsini-icon-home\" onload=\"x")]
    [InlineData("dxbl-btn")]
    public void 이름_꼴이_아닌_아이콘은_메뉴에서_다시_찾는다(string icon)
    {
        var item = new BottomNavItem
        {
            Path = "/funeral/room-status",
            RouteKey = "funeral.room-status",
            Title = "빈소",
            Icon = icon,
        };

        Assert.Equal(MenuIcons.CssClass("lucide:church"), BottomNav.IconClass(item, Menus));
    }

    [Fact]
    public void 메뉴에_달린_아이콘을_따라간다()
    {
        var item = new BottomNavItem { Path = "/funeral/room-status", Title = "빈소" };

        Assert.Equal(MenuIcons.CssClass("lucide:church"), BottomNav.IconClass(item, Menus));
    }

    /// <summary>
    /// 아이콘 없는 메뉴에는 아무 그림이나 하나 붙인다. <b>같은 경로는 늘 같은
    /// 그림</b>이라야 한다 — 프로세스마다 달라지면 서버를 다시 띄울 때마다
    /// 띠의 그림이 바뀐다(`string.GetHashCode` 를 쓰지 않는 까닭).
    /// </summary>
    [Fact]
    public void 아이콘이_없으면_경로로_정한_그림이_늘_같다()
    {
        var item = new BottomNavItem { Path = "/funeral/setting/environment", Title = "환경설정" };

        var first = BottomNav.IconClass(item, Menus);

        Assert.NotEqual(MenuIcons.BaseClass, first);
        Assert.Equal(first, BottomNav.IconClass(item, Menus));
    }

    [Theory]
    [InlineData(BottomNav.HomePath, "jsini-icon-home")]
    [InlineData(BottomNav.ThemePath, "jsini-icon-palette")]
    public void 메뉴가_아닌_칸은_제_그림이_있다(string path, string expected) =>
        Assert.Equal(expected, BottomNav.IconClass(new BottomNavItem { Path = path, Title = "x" }, Menus));

    // ── 주소 풀기 ───────────────────────────────────────────

    /// <summary>
    /// DB 경로와 Blazor 라우트가 아직 다를 수 있다(<see cref="MenuNode.Href"/>).
    /// 적어 둔 값으로 곧장 가면 이관이 안 끝난 화면에서 "준비 중" 이 뜬다.
    /// </summary>
    [Theory]
    [InlineData("funeral.room-status", "/room_status")]
    [InlineData(null, "/room_status")]
    [InlineData(null, "/funeral/room-status")]
    public void 링크는_메뉴를_한_번_거친다(string? key, string path) =>
        Assert.Equal(
            "/funeral/room-status",
            BottomNav.Resolve(new BottomNavItem { Path = path, RouteKey = key, Title = "빈소" }, Menus));

    [Fact]
    public void 못_찾으면_적어_둔_값_그대로다() =>
        Assert.Equal(
            "/somewhere",
            BottomNav.Resolve(new BottomNavItem { Path = "/somewhere", Title = "어딘가" }, Menus));

    // ── 고르개 ──────────────────────────────────────────────

    /// <summary>
    /// 묶음(CATALOG)은 제 화면이 없어서 골라 봐야 "준비 중" 이 뜬다.
    /// 자식은 그대로 고를 수 있어야 한다.
    /// </summary>
    [Fact]
    public void 고르개에_묶음은_안_들어간다()
    {
        var choices = BottomNav.Choices(Menus);

        Assert.DoesNotContain(choices, c => c.Path == "/funeral/setting");
        Assert.Contains(choices, c => c.Path == "/funeral/setting/environment");
    }

    /// <summary>
    /// 이름에 줄기를 붙인다. 「목록」·「현황」 같은 이름이 업무마다 있어서
    /// 붙이지 않으면 어느 것을 고르는지 알 수 없다.
    /// </summary>
    [Fact]
    public void 고르개_이름에_줄기가_붙는다() =>
        Assert.Equal(
            "설정 › 환경설정",
            BottomNav.Choices(Menus).Single(c => c.Path == "/funeral/setting/environment").Label);

    [Fact]
    public void 고르개에_홈과_테마_서랍이_있다()
    {
        var choices = BottomNav.Choices(Menus);

        Assert.Contains(choices, c => c.Path == BottomNav.HomePath);
        Assert.Contains(choices, c => c.Path == BottomNav.ThemePath);
    }

    /// <summary>
    /// 기본 다섯은 <b>지금 화면에 떠 있는 그대로</b>여야 한다 — 한 번도 안
    /// 고친 사람의 띠가 바뀌면 안 된다. 그래서 다섯이 아이콘을 직접 들고 있고,
    /// 메뉴 트리가 비어 있어도 그 그림이 나온다.
    /// </summary>
    [Fact]
    public void 기본_다섯은_메뉴가_없어도_제_그림이_나온다()
    {
        Assert.Equal(BottomNav.MaxItems, BottomNav.Defaults.Count);

        foreach (var item in BottomNav.Defaults)
        {
            var css = BottomNav.IconClass(item, []);

            Assert.StartsWith("jsini-icon-", css, StringComparison.Ordinal);
            Assert.DoesNotContain(' ', css);
        }
    }
}
