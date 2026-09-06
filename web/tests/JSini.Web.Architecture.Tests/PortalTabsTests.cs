using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 탭 닫기 규칙.
///
/// <para>
/// [화면으로는 겨눌 수 없어서 여기서 검사한다]
/// </para>
///
/// <para>
/// 이 규칙들은 탭을 오른쪽 클릭해 뜨는 창에서만 부를 수 있다. 그 창은
/// DevExpress 가 그리는데, 브라우저 자동화로 만든 <b>합성 클릭은 어느 항목에
/// 떨어졌는지 겨눌 수가 없었다</b> — 눌리기는 하는데 옆 항목이 눌린다.
/// 그래서 손으로 눌러 보는 것과 별개로, 규칙 자체는 여기서 못 박는다.
/// </para>
///
/// <para>
/// 어느 것이든 <b>고정한 탭은 살아남는다</b>. 그리고 보고 있던 탭이 닫히면
/// 옮겨 갈 주소를 돌려줘야 한다 — 안 돌려주면 주소는 그대로인데 그 탭만
/// 없는 상태가 되어 탭 줄과 화면이 어긋난다.
/// </para>
/// </summary>
public sealed class PortalTabsTests
{
    /// <summary>탭 넷을 열고 <paramref name="active"/> 를 보고 있는 상태로 만든다.</summary>
    private static PortalTabs Four(string active = "/d")
    {
        var tabs = new PortalTabs();

        tabs.Open("/a", "가");
        tabs.Open("/b", "나");
        tabs.Open("/c", "다");
        tabs.Open("/d", "라");
        tabs.Open(active, "");

        return tabs;
    }

    private static string[] Hrefs(PortalTabs tabs) => [.. tabs.Items.Select(t => t.Href)];

    [Fact]
    public void 오른쪽_탭_닫기는_오른쪽만_닫는다()
    {
        var tabs = Four(active: "/a");

        var next = tabs.CloseRight("/b");

        Assert.Equal(["/a", "/b"], Hrefs(tabs));

        // 보고 있던 /a 는 살아 있으므로 옮길 필요가 없다.
        Assert.Null(next);
    }

    [Fact]
    public void 왼쪽_탭_닫기는_왼쪽만_닫는다()
    {
        var tabs = Four(active: "/d");

        var next = tabs.CloseLeft("/c");

        Assert.Equal(["/c", "/d"], Hrefs(tabs));
        Assert.Null(next);
    }

    [Fact]
    public void 보고_있던_탭이_닫히면_옮겨_갈_주소를_준다()
    {
        var tabs = Four(active: "/d");

        // /b 의 오른쪽을 닫으면 보고 있던 /d 가 사라진다.
        var next = tabs.CloseRight("/b");

        Assert.Equal(["/a", "/b"], Hrefs(tabs));
        Assert.Equal("/a", next);
        Assert.Equal("/a", tabs.ActiveHref);
    }

    [Fact]
    public void 고정한_탭은_어느_닫기에서도_살아남는다()
    {
        var tabs = Four(active: "/a");
        tabs.TogglePin("/d");

        tabs.CloseRight("/a");

        Assert.Equal(["/a", "/d"], Hrefs(tabs));

        tabs.CloseOthers("/a");
        Assert.Equal(["/a", "/d"], Hrefs(tabs));

        tabs.CloseAll();
        Assert.Equal(["/d"], Hrefs(tabs));
    }

    [Fact]
    public void 고정한_탭은_닫기가_듣지_않는다()
    {
        var tabs = Four(active: "/a");
        tabs.TogglePin("/b");

        var next = tabs.Close("/b");

        Assert.Contains("/b", Hrefs(tabs));
        Assert.Null(next);
    }

    [Fact]
    public void 닫을_것이_없으면_아무_일도_없다()
    {
        var tabs = Four(active: "/a");

        Assert.Null(tabs.CloseLeft("/a"));
        Assert.Equal(["/a", "/b", "/c", "/d"], Hrefs(tabs));

        // 목록에 없는 주소로 불러도 죽지 않는다.
        Assert.Null(tabs.CloseRight("/없는것"));
        Assert.Equal(["/a", "/b", "/c", "/d"], Hrefs(tabs));
    }

    [Fact]
    public void 전부_닫히면_첫_화면으로_보낸다()
    {
        var tabs = Four(active: "/a");

        var next = tabs.CloseRight("/a");
        Assert.Null(next);

        next = tabs.Close("/a");

        Assert.Empty(tabs.Items);
        Assert.Equal("/", next);
        Assert.Null(tabs.ActiveHref);
    }
}
