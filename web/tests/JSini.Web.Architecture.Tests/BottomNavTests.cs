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
    [InlineData(BottomNav.MenuPath, "jsini-icon-menu")]
    [InlineData(BottomNav.ProfilePath, "jsini-icon-user")]
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
    /// 메뉴 단추(햄버거)와 사용자 프로필 아바타는 <b>메뉴가 아니라서</b>
    /// 트리에서 나오지 않는다. 고르개에 없으면 띠에 놓을 길이 아예 없다.
    /// </summary>
    [Theory]
    [InlineData(BottomNav.MenuPath)]
    [InlineData(BottomNav.ProfilePath)]
    public void 고르개에_메뉴_단추와_프로필이_있다(string path)
    {
        Assert.Contains(BottomNav.Choices(Menus), c => c.Path == path);
        Assert.Contains(BottomNav.Choices([]), c => c.Path == path);
    }

    /// <summary>
    /// 메뉴가 아닌 넷은 고르개 <b>맨 위</b>에 선다. 179건 아래로 내려가면
    /// 찾을 길이 글자를 쳐 보는 것뿐이다.
    /// </summary>
    [Fact]
    public void 메뉴가_아닌_넷이_고르개_맨_위다() =>
        Assert.Equal(
            [.. BottomNav.Fixed.Select(c => c.Path)],
            [.. BottomNav.Choices(Menus).Take(BottomNav.Fixed.Count).Select(c => c.Path)]);

    /// <summary>
    /// 고르개 이름에는 괄호 설명이 붙지만(「메뉴 단추 (햄버거 — …)」) 띠에
    /// 적히는 것은 <b>그것이 아니다.</b> 72px 칸이라 그대로 넣으면 두 글자에서
    /// 끊긴다 — 환경설정이 <see cref="BottomNavChoice.Title"/> 을 쓴다.
    /// </summary>
    [Fact]
    public void 띠에_적을_이름은_짧다()
    {
        foreach (var choice in BottomNav.Fixed)
        {
            Assert.DoesNotContain('(', choice.Title);
            Assert.InRange(choice.Title.Length, 1, 4);
        }

        Assert.All(
            BottomNav.Choices(Menus),
            c => Assert.False(string.IsNullOrWhiteSpace(c.Title)));
    }

    /// <summary>
    /// 고른 적이 없는 사람의 다섯은 <b>홈 · 알림 · 설정 · 프로필 · 메뉴</b> 다.
    /// </summary>
    /// <remarks>
    /// 차례까지 못 박는다 — 맨 끝이 「메뉴」인 것이 이 기본값의 뜻이다.
    /// 띠에서 못 닿는 화면은 전부 그 칸으로 열고, 한 손으로 쥔 엄지가 가장
    /// 멀리 닿는 자리가 거기다.
    /// </remarks>
    [Fact]
    public void 기본_다섯은_홈_알림_설정_프로필_메뉴다()
    {
        Assert.Equal(
            ["홈", "알림", "설정", "프로필", "메뉴"],
            BottomNav.Defaults.Select(item => item.Title));

        Assert.Equal(
            [BottomNav.HomePath, "/admin/push/history", BottomNav.ThemePath,
                BottomNav.ProfilePath, BottomNav.MenuPath],
            BottomNav.Defaults.Select(item => item.Path));
    }

    /// <summary>
    /// 기본 다섯에는 <b>업무 하나에 매인 화면이 없다.</b> 누가 로그인해도
    /// 뜻이 같은 칸만 남긴다 — 장례식장만 쓰는 사람에게 「빠른지시」가
    /// 기본으로 붙던 것이 이 규칙을 어긴 자리였다.
    /// </summary>
    /// <remarks>
    /// 알림(<c>/admin/push/history</c>)만 주소를 갖는다. 그것은 업무가 아니라
    /// 어느 업무에서 오든 한 곳에 쌓이는 화면이다.
    /// </remarks>
    [Fact]
    public void 기본_다섯에는_업무_화면이_없다()
    {
        foreach (var item in BottomNav.Defaults)
        {
            Assert.Null(item.RouteKey);

            var isBusinessScreen = !item.Path.StartsWith('#')
                && item.Path != BottomNav.HomePath
                && item.Path != "/admin/push/history";

            Assert.False(isBusinessScreen, $"업무 화면이 기본값에 있다: {item.Path}");
        }
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

    // ── 차례 바꾸기(`BottomNav.Move`) ──────────────────────────

    /// <summary>
    /// 목록의 차례가 곧 띠의 차례다(위가 왼쪽). 한 칸씩 옮기는 것이
    /// 화면의 꺾쇠 단추 둘이다.
    /// </summary>
    [Theory]
    [InlineData(0, 1, "1,0,2,3")]
    [InlineData(3, -1, "0,1,3,2")]
    [InlineData(1, -1, "1,0,2,3")]
    [InlineData(2, 1, "0,1,3,2")]
    public void 한_칸씩_옮긴다(int index, int delta, string expected)
    {
        var items = Four();

        Assert.True(BottomNav.Move(items, index, delta));
        Assert.Equal(expected, Titles(items));
    }

    /// <summary>
    /// 여러 칸을 건너뛰어도 <b>사이에 있던 것들의 차례는 그대로다.</b>
    /// 자리를 맞바꾸는 방식이면 여기서 0 과 3 이 뒤집힌다.
    /// </summary>
    [Fact]
    public void 건너뛰어_옮겨도_사이는_그대로다()
    {
        var items = Four();

        Assert.True(BottomNav.Move(items, 3, -3));
        Assert.Equal("3,0,1,2", Titles(items));
    }

    /// <summary>
    /// <b>목록 밖으로는 나가지 않는다.</b> 화면이 양 끝에서 단추를 잠그지만,
    /// 저장된 것이 사람 손을 탄 뒤에도 자리가 어긋나면 안 된다.
    /// </summary>
    [Theory]
    [InlineData(0, -1)]
    [InlineData(3, 1)]
    [InlineData(0, -5)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(4, -1)]
    public void 밖으로_나가는_것은_아무_일도_없다(int index, int delta)
    {
        var items = Four();

        Assert.False(BottomNav.Move(items, index, delta));
        Assert.Equal("0,1,2,3", Titles(items));
    }

    /// <summary>
    /// 옮긴 것이 <b>저장을 거쳐도 그 차례다.</b> 화면은 옮긴 즉시
    /// <see cref="BottomNav.Serialize"/> 로 적고, 다음에 열 때
    /// <see cref="BottomNav.Parse"/> 로 읽는다.
    /// </summary>
    [Fact]
    public void 옮긴_차례가_저장을_거쳐도_남는다()
    {
        var items = Four();

        BottomNav.Move(items, 0, 3);

        Assert.Equal("1,2,3,0", Titles([.. BottomNav.Parse(BottomNav.Serialize(items))]));
    }

    private static List<BottomNavItem> Four() =>
        [.. Enumerable.Range(0, 4)
            .Select(i => new BottomNavItem { Path = $"/x{i}", Title = $"{i}" })];

    private static string Titles(IEnumerable<BottomNavItem> items) =>
        string.Join(',', items.Select(i => i.Title));
}
