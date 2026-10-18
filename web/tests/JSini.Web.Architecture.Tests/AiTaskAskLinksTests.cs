using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 지시 상세의 지시문에서 <b>화면 주소만</b> 링크로 가른다. 파일·API 경로까지 걸면
/// 눌렀을 때 「준비 중」이나 빈 화면으로 간다.
/// </summary>
public sealed class AiTaskAskLinksTests
{
    private static readonly string[] Templates =
    [
        "/helpdesk/monitor/maintenance",
        "/projmng/ai/task/{TaskKey:long}",
        "/admin/system/account/{LoginId}/app",
    ];

    private static bool IsScreen(string path) => AiTaskAskLinks.Matches(path, Templates);

    [Fact]
    public void 화면_주소는_글자_그대로_링크가_된다()
    {
        var pieces = AiTaskAskLinks.Split("/helpdesk/monitor/maintenance 화면의 조회부의 ...", IsScreen);

        Assert.Equal(2, pieces.Count);
        Assert.Equal("/helpdesk/monitor/maintenance", pieces[0].Href);
        Assert.Equal("/helpdesk/monitor/maintenance", pieces[0].Text);
        Assert.Equal(" 화면의 조회부의 ...", pieces[1].Text);
        Assert.Null(pieces[1].Href);
    }

    [Fact]
    public void 한글이_붙거나_문장_끝_마침표가_와도_주소만_잘라낸다()
    {
        var pieces = AiTaskAskLinks.Split("먼저 /helpdesk/monitor/maintenance화면, 그리고 /admin/system/account/lee/app.", IsScreen);

        Assert.Equal(["/helpdesk/monitor/maintenance", "/admin/system/account/lee/app"],
            pieces.Where(p => p.Href is not null).Select(p => p.Href!));
        Assert.Equal("먼저 /helpdesk/monitor/maintenance화면, 그리고 /admin/system/account/lee/app.",
            string.Concat(pieces.Select(p => p.Text)));
    }

    [Fact]
    public void 매개변수_자리는_채운_값을_받는다()
    {
        Assert.True(IsScreen("/projmng/ai/task/89"));
        Assert.False(IsScreen("/projmng/ai/task"));
        Assert.False(IsScreen("/projmng/ai/task/abc"));
    }

    [Fact]
    public void 화면이_아닌_경로와_바깥_주소는_글자로_남는다()
    {
        const string text = "/srv/jsini/.env 와 /api/notification/x, https://portal.jsini.co.kr/helpdesk/monitor/maintenance";
        var pieces = AiTaskAskLinks.Split(text, IsScreen);

        Assert.All(pieces, p => Assert.Null(p.Href));
        Assert.Equal(text, string.Concat(pieces.Select(p => p.Text)));
    }
}
