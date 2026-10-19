using System.Net.Http.Headers;
using System.Text.Json;

namespace AuthServer.Services;

/// <summary>옮겨 온 사진. <see cref="DownloadUrl"/> 이 계정의 <c>Avatar</c> 값이 된다.</summary>
/// <param name="GroupId">FileServer 파일 그룹. 계정의 <c>avatar_group_id</c> 가 된다.</param>
/// <param name="FileId">올린 파일 번호. 대표로 지정할 때 쓴다.</param>
/// <param name="DownloadUrl">FileServer 가 준 주소(<c>/api/file/download/{guid}</c>).</param>
/// <param name="IsRepresentative">FileServer 가 대표로 정한 파일인가. 그룹에 대표가 없을 때만 참이다.</param>
public sealed record ImportedAvatar(string GroupId, string FileId, string DownloadUrl, bool IsRepresentative);

/// <summary>소셜 공급자 프로필 사진을 우리 FileServer 로 옮겨 계정 대표 사진으로 건다.</summary>
/// 포털은 Avatar 에서 우리 파일 번호만 쓰므로 바깥 주소를 적으면 「사진 없음」이 된다. 서버가 바깥 주소를 여는 일이라 https·허용 호스트·크기 상한·image/* 를 모두 맞아야 받는다.
public class SocialAvatarImporter(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SocialAvatarImporter> logger)
{
    /// <summary>공급자 사진을 받는 클라이언트 이름.</summary>
    public const string PictureClient = "social-picture";

    /// <summary>FileServer 를 부르는 클라이언트 이름.</summary>
    public const string FileClient = "file-server";

    /// <summary>받을 사진의 상한. 프로필 사진은 커야 수백 KB 다.</summary>
    private const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>사진을 받아 FileServer 에 올린다. 어느 단계든 실패하면 <c>null</c>.</summary>
    /// <param name="pictureUrl">공급자가 준 사진 주소(https).</param>
    /// <param name="allowedHosts">받아도 되는 호스트(끝이 같으면 된다). 비어 있으면 받지 않는다.</param>
    /// <param name="provider">공급자 열쇠. 파일 이름에 쓴다.</param>
    /// <param name="createdBy">FileServer 기록에 남길 이름(<c>X-User-Id</c>).</param>
    /// <param name="groupId">이미 있는 그룹이면 그 그룹에 한 장을 더하고, 없으면 새 그룹을 만든다(가입 신청).</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task<ImportedAvatar?> ImportAsync(
        string pictureUrl, IReadOnlyCollection<string> allowedHosts, string provider, string createdBy,
        string? groupId = null, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(pictureUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !IsAllowedHost(uri.Host, allowedHosts))
        {
            logger.LogWarning(
                "{Provider} 프로필 사진 주소가 허용 호스트가 아니라 옮기지 않는다: {Host}",
                provider, uri?.Host ?? "(주소 아님)");
            return null;
        }

        try
        {
            var (bytes, contentType) = await DownloadAsync(uri, ct);
            if (bytes is null || contentType is null)
            {
                return null;
            }

            return await UploadAsync(bytes, contentType, provider, createdBy, groupId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "{Provider} 프로필 사진을 옮기지 못했다 — 사진 없이 진행한다.", provider);
            return null;
        }
    }

    private static bool IsAllowedHost(string host, IReadOnlyCollection<string> allowed) =>
        allowed.Any(a =>
            !string.IsNullOrWhiteSpace(a)
            && (host.Equals(a.Trim(), StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + a.Trim().TrimStart('.'), StringComparison.OrdinalIgnoreCase)));

    private async Task<(byte[]? Bytes, string? ContentType)> DownloadAsync(Uri uri, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(PictureClient);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("프로필 사진을 받지 못했다: HTTP {Status}", (int)response.StatusCode);
            return (null, null);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("프로필 사진 응답이 그림이 아니다: {Type}", contentType ?? "(없음)");
            return (null, null);
        }

        if (response.Content.Headers.ContentLength is > MaxBytes)
        {
            logger.LogWarning("프로필 사진이 너무 크다: {Length}", response.Content.Headers.ContentLength);
            return (null, null);
        }

        // 길이를 안 알려 주는 응답도 있으므로 읽으면서 센다.
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                logger.LogWarning("프로필 사진이 {Max} 바이트를 넘어 받지 않는다.", MaxBytes);
                return (null, null);
            }

            buffer.Write(chunk, 0, read);
        }

        return (buffer.ToArray(), contentType);
    }

    /// <summary>그룹의 대표 사진을 바꾼다. [내 정보] 의 대표 지정 단추가 부르는 것과 같은 자리다.</summary>
    public async Task<bool> SetRepresentativeAsync(
        string groupId, string fileId, string createdBy, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(FileClient);
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"/group/{Uri.EscapeDataString(groupId)}/representative/{Uri.EscapeDataString(fileId)}");
            request.Headers.Add("X-User-Id", createdBy);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("대표 사진을 지정하지 못했다: HTTP {Status}", (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "대표 사진 지정 요청 예외");
            return false;
        }
    }

    private async Task<ImportedAvatar?> UploadAsync(
        byte[] bytes, string contentType, string provider, string createdBy, string? groupId,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(FileClient);

        using var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(bytes);

        // **그림 형식을 정확히 싣는다.** FileServer 는 이 값으로 그림인지를 정하고,
        // 그림이 아니면 썸네일을 거절한다 — 그러면 헤더의 작은 얼굴이 비어 나온다.
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", $"social-{provider}{ExtensionOf(contentType)}");
        form.Add(new StringContent("PROFILE"), "bizType");

        if (!string.IsNullOrWhiteSpace(groupId))
        {
            form.Add(new StringContent(groupId), "groupId");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/group/upload") { Content = form };
        request.Headers.Add("X-User-Id", createdBy);

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("FileServer 가 프로필 사진을 받지 않았다: HTTP {Status}", (int)response.StatusCode);
            return null;
        }

        var imported = Read(body);
        if (imported is null)
        {
            logger.LogWarning("FileServer 응답에서 그룹·주소를 찾지 못했다.");
        }

        return imported;
    }

    /// <summary>응답에서 그룹과 첫 파일 주소를 꺼낸다. 셸의 ProfilePhotoUpload.ReadGroupId 와 같은 모양이다.</summary>
    private static ImportedAvatar? Read(string body)
    {
        using var json = JsonDocument.Parse(body);

        if (!json.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }

        var item = data.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array
            ? (result.GetArrayLength() > 0 ? result[0] : default)
            : data;

        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("groupId", out var groupId)
            || !item.TryGetProperty("files", out var files)
            || files.ValueKind != JsonValueKind.Array
            || files.GetArrayLength() == 0
            || !files[0].TryGetProperty("downloadUrl", out var url)
            || !files[0].TryGetProperty("id", out var id))
        {
            return null;
        }

        var group = groupId.ValueKind == JsonValueKind.String ? groupId.GetString() : groupId.ToString();
        var fileId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
        var download = url.GetString();
        var representative = files[0].TryGetProperty("isRepresentative", out var rep)
            && rep.ValueKind == JsonValueKind.True;

        return string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(fileId)
               || string.IsNullOrWhiteSpace(download)
            ? null
            : new ImportedAvatar(group, fileId, download, representative);
    }

    private static string ExtensionOf(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".img",
    };

    /// <summary>FileServer 주소. 게이트웨이가 아니라 서비스 자체다.</summary>
    public string FileServerBaseUrl =>
        (configuration["FileServer:BaseUrl"] ?? "http://127.0.0.1:5350").TrimEnd('/');
}
