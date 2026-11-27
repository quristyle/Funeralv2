using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 공지 본문 정제기.
///
/// <summary>공지 본문 정제기. 공지 본문만 MarkupString 으로 그린다. 그 한 곳이 뚫리면 로그인 전 화면을 포함한 모든 사용자에게 실리고,
/// 정제가 틀려도 화면은 멀쩡해 눈으로는 확인할 수 없다.</summary>
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

    /// <summary>브라우저는 javascript 와 : 사이의 공백·탭·줄바꿈을 무시한다 — 시작 글자만 보고 판단하면 java\tscript: 가 통과한다.</summary>
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

    /// <summary>고객이 쓴 글이 담당자 화면을 가짜 로그인 창으로 덮는 길. 위치·겹침 속성은 목록에 없고, 이스케이프로 비켜 가도 글자 검사에서 떨어진다.</summary>
    [Theory]
    [InlineData("<div style=\"position:fixed;inset:0;z-index:99999;background:#fff\">다시 로그인</div>")]
    [InlineData("<div style=\"position: absolute; top: 0; left: 0\">다시 로그인</div>")]
    [InlineData("<div style=\"pos\\69 tion:fixed\">다시 로그인</div>")]
    [InlineData("<div style=\"background:\\75 rl(https://evil.example/x)\">다시 로그인</div>")]
    [InlineData("<div style=\"display:block;transform:translateY(-200px)\">다시 로그인</div>")]
    [InlineData("<div style=\"margin-top:-500px\">다시 로그인</div>")]
    [InlineData("<div style=\"color:red !important\">다시 로그인</div>")]
    [InlineData("<div style=\"color:&#x72;ed\">다시 로그인</div>")]
    [InlineData("<div style=\"width:calc(100vw)\">다시 로그인</div>")]
    public void 화면을_덮는_style_은_떨어진다(string html) =>
        Assert.Equal("<div>다시 로그인</div>", NoticeHtml.Sanitize(html));

    [Fact]
    public void 서식_style_은_남고_섞인_위험한_선언만_빠진다()
    {
        Assert.Equal(
            "<span style=\"color: rgb(230, 0, 0); font-weight: bold\">글</span>",
            NoticeHtml.Sanitize("<span style=\"color: rgb(230, 0, 0); position: fixed; font-weight: bold\">글</span>"));

        Assert.Equal(
            "<blockquote style=\"margin: 0; border-left: 1px solid #94a3b8; padding: 2px 14px\">요약</blockquote>",
            NoticeHtml.Sanitize("<blockquote style=\"margin:0;border-left:1px solid #94a3b8;padding:2px 14px;\">요약</blockquote>"));
    }

    /// <summary>포털 CSS 에 이미 있는 이름(position-fixed · modal)으로는 style 없이도 화면을 덮는다. 편집기 것과 요약 상자만 받는다.</summary>
    [Fact]
    public void class_는_편집기_것만_받는다()
    {
        Assert.Equal("<div>글</div>", NoticeHtml.Sanitize("<div class=\"position-fixed top-0 start-0 w-100 h-100 modal\">글</div>"));
        Assert.Equal(
            "<p class=\"ql-align-center ql-indent-1\">글</p>",
            NoticeHtml.Sanitize("<p class=\"ql-align-center fixed-top ql-indent-1\">글</p>"));
        Assert.Equal(
            "<blockquote class=\"hd-summary\">요약</blockquote>",
            NoticeHtml.Sanitize("<blockquote class=\"hd-summary\">요약</blockquote>"));
    }

    /// <summary>모르는 태그는 껍데기만 버리고 글자는 남긴다 — 서식이 조금 빠지는 것이 문장이 통째로 사라지는 것보다 낫다.</summary>
    [Fact]
    public void 모르는_태그는_글자만_남는다() =>
        Assert.Equal("<p>중요한 안내</p>", NoticeHtml.Sanitize("<p><marquee>중요한 안내</marquee></p>"));

    /// <summary>닫히지 않은 태그를 그대로 내보내면 우리 레이아웃이 그 안에 먹힌다 — 공지 창을 닫아도 화면이 어긋난 채로 남는다.</summary>
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

    /// <summary>
    /// Vue 시절 본문에 박힌 <c>/api/file/…</c> 는 포털 오리진에 없다.
    /// 저장된 값을 고치지 않고 보여 줄 때만 셸의 중계 경로로 옮긴다.
    /// </summary>
    [Theory]
    [InlineData("<p><img src=\"/api/file/download/79b266d9-16ff-49f9-809d-0957f8705ac6\" /></p>")]
    [InlineData("<p><img src=\"/api/file/download/id/79b266d9-16ff-49f9-809d-0957f8705ac6\" /></p>")]
    [InlineData("<p><img src=\"/api/file/thumbnail/79b266d9-16ff-49f9-809d-0957f8705ac6\" /></p>")]
    public void 본문의_옛_파일_주소를_중계_경로로_옮긴다(string html) =>
        Assert.Equal(
            "<p><img src=\"/files/79b266d9-16ff-49f9-809d-0957f8705ac6\" /></p>",
            NoticeHtml.Sanitize(html));

    [Fact]
    public void 그림_주소_변환과_지연_로딩을_선택할_수_있다() =>
        Assert.Equal(
            "<p><img src=\"/files/thumbnail/79b266d9-16ff-49f9-809d-0957f8705ac6\" loading=\"lazy\" /></p>",
            NoticeHtml.Sanitize(
                "<p><img src=\"/api/file/download/79b266d9-16ff-49f9-809d-0957f8705ac6\"></p>",
                source => source.Replace("/files/", "/files/thumbnail/", StringComparison.Ordinal),
                lazyLoadImages: true));

    /// <summary>파일 주소가 아닌 것은 그대로 둔다.</summary>
    [Fact]
    public void 파일_주소가_아니면_건드리지_않는다() =>
        Assert.Equal(
            "<p><a href=\"https://jin114.co.kr/api/file/download/x\">밖</a></p>",
            NoticeHtml.Sanitize("<p><a href=\"https://jin114.co.kr/api/file/download/x\">밖</a></p>"));

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
