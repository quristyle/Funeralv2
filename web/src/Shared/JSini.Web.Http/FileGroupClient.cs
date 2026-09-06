using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Http;

/// <summary>
/// 파일 <b>그룹</b> 다루기 — 여러 장을 한 묶음으로 올리고 지운다.
/// </summary>
/// <remarks>
/// <para>
/// [파일 하나와 그룹은 다른 이야기다]
/// </para>
///
/// <para>
/// 첨부 한 장은 <c>file/upload</c> 로 올리고 그 아이디를 업무 자료에 적어 둔다.
/// 사진 여러 장은 그렇게 하지 않는다 — <b>그룹 아이디 하나</b>를 업무 자료에 적고
/// (건물의 <c>building_photo_group_id</c> 처럼) 사진은 그 그룹에 매단다.
/// 장수가 바뀌어도 업무 자료는 그대로다.
/// </para>
///
/// <para>
/// [첫 장을 올릴 때 그룹이 생긴다 — <b>그 값을 놓치면 사진이 사라진다</b>]
/// </para>
///
/// <para>
/// 그룹 아이디를 비운 채 올리면 FileServer 가 새로 발급해 응답에 담아 준다.
/// 그 값을 업무 자료에 적어 두지 않으면 다음에 들어올 때 그룹을 몰라
/// <b>방금 올린 사진이 없어진 것처럼 보인다.</b> 그래서 이 클라이언트는
/// 올린 결과에 그룹 아이디를 반드시 실어 돌려준다.
/// </para>
///
/// <para>
/// [왜 게이트웨이 클라이언트로 못 하나]
/// </para>
///
/// <para>
/// <see cref="GatewayClient"/> 에는 멀티파트가 없다. 그래서 올리는 것만 여기서
/// 자기 <c>HttpClient</c> 로 하고, 읽기·지우기는 그쪽을 그대로 쓴다 —
/// 봉투를 벗기는 코드를 두 벌 만들지 않는다.
/// </para>
///
/// <para>
/// 등록은 <c>AddJSiniGateway</c> 에서 한다. <b>모듈이 아니라 거기인 이유</b>는
/// 이 클라이언트를 쓰는 부품(<c>ImageGroup</c>)이 Blazor Common 에 있어서다 —
/// 모듈이 등록하면 다른 모듈이 그 부품을 못 쓴다.
/// </para>
/// </remarks>
public sealed class FileGroupClient(HttpClient http, GatewayClient gateway)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>그룹에 담긴 파일들. 그룹이 없으면 빈 목록이다.</summary>
    public async Task<IReadOnlyList<GroupFile>> ListAsync(
        string? groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return [];
        }

        return await gateway.GetListAsync<GroupFile>($"file/group/{groupId}", cancellationToken);
    }

    /// <summary>
    /// 여러 장을 한 번에 올린다.
    /// </summary>
    /// <param name="groupId">
    /// 기존 그룹. <b>비우면 서버가 새로 만든다</b> — 돌려주는 값을 업무 자료에
    /// 적어 두어야 한다.
    /// </param>
    /// <param name="files">올릴 파일들 (스트림·이름·형식)</param>
    /// <param name="bizType">FileServer 의 업무 구분. 저장 폴더 이름이 된다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>그룹 아이디와 올라간 파일들</returns>
    public async Task<GroupUploadResult> UploadAsync(
        string? groupId,
        IReadOnlyList<(Stream Content, string FileName, string? ContentType)> files,
        string bizType,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();

        // StreamContent 는 form 이 처분한다. 여기서 using 으로 감싸면
        // 보내기 전에 닫혀 0바이트가 올라간다.
        foreach (var (content, fileName, contentType) in files)
        {
            var part = new StreamContent(content);
            part.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            // 칸 이름이 files 다 — 서버가 그 이름으로 먼저 찾는다.
            form.Add(part, "files", fileName);
        }

        if (!string.IsNullOrWhiteSpace(groupId))
        {
            form.Add(new StringContent(groupId), "groupId");
        }

        form.Add(new StringContent(bizType), "bizType");

        using var response = await http.PostAsync("file/group/upload", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(Describe(body, response.StatusCode));
        }

        var envelope = JsonSerializer.Deserialize<UploadEnvelope>(body, JsonOptions);

        if (envelope is null || !envelope.Success || envelope.Data is null)
        {
            throw new ApiException(
                string.IsNullOrWhiteSpace(envelope?.Message) ? "사진을 올리지 못했습니다." : envelope.Message);
        }

        return envelope.Data;
    }

    /// <summary>한 장을 지운다. 그룹은 그대로 남는다.</summary>
    public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        => gateway.DeleteAsync($"file/{fileId}", cancellationToken);

    /// <summary>
    /// 대표 사진을 정한다. 목록에서 한 장만 보여 주는 자리가 이 값을 쓴다.
    /// </summary>
    public Task SetRepresentativeAsync(
        string groupId, string fileId, CancellationToken cancellationToken = default)
        => gateway.PutAsync($"file/group/{groupId}/representative/{fileId}", null, cancellationToken);

    /// <summary>실패 응답에서 사람이 읽을 말을 꺼낸다. 못 꺼내면 상태 코드로 말한다.</summary>
    private static string Describe(string body, System.Net.HttpStatusCode status)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<UploadEnvelope>(body, JsonOptions);

            if (!string.IsNullOrWhiteSpace(envelope?.Message))
            {
                return envelope.Message;
            }
        }
        catch (JsonException)
        {
            // 봉투가 아니면 아래에서 상태 코드로 말한다.
        }

        return $"사진을 올리지 못했습니다. (HTTP {(int)status})";
    }

    private sealed class UploadEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("data")] public GroupUploadResult? Data { get; set; }
    }
}

/// <summary>그룹에 올린 결과.</summary>
public sealed class GroupUploadResult
{
    /// <summary>이 묶음의 그룹 아이디. <b>업무 자료에 적어 두어야 한다.</b></summary>
    public string GroupId { get; set; } = string.Empty;

    public List<GroupFile> Files { get; set; } = [];
}

/// <summary>그룹에 담긴 파일 하나.</summary>
public sealed class GroupFile
{
    public string Id { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? ContentType { get; set; }
    public bool IsImage { get; set; }

    /// <summary>대표 사진인가. 한 장만 보여 주는 자리가 이 값을 본다.</summary>
    public bool IsRepresentative { get; set; }

    public int SortOrder { get; set; }
    public DateTime? CreatedAt { get; set; }
}
