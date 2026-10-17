using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 로그 탭의 연속 반복 구간 축약 및 반복 횟수 뱃지 표시 규칙 검증.
/// </summary>
public sealed class LogRepeatCollapseTests
{
    [Fact]
    public void 연속으로_동일한_로그는_반복횟수와_함께_한줄로_축약된다()
    {
        var input = new List<string>
        {
            "[INF] Service starting...",
            "[INF] Service starting...",
            "[INF] Service starting...",
            "[INF] Service started successfully.",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(2, actual.Count);
        Assert.Equal("(3) [INF] Service starting...", actual[0]);
        Assert.Equal("[INF] Service started successfully.", actual[1]);
    }

    [Fact]
    public void 중간중간_다른_로그가_끼어있으면_각_구간별로_따로_축약된다()
    {
        var input = new List<string>
        {
            "Building A",
            "Building A",
            "Building B",
            "Waiting for lock",
            "Waiting for lock",
            "Waiting for lock",
            "Building A", // 다시 나온 A는 이전 구간과 합쳐지지 않고 독립적으로 표시
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        var expected = new List<string>
        {
            "(2) Building A",
            "Building B",
            "(3) Waiting for lock",
            "Building A",
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 반복되지_않는_단일_로그는_접두사_없이_그대로_유지된다()
    {
        var input = new List<string> { "Step 1", "Step 2", "Step 3" };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(input, actual);
    }

    [Fact]
    public void 빈_줄이_연속으로_반복되어도_반복횟수가_표시된다()
    {
        var input = new List<string> { "", "", "", "Next line" };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(2, actual.Count);
        Assert.Equal("(3)", actual[0]);
        Assert.Equal("Next line", actual[1]);
    }

    [Fact]
    public void 빈_목록이나_단일_항목도_안전하게_처리된다()
    {
        Assert.Empty(LogTranslateView.CollapseRepeatsList([]));

        var single = LogTranslateView.CollapseRepeatsList(["Only one"]);
        Assert.Single(single);
        Assert.Equal("Only one", single[0]);
    }

    [Theory]
    [InlineData("(3) [INFO] Starting", true, 3, "[INFO] Starting")]
    [InlineData("(15) Download progress", true, 15, "Download progress")]
    [InlineData("(5)", true, 5, "")]
    [InlineData("Normal log message", false, 1, "Normal log message")]
    [InlineData("(1) Not a repeat", false, 1, "(1) Not a repeat")]
    [InlineData("(INFO) Bracket tag", false, 1, "(INFO) Bracket tag")]
    [InlineData("", false, 1, "")]
    public void TryGetRepeat_반복_포맷이_정상적으로_파싱된다(
        string line, bool expectedSuccess, int expectedCount, string expectedRaw)
    {
        var success = LogTranslateView.TryGetRepeat(line, out var count, out var raw);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedRaw, raw);
    }

    [Theory]
    [InlineData("Single line", 1, "Single line")]
    [InlineData("Repeated line", 4, "(4) Repeated line")]
    [InlineData("", 1, "")]
    [InlineData("", 5, "(5)")]
    public void FormatRepeatLine_횟수에_따라_올바르게_포맷된다(string line, int count, string expected)
    {
        var actual = LogTranslateView.FormatRepeatLine(line, count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AiTaskLogTab은_LogTranslateView에_CollapseRepeats_참을_전달한다()
    {
        var root = WorkspaceRoot();
        var tabPath = Path.Combine(root, "web", "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiTaskLogTab.razor");

        Assert.True(File.Exists(tabPath), $"AiTaskLogTab.razor 파일이 존재해야 합니다: {tabPath}");

        var content = File.ReadAllText(tabPath);
        Assert.Contains("CollapseRepeats=\"true\"", content);
    }

    private static string WorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
