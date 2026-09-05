using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Funeral.Api;

/// <summary>
/// FileServer 업로드 (게이트웨이 <c>/api/file/upload</c>).
///
/// GatewayClient 에는 멀티파트가 없어서 따로 둔다 — 같은 BaseAddress 와
/// <c>AuthTokenHandler</c> 로 등록하므로(FuneralModule) 토큰 처리도 같다.
/// Vue 원본: <c>api/examples/upload.ts</c> 의 <c>upload_file</c>.
/// </summary>
public sealed class FileUploadClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 파일 하나를 올린다. 성공하면 서버가 발급한 파일 정보(주소·썸네일)를 준다.
    /// </summary>
    /// <param name="content">파일 내용 스트림</param>
    /// <param name="fileName">원본 파일 이름</param>
    /// <param name="contentType">MIME 타입. 모르면 application/octet-stream.</param>
    /// <param name="bizType">FileServer 의 업무 구분. 장례식장은 funeralv2.</param>
    /// <param name="ct">취소 토큰</param>
    public async Task<UploadedFile?> UploadAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        string bizType = "funeralv2",
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(streamContent, "file", fileName);

        using var response = await http.PostAsync(
            $"file/upload?bizType={Uri.EscapeDataString(bizType)}", form, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<UploadEnvelope>(JsonOptions, ct);
        if (envelope is null || !envelope.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(envelope?.Message) ? "파일 업로드에 실패했습니다." : envelope.Message);
        }

        return envelope.Data?.Result is { Count: > 0 } rows ? rows[0] : null;
    }

    private sealed class UploadEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("data")] public UploadPayload? Data { get; set; }
    }

    private sealed class UploadPayload
    {
        [JsonPropertyName("result")] public List<UploadedFile>? Result { get; set; }
    }
}
