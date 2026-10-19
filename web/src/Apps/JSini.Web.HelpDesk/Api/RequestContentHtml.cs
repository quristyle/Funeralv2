using System.Text.RegularExpressions;
using JSini.Web.Components.Data;

namespace JSini.Web.HelpDesk.Api;

/// <summary>요청 본문(HTML)의 img src 를 옮긴다. 같은 그림의 주소는 두 모양이다 — 저장은 백엔드 정본(/api/file/download/id/{guid}), 화면은 셸 중계(/files/{guid}, FileDownload)다.
/// 편집기에서는 중계 주소를 쓰고 저장 직전에 정본으로 되돌린다. base64 는 저장 직전에 한 번 더 훑어 걸러 낸다 — 그대로 저장되면 본문이 수 MB 가 된다.</summary>
public static class RequestContentHtml
{
    /// <summary>img 의 src 값 하나. 따옴표 안까지 한 번에 짚는다 — 속성 값의 부등호(alt="a &gt; b")를 태그 끝으로 읽으면 그 그림만 조용히 안 바뀐다.</summary>
    private static readonly Regex ImageSrc = new(
        """<img\b(?:[^>"']|"[^"]*"|'[^']*')*?\bsrc\s*=\s*(?:"(?<v>[^"]*)"|'(?<v>[^']*)')""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>data URI 앞머리. <c>data:image/png;base64,</c> 에서 종류를 꺼낸다.</summary>
    private static readonly Regex DataUri = new(
        """^data:(?<mime>image/[a-z0-9.+-]+);base64,(?<data>.+)$""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>본문에 든 <c>src</c> 를 하나씩 훑는다.</summary>
    private static IEnumerable<string> Sources(string html) =>
        ImageSrc.Matches(html).Select(m => m.Groups["v"].Value);

    /// <summary>본문의 <c>src</c> 를 바꿔 끼운다. <paramref name="map"/> 에 없는 것은 그대로.</summary>
    private static string Rewrite(string html, Func<string, string?> map) =>
        ImageSrc.Replace(html, match =>
        {
            var value = match.Groups["v"];
            var replacement = map(value.Value);

            if (replacement is null || replacement == value.Value)
            {
                return match.Value;
            }

            // 짚은 자리는 태그 전체이고 바꿀 것은 그 안의 값 하나다.
            var at = value.Index - match.Index;
            return string.Concat(match.Value.AsSpan(0, at), replacement, match.Value.AsSpan(at + value.Length));
        });

    /// <summary>아직 파일로 올리지 못한 그림들. 같은 그림이 여러 번 박혀 있어도 한 번만 준다.</summary>
    public static IReadOnlyList<PendingImage> PendingImages(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var found = new List<PendingImage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var src in Sources(html))
        {
            if (!seen.Add(src))
            {
                continue;
            }

            var match = DataUri.Match(src);
            if (!match.Success)
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(match.Groups["data"].Value);
            }
            catch (FormatException)
            {
                // 끊긴 data URI 다. 던지면 등록이 막히므로 넘긴다 — 그대로 저장돼도 깨진 그림 한 장일 뿐이다.
                continue;
            }

            var mime = match.Groups["mime"].Value.ToLowerInvariant();
            found.Add(new PendingImage(src, mime, bytes));
        }

        return found;
    }

    /// <summary>올린 그림의 주소로 data URI 를 갈아 끼운다.</summary>
    public static string ReplaceAll(string html, IReadOnlyDictionary<string, string> uploaded) =>
        uploaded.Count == 0 ? html : Rewrite(html, src => uploaded.GetValueOrDefault(src));

    /// <summary>저장할 모양으로 되돌린다 — 셸 중계 주소(/files/{guid})를 정본으로. 우리 파일이 아닌 바깥 주소는 건드리지 않는다.</summary>
    public static string ToStored(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return Rewrite(html, src =>
        {
            if (!src.StartsWith(FileDownload.Path + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fileId = FileDownload.FileIdOf(src);
            return fileId is null ? null : $"/api/file/download/id/{fileId}";
        });
    }

    /// <summary>글자가 하나도 없는 본문인가. 그림만 있는 것은 빈 것이 아니다.</summary>
    public static bool IsBlank(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return true;
        }

        if (html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 태그를 걷어내고 남는 글자로 본다 — 빈 편집기가 남기는 <p><br></p> 같은 뼈대를 내용으로 세지 않기 위해서다.
        var text = TagOrEntity.Replace(html, " ");
        return string.IsNullOrWhiteSpace(text);
    }

    /// <summary>태그와 <c>&amp;nbsp;</c> 같은 실체 참조.</summary>
    private static readonly Regex TagOrEntity = new(
        """<[^>]*>|&[a-z]+;|&#\d+;""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>본문에서 처음 일부를 추출하여 제목으로 쓴다.</summary>
    public static string ExtractTitle(string? html, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = TagOrEntity.Replace(html, " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (text.Length == 0)
        {
            // 그림만 있거나 텍스트가 없을 경우
            return string.Empty;
        }

        return text.Length > maxLength ? text[..maxLength].TrimEnd() + "..." : text;
    }
}

/// <summary>본문에 박혀 있는, 아직 파일이 되지 못한 그림 한 장.</summary>
public sealed record PendingImage(string Source, string ContentType, byte[] Bytes)
{
    /// <summary>올릴 때 쓸 파일 이름. 원본 이름을 알 길이 없어 종류로만 짓는다.</summary>
    public string FileName =>
        $"pasted-{AppTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..8]}{Extension}";

    private string Extension => ContentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/bmp" => ".bmp",
        "image/svg+xml" => ".svg",
        _ => ".img",
    };
}
