using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>
/// 결과 메일 본문에 <b>처리 요약이 실리는지</b>를 확인한다.
/// </summary>
/// 제보는 「요약이 빠진 채 온다」였지만 값은 내내 실려 있었다 — 빠진 것은 자리(좁은 칸에 접혀 곁줄로 읽혔다)였다. 그래서 실렸나와 앞에 섰나를 함께 묶는다.
public class AiTaskMailBodyTests
{
    /// <summary>
    /// 본문을 그려 보는 자리. <b>DB 도 네트워크도 타지 않는다</b> —
    /// <c>Body()</c> 가 쓰는 것은 넘겨받은 줄과 설정 두 값뿐이다.
    /// </summary>
    private static string Render(AiTaskNotifier.MailRow row, AiResultSummary? summary)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiTasks:PortalUrl"] = "https://portal.example",
            })
            .Build();

        // IHttpClientFactory 는 이 길에서 쓰이지 않는다.
        var notifier = new AiTaskNotifier(config, null!, NullLogger<AiTaskNotifier>.Instance);

        return notifier.Body(row, summary);
    }

    private static AiTaskNotifier.MailRow Row(string? instruction = null, string? resultText = null) => new()
    {
        TaskKey = 1,
        Title = "로그 탭 스크롤 개선",
        TaskStatus = "succeeded",
        Seq = 1,
        TargetNm = "portal-jsini",
        Instruction = instruction ?? "휴대폰 로그 탭이 제 안에서 구르는 것을 걷어라.",
        ResultText = resultText ?? "먼저 어디서 구르는지 찾아보겠습니다.\n…\n다 고쳤습니다.",
    };

    private static readonly AiResultSummary Summary = new(
        "모바일 로그 탭의 내부 스크롤을 제거하고 브라우저 스크롤로 전환했습니다",
        ["로그 목록 높이를 내용에 맞춰 자동 조정", "내부 스크롤 상자를 제거"],
        ["배포 뒤 휴대폰에서 다시 확인"]);

    /// <summary>요약의 세 토막이 <b>하나도 빠짐없이</b> 본문에 있다.</summary>
    [Fact]
    public void 처리요약의_결론과_한일과_확인할것이_모두_실린다()
    {
        var html = Render(Row(), Summary);

        Assert.Contains("처리 요약", html, StringComparison.Ordinal);
        Assert.Contains(Summary.Headline, html, StringComparison.Ordinal);

        foreach (var point in Summary.Points)
        {
            Assert.Contains(point, html, StringComparison.Ordinal);
        }

        Assert.Contains("확인할 것", html, StringComparison.Ordinal);

        foreach (var check in Summary.Checks)
        {
            Assert.Contains(check, html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>요약이 지시문보다 먼저 선다.</b> 지시문 세 줄 아래 깔리면 값이 있어도 안 읽힌다.
    /// </summary>
    [Fact]
    public void 처리요약이_시킨것보다_앞에_선다()
    {
        var html = Render(Row(), Summary);

        var summaryAt = html.IndexOf("처리 요약", StringComparison.Ordinal);
        var glanceAt = html.IndexOf("한눈에", StringComparison.Ordinal);

        Assert.True(summaryAt >= 0 && glanceAt >= 0);
        Assert.True(summaryAt < glanceAt, "처리 요약이 「한눈에」(시킨 것·바뀐 것)보다 뒤에 섰습니다.");
    }

    /// <summary>
    /// <b>요약이 라벨 칸 뒤에 접히지 않는다.</b> 결론은 라벨 뒤 좁은 칸이 아니라 요약 자리에 선다.
    /// </summary>
    [Fact]
    public void 요약칸에는_AI의답_라벨이_남아_있지_않다()
    {
        var html = Render(Row(), Summary);

        var summaryAt = html.IndexOf("처리 요약", StringComparison.Ordinal);
        var rawAt = html.IndexOf("AI 의 답", StringComparison.Ordinal);

        Assert.True(rawAt < 0 || rawAt > summaryAt,
            "「AI 의 답」 라벨이 처리 요약보다 앞에 있습니다 — 옛 요약 표가 남았습니다.");
    }

    /// <summary>
    /// 요약을 못 받은 건에도 <b>칸은 선다.</b> 빈 칸을 그리면 그것이야말로
    /// 「요약이 빠졌다」로 읽힌다.
    /// </summary>
    [Fact]
    public void 요약이_없으면_까닭을_적고_결과문_앞부분을_싣는다()
    {
        var html = Render(Row(resultText: "첫 줄입니다.\n둘째 줄입니다."), summary: null);

        Assert.Contains("처리 요약", html, StringComparison.Ordinal);
        Assert.Contains("요약을 받지 못했습니다", html, StringComparison.Ordinal);
        Assert.Contains("첫 줄입니다.", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 답조차 없는 건은 <b>그 사실을 적는다.</b> 빈 메일은 메일 사고로 읽힌다.
    /// </summary>
    [Fact]
    public void 답이_아예_없으면_그_사실을_적는다()
    {
        var html = Render(Row(resultText: string.Empty), summary: null);

        Assert.Contains("아무 말 없이 끝났습니다", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// AI 의 답에 섞여 들어온 태그가 <b>보고서를 무너뜨리지 않는다.</b>
    /// 마스킹 → 이스케이프 순서를 지키는지 보는 자리다.
    /// </summary>
    [Fact]
    public void 요약에_섞인_태그는_글자로_나간다()
    {
        var summary = new AiResultSummary(
            "<div> 를 지웠습니다", ["<style> 블록 제거"], []);

        var html = Render(Row(), summary);

        Assert.Contains("&lt;div&gt; 를 지웠습니다", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<div> 를 지웠습니다", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 이어가기 건의 「시킨 것」은 이번에 시킨 것이다 — 지난 회차의 머리말이 아니다.
    /// </summary>
    [Fact]
    public void 이어가기_건은_이번_지시만_시킨것에_뜬다()
    {
        var html = Render(
            Row(instruction: "## 이전까지의 진행\n지난 회차에서 색띠를 깔았다.\n\n## 이번에 할 일\n메일에 요약을 담아라."),
            Summary);

        var glanceAt = html.IndexOf("한눈에", StringComparison.Ordinal);
        var glance = html[glanceAt..html.IndexOf("전문", glanceAt, StringComparison.Ordinal)];

        Assert.Contains("메일에 요약을 담아라.", glance, StringComparison.Ordinal);
        Assert.DoesNotContain("지난 회차에서 색띠를 깔았다.", glance, StringComparison.Ordinal);
    }
}
