using System;
using System.Collections.Generic;
using System.Text;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 헬프데스크 요청 본문의 그림 주소 다루기.
///
/// <para>
/// 요청 본문은 서식 편집기로 쓴 HTML 이고 그 안에 붙여넣은 그림이 박혀 있다.
/// 주소가 <b>저장할 때와 보여 줄 때 서로 다른 모양</b>이라(정본
/// <c>/api/file/download/id/{guid}</c> ↔ 셸 중계 <c>/files/{guid}</c>),
/// 옮기는 자리가 틀리면 증상이 늘 <b>깨진 네모 한 장</b>이다 — 글은 멀쩡하고
/// 저장도 성공했다고 나오므로 원인이 보이지 않는다.
/// </para>
///
/// <para>
/// base64 가 하나라도 빠져나가면 수 MB 짜리 본문이 DB 로 들어가고, 그 뒤로
/// 목록·검색·메일이 계속 무거워진다. 그쪽도 여기서 막는다.
/// </para>
/// </summary>
public sealed class RequestContentHtmlTests
{
    private const string Guid1 = "11111111-2222-3333-4444-555555555555";
    private const string Guid2 = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    /// <summary>1x1 투명 PNG. 진짜 그림이라 base64 해독이 실제로 돈다.</summary>
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    // ── 저장할 모양으로 되돌리기 ─────────────────────────

    [Fact]
    public void 셸_중계_주소는_백엔드_정본으로_되돌아간다()
    {
        var html = $"""<p>이렇게 됩니다</p><img src="/files/{Guid1}">""";

        Assert.Equal(
            $"""<p>이렇게 됩니다</p><img src="/api/file/download/id/{Guid1}">""",
            RequestContentHtml.ToStored(html));
    }

    [Fact]
    public void 이름이_붙은_중계_주소도_되돌아간다()
    {
        var html = $"""<img src="/files/{Guid1}?name=%EC%BA%A1%EC%B2%98.png">""";

        Assert.Equal(
            $"""<img src="/api/file/download/id/{Guid1}">""",
            RequestContentHtml.ToStored(html));
    }

    [Fact]
    public void 이미_정본인_주소는_그대로_둔다()
    {
        var html = $"""<img src="/api/file/download/id/{Guid1}" alt="캡처">""";

        Assert.Equal(html, RequestContentHtml.ToStored(html));
    }

    /// <summary>
    /// 바깥에서 복사해 온 그림은 건드리지 않는다. 지우거나 고치면 본문이
    /// 사람이 보던 것과 달라지고, 그쪽이 늘 더 나쁘다.
    /// </summary>
    [Fact]
    public void 남의_주소는_손대지_않는다()
    {
        var html = """<img src="https://example.com/a.png"><img src="/other/x.png">""";

        Assert.Equal(html, RequestContentHtml.ToStored(html));
    }

    /// <summary>
    /// <b>속성 값에 든 부등호에 속으면 안 된다.</b> 태그를 <c>&gt;</c> 로 잘라
    /// 찾는 방식이면 여기서 뒤가 통째로 어긋난다.
    /// </summary>
    [Fact]
    public void 속성_값에_부등호가_있어도_어긋나지_않는다()
    {
        var html = $"""<img alt="a > b" src="/files/{Guid1}"><p>뒤</p>""";

        Assert.Equal(
            $"""<img alt="a > b" src="/api/file/download/id/{Guid1}"><p>뒤</p>""",
            RequestContentHtml.ToStored(html));
    }

    [Fact]
    public void 여러_장을_한꺼번에_되돌린다()
    {
        var html = $"""<img src="/files/{Guid1}"><p>가운데</p><img src='/files/{Guid2}'>""";
        var stored = RequestContentHtml.ToStored(html);

        Assert.Contains($"/api/file/download/id/{Guid1}", stored, StringComparison.Ordinal);
        Assert.Contains($"/api/file/download/id/{Guid2}", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("/files/", stored, StringComparison.Ordinal);
    }

    // ── 되돌린 것이 화면에서 다시 열린다 ─────────────────

    /// <summary>
    /// 한 바퀴를 돈다 — 편집기에서 만든 주소를 저장 모양으로 바꾸고, 보여 줄 때
    /// 쓰는 정제기에 넣으면 <b>다시 중계 주소</b>가 되어야 한다. 이 둘이
    /// 어긋나면 등록은 되는데 상세 화면에서만 그림이 깨진다.
    /// </summary>
    [Fact]
    public void 저장하고_보여_주면_중계_주소로_돌아온다()
    {
        var stored = RequestContentHtml.ToStored($"""<p>증상</p><img src="/files/{Guid1}">""");
        var shown = NoticeHtml.Sanitize(stored);

        Assert.Contains($"/files/{Guid1}", shown, StringComparison.Ordinal);
    }

    // ── 남은 base64 걷어내기 ─────────────────────────────

    [Fact]
    public void 본문에_남은_base64_를_찾아낸다()
    {
        var html = $"""<p>증상</p><img src="data:image/png;base64,{PngBase64}">""";

        var pending = RequestContentHtml.PendingImages(html);

        var one = Assert.Single(pending);
        Assert.Equal("image/png", one.ContentType);
        Assert.NotEmpty(one.Bytes);
        Assert.EndsWith(".png", one.FileName, StringComparison.Ordinal);
    }

    /// <summary>같은 그림이 두 번 박혀 있어도 한 번만 올린다.</summary>
    [Fact]
    public void 같은_그림은_한_번만_센다()
    {
        var src = $"data:image/png;base64,{PngBase64}";
        var html = $"""<img src="{src}"><img src="{src}">""";

        Assert.Single(RequestContentHtml.PendingImages(html));
    }

    [Fact]
    public void 파일로_바뀐_주소는_본문_전체에서_갈린다()
    {
        var src = $"data:image/png;base64,{PngBase64}";
        var html = $"""<img src="{src}"><p>가운데</p><img src="{src}">""";

        var replaced = RequestContentHtml.ReplaceAll(
            html,
            new Dictionary<string, string>(StringComparer.Ordinal) { [src] = $"/files/{Guid1}" });

        Assert.DoesNotContain("data:image", replaced, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(replaced, $"/files/{Guid1}"));
    }

    /// <summary>
    /// 끊긴 data URI 에 걸려 등록 자체가 막히면 안 된다 — 그대로 저장돼도
    /// 깨진 그림 한 장일 뿐이다.
    /// </summary>
    [Fact]
    public void 끊긴_data_URI_는_조용히_넘어간다()
    {
        var html = """<img src="data:image/png;base64,이것은base64가아니다">""";

        Assert.Empty(RequestContentHtml.PendingImages(html));
    }

    [Fact]
    public void 파일_주소만_있는_본문에는_올릴_것이_없다()
    {
        var html = $"""<img src="/files/{Guid1}">""";

        Assert.Empty(RequestContentHtml.PendingImages(html));
    }

    // ── 빈 본문 판정 ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p><br></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<div><p></p></div>")]
    public void 뼈대만_남은_본문은_빈_것으로_본다(string? html) =>
        Assert.True(RequestContentHtml.IsBlank(html));

    /// <summary>
    /// <b>그림만 있는 글은 빈 글이 아니다.</b> 헬프데스크에 오는 글 중에는
    /// 캡처 한 장이 전부인 것이 실제로 있고, 그것을 막으면 그 사람은 글을
    /// 넣을 방법이 없다.
    /// </summary>
    [Theory]
    [InlineData("<p>증상이 있습니다</p>")]
    [InlineData("""<p><img src="/files/11111111-2222-3333-4444-555555555555"></p>""")]
    public void 글자나_그림이_있으면_빈_것이_아니다(string html) =>
        Assert.False(RequestContentHtml.IsBlank(html));

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        var at = 0;

        while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }
}
