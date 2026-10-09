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
    public void 읽을_것이_없으면_기본값이다(string? json) =>
        Assert.Equal(BottomNav.Defaults, BottomNav.Parse(json));

    /// <summary>
    /// 칸 수의 띠는 <b>셋에서 여덟</b>이다. 양 끝을 못 박아 둔다 — 화면의
    /// 안내(「5 / 3~8칸」)와 단추 잠금이 이 두 값만 보므로, 여기가 조용히
    /// 바뀌면 화면이 말하는 것과 실제로 되는 것이 갈린다.
    /// </summary>
    [Fact]
    public void 칸은_셋에서_여덟이다()
    {
        Assert.Equal(3, BottomNav.MinItems);
        Assert.Equal(8, BottomNav.MaxItems);
    }

    /// <summary>
    /// 천장을 넘겨 적어 두어도 천장까지만 쓴다. 저장하는 쪽도 같이 자르지만,
    /// <b>읽는 쪽에서 한 번 더</b> 자른다 — 저장소는 사람이 고칠 수 있다.
    /// </summary>
    [Fact]
    public void 여덟을_넘으면_앞에서_자른다()
    {
        var many = Enumerable.Range(0, BottomNav.MaxItems + 4)
            .Select(i => new BottomNavItem { Path = $"/x{i}", Title = $"칸{i}" })
            .ToArray();

        Assert.Equal(BottomNav.MaxItems, BottomNav.Parse(BottomNav.Serialize(many)).Count);
    }

    /// <summary>
    /// <b>바닥에 못 미치는 것도 「없는 것」으로 본다.</b> 바닥이 하나이던
    /// 시절에 적어 둔 것(칸 하나·둘)이 아직 브라우저에 남아 있다 — 그대로
    /// 읽어 주면 화면은 3~8칸이라고 말하는데 띠는 그 밖에 서 있고, 빼기
    /// 단추가 처음부터 잠긴 줄이 생긴다.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 셋에_못_미치면_기본값으로_떨어진다(int count)
    {
        var few = Enumerable.Range(0, count)
            .Select(i => new BottomNavItem { Path = $"/x{i}", Title = $"칸{i}" })
            .ToArray();

        Assert.Equal(BottomNav.Defaults, BottomNav.Parse(BottomNav.Serialize(few)));
    }

    [Fact]
    public void 적어_둔_것을_그대로_읽어_온다()
    {
        var saved = new BottomNavItem[]
        {
            new() { Path = "/funeral/room-status", RouteKey = "funeral.room-status", Title = "빈소" },
            new() { Path = BottomNav.HomePath, Title = "홈" },
            new() { Path = BottomNav.MenuPath, Title = "메뉴" },
        };

        var read = BottomNav.Parse(BottomNav.Serialize(saved));

        Assert.Equal(saved, read);
    }

    /// <summary>
    /// 이름이 빠진 칸은 버린다. 남겨 두면 띠에 <b>글자 없는 단추</b>가 서고,
    /// 그것은 눌러 보기 전에는 무엇인지 알 수 없다.
    /// </summary>
    /// <remarks>
    /// 성한 칸을 <see cref="BottomNav.MinItems"/> 만큼 넣어 둔다 — 적게 두면
    /// 거른 결과가 바닥에 못 미쳐 기본값으로 떨어지고, 그러면 이 검사가
    /// <b>거르기가 아니라 바닥 규칙을 보게 된다.</b>
    /// </remarks>
    [Fact]
    public void 이름이_없는_칸은_버린다()
    {
        var read = BottomNav.Parse(
            """
            [{"p":"/a","t":"가"},{"p":"/b","t":""},{"p":"","t":"다"},
             {"p":"/d","t":"라"},{"p":"/e","t":"마"}]
            """);

        Assert.Equal(["/a", "/d", "/e"], read.Select(i => i.Path));
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
        // 기본값은 다섯이고 **띠 안에 있어야 한다** — 밖에 두면 아무것도 안
        // 고친 사람의 띠가 `Parse` 에서 제 값으로 안 돌아오거나(바닥 미달)
        // 잘린다(천장 초과).
        Assert.Equal(5, BottomNav.Defaults.Count);
        Assert.InRange(BottomNav.Defaults.Count, BottomNav.MinItems, BottomNav.MaxItems);

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

    // ── 띠가 화면 안에 남는가 (`app.css`) ──────────────────────

    /// <summary>
    /// 칸이 <b>줄어들 수 있어야 한다</b>(<c>.jsini-bottom-nav__item</c> 의
    /// <c>min-width: 0</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// [왜 글자로 검사하나]
    /// </para>
    /// <para>
    /// flex 항목의 기본 최소 폭은 내용 폭이고, 띠의 이름은 <c>nowrap</c> 이다 —
    /// 그래서 <b>이름이 긴 칸은 <c>flex: 1</c> 을 주어도 제 글자 폭을 쥐고
    /// 버틴다.</b> 칸이 다섯일 때는 드러나지 않았는데, 여덟까지 놓을 수 있게
    /// 되면서 320px 에 네 글자 이름 여덟을 채우면 띠가 376px 이 되어
    /// <b>끝 칸이 화면 밖으로 밀려 아예 안 눌린다.</b> 하필 그 칸이 대개
    /// 「메뉴」 — 띠에서 못 닿는 화면을 전부 여는 칸이다.
    /// </para>
    /// <para>
    /// 이 한 줄이 없어도 <b>빌드도 테스트도 화면도 멀쩡하다.</b> 드러나는
    /// 조건이 「좁은 기기 · 칸 여덟 · 긴 이름」 셋이 겹칠 때뿐이라 눈으로는
    /// 못 지킨다. 말줄임으로 자르는 짝도 함께 본다 — 줄어들기만 하고 안
    /// 자르면 글자가 옆 칸을 침범한다.
    /// </para>
    /// </remarks>
    [Fact]
    public void 칸은_줄어들고_이름은_말줄임으로_잘린다()
    {
        Assert.Contains("min-width: 0", Rule(@"^\.jsini-bottom-nav__item \{(.*?)\}"),
            StringComparison.Ordinal);

        var label = Rule(@"^\.jsini-bottom-nav__item > span:last-child \{(.*?)\}");

        Assert.Contains("overflow: hidden", label, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis", label, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%", label, StringComparison.Ordinal);
    }

    private static string Rule(string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            AppCss(), pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline
                | System.Text.RegularExpressions.RegexOptions.Multiline);

        Assert.True(match.Success, $"app.css 에서 `{pattern}` 규칙을 찾지 못했다.");
        return match.Groups[1].Value;
    }

    private static string AppCss() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static List<BottomNavItem> Four() =>
        [.. Enumerable.Range(0, 4)
            .Select(i => new BottomNavItem { Path = $"/x{i}", Title = $"{i}" })];

    private static string Titles(IEnumerable<BottomNavItem> items) =>
        string.Join(',', items.Select(i => i.Title));
}
