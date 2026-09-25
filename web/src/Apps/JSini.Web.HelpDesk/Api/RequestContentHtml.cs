using System.Text.RegularExpressions;
using JSini.Web.Components.Data;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 요청 본문(HTML)에 든 <c>&lt;img src&gt;</c> 를 옮기는 자리.
/// </summary>
/// <remarks>
/// <para>
/// [주소가 두 모양인 이유]
/// </para>
///
/// <para>
/// 같은 그림인데 <b>저장하는 주소</b>와 <b>화면에 거는 주소</b>가 다르다.
/// </para>
///
/// <list type="table">
///   <item>
///     <term><c>/api/file/download/id/{guid}</c></term>
///     <description>
///       백엔드가 쓰는 정본. DB 에는 이것을 넣는다 — 영정 사진·공지 첨부·
///       옛 본문 그림이 전부 이 모양이라, 새로 쓰는 글만 다른 모양으로 두면
///       주소를 읽는 곳마다 갈래가 하나씩 는다.
///     </description>
///   </item>
///   <item>
///     <term><c>/files/{guid}</c></term>
///     <description>
///       브라우저가 실제로 부를 수 있는 주소. 브라우저가 보는 것은 포털
///       (:5557)이고 <b>거기에는 <c>/api</c> 가 없다</b> — 정본을 그대로
///       걸면 깨진 네모가 된다. 셸이 중계한다(<see cref="FileDownload"/>).
///     </description>
///   </item>
/// </list>
///
/// <para>
/// 그래서 편집기 안에서는 중계 주소를 쓰고, 저장 직전에 정본으로 되돌린다.
/// 보여 줄 때 되돌리는 쪽은 <c>NoticeHtml.Sanitize</c> 가 이미 하고 있다.
/// </para>
///
/// <para>
/// [base64 는 여기서 걸러 낸다]
/// </para>
///
/// <para>
/// 붙여넣기는 <c>wwwroot/js/request-editor.js</c> 가 가로채 파일로 올리므로
/// 보통은 data URI 가 생기지 않는다. 그런데 그 길로 오지 않는 그림이 있다 —
/// 편집기 도구줄의 「그림」 단추, 브라우저가 가로채기를 허락하지 않는 경우,
/// 다른 글에서 서식째로 복사해 온 경우. <b>그것이 한 장이라도 그대로 저장되면
/// 본문이 수 MB 짜리 글자가 되고</b>, 서버는 그것을 배포 장비 디스크에 떨군다
/// (결정 D5-B 가 그만두기로 한 방식). 그래서 저장 직전에 한 번 더 훑는다.
/// </para>
/// </remarks>
public static class RequestContentHtml
{
    /// <summary>
    /// <c>&lt;img&gt;</c> 의 <c>src</c> 값 하나.
    /// </summary>
    /// <remarks>
    /// <b>따옴표 안까지 한 번에 짚는다.</b> <c>&lt;img</c> 와 <c>src</c> 사이를
    /// <c>[^&gt;]</c> 로 건너뛰면 속성 값에 든 부등호(<c>alt="a &gt; b"</c>)를
    /// 태그 끝으로 읽어 <b>그 그림만 조용히 안 바뀐다</b> — 본문 한가운데
    /// 캡처 한 장만 깨지는 모양이라 원인이 보이지 않는다. 그래서 따옴표로
    /// 묶인 값은 통째로 건너뛴다.
    /// </remarks>
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

    /// <summary>
    /// 아직 파일로 올리지 못한 그림들. 같은 그림이 여러 번 박혀 있어도 한 번만 준다.
    /// </summary>
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
                // 끊긴 data URI 다. 여기서 던지면 등록 자체가 막히므로 넘긴다 —
                // 그대로 저장돼도 깨진 그림 한 장일 뿐이다.
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

    /// <summary>
    /// 저장할 모양으로 되돌린다 — 셸 중계 주소(<c>/files/{guid}</c>)를 정본으로.
    /// </summary>
    /// <remarks>
    /// 우리 파일이 아닌 주소(바깥 그림을 복사해 온 경우)는 건드리지 않는다.
    /// 그것을 지우거나 고치는 것은 이 함수의 일이 아니다 — 본문을 사람이 보는
    /// 것과 다르게 만드는 쪽이 늘 더 나쁘다.
    /// </remarks>
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

        // 태그를 걷어내고 남는 글자로 본다. 빈 편집기가 남기는 `<p><br></p>`
        // 같은 뼈대를 「내용 있음」으로 세지 않으려는 것이다.
        var text = TagOrEntity.Replace(html, " ");
        return string.IsNullOrWhiteSpace(text);
    }

    /// <summary>태그와 <c>&amp;nbsp;</c> 같은 실체 참조.</summary>
    private static readonly Regex TagOrEntity = new(
        """<[^>]*>|&[a-z]+;|&#\d+;""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}

/// <summary>본문에 박혀 있는, 아직 파일이 되지 못한 그림 한 장.</summary>
/// <param name="Source">본문에 들어 있는 <c>src</c> 값 그대로. 갈아 끼울 때 열쇠가 된다.</param>
/// <param name="ContentType">MIME 타입</param>
/// <param name="Bytes">그림 바이트</param>
public sealed record PendingImage(string Source, string ContentType, byte[] Bytes)
{
    /// <summary>올릴 때 쓸 파일 이름. 원본 이름을 알 길이 없어 종류로만 짓는다.</summary>
    public string FileName =>
        $"pasted-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..8]}{Extension}";

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
