using JSini.Web.Components.Data;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 요청 한 건을 <b>한 장으로</b> 대신할 그림을 고른다 — 목록의 줄에 거는 것.
/// </summary>
/// <remarks>
/// <para>
/// [그림이 있을 수 있는 자리가 둘이다]
/// </para>
///
/// <list type="number">
///   <item>
///     <b>본문에 박힌 그림.</b> 서식 편집기로 쓴 글이라 <c>&lt;img&gt;</c> 로
///     들어 있다. 목록 조회는 본문을 아예 안 읽어 오지만
///     (<c>remove=description,content</c>) 서버가 글을 저장할 때마다 본문의
///     <b>첫 그림</b> 주소를 <see cref="ImprovementRequest.MainPhoto"/> 에
///     적어 두므로(<c>FileUtil.GetFirstImageUrl</c>), 본문 없이도 알 수 있다.
///   </item>
///   <item>
///     <b>따로 붙인 첨부.</b> 목록 응답에 <c>attachments</c> 로 함께 온다.
///   </item>
/// </list>
///
/// <para>
/// [본문 쪽을 먼저 본다]
/// </para>
///
/// <para>
/// 그것이 글쓴이가 <b>글 안에 세운</b> 그림이라, 붙여 놓기만 한 첨부보다
/// 그 글을 잘 대신한다. 첨부만 있으면 그중 <b>가장 가벼운 것</b>을 고른다 —
/// 어느 것이든 한 장만 보일 것이고, 썸네일을 만들지 못한 파일은 원본으로
/// 되돌아가므로(<c>FileDownload.ThumbnailUrlFor</c> 의 폴백) 그때 실제로
/// 흘러가는 바이트가 가장 적다.
/// </para>
///
/// <para>
/// [주소는 반드시 썸네일 경로다]
/// </para>
///
/// <para>
/// 원본을 걸면 줄 스물다섯에 1.5MB 짜리 사진 스물다섯 장이 걸린다. 파일
/// 서버가 이미 150×150 WebP 를 만들어 두므로 그것을 부른다. 못 만든 파일은
/// 셸의 중계가 원본으로 한 번 되돌린다 — 그래서 <b>여기서 갈래를 나누지
/// 않는다.</b>
/// </para>
/// </remarks>
public static class RequestThumb
{
    /// <summary>이 요청을 대신할 그림의 썸네일 주소. 그림이 없으면 <c>null</c>.</summary>
    public static string? UrlOf(ImprovementRequest request)
    {
        // ① 본문의 첫 그림. 저장된 값은 백엔드 정본 주소라 아이디만 꺼낸다.
        if (FileDownload.FileIdOf(request.MainPhoto) is { Length: > 0 } inBody)
        {
            return FileDownload.ThumbnailUrlFor(inBody);
        }

        // ② 첨부한 그림 중 가장 가벼운 것.
        var attached = request.Attachments?
            .Where(IsImage)
            .OrderBy(a => a.FileSize ?? long.MaxValue)
            .FirstOrDefault();

        return attached?.FileId is { Length: > 0 } fileId
            ? FileDownload.ThumbnailUrlFor(fileId)
            : null;
    }

    /// <summary>
    /// 그림 첨부인가. <b>종류와 확장자를 둘 다 본다</b> — 브라우저가 종류를
    /// 못 읽으면 <c>application/octet-stream</c> 으로 올라오고, 그러면 그림인데도
    /// 조용히 빠진다.
    /// </summary>
    private static bool IsImage(Attachment a)
    {
        if (string.IsNullOrWhiteSpace(a.FileId))
        {
            // FileServer 로 아직 안 옮긴 첨부다. 썸네일을 부를 길이 없다.
            return false;
        }

        if (a.FileType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var name = a.OriginalFileName;
        return name.Length > 0 && ImageExtensions.Any(
            ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg"];
}
