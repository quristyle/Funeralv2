using System.Net;
using System.Text;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>일정표의 실시간 반영. AI 일정 메모 팝업은 새 DI 스코프에서 저장하고, 다른 사람은 다른 회로에서 저장한다 —
/// 둘 다 WbsClient 가 WbsChangeFeed 에 알려야 열려 있는 일정표가 다시 읽는다. 끊겨도 화면은 멀쩡하고 「다른 달을 갔다 와야 보인다」로만 드러난다.</summary>
public sealed class WbsChangeFeedTests
{
    private const string SavedBody =
        """
        {"success":true,"code":"S000","message":"",
         "data":{"result":[{"wbsId":77,"prjRid":8,"procNm":"고객 회의","planSdt":"2026-11-07T10:00:00","planEdt":"2026-11-07T11:00:00"}],"page":{"total":1}}}
        """;

    private const string DeletedBody = """{"success":true,"code":"S000","message":"","data":{"result":[true],"page":{"total":1}}}""";

    [Fact]
    public async Task 다른_회로에서_등록한_일정이_소식으로_온다()
    {
        var feed = new WbsChangeFeed(NullLogger<WbsChangeFeed>.Instance);
        var popup = Client(feed);
        var scheduler = Client(feed);
        popup.ActingAs = ("kdh", "김대현");

        var heard = new List<WbsChange>();
        feed.Changed += heard.Add;

        await popup.CreateAsync(new WbsItemDto { PrjRid = 8, ProcNm = "고객 회의" });

        var change = Assert.Single(heard);
        Assert.Equal(WbsChangeKind.Created, change.Kind);
        Assert.Equal(77, change.WbsId);
        Assert.Equal(8, change.PrjRid);
        Assert.Equal("고객 회의", change.Title);
        Assert.Equal(new DateTime(2026, 11, 7, 10, 0, 0), change.Start);
        Assert.Equal("kdh", change.ActorLogin);
        Assert.Equal("김대현", change.ActorName);

        // 일정표는 Source 로 제가 저장한 것을 가려낸다 — 팝업의 것은 남의 것이어야 한다.
        Assert.Same(popup, change.Source);
        Assert.NotSame(scheduler, change.Source);
    }

    [Fact]
    public async Task 고치기와_지우기도_알린다()
    {
        var feed = new WbsChangeFeed(NullLogger<WbsChangeFeed>.Instance);
        var api = Client(feed);

        var heard = new List<WbsChange>();
        feed.Changed += heard.Add;

        await api.UpdateAsync(new WbsItemDto { WbsId = 77, PrjRid = 8 });
        await api.DeleteAsync(77);

        Assert.Equal([WbsChangeKind.Updated, WbsChangeKind.Deleted], heard.Select(c => c.Kind));
        Assert.All(heard, c => Assert.Equal(77, c.WbsId));
    }

    /// <summary>받는 쪽 하나(닫히는 중인 화면)가 던져도 저장한 사람에게 예외가 가지 않고 다른 일정표도 소식을 받는다.</summary>
    [Fact]
    public async Task 받는_쪽이_던져도_저장과_다른_받는_쪽은_멀쩡하다()
    {
        var feed = new WbsChangeFeed(NullLogger<WbsChangeFeed>.Instance);
        var api = Client(feed);

        var heard = 0;
        feed.Changed += _ => throw new InvalidOperationException("사라진 화면");
        feed.Changed += _ => heard++;

        var saved = await api.CreateAsync(new WbsItemDto { PrjRid = 8 });

        Assert.NotNull(saved);
        Assert.Equal(1, heard);
    }

    [Fact]
    public void 일정표가_소식을_듣고_닫힐_때_놓는다()
    {
        var page = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Pages", "ProjectScheduler.razor"));

        Assert.Contains("Feed.Changed += OnWbsChanged", page, StringComparison.Ordinal);
        Assert.Contains("Feed.Changed -= OnWbsChanged", page, StringComparison.Ordinal);
    }

    /// <summary>팝업은 새 스코프에서 저장하므로 그 스코프의 CurrentUser 는 비어 있다. 바꾼 사람을 대신 적지 않으면 일정표가 「다른 사용자가」로 말한다.</summary>
    [Fact]
    public void AI_일정_메모_팝업이_바꾼_사람을_적는다()
    {
        var popup = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "ScheduleNotePopup.razor.cs"));

        Assert.Contains("Api.ActingAs =", popup, StringComparison.Ordinal);
    }

    private static WbsClient Client(WbsChangeFeed feed)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://localhost/api/") };
        var gateway = new GatewayClient(http);

        return new WbsClient(gateway, feed, new CurrentUser(gateway, NullLogger<CurrentUser>.Instance));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.Method == HttpMethod.Delete ? DeletedBody : SavedBody, Encoding.UTF8, "application/json"),
            });
    }

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
