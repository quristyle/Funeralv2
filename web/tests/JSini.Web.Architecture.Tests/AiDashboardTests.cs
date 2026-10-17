using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// AI 작업 대시보드(/projmng/ai/dashboard) 화면 요소 검증.
/// 오늘의 사용 토큰, 총사용시간 등 핵심 집계 항목이 누락되지 않도록 방지한다.
/// </summary>
public sealed class AiDashboardTests
{
    private const string DashboardRazor =
        "src/Apps/JSini.Web.ProjMng/Components/Pages/AiDashboard.razor";

    [Fact]
    public void 대시보드_요약_모델에_토큰과_사용시간_칸이_존재한다()
    {
        var summary = new AiDashboardSummary
        {
            TotalSeconds = 120,
            TodayTotalSeconds = 60,
            TodayTokens = 1500,
            PeriodTokens = 3000,
            TodayCalls = 10,
            PeriodCalls = 20,
        };

        Assert.Equal(120m, summary.TotalSeconds);
        Assert.Equal(60m, summary.TodayTotalSeconds);
        Assert.Equal(1500L, summary.TodayTokens);
        Assert.Equal(3000L, summary.PeriodTokens);
        Assert.Equal(10, summary.TodayCalls);
        Assert.Equal(20, summary.PeriodCalls);
    }

    [Fact]
    public void 대시보드_마크업에_총사용시간과_오늘_사용_토큰이_포함되어_있다()
    {
        var path = Path.Combine(SolutionRoot(), DashboardRazor.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"파일 없음: {path}");

        var html = RazorSource.Read(path);

        Assert.Contains("총사용시간", html);
        Assert.Contains("오늘 사용 토큰", html);
        Assert.Contains("오늘 사용시간", html);
    }

    /// <summary>
    /// 「작업지시 CLI 한도」 카드의 계량기는 갱신 시각 옆에 <b>지금부터 남은
    /// 시간</b>을 함께 적는다.
    /// </summary>
    /// <remarks>
    /// 절대 시각만 적어 두면 읽는 사람이 UTC → 한국 시각으로 옮겨 적힌 값에서
    /// 빼기를 해야 한다. 글자를 짓는 것은 <c>AiUsageText.Remain</c> 한 곳이고
    /// (<see cref="AiUsageTextTests"/>가 그 글자를 본다) 여기서는 <b>화면이
    /// 그것을 부르는지</b>만 본다 — 안 부르면 아무 오류 없이 그냥 안 보인다.
    /// </remarks>
    [Fact]
    public void 한도_계량기에_갱신까지_남은_시간이_붙는다()
    {
        var path = Path.Combine(SolutionRoot(), DashboardRazor.Replace('/', Path.DirectorySeparatorChar));
        var html = RazorSource.Read(path);

        Assert.Contains("작업지시 CLI 한도", html);
        Assert.Contains("pm-aidash__remain", html);
        Assert.Contains("AiUsageText.Remain", html);
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
