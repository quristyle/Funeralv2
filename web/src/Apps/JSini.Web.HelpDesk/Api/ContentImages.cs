using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>서식 편집기 본문의 그림 한 장을 파일로 올린다. 올리는 길은 POST files/image 하나이고, 앞뒤로 틀리면 회로가 끊기는 자리가 셋이라 여기 모았다(요청 등록·댓글 쓰기가 같이 쓴다).
/// 종류만 떼어 쓰고(MediaTypeHeaderValue 예외가 새면 회로가 끊긴다), 칸 이름은 file 이어야 하며, 주소는 셸 중계 경로로 옮긴다. 던지지 않고 Uploaded.Error 로 돌려준다.</summary>
public sealed class ContentImages(HelpDeskApi api)
{
    /// <summary>본문에 붙이는 그림 한 장의 상한. 서버와 같은 값이다(FileUploadEndpoints.MaxImageBytes) — 여기서 막는 것은 먼저 말해 주려는 것이고 막는 쪽은 서버다.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>상한을 사람이 읽는 글자로. 안내 문구가 두 벌이 되지 않게 한다.</summary>
    public static string MaxText => $"{MaxBytes / 1024 / 1024}MB";

    /// <summary>올린 결과. Url(본문에 박을 셸 중계 주소)과 Error(화면이 그대로 띄울 실패 글) 중 하나만 값이 있다.</summary>
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

    /// <summary>본문에 아직 data: 로 박혀 있는 그림을 전부 파일로 바꾼다. 붙여넣기는 보통 파일로 가므로 남는 것은 서식째 복사한 그림뿐이다.
    /// 한 장이라도 실패하면 Error 를 채워 돌려주고 저장하지 않는 편이 낫다(base64 본문은 수 MB 가 된다).</summary>
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
