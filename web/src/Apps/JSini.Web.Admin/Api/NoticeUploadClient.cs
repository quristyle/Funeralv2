using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 공지 첨부를 FileServer 로 올린다 (게이트웨이 <c>/api/file/upload</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>장례식장 모듈의 <c>FileUploadClient</c> 와 같은 일을 한다.</b> 올리지
/// 않고 복제한 것은 규칙 때문이다 — 「두 모듈이 쓰면 복제, 세 번째부터 승격」
/// (web/CLAUDE.md).
/// </para>
///
/// <para>
/// [이름이 <c>FileUploadClient</c> 가 아닌 이유 — 실제로 밟았다]
/// </para>
///
/// <para>
/// <c>AddHttpClient&lt;T&gt;</c> 는 클라이언트 이름을 <b>네임스페이스를 빼고
/// 타입 이름만으로</b> 짓는다. 그래서 이름이 같은 타입이 두 모듈에 있으면
/// 두 번째 등록이 던진다 —
/// <c>The HttpClient factory already has a registered client with the name
/// 'FileUploadClient'</c>.
/// </para>
///
/// <para>
/// <b>그런데 그 예외는 화면에 안 나온다.</b> <c>PortalModuleRegistry</c> 가
/// 모듈 하나가 깨져도 포털을 세우지 않으려고 잡아 삼키고, 모듈은 이미 목록에
/// 등록된 뒤라 기동 대조(<c>PortalApps</c>)도 통과한다. 결과는 <b>장례식장
/// 모듈의 서비스 절반이 등록되지 않은 채 뜨는 것</b>이었다. 화면은 열리고
/// 파일을 올릴 때만 죽는다. <c>HttpClientNamingTests</c> 가 이제 빌드 때 막는다.
/// </para>
///
/// <para>
/// <c>GatewayClient</c> 에는 멀티파트가 없어서 따로 둔다. 같은 BaseAddress 와
/// <c>AuthTokenHandler</c> 로 등록하므로(<c>AdminModule</c>) 토큰 처리는 같다.
/// </para>
///
/// <para>
/// [<c>bizType</c> 이 곧 저장 폴더다 — 공지는 <c>notice</c>]
/// </para>
///
/// <para>
/// FileServer 가 <c>bizType</c> 을 그대로 폴더 이름으로 쓴다
/// (<c>FileService.GetBizFolder</c>). 그리고 익명 열람 판정
/// (<c>PublicFileAccessFilter</c>)이 <c>Files:AnonymousReadablePaths</c> 로
/// <b>폴더 앞머리</b>를 본다 — 거기에 <c>funeralv2/</c> 만 들어 있고
/// <c>notice/</c> 는 <b>일부러 빠져 있다.</b>
/// </para>
///
/// <para>
/// 그래서 공지 첨부는 폴더로 열리지 않고 <c>is_public</c> 하나로만 열린다.
/// 그 값을 켜고 끄는 것은 AuthServer 가 공지를 저장할 때 한다
/// (<c>PublicFileSyncService</c> · 결정 D-S10). <b>공개 공지의 첨부만 켜진다.</b>
/// <c>bizType</c> 을 <c>funeralv2</c> 같은 것으로 바꿔 올리면 비공개 공지의
/// 첨부까지 아이디를 아는 사람에게 열린다.
/// </para>
/// </remarks>
public sealed class NoticeUploadClient(HttpClient http)
{
    /// <summary>공지 첨부가 담기는 업무 영역. 위 주석의 이유로 바꾸지 않는다.</summary>
    public const string NoticeBizType = "notice";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 파일 하나를 올린다. 성공하면 서버가 발급한 파일 정보를 준다.
    /// </summary>
    /// <param name="content">파일 내용 스트림</param>
    /// <param name="fileName">원본 파일 이름</param>
    /// <param name="contentType">MIME 타입. 모르면 application/octet-stream.</param>
    /// <param name="bizType">FileServer 의 업무 구분. 기본은 공지다.</param>
    /// <param name="ct">취소 토큰</param>
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

/// <summary>
/// 업로드 응답 한 건.
/// </summary>
/// <remarks>
/// <b>응답에 <c>fileId</c> 칸이 없고 <c>id</c> 에 들어 있다</b> — 게이트웨이로
/// 직접 던져 확인한 것이다(D5). 어느 쪽이 올지 모르니 둘 다 받는다.
/// </remarks>
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
