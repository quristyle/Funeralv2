using System.Net.Http.Headers;
using System.Text.Json;
using JSini.Web.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Data;

/// <summary>프로필 사진 올리기를 셸이 중계한다 — FileDownload 의 거울상이다. DxUpload 는 브라우저가 게이트웨이 주소로 직접 POST 해 BFF 에 토큰이 없어 못 쓰지만, 주소를 우리 오리진으로 주면 셸이 쿠키를 풀어 게이트웨이 토큰을 붙일 수 있다. 위조방지 토큰은 XHR 멀티파트에 실리지 않으므로 대신 Sec-Fetch-Site 와 Origin 으로 다른 사이트의 요청을 막는다.
/// 그룹 아이디는 여기서 계정에 저장한다 — DxUpload 의 FileUploaded 가 응답 본문을 주지 않아 화면이 저장할 길이 없고, 안 저장하면 방금 올린 사진이 사라진 것처럼 보인다. FilePicker 는 폼 저장과 함께 올리는 흐름이라 여기와 맞지 않아 지우지 않았다.</summary>
public static class ProfilePhotoUpload
{
    /// <summary>올리는 자리. 화면은 이 글자를 <c>DxUpload.UploadUrl</c> 에 준다.</summary>
    public const string Path = "/uploads/profile-photo";

    /// <summary>사진이 담기는 업무 영역. Vue 가 쓰던 값 그대로다 — FileServer 가 저장 폴더 이름으로 쓰고 익명 열람 목록에 PROFILE 이 없어서 아이디만 알면 남의 얼굴이 열리지 않는다. 바꾸지 않는다.</summary>
    public const string BizType = "PROFILE";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>올리는 경로를 연다. <c>UseJSiniWebApp</c> 이 부른다.</summary>
    public static IEndpointRouteBuilder MapJSiniProfilePhotoUpload(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, HandleAsync)
            .RequireAuthorization()

            // XHR 이라 위조방지 토큰을 실을 수 없다. 대신 출처를 본다 —
            // 이유와 방법은 이 클래스 머리말에 있다.
            .DisableAntiforgery()
            .WithName("JSiniProfilePhotoUpload");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(typeof(ProfilePhotoUpload));

        if (!IsSameOrigin(http))
        {
            logger.LogWarning(
                "다른 사이트에서 온 사진 업로드를 막았다. Sec-Fetch-Site={Site} Origin={Origin}",
                http.Request.Headers["Sec-Fetch-Site"].ToString(),
                http.Request.Headers.Origin.ToString());

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!http.Request.HasFormContentType)
        {
            return Results.BadRequest("멀티파트로 보내야 합니다.");
        }

        var form = await http.Request.ReadFormAsync(cancellationToken);

        if (form.Files.Count == 0)
        {
            return Results.BadRequest("올릴 파일이 없습니다.");
        }

        // 지금 이 계정의 사진 그룹. **첫 업로드면 없다** — 그때는 서버가 발급한다.
        var me = await gateway.GetOneAsync<AccountAvatar>("auth/user/info", cancellationToken);
        var groupId = me?.AvatarGroupId;

        using var multipart = new MultipartFormDataContent();
        var streams = new List<Stream>();

        try
        {
            foreach (var file in form.Files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType);

                // 칸 이름이 `files` 다 — FileServer 가 그 이름으로 먼저 찾는다.
                multipart.Add(part, "files", file.FileName);
            }

            if (!string.IsNullOrWhiteSpace(groupId))
            {
                multipart.Add(new StringContent(groupId), "groupId");
            }

            multipart.Add(new StringContent(BizType), "bizType");

            using var upstream = await gateway.SendRawAsync(
                HttpMethod.Post, "file/group/upload", multipart,
                cancellationToken: cancellationToken);

            if (!upstream.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "사진을 올리지 못했다 ({Status}).", (int)upstream.StatusCode);

                // 부품이 이 글자를 오류 자리에 보여 준다.
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            var body = await upstream.Content.ReadAsStringAsync(cancellationToken);
            var newGroupId = ReadGroupId(body);

            await SaveGroupAndAvatarAsync(gateway, logger, groupId, newGroupId, cancellationToken);

            return Results.Ok(new { groupId = newGroupId ?? groupId });
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "사진을 올리지 못했다 (게이트웨이 연결).");
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>새 그룹이면 계정에 적고 대표 사진을 계정의 Avatar 로 맞춘다. 여기서 실패해도 업로드는 성공으로 돌려준다 — 바이트는 이미 올라갔고 뒤처리가 안 되면 헤더 얼굴이 늦게 따라올 뿐이라, 「올리지 못했습니다」를 띄우면 사용자가 같은 파일을 또 올린다.</summary>
    private static async Task SaveGroupAndAvatarAsync(
        GatewayClient gateway,
        ILogger logger,
        string? oldGroupId,
        string? newGroupId,
        CancellationToken cancellationToken)
    {
        var groupId = newGroupId ?? oldGroupId;

        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        try
        {
            if (!string.Equals(groupId, oldGroupId, StringComparison.OrdinalIgnoreCase))
            {
                await gateway.PostAsync(
                    "auth/user/profile", new { avatarGroupId = groupId }, cancellationToken);
            }

            // 대표가 정해져 있으면 그 주소를 계정에 적어 둔다. 첫 업로드에서는
            // FileServer 가 첫 장을 대표로 잡아 준다.
            var files = await gateway.GetListAsync<GroupFile>(
                $"file/group/{groupId}", cancellationToken);

            var rep = files.FirstOrDefault(f => f.IsRepresentative) ?? files.FirstOrDefault();

            if (rep?.DownloadUrl is { Length: > 0 } url)
            {
                // **서버가 준 주소 그대로** 적는다. 셸 중계 경로로 바꿔 적으면
                // 게이트웨이와 같은 오리진에서 보는 다른 서비스가 못 읽는다.
                await gateway.PostAsync(
                    "auth/user/profile", new { avatar = url }, cancellationToken);
            }
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "사진은 올렸지만 계정에 대표·그룹을 적지 못했다.");
        }
    }

    /// <summary>응답에서 groupId 를 꺼낸다. 봉투가 data.result 배열이라 타입을 만드는 대신 필요한 칸 하나만 훑는다 — 나머지는 화면이 그룹을 다시 조회해서 얻는다.</summary>
    private static string? ReadGroupId(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);

            if (!json.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                return null;
            }

            return result[0].TryGetProperty("groupId", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 이 요청이 우리 화면에서 온 것인가. 이유는 클래스 머리말에 있다.
    /// </summary>
    private static bool IsSameOrigin(HttpContext http)
    {
        var site = http.Request.Headers["Sec-Fetch-Site"].ToString();

        if (!string.IsNullOrEmpty(site))
        {
            // `same-origin` 만 받는다. `same-site` 는 다른 서브도메인이라는 뜻이고,
            // 포털은 한 호스트에서만 돈다.
            return string.Equals(site, "same-origin", StringComparison.OrdinalIgnoreCase);
        }

        // 그 헤더가 없는 브라우저. `Origin` 이 없으면 같은 오리진의 평범한
        // 요청이고(브라우저가 교차 출처에는 반드시 붙인다), 있으면 맞춰 본다.
        var origin = http.Request.Headers.Origin.ToString();

        return string.IsNullOrEmpty(origin)
            || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && string.Equals(uri.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary><c>auth/user/info</c> 에서 그룹 아이디만.</summary>
    private sealed class AccountAvatar
    {
        public string? AvatarGroupId { get; set; }
    }

    /// <summary>그룹 안의 사진 하나. 대표를 찾는 데만 쓴다.</summary>
    private sealed class GroupFile
    {
        public bool IsRepresentative { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
