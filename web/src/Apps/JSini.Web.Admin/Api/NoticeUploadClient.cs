using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Admin.Api;

/// <summary>공지 첨부를 FileServer 로 올린다 (게이트웨이 <c>/api/file/upload</c>).</summary>
public sealed class NoticeUploadClient(HttpClient http)
{
    /// <summary>공지 첨부가 담기는 업무 영역. 위 주석의 이유로 바꾸지 않는다.</summary>
    public const string NoticeBizType = "notice";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>파일 하나를 올린다. 성공하면 서버가 발급한 파일 정보를 준다.</summary>
    public async Task<UploadedNoticeFile?> UploadAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        string bizType = NoticeBizType,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(streamContent, "file", fileName);

        using var response = await http.PostAsync(
            $"file/upload?bizType={Uri.EscapeDataString(bizType)}", form, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<UploadEnvelope>(JsonOptions, ct);

        if (envelope is null || !envelope.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(envelope?.Message)
                    ? "파일 업로드에 실패했습니다."
                    : envelope.Message);
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
        [JsonPropertyName("result")] public List<UploadedNoticeFile>? Result { get; set; }
    }
}

/// <summary>업로드 응답 한 건.</summary>
public sealed class UploadedNoticeFile
{
    public string? Id { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }

    /// <summary>공지에 매달 때 쓰는 아이디. <c>fileId</c> 가 없으면 <c>id</c>.</summary>
    public string Key => string.IsNullOrWhiteSpace(FileId) ? Id ?? string.Empty : FileId;
}
