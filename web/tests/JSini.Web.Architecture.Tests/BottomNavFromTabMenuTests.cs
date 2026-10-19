using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>탭 메뉴에서 지금 화면을 휴대폰 아래 띠에 넣고 빼는 길을 글자로 지킨다. 이 길은 휴대폰 폭에서만 눈에 보여 큰 화면으로는 깨진 것을 알 수 없다.
/// 지키는 것은 셋이다 — 이미 놓은 칸을 없다고 읽지 않기, 다 찼을 때 조용히 넘기지 않기, 고른 자리에 그대로 끼우기.</summary>
public sealed class BottomNavFromTabMenuTests
{
    /// <summary>띠에 선 다섯. 셋째가 메뉴에서 온 칸이다. 꽉 찬 띠가 아니라서 가장 흔한 기본 다섯을 쓴다 — 천장(MaxItems)에 닿았을 때는 글자 검사 「다_찼으면_무엇과_바꿀지_묻는다」가 맡는다.</summary>
    private static List<BottomNavItem> Full() =>
    [
        new() { Path = BottomNav.HomePath, Title = "홈", Icon = "jsini-icon-home" },
        new() { Path = "/admin/push/history", Title = "알림", Icon = "jsini-icon-bell" },
        new() { Path = "/funeral/room-status", RouteKey = "funeral.room-status", Title = "빈소" },
        new() { Path = BottomNav.ProfilePath, Title = "프로필", Icon = "jsini-icon-user" },
        new() { Path = BottomNav.MenuPath, Title = "메뉴", Icon = "jsini-icon-menu" },
    ];

    [Fact]
    public void 없는_화면은_못_찾는다() =>
        Assert.Equal(-1, BottomNav.IndexOf(Full(), "/projmng/proj/wbs", "projmng.proj.wbs"));

    [Fact]
    public void 놓아_둔_경로를_찾는다() =>
        Assert.Equal(2, BottomNav.IndexOf(Full(), "/funeral/room-status", null));

    /// <summary>경로가 옮겨 가도 찾는다. 띠에 적힌 것은 넣을 때의 경로라, 경로만 맞대 보면 이미 놓은 칸을 없다고 읽어 같은 화면이 둘 선다.</summary>
    [Fact]
    public void 경로가_옮겨_가도_열쇠로_찾는다() =>
        Assert.Equal(2, BottomNav.IndexOf(Full(), "/funeral/room_status_v2", "funeral.room-status"));

    /// <summary>
    /// 열쇠가 없는 칸(메뉴가 아닌 홈·프로필·메뉴 단추)도 경로로 찾는다.
    /// </summary>
    [Fact]
    public void 메뉴가_아닌_칸도_찾는다() =>
        Assert.Equal(4, BottomNav.IndexOf(Full(), BottomNav.MenuPath, null));

    /// <summary>
    /// 찾는 쪽이 대소문자를 가리지 않는다. 주소는 사람이 친 것이 섞여 들어온다.
    /// </summary>
    [Fact]
    public void 대소문자를_가리지_않는다() =>
        Assert.Equal(2, BottomNav.IndexOf(Full(), "/Funeral/Room-Status", null));

    /// <summary>꽉 찬 띠에서 고른 자리에 그대로 끼운다 — 뒤에 붙이면 밀어낸 칸 뒤의 것들이 한 칸씩 당겨져 건드리지 않은 칸의 자리가 바뀐다.</summary>
    [Fact]
    public void 고른_자리에_그대로_끼운다()
    {
        var items = Full();

        items[2] = new BottomNavItem
        {
            Path = "/projmng/proj/wbs",
            RouteKey = "projmng.proj.wbs",
            Title = "WBS",
        };

        Assert.Equal(
            "홈,알림,WBS,프로필,메뉴",
            string.Join(',', BottomNav.Parse(BottomNav.Serialize(items)).Select(i => i.Title)));
    }

    /// <summary>
    /// 탭 메뉴에 넣고 빼는 항목이 <b>있다.</b> 이것이 없으면 아래 검사들이
    /// 지키는 것이 뜻을 잃는다.
    /// </summary>
    [Fact]
    public void 탭_메뉴에_넣고_빼는_항목이_있다()
    {
        var source = TabBarSource();

        Assert.Contains("휴대폰 아래 띠에 넣기", source, StringComparison.Ordinal);
        Assert.Contains("휴대폰 아래 띠에서 빼기", source, StringComparison.Ordinal);
        Assert.Contains("ToggleBottomNavAsync", source, StringComparison.Ordinal);
    }

    /// <summary>띠를 안 쓰기로 한 사람에게는 자리를 비운다 — 꺼 놓기만 하면 회색 항목이 되고, 넣어 봐야 그릴 띠가 없다.</summary>
    [Fact]
    public void 띠를_안_쓰면_항목을_안_그린다() =>
        Assert.Contains("@if (!_navHidden)", TabBarSource(), StringComparison.Ordinal);

    /// <summary>다 찼을 때 묻는다. 조용히 넘기면 눌렀는데 아무 일도 없고, 아무거나 밀어내면 오래 쓰던 칸이 말없이 사라진다.</summary>
    [Fact]
    public void 다_찼으면_무엇과_바꿀지_묻는다()
    {
        var source = TabBarSource();

        Assert.Contains("_navItems.Count < BottomNav.MaxItems", source, StringComparison.Ordinal);
        Assert.Contains("_swap.AskAsync(", source, StringComparison.Ordinal);
    }

    /// <summary>창들은 .jsini-tabs 밖에 그린다 — 안에 두면 휴대폰에서 탭 줄째 감춰져 창이 안 뜬다. 끝나는 자리는 깊이를 세어 찾는다(뒤의 주석 글자로 집으면 탭 줄 하나 더 붙을 때 밀려 못 찾는다).</summary>
    [Fact]
    public void 창들은_감춰지는_탭_줄_밖에_있다()
    {
        var razor = File.ReadAllText(TabBarRazorPath());

        var listEnd = TabsBoxEnd(razor);

        Assert.True(listEnd > 0, "`TabBar.razor` 에서 탭 줄 상자의 끝을 못 찾았다.");

        foreach (var tag in new[] { "<BottomNavSwapDialog", "<DxContextMenu" })
        {
            Assert.True(
                razor.IndexOf(tag, StringComparison.Ordinal) > listEnd,
                $"`{tag}` 가 `.jsini-tabs` 상자 안에 있다 — 휴대폰에서 안 뜬다.");
        }
    }

    /// <summary>.jsini-tabs 상자가 닫히는 자리. 여는 태그부터 div 를 세어 깊이가 0 이 되는 곳이다.</summary>
    private static int TabsBoxEnd(string razor)
    {
        var at = razor.IndexOf("<div class=\"jsini-tabs\">", StringComparison.Ordinal);

        if (at < 0)
        {
            return -1;
        }

        var depth = 0;

        for (var i = at; i < razor.Length; i++)
        {
            if (string.CompareOrdinal(razor, i, "</div>", 0, 6) == 0)
            {
                if (--depth == 0)
                {
                    return i;
                }
            }
            else if (string.CompareOrdinal(razor, i, "<div", 0, 4) == 0)
            {
                depth++;
            }
        }

        return -1;
    }

    /// <summary>고친 것을 저장한다. 안 하면 아래 띠는 바뀌는데 새로고침에 되돌아간다.</summary>
    [Fact]
    public void 넣고_뺀_것을_저장한다() =>
        Assert.Contains("Boot.SetBottomNavItemsAsync(BottomNav.Serialize(_navItems))",
            TabBarSource(), StringComparison.Ordinal);

    /// <summary>바닥에서는 빼지 않는다(MinItems). 말로 돌려보낸다 — 이 항목은 창을 열어야 보이므로 잠근 낯을 미리 보여 줄 수 없다. 조용히 넘기면 눌렀는데 아무 일도 없게 된다.</summary>
    [Fact]
    public void 바닥에서는_빼지_않고_말해_준다()
    {
        var source = TabBarSource();

        Assert.Contains("_navItems.Count <= BottomNav.MinItems", source, StringComparison.Ordinal);
        Assert.Contains("NoticeTone.Warning", source, StringComparison.Ordinal);

        // 빈 목록을 적는 옛 길이 남으면 띠가 빈 채로 저장될 수 있고, 읽는 쪽은 기본값을 돌려주어 화면과 갈라진다.
        Assert.DoesNotContain("_navItems.Count == 0", source, StringComparison.Ordinal);
    }

    /// <summary>환경설정에서 고친 것을 듣고 있다. 안 들으면 이 창이 옛 목록으로 판정해 이미 넣은 화면에 또 「넣기」가 뜬다.</summary>
    [Fact]
    public void 다른_자리에서_고친_것을_듣는다()
    {
        var source = TabBarSource();

        Assert.Contains("Boot.BottomNavItemsChanged += ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavItemsChanged -= ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavHiddenChanged += ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavHiddenChanged -= ", source, StringComparison.Ordinal);
    }

    /// <summary>읽는 것은 부트스트랩의 단일 왕복에 얹는다. 저장소를 직접 읽으면 회로가 붙을 때 왕복이 하나 더 는다.</summary>
    [Fact]
    public void 읽기는_단일_왕복에_얹힌다()
    {
        var source = TabBarSource();

        Assert.Contains("state.BottomNavItemsJson", source, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage.getItem\", PortalBoot.BottomNavItemsKey",
            source, StringComparison.Ordinal);
    }

    private static string TabBarSource() => RazorSource.Read(TabBarRazorPath());

    private static string TabBarRazorPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "TabBar.razor");

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
}
