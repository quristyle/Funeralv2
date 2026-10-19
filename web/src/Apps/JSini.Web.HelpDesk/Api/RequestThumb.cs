using JSini.Web.Components.Data;

namespace JSini.Web.HelpDesk.Api;

/// <summary>요청 한 건을 한 장으로 대신할 그림을 고른다(목록 줄에 거는 것). 본문의 첫 그림을 먼저 보고, 없으면 첨부 중 가장 가벼운 것을 고른다.
/// 주소는 썸네일 경로다 — 원본을 걸면 줄마다 1.5MB 사진이 걸린다. 못 만든 썸네일은 셸 중계가 원본으로 되돌린다.</summary>
public static class RequestThumb
{
    /// <summary>이 요청을 대신할 그림의 썸네일 주소. 그림이 없으면 <c>null</c>.</summary>
    public static string? UrlOf(ImprovementRequest request) => PickOf(request)?.ThumbnailUrl;

    /// <summary>이 요청을 대신할 그림 한 장. 그림이 없으면 <c>null</c>.</summary>
    public static RequestImage? PickOf(ImprovementRequest request)
    {
        // ① 본문의 첫 그림. 저장된 값은 백엔드 정본 주소라 아이디만 꺼낸다.
        if (FileDownload.FileIdOf(request.MainPhoto) is { Length: > 0 } inBody)
        {
            return new RequestImage(inBody, null);
        }

        // ② 첨부한 그림 중 가장 가벼운 것.
        var attached = request.Attachments?
            .Where(IsImage)
            .OrderBy(a => a.FileSize ?? long.MaxValue)
            .FirstOrDefault();

        return attached?.FileId is { Length: > 0 } fileId
            ? new RequestImage(fileId, attached.OriginalFileName)
            : null;
    }

    /// <summary>이 요청에 붙은 그림 전부. 미리보기에서 앞뒤로 넘길 목록이고, 줄에 걸린 한 장이 맨 앞이다.
    /// 같은 파일은 두 번 담지 않는다 — 본문에 박은 그림을 첨부로도 올린 글이 흔하다.</summary>
    public static IReadOnlyList<RequestImage> ImagesOf(ImprovementRequest request)
    {
        var picked = PickOf(request);

        if (picked is null)
        {
            return [];
        }

        var images = new List<RequestImage> { picked };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { picked.FileId };

        foreach (var a in request.Attachments ?? [])
        {
            if (!IsImage(a) || a.FileId is not { Length: > 0 } fileId || !seen.Add(fileId))
            {
                continue;
            }

            images.Add(new RequestImage(fileId, a.OriginalFileName));
        }

        return images;
    }

    /// <summary>그림 첨부인가. 종류와 확장자를 둘 다 본다 — 브라우저가 종류를 못 읽으면 octet-stream 으로 올라와 그림이 조용히 빠진다.</summary>
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

/// <summary>요청에 붙은 그림 한 장.</summary>
/// <param name="FileId">FileServer 가 발급한 파일 아이디.</param>
/// <param name="Name">원본 파일 이름. 본문에 박힌 그림에는 없다.</param>
public sealed record RequestImage(string FileId, string? Name)
{
    /// <summary>목록의 줄에 거는 작은 그림(150×150 WebP).</summary>
    public string ThumbnailUrl => FileDownload.ThumbnailUrlFor(FileId);

    /// <summary>미리보기로 키워 볼 원본.</summary>
    public string OriginalUrl => FileDownload.UrlFor(FileId, Name);
}
