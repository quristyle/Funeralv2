using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AIAgentServer.Services.Portal;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// search_schedules 가 사람·날짜 조건에 걸려 「없다」가 되던 것. 운영 감사 기록 56·58 — 모델이 묻는 사람
/// 아이디(quristyle)를 붙여 불러서, 박민성 담당인 공용 일정 「똥싸기」가 빠졌다.
/// </summary>
public class PortalToolRunnerScheduleTests
{
    private const string Hit = """{"total":1,"items":[{"source":"projmng","title":"똥싸기","startDate":"2026-11-10","endDate":"2026-11-10","done":false,"owner":"박민성","state":"READY","role":null,"note":"똥을 쌘다.","project":"수빅자재모듈","scheduleType":"Public"}]}""";
    private const string Empty = """{"total":0,"items":[]}""";

    /// <summary>프로젝트관리 대역 — 사람·날짜 조건이 걸리면 0건, 낱말만이면 그 한 건.</summary>
    private sealed class ProjMngStub(List<string> calls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            calls.Add(request.RequestUri!.Query);
            var q = QueryHelpers.ParseQuery(request.RequestUri.Query);
            var narrowed = q.ContainsKey("userId") || q.ContainsKey("name") || q.ContainsKey("from") || q.ContainsKey("to");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(narrowed ? Empty : Hit) });
        }
    }

    private sealed class EmptyStub(List<string> calls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            calls.Add(request.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Empty) });
        }
    }

    private static (PortalToolRunner Runner, List<string> ProjMng, List<string> HelpDesk) Runner()
    {
        var config = new ConfigurationBuilder().Build();
        List<string> projMng = [], helpDesk = [];
        var runner = new PortalToolRunner(
            null!,
            new HelpDeskGateway(new HttpClient(new EmptyStub(helpDesk)) { BaseAddress = new Uri("http://hd") }, config),
            new ProjMngGateway(new HttpClient(new ProjMngStub(projMng)) { BaseAddress = new Uri("http://pm") }, config),
            null!, null!, null!, null!, config, NullLogger<PortalToolRunner>.Instance);
        return (runner, projMng, helpDesk);
    }

    [Fact]
    public async Task 사람_조건에_걸려_없으면_빼고_낱말로만_다시_찾는다()
    {
        var (runner, projMng, _) = Runner();

        var text = await runner.CallAsync("search_schedules",
            new Dictionary<string, object?> { ["userId"] = "quristyle", ["name"] = "이종필", ["keyword"] = "똥싸기" }, null);

        Assert.Contains("똥싸기", text);
        Assert.Contains("조건을 빼고", text);
        Assert.Equal(2, projMng.Count);
    }

    [Fact]
    public async Task 날짜_조건에_걸려도_다시_찾는다()
    {
        var (runner, _, _) = Runner();

        var text = await runner.CallAsync("search_schedules",
            new Dictionary<string, object?> { ["from"] = "2026-11-05", ["to"] = "2026-11-05", ["keyword"] = "똥싸기" }, null);

        Assert.Contains("2026-11-10", text);
        Assert.Contains("조건을 빼고", text);
    }

    /// <summary>낱말 없이 「내 일정」만 물었으면 남의 일정을 끌어오지 않는다.</summary>
    [Fact]
    public async Task 낱말이_없으면_다시_찾지_않는다()
    {
        var (runner, projMng, _) = Runner();

        var text = await runner.CallAsync("search_schedules",
            new Dictionary<string, object?> { ["userId"] = "quristyle", ["name"] = "이종필" }, null);

        Assert.DoesNotContain("똥싸기", text);
        Assert.Single(projMng);
    }

    [Fact]
    public async Task 조건이_없으면_한_번만_부르고_표시도_없다()
    {
        var (runner, projMng, _) = Runner();

        var text = await runner.CallAsync("search_schedules",
            new Dictionary<string, object?> { ["keyword"] = "똥싸기" }, null);

        Assert.Contains("똥싸기", text);
        Assert.DoesNotContain("조건을 빼고", text);
        Assert.Single(projMng);
    }

    /// <summary>「|」 로 이은 다른 말은 프로젝트관리만 안다 — 헬프데스크의 ILIKE 에 통째로 넘기면 늘 0건이다.</summary>
    [Fact]
    public async Task 헬프데스크에는_첫_말만_넘긴다()
    {
        var (runner, projMng, helpDesk) = Runner();

        await runner.CallAsync("search_schedules",
            new Dictionary<string, object?> { ["keyword"] = "똥싸기|배변" }, null);

        Assert.Equal("똥싸기", QueryHelpers.ParseQuery(Assert.Single(helpDesk))["keyword"].ToString());
        Assert.Equal("똥싸기|배변", QueryHelpers.ParseQuery(Assert.Single(projMng))["keyword"].ToString());
    }
}
