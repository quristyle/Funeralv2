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
    public void 두_줄_패턴이_연속으로_반복되면_묶음은_한번만_표시되고_첫줄에_수치가_표시된다()
    {
        var input = new List<string>
        {
            "Step 1: Download package",
            "Step 2: Verify checksum",
            "Step 1: Download package",
            "Step 2: Verify checksum",
            "Step 1: Download package",
            "Step 2: Verify checksum",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(2, actual.Count);
        Assert.Equal("(3) Step 1: Download package", actual[0]);
        Assert.Equal("Step 2: Verify checksum", actual[1]);
    }

    [Fact]
    public void 세_줄_패턴이_연속으로_반복되면_묶음은_한번만_표시되고_첫줄에_수치가_표시된다()
    {
        var input = new List<string>
        {
            "Checking database...",
            "Connecting to replica...",
            "Connection established.",
            "Checking database...",
            "Connecting to replica...",
            "Connection established.",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(3, actual.Count);
        Assert.Equal("(2) Checking database...", actual[0]);
        Assert.Equal("Connecting to replica...", actual[1]);
        Assert.Equal("Connection established.", actual[2]);
    }

    [Fact]
    public void 네_줄_패턴이_연속으로_반복되면_묶음은_한번만_표시되고_첫줄에_수치가_표시된다()
    {
        var input = new List<string>
        {
            "[1/4] Pre-check",
            "[2/4] Compilation",
            "[3/4] Packaging",
            "[4/4] Deployment",
            "[1/4] Pre-check",
            "[2/4] Compilation",
            "[3/4] Packaging",
            "[4/4] Deployment",
            "[1/4] Pre-check",
            "[2/4] Compilation",
            "[3/4] Packaging",
            "[4/4] Deployment",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(4, actual.Count);
        Assert.Equal("(3) [1/4] Pre-check", actual[0]);
        Assert.Equal("[2/4] Compilation", actual[1]);
        Assert.Equal("[3/4] Packaging", actual[2]);
        Assert.Equal("[4/4] Deployment", actual[3]);
    }

    [Fact]
    public void 다섯_줄_패턴이_연속으로_반복되면_묶음은_한번만_표시되고_첫줄에_수치가_표시된다()
    {
        var input = new List<string>
        {
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5",
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(5, actual.Count);
        Assert.Equal("(2) Line 1", actual[0]);
        Assert.Equal("Line 2", actual[1]);
        Assert.Equal("Line 3", actual[2]);
        Assert.Equal("Line 4", actual[3]);
        Assert.Equal("Line 5", actual[4]);
    }

    [Fact]
    public void 여섯_줄_패턴이_연속으로_반복되면_최대_10줄_범위_내이므로_정상적으로_축약된다()
    {
        var input = new List<string>
        {
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "Line 6",
            "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "Line 6",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(6, actual.Count);
        Assert.Equal("(2) Line 1", actual[0]);
        Assert.Equal("Line 2", actual[1]);
        Assert.Equal("Line 3", actual[2]);
        Assert.Equal("Line 4", actual[3]);
        Assert.Equal("Line 5", actual[4]);
        Assert.Equal("Line 6", actual[5]);
    }

    [Fact]
    public void 열_줄_패턴이_연속으로_반복되면_최대_10줄_범위_내이므로_정상적으로_축약된다()
    {
        var pattern = Enumerable.Range(1, 10).Select(n => $"Line {n}").ToList();
        var input = pattern.Concat(pattern).ToList();

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(10, actual.Count);
        Assert.Equal("(2) Line 1", actual[0]);
        for (var i = 1; i < 10; i++)
        {
            Assert.Equal($"Line {i + 1}", actual[i]);
        }
    }

    [Fact]
    public void 열한_줄_이상의_패턴은_최대_10줄_제한으로_인해_축약되지_않고_그대로_유지된다()
    {
        var pattern = Enumerable.Range(1, 11).Select(n => $"Line {n}").ToList();
        var input = pattern.Concat(pattern).ToList();

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(22, actual.Count);
        Assert.Equal(input, actual);
    }

    [Fact]
    public void 세_줄_패턴_앞에_첫_줄이_중복되어도_세_줄_패턴이_선점당하지_않고_올바르게_축약된다()
    {
        var input = new List<string>
        {
            "Connecting...",
            "Connecting...",
            "Querying...",
            "Done.",
            "Connecting...",
            "Querying...",
            "Done.",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        var expected = new List<string>
        {
            "Connecting...",
            "(2) Connecting...",
            "Querying...",
            "Done.",
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 단일줄_반복과_여러줄_반복이_섞여있는_경우_각각_올바르게_축약된다()
    {
        var input = new List<string>
        {
            "Initial setup",
            "Initial setup",
            "Initial setup",
            "Ping",
            "Pong",
            "Ping",
            "Pong",
            "All completed.",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        var expected = new List<string>
        {
            "(3) Initial setup",
            "(2) Ping",
            "Pong",
            "All completed.",
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 내부에_중복된_줄을_포함하는_묶음_패턴도_전체_묶음으로_우선_축약된다()
    {
        var input = new List<string>
        {
            "A", "A", "B",
            "A", "A", "B",
            "A", "A", "B",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(3, actual.Count);
        Assert.Equal("(3) A", actual[0]);
        Assert.Equal("A", actual[1]);
        Assert.Equal("B", actual[2]);
    }

    [Fact]
    public void 반복_도중_불완전한_패턴이_뒤에_남으면_완전한_반복만_묶고_나머지는_그대로_유지된다()
    {
        var input = new List<string>
        {
            "Ping",
            "Pong",
            "Ping",
            "Pong",
            "Ping", // 2회 반복 후 마지막에 Ping만 남음
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        var expected = new List<string>
        {
            "(2) Ping",
            "Pong",
            "Ping",
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 빈_줄을_포함하는_여러줄_패턴도_정상적으로_축약된다()
    {
        var input = new List<string>
        {
            "Header",
            "",
            "Header",
            "",
        };

        var actual = LogTranslateView.CollapseRepeatsList(input);

        Assert.Equal(2, actual.Count);
        Assert.Equal("(2) Header", actual[0]);
        Assert.Equal("", actual[1]);
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
