using System.Text.Json;
using JSini.Web.HelpDesk.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 요청 목록의 줄에 거는 그림 한 장(<see cref="RequestThumb"/>).
/// </summary>
/// <remarks>
/// <para>
/// [이 기능은 조용히 빈칸이 된다]
/// </para>
///
/// <para>
/// 그림을 못 고르면 예외도 안 나고 로그도 안 남는다 — 「그림」 칸이 비어 있을
/// 뿐이고, 목록에는 원래 그림이 없는 요청이 훨씬 많아서 <b>틀린 것과 없는 것이
/// 구분되지 않는다.</b>
/// </para>
///
/// <para>
/// 그래서 <b>서버가 실제로 내려준 글자</b>로 검사한다. 이 본문은 2026-09-28 에
/// <c>POST helpdesk/requests/srch</c> 의 답에서 그대로 떼어 온 것이다. 칸 이름이
/// 하나라도 어긋나면(<c>originalFileName</c> 을 <c>fileName</c> 으로 적는 따위)
/// 역직렬화가 <b>오류 없이</b> 빈 값을 채우고, 그때 이 테스트만 붉어진다.
/// </para>
/// </remarks>
public sealed class RequestThumbTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ImprovementRequest Parse(string json) =>
        JsonSerializer.Deserialize<ImprovementRequest>(json, Json)!;

    /// <summary>
    /// 본문에 박은 그림 — 서버가 <c>mainPhoto</c> 에 적어 둔 것을 쓴다.
    /// </summary>
    /// <remarks>
    /// 목록 조회는 본문을 아예 안 읽어 온다(<c>remove=description,content</c>).
    /// 이 칸이 유일한 단서다.
    /// </remarks>
    [Fact]
    public void 본문에_박은_그림을_고른다()
    {
        var request = Parse("""
            {
              "id": 5,
              "title": "테스트 글작성",
              "mainPhoto": "/api/file/download/id/6d9292df-5b6d-42e8-b6e3-8c15a5918d92",
              "attachmentCount": 0,
              "attachments": []
            }
            """);

        Assert.Equal(
            "/files/thumbnail/6d9292df-5b6d-42e8-b6e3-8c15a5918d92",
            RequestThumb.UrlOf(request));
    }

    /// <summary>
    /// 첨부한 그림 — <b>서버가 내려주는 칸 이름 그대로</b> 읽는가.
    /// </summary>
    [Fact]
    public void 첨부한_그림을_고른다()
    {
        var request = Parse("""
            {
              "id": 6,
              "title": "테스트",
              "mainPhoto": "",
              "attachmentCount": 1,
              "attachments": [
                {
                  "entityType": "ImprovementRequest",
                  "entityId": 6,
                  "originalFileName": "probe.png",
                  "storedFileName": "probe-stored.png",
                  "filePath": "/tmp/probe.png",
                  "fileType": "image/png",
                  "fileSize": 1234,
                  "fileId": "2f1e54cb-7eaa-4cfa-a9ea-ea1ceeb63471",
                  "migratedAt": null,
                  "id": 2
                }
              ]
            }
            """);

        Assert.Equal(
            "/files/thumbnail/2f1e54cb-7eaa-4cfa-a9ea-ea1ceeb63471",
            RequestThumb.UrlOf(request));
    }

    /// <summary>
    /// 그림이 여럿이면 <b>가장 가벼운 것</b> 하나다.
    /// </summary>
    /// <remarks>
    /// 줄마다 한 장만 걸리므로 어느 것이든 되지만, 썸네일을 못 만든 파일은
    /// 원본으로 되돌아간다 — 그때 실제로 흘러가는 바이트가 가장 적은 쪽을
    /// 고른다.
    /// </remarks>
    [Fact]
    public void 그림이_여럿이면_가장_가벼운_것을_고른다()
    {
        var request = Parse("""
            {
              "id": 8,
              "mainPhoto": "",
              "attachments": [
                { "originalFileName": "big.jpg",   "fileType": "image/jpeg", "fileSize": 9000000,
                  "fileId": "11111111-1111-1111-1111-111111111111" },
                { "originalFileName": "small.png", "fileType": "image/png",  "fileSize": 1200,
                  "fileId": "22222222-2222-2222-2222-222222222222" }
              ]
            }
            """);

        Assert.Equal(
            "/files/thumbnail/22222222-2222-2222-2222-222222222222",
            RequestThumb.UrlOf(request));
    }

    /// <summary>
    /// 종류를 못 읽은 그림도 고른다 — <b>확장자로 한 번 더 본다</b>.
    /// </summary>
    /// <remarks>
    /// 브라우저가 종류를 모르면 <c>application/octet-stream</c> 으로 올라온다.
    /// 그것만 보고 거르면 그림인데도 조용히 빠진다.
    /// </remarks>
    [Fact]
    public void 종류를_못_읽은_그림은_확장자로_고른다()
    {
        var request = Parse("""
            {
              "id": 9,
              "mainPhoto": "",
              "attachments": [
                { "originalFileName": "캡처.PNG", "fileType": "application/octet-stream",
                  "fileSize": 5000, "fileId": "33333333-3333-3333-3333-333333333333" }
              ]
            }
            """);

        Assert.Equal(
            "/files/thumbnail/33333333-3333-3333-3333-333333333333",
            RequestThumb.UrlOf(request));
    }

    /// <summary>
    /// 그림이 아닌 첨부와 <b>아직 파일 서버로 안 옮긴</b> 첨부는 고르지 않는다.
    /// </summary>
    /// <remarks>
    /// <c>fileId</c> 가 없으면 썸네일을 부를 길이 자체가 없다 — 걸면 깨진
    /// 네모가 된다.
    /// </remarks>
    [Fact]
    public void 그림이_아니거나_아이디가_없으면_고르지_않는다()
    {
        var request = Parse("""
            {
              "id": 10,
              "mainPhoto": "",
              "attachments": [
                { "originalFileName": "명세.xlsx", "fileType": "application/vnd.ms-excel",
                  "fileSize": 100, "fileId": "44444444-4444-4444-4444-444444444444" },
                { "originalFileName": "옛날.png", "fileType": "image/png",
                  "fileSize": 200, "fileId": null }
              ]
            }
            """);

        Assert.Null(RequestThumb.UrlOf(request));
    }

    /// <summary>그림이 하나도 없으면 아무것도 안 건다.</summary>
    [Fact]
    public void 그림이_없으면_비운다()
    {
        Assert.Null(RequestThumb.UrlOf(Parse("""
            { "id": 7, "title": "ㅁㅁ", "mainPhoto": "", "attachmentCount": 0, "attachments": [] }
            """)));

        Assert.Null(RequestThumb.UrlOf(Parse("""{ "id": 7 }""")));
    }

    /// <summary>
    /// <b>원본이 아니라 썸네일</b>을 건다.
    /// </summary>
    /// <remarks>
    /// 원본을 걸면 줄 스물다섯에 원본 스물다섯 장이 걸린다. 실제로 잰 차이가
    /// 332KB 대 7KB 였다(2026-09-28).
    /// </remarks>
    [Fact]
    public void 언제나_썸네일_경로다()
    {
        var url = RequestThumb.UrlOf(Parse("""
            { "id": 5, "mainPhoto": "/api/file/download/id/6d9292df-5b6d-42e8-b6e3-8c15a5918d92" }
            """));

        Assert.NotNull(url);
        Assert.StartsWith("/files/thumbnail/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("download", url, StringComparison.Ordinal);
    }
}
