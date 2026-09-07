using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 첨부 중계가 무엇을 브라우저에 들고 있게 하는지.
///
/// [왜 시험으로 못 박나]
///
/// 한동안 중계하는 <b>모든 것</b>에 <c>private, no-store</c> 가 걸려 있었다.
/// 첨부에는 맞는 값이다 — 사람이 눌러 한 번 받고, 공개 여부가 바뀔 수 있다.
/// 문제는 이 경로로 <b>그림도 지나간다</b>는 것이었다: 헤더의 얼굴 · 영정 사진 ·
/// 장비 미리보기 · 미디어 썸네일 · 공지 본문의 <c>&lt;img&gt;</c>. 그래서
/// 화면을 열 때마다 셸→게이트웨이→FileServer 를 다시 탔고,
/// <c>no-store</c> 는 메모리 캐시까지 금지하므로 같은 그림을 한 페이지에서
/// 두 번 쓰면 두 번 받았다.
///
/// 이제 갈라 두었는데, <b>이 판정은 되돌리기 쉬운 종류</b>다. 헤더 한 줄이고
/// 잘못되어도 화면은 멀쩡히 나온다 — 느려지거나, 반대로 열려서는 안 되는 것이
/// 열릴 뿐이다. 그래서 눈으로는 못 지킨다.
///
/// [개발 장비에서는 실물로 못 잰다]
///
/// FileServer 는 로컬에 바이트가 없으면 운영 호스트로 302 를 주는데 그 호스트가
/// 개발망에서 안 풀린다(루트 CLAUDE.md 의 「개발 장비에서 올린 파일은 운영에
/// 바이트가 없다」). 그래서 중계는 502 를 돌려주고 성공 경로의 헤더를 볼 수가
/// 없다. 판정을 여기서 직접 부르는 이유다.
/// </summary>
public class FileDownloadCacheTests
{
    /// <summary>
    /// 화면에 박히는 그림은 브라우저가 들고 있어도 된다.
    /// </summary>
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/svg+xml")]
    [InlineData("image/webp")]
    // 형식 뒤에 매개변수가 붙어 오는 경우가 있다.
    [InlineData("image/jpeg; charset=binary")]
    // 대소문자를 가리지 않는다 — HTTP 형식은 원래 그렇다.
    [InlineData("IMAGE/JPEG")]
    public void 그림은_캐시한다(string contentType)
    {
        Assert.True(FileDownload.Cacheable(allowed: true, contentType));
    }

    /// <summary>
    /// 사람이 눌러 받는 것은 캐시하지 않는다. 한 번 받으면 끝이라 얻을 것이
    /// 없고, 공개 여부가 바뀌었을 때 옛것이 남으면 안 된다.
    /// </summary>
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/octet-stream")]
    [InlineData("application/zip")]
    [InlineData("text/plain")]
    [InlineData("video/mp4")]
    [InlineData("audio/mpeg")]
    // 형식을 모를 때도 캐시하지 않는다 — 모르면 안 여는 쪽이다.
    [InlineData(null)]
    [InlineData("")]
    public void 그림이_아니면_캐시하지_않는다(string? contentType)
    {
        Assert.False(FileDownload.Cacheable(allowed: true, contentType));
    }

    /// <summary>
    /// 이름에 image 가 들어가는 다른 형식에 걸리지 않는다.
    /// </summary>
    [Theory]
    [InlineData("application/x-image")]
    [InlineData("multipart/image")]
    [InlineData("imagex/jpeg")]
    public void 형식의_앞칸이_image_일_때만_캐시한다(string contentType)
    {
        Assert.False(FileDownload.Cacheable(allowed: true, contentType));
    }

    /// <summary>
    /// <b>자료실 갈래는 그림이어도 캐시하지 않는다.</b>
    ///
    /// <para>
    /// 그 경로의 목적 절반이 <b>내려받은 횟수를 세는 것</b>이다. 브라우저가
    /// 캐시본을 쓰면 요청이 서버에 닿지 않아 숫자가 멈춘다 — 자료실 화면의
    /// 「내려받기」 칸이 그 숫자다. 화면에는 아무 표시도 안 나므로
    /// <b>세다가 멈춘 것을 알아차릴 방법이 없다.</b>
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    public void 횟수를_세는_갈래는_캐시하지_않는다(string contentType)
    {
        Assert.False(FileDownload.Cacheable(allowed: false, contentType));
    }

    // ── If-None-Match ────────────────────────────────────────────

    private static readonly Guid Id = Guid.Parse("3225f1b1-95c9-43bb-b07d-fae00371b162");

    private static HttpRequest RequestWith(params string[] ifNoneMatch)
    {
        var context = new DefaultHttpContext();

        if (ifNoneMatch.Length > 0)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        return context.Request;
    }

    /// <summary>
    /// 브라우저가 들고 있는 것이 이 파일이면 바이트를 다시 보내지 않는다.
    /// </summary>
    [Fact]
    public void 같은_검증표면_다시_보내지_않는다()
    {
        Assert.True(FileDownload.NoneMatch(RequestWith($"\"{Id}\""), Id));
    }

    /// <summary>
    /// 약한 검증표로 돌려주는 프록시가 있다. 앞의 표시만 떼고 같이 본다.
    /// </summary>
    [Fact]
    public void 약한_검증표도_같은_것으로_본다()
    {
        Assert.True(FileDownload.NoneMatch(RequestWith($"W/\"{Id}\""), Id));
    }

    /// <summary>
    /// <c>*</c> 는 「무엇이든 들고 있다」다. 이 주소에서 나올 수 있는 것은
    /// 그 아이디의 바이트 하나뿐이라 언제나 같다.
    /// </summary>
    [Fact]
    public void 별표는_같은_것으로_본다()
    {
        Assert.True(FileDownload.NoneMatch(RequestWith("*"), Id));
    }

    /// <summary>여러 개를 한 줄에 실어 보내는 브라우저가 있다.</summary>
    [Fact]
    public void 한_줄에_여럿이_와도_찾는다()
    {
        Assert.True(FileDownload.NoneMatch(
            RequestWith($"\"다른것\", W/\"{Id}\", \"또다른것\""), Id));
    }

    [Fact]
    public void 헤더가_없으면_보낸다()
    {
        Assert.False(FileDownload.NoneMatch(RequestWith(), Id));
    }

    /// <summary>
    /// <b>다른 파일의 검증표를 들고 와도 통과시키지 않는다.</b>
    /// 통과시키면 브라우저가 엉뚱한 그림을 계속 쓰게 된다.
    /// </summary>
    [Fact]
    public void 다른_파일의_검증표면_보낸다()
    {
        Assert.False(FileDownload.NoneMatch(
            RequestWith($"\"{Guid.NewGuid()}\""), Id));
    }

    /// <summary>따옴표가 빠진 값에 걸리지 않는다.</summary>
    [Fact]
    public void 따옴표가_없으면_같은_것으로_보지_않는다()
    {
        Assert.False(FileDownload.NoneMatch(RequestWith(Id.ToString()), Id));
    }

    [Fact]
    public void 빈_값은_넘긴다()
    {
        Assert.False(FileDownload.NoneMatch(RequestWith(""), Id));
    }
}
