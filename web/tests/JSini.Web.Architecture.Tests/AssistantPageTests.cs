using System.Linq;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 업무 비서 화면(<c>/admin/ai/assistant</c>)의 본문 대조. <c>AiAskLayoutTests.cs</c> 의
/// 본문 대조 방식을 따른다.
/// </summary>
public sealed class AssistantPageTests
{
    [Fact]
    public void 화면_경로와_라우트키가_짝이다()
    {
        var razor = PageRazor();
        Assert.Contains("@page \"/admin/ai/assistant\"", razor, StringComparison.Ordinal);
        Assert.Contains("[RouteKey(\"admin.ai.assistant\")]", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void 도구_진행_안내를_그리는_자리가_있다()
        => Assert.Contains("\"tool\"", PageCode(), StringComparison.Ordinal);

    [Fact]
    public void 자료_범위_고르개가_있다()
        => Assert.Contains("assistant__scope", PageRazor(), StringComparison.Ordinal);

    /// <summary>
    /// 리뷰에서 재현된 Critical — <c>#9999999999</c> 처럼 11자리 이상(또는 int 범위를 넘는
    /// 10자리)이 들어오면 옛 정규식(<c>#(\d+)</c>)은 그대로 걸려 <c>int.Parse</c> 가
    /// <see cref="OverflowException"/> 을 던졌다. 렌더링 도중 도는 이터레이터에서 예외가
    /// 새면 Blazor 회로가 끊겨 대화 전체를 잃는다 — 호출 자체가 던지지 않는 것만으로도
    /// 이 시험은 그 결함을 잡는다.
    /// </summary>
    [Theory]
    [InlineData("#9999999999")]
    [InlineData("#12345678901")]
    [InlineData("#2024102500123")]
    public void 아주_긴_숫자는_예외_없이_그냥_글자로_남는다(string text)
    {
        var segments = JSini.Web.Admin.Components.Pages.Assistant.AnswerSegments(text).ToList();

        Assert.All(segments, s => Assert.Null(s.RequestId));
        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
    }

    /// <summary>
    /// 자릿수만 맞을 뿐 글 번호가 아닌 자리는 링크가 안 된다 — "매출 순위 #1" · "#000000" ·
    /// 마크다운 제목 꾸밈("##3단계") · 띄어 쓴 "# 3단계". 반대로 진짜 요청글 번호 모양
    /// ("#1042")은 걸려야 한다.
    /// </summary>
    [Theory]
    [InlineData("매출 순위 #1", null)]
    [InlineData("#000000", null)]
    [InlineData("##3단계", null)]
    [InlineData("# 3단계", null)]
    [InlineData("요청글 #1042 확인", 1042)]
    public void 그럴듯한_자리만_요청글_번호로_링크된다(string text, int? expectedId)
    {
        var segments = JSini.Web.Admin.Components.Pages.Assistant.AnswerSegments(text).ToList();
        var linkedIds = segments.Where(s => s.RequestId is not null).Select(s => s.RequestId).ToList();

        if (expectedId is null)
        {
            Assert.Empty(linkedIds);
        }
        else
        {
            Assert.Equal([expectedId], linkedIds);
        }

        // 링크가 안 되어도 글자 자체는 남아야 한다 — "#000000" 같은 조각을 통째로 삼키면
        // 답 글이 눈앞에서 사라진 것처럼 보인다.
        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
    }

    private static string PageRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "Assistant.razor"));

    private static string PageCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "Assistant.razor.cs"));

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
