using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 탭 메뉴에서 <b>지금 화면을 휴대폰 아래 띠에 넣고 빼는 것</b>
/// (<c>TabBar</c> · <see cref="BottomNav.IndexOf"/> · <c>BottomNavSwapDialog</c>).
///
/// <para>
/// [왜 기계가 봐야 하는가]
/// </para>
///
/// <para>
/// 이 길은 <b>휴대폰에서만 눈에 보인다</b> — 창을 여는 자리가 좁은 화면에서
/// 헤더에 남는 그 화면 이름이고(<c>TabMenuRequest</c>), 결과가 나타나는 띠도
/// ≤767px 에서만 그려진다(<c>app.css</c>). 큰 화면으로 열어 보는 것으로는
/// 깨진 것을 알 수 없어서 여기서 글자로 지킨다.
/// </para>
///
/// <para>
/// 지키는 것은 셋이다 — <b>이미 놓은 칸을 「없다」고 읽지 않을 것</b>(같은
/// 화면이 띠에 둘 선다), <b>다 찼을 때 조용히 넘기지 않을 것</b>(눌렀는데
/// 아무 일도 없다), <b>고른 자리에 그대로 끼울 것</b>(건드리지도 않은 칸의
/// 자리가 바뀐다).
/// </para>
/// </summary>
public sealed class BottomNavFromTabMenuTests
{
    /// <summary>띠에 다섯이 다 선 모양. 셋째가 메뉴에서 온 칸이다.</summary>
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

    /// <summary>
    /// <b>경로가 옮겨 가도 찾는다.</b> 띠에 적힌 것은 넣을 때의 경로라 라우트가
    /// 바뀌면 지금 주소와 다르다 — 그때 경로만 맞대 보면 이미 놓아 둔 칸을
    /// 「없다」고 읽어 같은 화면이 둘 서고, 다섯 중 하나를 헛되이 쓴다.
    /// </summary>
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

    /// <summary>
    /// 다섯이 다 찬 띠에서 <b>고른 자리에 그대로 끼운다.</b> 뒤에 붙이면
    /// 밀어낸 칸 뒤의 것들이 한 칸씩 당겨져 건드리지도 않은 칸의 자리가 바뀐다 —
    /// 띠의 차례는 곧 엄지가 닿는 거리다(<see cref="BottomNav.Move"/> 머리말).
    /// </summary>
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

    /// <summary>
    /// <b>띠를 안 쓰기로 한 사람에게는 자리를 비운다.</b> 꺼 놓기만 하면
    /// 「왜 회색이지」가 되고(즐겨찾기 항목이 같은 이유로 같은 선택을 했다),
    /// 넣어 봐야 그릴 띠가 없다.
    /// </summary>
    [Fact]
    public void 띠를_안_쓰면_항목을_안_그린다() =>
        Assert.Contains("@if (!_navHidden)", TabBarSource(), StringComparison.Ordinal);

    /// <summary>
    /// 다 찼을 때 <b>묻는다.</b> 조용히 넘기면 「눌렀는데 아무 일도 없다」가 되고,
    /// 아무거나 밀어내면 오래 쓰던 칸이 말없이 사라진다.
    /// </summary>
    [Fact]
    public void 다_찼으면_무엇과_바꿀지_묻는다()
    {
        var source = TabBarSource();

        Assert.Contains("_navItems.Count < BottomNav.MaxItems", source, StringComparison.Ordinal);
        Assert.Contains("_swap.AskAsync(", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 묻는 창은 <c>.jsini-tabs</c> <b>밖</b>에 그린다. 안에 두면 휴대폰에서
    /// 탭 줄을 통째로 감추므로(<c>display: none</c>) <b>창이 아예 안 뜬다</b> —
    /// 오른쪽 클릭 창이 같은 이유로 같은 자리에 있다.
    /// </summary>
    [Fact]
    public void 묻는_창은_감춰지는_탭_줄_밖에_있다()
    {
        var razor = File.ReadAllText(TabBarRazorPath());

        var listEnd = razor.IndexOf("</div>\n\n    @* 오른쪽 클릭 창", StringComparison.Ordinal);
        var dialog = razor.IndexOf("<BottomNavSwapDialog", StringComparison.Ordinal);

        Assert.True(listEnd > 0, "`TabBar.razor` 에서 탭 줄 상자의 끝을 못 찾았다.");
        Assert.True(dialog > listEnd, "묻는 창이 `.jsini-tabs` 상자 안에 있다 — 휴대폰에서 안 뜬다.");
    }

    /// <summary>
    /// 고친 것을 <b>저장한다.</b> 안 하면 화면 아래 띠는 바뀌는데 새로고침에
    /// 되돌아간다.
    /// </summary>
    [Fact]
    public void 넣고_뺀_것을_저장한다() =>
        Assert.Contains("Boot.SetBottomNavItemsAsync(BottomNav.Serialize(_navItems))",
            TabBarSource(), StringComparison.Ordinal);

    /// <summary>
    /// <b>다 빼면 기본 다섯으로 되돌린다.</b> 빈 목록을 적어 두면 읽는 쪽이
    /// 기본값을 돌려주어(<see cref="BottomNav.Parse"/>) 화면과 저장된 것이
    /// 갈라진다 — 환경설정 판이 같은 이유로 같은 일을 한다.
    /// </summary>
    [Fact]
    public void 다_빼면_기본값으로_되돌린다()
    {
        var source = TabBarSource();

        Assert.Contains("_navItems.Count == 0", source, StringComparison.Ordinal);
        Assert.Contains("Boot.SetBottomNavItemsAsync(null)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 환경설정에서 고친 것을 <b>듣고 있다.</b> 안 듣고 있으면 이 창이 옛
    /// 다섯을 보고 판정해서, 이미 넣어 둔 화면에 또 「넣기」가 뜬다.
    /// </summary>
    [Fact]
    public void 다른_자리에서_고친_것을_듣는다()
    {
        var source = TabBarSource();

        Assert.Contains("Boot.BottomNavItemsChanged += ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavItemsChanged -= ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavHiddenChanged += ", source, StringComparison.Ordinal);
        Assert.Contains("Boot.BottomNavHiddenChanged -= ", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 읽는 것은 <b>부트스트랩의 단일 왕복에 얹혀 간다.</b> 저장소를 직접
    /// 읽으면 회로가 붙을 때 왕복이 하나 더 는다(<c>PortalBoot</c> 머리말).
    /// </summary>
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
