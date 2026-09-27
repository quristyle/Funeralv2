using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 서식 편집기 본문에 들어가는 그림 한 장을 파일로 올리는 자리.
/// </summary>
/// <remarks>
/// <para>
/// 올리는 길 자체는 <c>POST files/image</c> 하나인데, 그 앞뒤로 <b>틀리면
/// 회로가 끊기는 자리가 셋</b> 있어서 한곳에 모아 둔다 — 요청 등록
/// (<c>RequestNew</c>)과 댓글 쓰기(<c>CommentEditor</c>) 둘이 같은 것을 쓴다.
/// </para>
///
/// <list type="number">
///   <item>
///     <b>종류만 떼어 쓴다.</b> 브라우저가 <c>image/png; charset=…</c> 처럼 주면
///     <see cref="System.Net.Http.Headers.MediaTypeHeaderValue"/> 가 던지는데,
///     그 예외가 화면으로 새어 나가면 붙여넣기 한 번에 회로가 끊긴다.
///   </item>
///   <item>
///     <b>칸 이름이 <c>file</c> 이어야 한다.</b> 서버가 그 이름으로만 찾는다 —
///     틀리면 오류가 아니라 「올렸는데 주소를 못 받았다」가 된다.
///   </item>
///   <item>
///     <b>주소를 셸 중계 경로로 옮겨서 돌려준다.</b> 서버가 주는 것은 백엔드
///     정본(<c>/api/file/download/id/{guid}</c>)인데 브라우저가 보는 포털
///     (:5557)에는 <c>/api</c> 가 없어서, 그대로 걸면 깨진 네모가 된다.
///     저장할 때 정본으로 되돌리는 쪽은 <see cref="RequestContentHtml.ToStored"/> 다.
///   </item>
/// </list>
///
/// <para>
/// <b>던지지 않는다.</b> 실패를 <see cref="Uploaded.Error"/> 로 돌려주고 무엇을
/// 말할지는 화면이 정한다 — 그림 한 장 때문에 쓰던 글이 통째로 날아가는 일을
/// 막는 것이 이 클래스가 있는 까닭이다.
/// </para>
/// </remarks>
public sealed class ContentImages(HelpDeskApi api)
{
    /// <summary>
    /// 본문에 붙이는 그림 한 장의 상한. <b>서버와 같은 값이다</b>
    /// (<c>FileUploadEndpoints.MaxImageBytes</c>) — 여기서 막는 것은 먼저
    /// 말해 주려는 것이고, 정작 막는 쪽은 서버다.
    /// </summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>상한을 사람이 읽는 글자로. 안내 문구가 두 벌이 되지 않게 한다.</summary>
    public static string MaxText => $"{MaxBytes / 1024 / 1024}MB";

    /// <summary>올린 결과. 둘 중 하나만 값이 있다.</summary>
    /// <param name="Url">본문에 박을 주소(셸 중계 경로).</param>
    /// <param name="Error">실패한 까닭. 화면이 그대로 띄울 수 있는 글이다.</param>
    public sealed record Uploaded(string? Url, string? Error);

    /// <summary>그림 하나를 올린다.</summary>
    public async Task<Uploaded> UploadAsync(
        Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var part = new StreamContent(content);

            part.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType.Split(';')[0].Trim());

            form.Add(part, "file", fileName);

            var uploaded = await api.PostMultipartAsync<UploadedImage>("files/image", form, ct);

            if (uploaded is null || string.IsNullOrWhiteSpace(uploaded.FileId))
            {
                return new Uploaded(null, $"{fileName} 을(를) 올렸는데 주소를 받지 못했습니다.");
            }

            return new Uploaded(FileDownload.UrlFor(uploaded.FileId), null);
        }
        catch (ApiException ex)
        {
            return new Uploaded(null, $"{fileName} 을(를) 올리지 못했습니다 — {ex.Message}");
        }
        catch (FormatException)
        {
            // 브라우저가 준 종류를 읽지 못했다. 여기서 던지면 회로가 끊긴다.
            return new Uploaded(null, $"{fileName} 의 파일 종류를 읽지 못했습니다.");
        }
    }

    /// <summary>
    /// 본문에 아직 <c>data:</c> 로 박혀 있는 그림을 전부 파일로 바꾼다.
    /// </summary>
    /// <remarks>
    /// 붙여넣기는 <c>request-editor.js</c> 가 가로채 파일로 보내므로 보통은
    /// 남는 것이 없다. 그 길로 오지 않는 그림이 있어서(다른 글에서 서식째
    /// 복사) 저장 직전에 한 번 더 훑는다. <b>한 장이라도 실패하면
    /// <see cref="Uploaded.Error"/> 를 채워 돌려준다</b> — 그때는 저장하지
    /// 않는 편이 낫다. base64 한 장이 든 본문은 수 MB 짜리 글자가 되어
    /// 목록·검색·메일이 그 뒤로 계속 무거워진다.
    /// </remarks>
    public async Task<Uploaded> AbsorbAsync(string html, CancellationToken ct = default)
    {
        var pending = RequestContentHtml.PendingImages(html);

        if (pending.Count == 0)
        {
            return new Uploaded(html, null);
        }

        var uploaded = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var image in pending)
        {
            if (image.Bytes.LongLength > MaxBytes)
            {
                return new Uploaded(
                    null, $"본문에 든 그림 한 장이 너무 큽니다 — 한 장 {MaxText} 까지입니다.");
            }

            using var buffer = new MemoryStream(image.Bytes, writable: false);
            var result = await UploadAsync(buffer, image.FileName, image.ContentType, ct);

            if (result.Url is null)
            {
                return new Uploaded(null, result.Error);
            }

            uploaded[image.Source] = result.Url;
        }

        return new Uploaded(RequestContentHtml.ReplaceAll(html, uploaded), null);
    }
}
