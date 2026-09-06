using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 프로필 사진을 <b>파일 그룹</b>으로 올린다 (게이트웨이 <c>/api/file/group/upload</c>).
/// </summary>
/// <remarks>
/// <para>
/// [한 장짜리 업로드(<c>file/upload</c>)와 다른 경로다]
/// </para>
///
/// <para>
/// 프로필 사진은 여러 장을 올려 두고 그중 하나를 <b>대표</b>로 삼는 방식이다
/// (Vue 의 <c>ImageGroupManager</c>). 그 「여러 장 + 대표 하나」를 묶어 주는 것이
/// FileServer 의 파일 그룹이고, 계정에는 <c>avatar_group_id</c> 하나만 붙는다.
/// <c>file/upload</c> 로 올리면 그룹이 없어 대표를 정할 수 없다.
/// </para>
///
/// <para>
/// <b>그룹 아이디는 서버가 발급한다.</b> 처음 올릴 때는 <c>groupId</c> 를 안
/// 싣고, 응답으로 온 아이디를 <c>auth/user/profile</c> 에 저장한다. 그 다음부터는
/// 그 아이디를 실어 같은 그룹에 쌓는다 — 안 실으면 <b>올릴 때마다 그룹이 새로
/// 생겨</b> 앞서 올린 사진이 화면에서 사라진다.
/// </para>
///
/// <para>
/// [이름이 <c>FileUploadClient</c> 가 아닌 이유]
/// </para>
///
/// <para>
/// <c>AddHttpClient&lt;T&gt;</c> 는 클라이언트 이름을 네임스페이스를 빼고 타입
/// 이름만으로 짓는다. 같은 이름이 두 모듈에 있으면 두 번째 등록이 던지고,
/// 그 예외는 <c>PortalModuleRegistry</c> 가 삼켜 <b>모듈 서비스 절반만 등록된
/// 채</b> 포털이 뜬다. <c>HttpClientNamingTests</c> 가 빌드 때 막는다.
/// </para>
///
/// <para>
/// [<c>bizType</c> 은 <c>PROFILE</c> 이다]
/// </para>
///
/// <para>
/// Vue 가 쓰던 값 그대로다. FileServer 는 이 글자를 저장 폴더 이름으로 쓰고
/// (<c>FileService.GetBizFolder</c>), 익명 열람 판정
/// (<c>Files:AnonymousReadablePaths</c>)에 <c>PROFILE</c> 은 들어 있지 않다 —
/// 남의 얼굴 사진이 아이디만 알면 열리는 일이 없다. 바꾸지 않는다.
/// </para>
/// </remarks>
public sealed class ProfileImageClient(HttpClient http)
{
    /// <summary>프로필 사진이 담기는 업무 영역. 위 주석의 이유로 바꾸지 않는다.</summary>
    public const string ProfileBizType = "PROFILE";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 사진 여러 장을 한 번에 올린다.
    /// </summary>
    /// <param name="files">올릴 것들. 스트림은 부르는 쪽이 정리한다.</param>
    /// <param name="groupId">
    /// 쌓아 넣을 그룹. <b>처음이면 <c>null</c></b> 이고 서버가 새로 발급해 준다.
    /// </param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>그룹 아이디와 그 그룹의 사진들.</returns>
    public async Task<GroupUploadResult> UploadAsync(
        IReadOnlyList<(Stream Content, string FileName, string? ContentType)> files,
        string? groupId,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();

        // 담은 StreamContent 들은 form 이 사라질 때 함께 정리된다.
        foreach (var (content, fileName, contentType) in files)
        {
            var part = new StreamContent(content);
            part.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            // 칸 이름이 `files` 다 — 서버가 그 이름으로 먼저 찾는다.
            form.Add(part, "files", fileName);
        }

        if (!string.IsNullOrWhiteSpace(groupId))
        {
            form.Add(new StringContent(groupId), "groupId");
        }

        form.Add(new StringContent(ProfileBizType), "bizType");

        using var response = await http.PostAsync("file/group/upload", form, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<UploadEnvelope>(JsonOptions, ct);

        if (envelope is null || !envelope.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(envelope?.Message)
                    ? "사진을 올리지 못했습니다."
                    : envelope.Message);
        }

        // 봉투가 객체 하나도 `result: [obj]` 로 싣는다. 첫 칸이 우리 것이다.
        var payload = envelope.Data?.Result is { Count: > 0 } rows ? rows[0] : null;

        return new GroupUploadResult(
            payload?.GroupId,
            payload?.Files ?? []);
    }

    private sealed class UploadEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("data")] public UploadPayload? Data { get; set; }
    }

    private sealed class UploadPayload
    {
        [JsonPropertyName("result")] public List<GroupUploadBody>? Result { get; set; }
    }

    private sealed class GroupUploadBody
    {
        [JsonPropertyName("groupId")] public string? GroupId { get; set; }
        [JsonPropertyName("files")] public List<GroupFileDto>? Files { get; set; }
    }
}

/// <summary>
/// 올리고 나서 받은 것. 그룹 아이디가 <b>새로 생겼을 수 있어</b> 함께 준다.
/// </summary>
public sealed record GroupUploadResult(string? GroupId, IReadOnlyList<GroupFileDto> Files);
