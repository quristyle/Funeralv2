using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>WBS 대시보드의 막대·사람×기간 판은 칸을 자료로 세운다. 합계 줄과 주 머리글이 원본(BarChart·HeatMatrix.vue)대로 나오는지 실제로 그려 본다.</summary>
public sealed class WbsBoardPartsRenderTests
{
    private static readonly WbsBoardUserBucketDto[] Weekly =
    [
        new() { UserBpId = "BP1", UserNm = "홍길동", Bucket = "2026-W33", WeekStart = "2026-08-10", WeekEnd = "2026-08-14", Cnt = 3, Done = 1 },
        new() { UserBpId = "BP1", UserNm = "홍길동", Bucket = "2026-W34", WeekStart = "2026-08-17", WeekEnd = "2026-08-21", Cnt = 2, Done = 0 },
        new() { UserBpId = "BP9", UserNm = "BP9", Bucket = "2026-W34", WeekStart = "2026-08-17", WeekEnd = "2026-08-21", Cnt = 4, Done = 4 },
    ];

    [Fact]
    public async Task 사람_기간_판은_주_머리글과_합계를_그린다()
    {
        var html = await RenderAsync<WbsHeatMatrix>(new()
        {
            [nameof(WbsHeatMatrix.Rows)] = Weekly,
            [nameof(WbsHeatMatrix.Weekly)] = true,
            [nameof(WbsHeatMatrix.ColHead)] = "주",
        });

        Assert.Contains("W33", html);
        Assert.Contains("08.14", html);          // 주 마지막 날(월.일)
        Assert.Contains("주 합계", html);
        Assert.Contains("✓5", html);             // 전체 완료 1 + 4
        Assert.Contains(">9", html);             // 전체 건수
        Assert.DoesNotContain("pm-heat__id\">BP9", html);   // 이름이 곧 아이디면 두 번 적지 않는다
        Assert.Contains("pm-heat__id\">BP1", html);
    }

    [Fact]
    public async Task 막대는_완료분을_겹쳐_그린다()
    {
        var html = await RenderAsync<WbsBarChart>(new()
        {
            [nameof(WbsBarChart.Items)] = new[] { new WbsBarItem("MM", "MM", 10, 4), new WbsBarItem("PD", "PD", 5, 0) },
            [nameof(WbsBarChart.Active)] = "MM",
        });

        Assert.Contains("width: 100%", html);
        Assert.Contains("width: 40%", html);
        Assert.Contains("width: 50%", html);
        Assert.Contains("✓4", html);
        Assert.Contains("is-on", html);
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
            System.Net.WebUtility.HtmlDecode(
                (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString()));
    }
}
