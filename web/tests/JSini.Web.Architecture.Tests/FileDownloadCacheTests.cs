using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>첨부 중계가 브라우저에 무엇을 들고 있게 하는지. 첨부에는 private, no-store 가 맞지만 그림(얼굴·영정 사진·미리보기·썸네일)도 이 경로로 지나가 매번 다시 받게 되므로 갈라 두었다.
/// 되돌리기 쉬운 헤더 한 줄이라 눈으로는 못 지키고, 개발 장비에서는 FileServer 가 운영 호스트로 302 를 줘서 성공 경로의 헤더를 실물로 볼 수 없어 판정을 직접 부른다.</summary>
public class FileDownloadCacheTests
{
    /// <summary>화면에 박히는 그림은 브라우저가 들고 있어도 된다.</summary>
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

    /// <summary>사람이 눌러 받는 것은 캐시하지 않는다 — 공개 여부가 바뀌었을 때 옛것이 남으면 안 된다.</summary>
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

    /// <summary>자료실 갈래는 그림이어도 캐시하지 않는다 — 내려받은 횟수를 세는 것이 목적이라, 캐시본을 쓰면 요청이 서버에 닿지 않아 숫자가 멈춘다.</summary>
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

    /// <summary>브라우저가 들고 있는 것이 이 파일이면 바이트를 다시 보내지 않는다.</summary>
    [Fact]
    public void 같은_검증표면_다시_보내지_않는다()
    {
        Assert.True(FileDownload.NoneMatch(RequestWith($"\"{Id}\""), Id));
    }

    /// <summary>약한 검증표로 돌려주는 프록시가 있다 — 앞의 표시만 떼고 같이 본다.</summary>
    [Fact]
    public void 약한_검증표도_같은_것으로_본다()
    {
        Assert.True(FileDownload.NoneMatch(RequestWith($"W/\"{Id}\""), Id));
    }

    /// <summary>* 는 무엇이든 들고 있다는 뜻이다 — 이 주소에서 나올 수 있는 것은 그 아이디의 바이트 하나뿐이라 언제나 같다.</summary>
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

    /// <summary>다른 파일의 검증표를 들고 와도 통과시키지 않는다 — 통과시키면 브라우저가 엉뚱한 그림을 계속 쓴다.</summary>
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

    [Fact]
    public void 썸네일_경로를_올바르게_생성한다()
    {
        var id = "3225f1b1-95c9-43bb-b07d-fae00371b162";
        Assert.Equal($"/files/thumbnail/{id}", FileDownload.ThumbnailUrlFor(id));
        Assert.Equal($"/files/{id}", FileDownload.UrlFor(id));
    }
}
