using System.Net.Http.Headers;
using System.Text.Json;
using JSini.Web.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Data;

/// <summary>
/// 프로필 사진 올리기를 <b>셸이 중계한다</b>. <see cref="FileDownload"/> 의 거울상이다.
/// </summary>
/// <remarks>
/// <para>
/// [「DxUpload 은 못 쓴다」가 절반만 맞았다]
/// </para>
///
/// <para>
/// 한동안 <c>DxUpload</c> 을 아예 금지 대상으로 적어 두고 표준
/// <c>InputFile</c>(<c>FilePicker</c>)만 썼다. 이유는 이랬다 — 그 부품은
/// <b>브라우저가 직접</b> 지정한 주소로 POST 하는데, 이 포털은 BFF 라
/// 브라우저에 게이트웨이 토큰이 없다. 맞는 말이다. <b>다만 그것은 주소를
/// 게이트웨이로 줬을 때의 이야기다.</b>
/// </para>
///
/// <para>
/// 주소를 <b>우리 오리진</b>으로 주면 사정이 다르다. 브라우저는 포털(:5557)로
/// 보내고 거기에는 인증 쿠키가 저절로 실린다. 그 쿠키를 푸는 것은 셸이고,
/// 셸에는 게이트웨이 토큰이 있다. 내려받기를 이미 그렇게 하고 있다
/// (<c>/files/{id}</c>) — 올리기도 같은 길로 두면 된다.
/// </para>
///
/// <para>
/// 그래서 BFF 를 지키면서 DevExpress 부품을 쓴다. 잃는 것은 없다.
/// </para>
///
/// <para>
/// [<c>FilePicker</c> 를 지우지 않았다]
/// </para>
///
/// <para>
/// 첨부를 붙이는 다른 화면 여섯이 아직 그것을 쓴다. 그쪽은 <b>폼을 저장할 때
/// 함께</b> 올리는 흐름이라(고른 뒤 폼을 채우고 저장을 누른다) 「고르는 즉시
/// 올라가는」 부품과 맞지 않는다. 프로필 사진은 저장 단추가 없고 올리는 것이
/// 곧 끝이라 이쪽이 맞다.
/// </para>
///
/// <para>
/// [<b>그룹 아이디를 서버가 저장한다</b> — 화면이 응답을 못 읽는다]
/// </para>
///
/// <para>
/// FileServer 는 첫 업로드에서 <c>groupId</c> 를 새로 발급해 응답에 담아 준다.
/// 그 값을 계정(<c>avatar_group_id</c>)에 적어 두지 않으면 다음에 들어올 때
/// 그룹을 몰라 <b>방금 올린 사진이 사라진 것처럼 보인다.</b>
/// </para>
///
/// <para>
/// 그런데 <c>DxUpload</c> 의 <c>FileUploaded</c> 는 <c>FileInfo</c> 만 준다 —
/// <b>응답 본문을 주지 않는다</b>(오류일 때만 <c>RequestInfo.ResponseText</c> 가
/// 있다). 26.1 에서 확인했다. 그래서 화면이 그 값을 받아 저장할 길이 없고,
/// <b>여기서 저장한다.</b> 화면은 올라간 뒤 <c>auth/user/info</c> 를 다시 읽어
/// 새 그룹을 알게 된다.
/// </para>
///
/// <para>
/// 대표 사진도 여기서 맞춘다. 계정의 <c>Avatar</c> 는 파일 그룹과 따로라
/// 누군가 맞춰 주지 않으면 헤더가 빈 동그라미로 남는다.
/// </para>
///
/// <para>
/// [위조 요청을 <c>Sec-Fetch-Site</c> 로 막는다]
/// </para>
///
/// <para>
/// <c>DxUpload</c> 은 XHR 로 멀티파트를 보내므로 위조방지 토큰을 싣지 못한다
/// (그 부품이 헤더를 넣을 자리는 주지만, 회로 안에서는 토큰을 발급할
/// <c>HttpContext</c> 가 없다). 그래서 위조방지를 끄는데, 끄기만 하면 남의
/// 페이지가 이 사용자의 쿠키로 사진을 밀어 넣을 수 있다.
/// </para>
///
/// <para>
/// 대신 <b>브라우저가 붙이는 출처 표시</b>를 본다. <c>Sec-Fetch-Site</c> 는
/// 스크립트가 위조할 수 없고, 다른 사이트에서 온 요청에는
/// <c>cross-site</c> 가 실린다. 그 값이 없는 브라우저에서는
/// <c>Origin</c> 을 우리 호스트와 맞춰 본다.
/// </para>
/// </remarks>
public static class ProfilePhotoUpload
{
    /// <summary>올리는 자리. 화면은 이 글자를 <c>DxUpload.UploadUrl</c> 에 준다.</summary>
    public const string Path = "/uploads/profile-photo";

    /// <summary>
    /// 사진이 담기는 업무 영역.
    /// </summary>
    /// <remarks>
    /// Vue 가 쓰던 값 그대로다. FileServer 는 이 글자를 저장 폴더 이름으로 쓰고
    /// (<c>FileService.GetBizFolder</c>), 익명 열람 목록
    /// (<c>Files:AnonymousReadablePaths</c>)에 <c>PROFILE</c> 은 들어 있지 않다 —
    /// 남의 얼굴 사진이 아이디만 알면 열리는 일이 없다. 바꾸지 않는다.
    /// </remarks>
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
                HttpMethod.Post, "file/group/upload", multipart, cancellationToken);

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

    /// <summary>
    /// 새 그룹이면 계정에 적고, 대표 사진을 계정의 <c>Avatar</c> 로 맞춘다.
    /// </summary>
    /// <remarks>
    /// <b>여기서 실패해도 업로드는 성공으로 돌려준다.</b> 바이트는 이미 올라갔고,
    /// 이 뒤처리가 안 되면 헤더 얼굴이 늦게 따라올 뿐이다 — 그것 때문에
    /// 「올리지 못했습니다」를 띄우면 사용자가 같은 파일을 또 올린다.
    /// </remarks>
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

    /// <summary>
    /// 응답에서 <c>groupId</c> 를 꺼낸다.
    /// </summary>
    /// <remarks>
    /// 봉투가 <c>{ data: { result: [ { groupId, files } ] } }</c> 다. 타입을
    /// 만들어 두는 대신 필요한 칸 하나만 훑는다 — 나머지는 화면이 그룹을 다시
    /// 조회해서 얻는다.
    /// </remarks>
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
