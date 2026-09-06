using JSini.Web.Admin.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 공지 본문 정제기.
///
/// <para>
/// 공지 본문만 <c>MarkupString</c> 으로 그린다(다른 화면은 태그를 걷어낸다).
/// 그 한 곳이 뚫리면 <b>로그인 전 화면을 포함해 모든 사용자</b>에게 그대로
/// 실린다 — 공지는 전 서비스 공용이다.
/// </para>
///
/// <para>
/// 눈으로는 확인할 수 없는 종류다. 정제가 틀려도 화면은 멀쩡히 보이고,
/// 잘못된 것이 도는 것은 브라우저 안이다.
/// </para>
/// </summary>
public sealed class NoticeHtmlTests
{
    [Theory]
    [InlineData("<p>안내드립니다.</p>")]
    [InlineData("<p><strong>굵게</strong> 와 <em>기울임</em></p>")]
    [InlineData("<ul><li>하나</li><li>둘</li></ul>")]
    [InlineData("<h2>제목</h2><p>본문</p>")]
    [InlineData("<blockquote><p>인용</p></blockquote>")]
    [InlineData("<table><tbody><tr><td colspan=\"2\">칸</td></tr></tbody></table>")]
    public void 서식은_그대로_남는다(string html) =>
        Assert.Equal(html, NoticeHtml.Sanitize(html));

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<style>body{display:none}</style>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<object data=\"x\"></object>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    public void 실행되는_태그는_안까지_사라진다(string html) =>
        Assert.Equal(string.Empty, NoticeHtml.Sanitize(html));

    [Fact]
    public void 이벤트_속성은_떨어진다() =>
        Assert.Equal(
            "<p>글</p>",
            NoticeHtml.Sanitize("<p onclick=\"alert(1)\" onmouseover=alert(2)>글</p>"));

    /// <summary>
    /// 브라우저는 <c>javascript</c> 와 <c>:</c> 사이의 공백·탭·줄바꿈을 무시한다.
    /// 그래서 시작 글자만 보고 판단하면 <c>java\tscript:</c> 가 통과한다.
    /// </summary>
    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">눌러</a>")]
    [InlineData("<a href=\"java\tscript:alert(1)\">눌러</a>")]
    [InlineData("<a href=\" JaVaScRiPt:alert(1)\">눌러</a>")]
    [InlineData("<a href=\"vbscript:msgbox(1)\">눌러</a>")]
    public void 링크_주소가_수상하면_주소만_떨어진다(string html) =>
        Assert.Equal("<a>눌러</a>", NoticeHtml.Sanitize(html));

    [Fact]
    public void 바깥으로_나가는_링크에는_rel_이_붙는다() =>
        Assert.Equal(
            "<a href=\"https://jin114.co.kr\" target=\"_blank\" rel=\"noopener noreferrer\">공지</a>",
            NoticeHtml.Sanitize("<a href=\"https://jin114.co.kr\" target=\"_blank\">공지</a>"));

    [Fact]
    public void 그림은_남기고_바깥으로_새는_style_은_뗀다()
    {
        Assert.Equal(
            "<img src=\"/api/file/download/id/abc\" alt=\"안내\" />",
            NoticeHtml.Sanitize("<img src=\"/api/file/download/id/abc\" alt=\"안내\">"));

        Assert.Equal(
            "<p>글</p>",
            NoticeHtml.Sanitize("<p style=\"background:url(https://evil.example/x)\">글</p>"));

        Assert.Equal(
            "<p style=\"text-align: center\">글</p>",
            NoticeHtml.Sanitize("<p style=\"text-align: center\">글</p>"));
    }

    /// <summary>
    /// 모르는 태그는 껍데기만 버리고 글자는 남긴다. 서식이 조금 빠지는 것이
    /// 문장이 통째로 사라지는 것보다 낫다.
    /// </summary>
    [Fact]
    public void 모르는_태그는_글자만_남는다() =>
        Assert.Equal("<p>중요한 안내</p>", NoticeHtml.Sanitize("<p><marquee>중요한 안내</marquee></p>"));

    /// <summary>
    /// 닫히지 않은 태그를 그대로 내보내면 <b>우리 레이아웃이 그 안에 먹힌다</b> —
    /// 공지 창을 닫아도 화면이 어긋난 채로 남는다.
    /// </summary>
    [Fact]
    public void 짝이_안_맞는_태그를_바로잡는다()
    {
        Assert.Equal("<div><p>글</p></div>", NoticeHtml.Sanitize("<div><p>글"));
        Assert.Equal("<p>글</p>", NoticeHtml.Sanitize("<p>글</p></div>"));
    }

    /// <summary>
    /// 태그가 아닌 부등호는 글자로 남겨야 한다. 그대로 두면 뒤에 오는 글자와
    /// 붙어 태그가 된다.
    /// </summary>
    [Fact]
    public void 태그가_아닌_부등호는_글자로_남는다() =>
        Assert.Equal("<p>가격 &lt; 100원</p>", NoticeHtml.Sanitize("<p>가격 < 100원</p>"));

    /// <summary>속성 값 안의 부등호를 태그 끝으로 읽으면 뒤가 통째로 어긋난다.</summary>
    [Fact]
    public void 속성_값_안의_부등호에_속지_않는다() =>
        Assert.Equal(
            "<p title=\"가 &gt; 나\">글</p>",
            NoticeHtml.Sanitize("<p title=\"가 > 나\">글</p>"));

    [Fact]
    public void 주석은_사라진다() =>
        Assert.Equal("<p>글</p>", NoticeHtml.Sanitize("<!-- 숨긴 말 --><p>글</p>"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 비어_있으면_빈_글자다(string? html) =>
        Assert.Equal(string.Empty, NoticeHtml.Sanitize(html));
}
