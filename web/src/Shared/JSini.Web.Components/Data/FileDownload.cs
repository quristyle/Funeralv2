using JSini.Web.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace JSini.Web.Components.Data;

/// <summary>첨부 내려받기를 셸이 중계한다. 백엔드 주소는 /api 를 가져 포털 오리진에서 404 가 나므로 그대로 쓰지 않는다.
/// 익명·로그인은 가르지 않고 지금 요청의 신원을 흘려보낸다 — FileServer 가 판정한다(공개 파일만 익명 통과).</summary>
public static class FileDownload
{
    /// <summary>내려받기 경로. 업무 모듈은 이 글자를 직접 적지 않고 <see cref="UrlFor"/> 를 쓴다.</summary>
    public const string Path = "/files";

    /// <summary>첨부 하나의 내려받기 주소.</summary>
    /// <param name="fileId">FileServer 가 발급한 파일 아이디</param>
    /// <param name="fileName">원본 이름. 한글 이름이 Content-Disposition 을 거치며 깨질 수 있어 주소에 싣는다.</param>
    public static string UrlFor(string fileId, string? fileName = null)
    {
        var url = $"{Path}/{Uri.EscapeDataString(fileId)}";

        return string.IsNullOrWhiteSpace(fileName)
            ? url
            : $"{url}?name={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>AI 작업 지시에 함께 올린 파일 하나의 주소. FileServer 가 아니라 ProjMngServer 로 간다 — 실행기는 게이트웨이를 못 지난다. 열쇠가 번호라 GUID 자리와 겹치지 않게 라우팅 모양을 갈랐다.</summary>
    public static string AiTaskUrlFor(long fileKey, string? fileName = null)
    {
        var url = $"{Path}/ai-task/{fileKey}";

        return string.IsNullOrWhiteSpace(fileName)
            ? url
            : $"{url}?name={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>첨부 하나의 썸네일(150x150 WebP) 주소. 작은 그림을 그릴 때 쓴다.</summary>
    public static string ThumbnailUrlFor(string fileId) =>
        $"{Path}/thumbnail/{Uri.EscapeDataString(fileId)}";

    /// <summary>앱알림 아이콘용 프로필 사진 주소. 못 받으면 사람 형상 그림자로 바꾼다 — 서비스워커에는 되받을 자리가 없어 실패를 여기서 바꾼다.</summary>
    public static string AvatarUrlFor(string fileId, string? token = null) =>
        string.IsNullOrWhiteSpace(token)
            ? $"{Path}/avatar/{Uri.EscapeDataString(fileId)}"
            : $"{Path}/avatar/{Uri.EscapeDataString(fileId)}?{AvatarTokenKey}={Uri.EscapeDataString(token)}";

    /// <summary>알림 아이콘 주소에 실려 오는 열쇠 칸. 알림은 로그인 쿠키가 없는 기기에도 가므로 알림 서비스가 그 사진 한 장만 여는 열쇠를 싣는다. 여기서는 풀지 않고 게이트웨이로 넘긴다.</summary>
    public const string AvatarTokenKey = "t";

    /// <summary>사람 형상 그림자(셸 wwwroot 의 avatar-fallback.png). 셸에 두는 이유는 서비스워커가 셸 오리진의 절대 경로로 받아 가기 때문이다.</summary>
    public const string FallbackAvatarPath = "/avatar-fallback.png";

    /// <summary>DB 의 /api/file/… 상대경로를 중계 경로로 옮긴다. 파일 주소가 아니면 그대로 돌려준다. DB 는 고치지 않고 보여 줄 때만 옮긴다 — 절대 URL 로 박으면 환경마다 다시 깨진다.</summary>
    public static string? RelayUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var match = FileApiUrl.Match(url);

        return match.Success ? UrlFor(match.Groups["id"].Value) : url;
    }

    /// <summary>게이트웨이의 파일 읽기 주소. /id/ 가 있는 형태와 없는 형태가 둘 다 있다.</summary>
    private static readonly Regex FileApiUrl = new(
        @"^/api/file/(?:download|thumbnail|medium|large)/(?:id/)?(?<id>[0-9a-fA-F-]{36})",
        RegexOptions.Compiled);

    /// <summary>사진·첨부 주소에서 파일 아이디(GUID)를 꺼낸다. 우리 파일이 아니면 null. 저장된 값은 두 모양이 섞여 있어 둘 다 받는다. 바깥 기본 이미지 주소도 여기서 걸러 「사진 없음」으로 본다.</summary>
    public static string? FileIdOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // ① 이미 셸 중계 경로인 경우 — 마지막 조각이 GUID 다.
        if (url.StartsWith(Path + "/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = url.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0 && Guid.TryParse(segments[^1], out var guid))
            {
                return guid.ToString();
            }
        }

        // FileApiUrl 은 쓰지 않는다 — 앞머리를 박은 정규식이라 오리진이 붙은 값을 놓친다.
        var match = AnyFileUrl.Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>어디에 박혀 있어도 찾아내는 파일 읽기 주소. <see cref="FileIdOf"/> 가 쓴다.</summary>
    private static readonly Regex AnyFileUrl = new(
        @"/api/file/(?:download|thumbnail|medium|large)/(?:id/)?(?<id>[0-9a-fA-F-]{36})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>자료실 첨부 하나의 내려받기 주소. 횟수를 세려면 FileServer 로 바로 가지 않고 AuthServer 를 거쳐야 한다. 그 경로는 로그인해야 열린다.</summary>
    /// <param name="archiveId">자료 아이디</param>
    /// <param name="fileId">그 자료에 매달린 파일 아이디</param>
    /// <param name="fileName">원본 파일 이름. 브라우저가 저장할 때 쓴다.</param>
    public static string ArchiveUrlFor(string archiveId, string fileId, string? fileName = null)
    {
        var url = $"{Path}/archive/{Uri.EscapeDataString(archiveId)}/{Uri.EscapeDataString(fileId)}";

        return string.IsNullOrWhiteSpace(fileName)
            ? url
            : $"{url}?name={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>내려받기 경로를 연다. UseJSiniWebApp 이 부른다.</summary>
    public static IEndpointRouteBuilder MapJSiniFileDownload(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{Path}/{{fileId}}", HandleAsync)
            // 로그인 화면의 공개 공지에서도 눌러야 하므로 여기서 막지 않는다.
            .AllowAnonymous()
            .WithName("JSiniFileDownload");

        // 썸네일 (150x150 WebP). 리터럴 thumbnail 이라 {fileId} 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/thumbnail/{{fileId}}", HandleThumbnailAsync)
            .AllowAnonymous()
            .WithName("JSiniFileThumbnail");

        // 앱알림 아이콘. 썸네일과 달리 실패해도 404 를 주지 않는다. 리터럴 avatar 라 {fileId} 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/avatar/{{fileId}}", HandleAvatarAsync)
            // 서비스워커가 받아 가는 자리다 — 쿠키가 있으면 그 사람 사진, 없으면 그림자. 어느 쪽이든 그림 하나를 돌려준다.
            .AllowAnonymous()
            .WithName("JSiniAvatarIcon");

        // AI 작업 첨부. 파일 서버가 아니라 프로젝트관리로 간다. 리터럴 ai-task 라 {fileId} 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/ai-task/{{fileKey:long}}", HandleAiTaskAsync)
            // 이 갈래에는 공개 파일이 없다 — AI 작업 첨부는 언제나 사내 자료라 로그인을 요구한다.
            .RequireAuthorization()
            .WithName("JSiniAiTaskFile");

        // 자료실·플레이어. 횟수를 세는 경로를 거친다. 리터럴 archive 라 {fileId} 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/archive/{{archiveId}}/{{fileId}}", HandleArchiveAsync)
            .RequireAuthorization()
            .WithName("JSiniArchiveDownload");

        return endpoints;
    }

    /// <summary>자료실 첨부를 중계한다. 횟수를 세는 경로가 FileServer 로 302 를 주고 HttpClient 가 따라간다. 같은 게이트웨이라 Authorization 이 그대로 따라간다.</summary>
    private static Task HandleArchiveAsync(
        string archiveId,
        string fileId,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var file))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return RelayAsync(
            $"auth/help/archives/{Uri.EscapeDataString(archiveId)}/files/{file}/download",
            file, name, http, gateway, loggers, cancellationToken,
            // 여기서는 캐시를 열지 않는다 — 캐시본을 쓰면 요청이 서버에 닿지 않아 횟수가 멈춘다.
            cacheable: false);
    }

    /// <summary>AI 작업 첨부를 중계한다. 무엇을 어떤 형식으로 낼지는 AiTaskFilesController 가 정한다 — 여기서 또 가르면 두 판정이 갈린다.</summary>
    private static Task HandleAiTaskAsync(
        long fileKey,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
        => RelayAsync(
            $"projmng/ai-tasks/files/{fileKey}/content",
            $"ai-task-{fileKey}", name, http, gateway, loggers, cancellationToken,
            // 그림이면 캐시한다 — 판정은 위쪽이 준 형식이 한다. 이 번호의 바이트는 덧쓰이지 않는다.
            cacheable: true);

    private static Task HandleThumbnailAsync(
        string fileId,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var id))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        // 썸네일(150x150 WebP)을 우선 요청하되, 미변환/오류 시 원본 파일로 1회 안전 폴백
        return RelayAsync(
            $"file/thumbnail/{id}", id, null, http, gateway, loggers, cancellationToken,
            cacheable: true,
            fallbackPath: $"file/download/id/{id}");
    }

    /// <summary>알림 아이콘용 프로필 사진. 못 받으면 사람 형상 그림자로 302 로 넘긴다. 검증표를 붙이지 않는다 — 같은 아이디라도 신원에 따라 답이 갈린다.</summary>
    private static async Task HandleAvatarAsync(
        string fileId,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        // 알림 서비스가 실어 보낸 열쇠. 없으면 지금 요청의 신원이 그대로 올라간다.
        var token = http.Request.Query[AvatarTokenKey].ToString();

        // o=1 은 썸네일을 건너뛰고 원본을 준다. 메일 본문에 싣는 사진이 쓴다 — Outlook 은 WebP 를 못 그린다.
        var original = http.Request.Query["o"] == "1";

        if (Guid.TryParse(fileId, out var id))
        {
            try
            {
                // 썸네일(150x150 WebP)을 먼저 받고, 아직 안 만들어졌으면 원본으로 한 번 물러선다.
                var upstream = original
                    ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                    : await gateway.SendRawAsync(
                        HttpMethod.Get, $"file/thumbnail/{id}",
                        bearer: token, cancellationToken: cancellationToken);

                if (!upstream.IsSuccessStatusCode)
                {
                    upstream.Dispose();
                    upstream = await gateway.SendRawAsync(
                        HttpMethod.Get, $"file/download/id/{id}",
                        bearer: token, cancellationToken: cancellationToken);
                }

                using (upstream)
                {
                    var contentType = upstream.Content.Headers.ContentType?.ToString();

                    // 그림인지 확인하고 흘려보낸다 — 위쪽이 200 과 함께 오류 JSON 을 줄 때가 있어 형식을 안 보면 글자를 아이콘으로 물린다.
                    if (upstream.IsSuccessStatusCode
                        && contentType is not null
                        && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        http.Response.ContentType = contentType;

                        if (upstream.Content.Headers.ContentLength is { } length)
                        {
                            http.Response.ContentLength = length;
                        }

                        http.Response.Headers.CacheControl =
                            $"private, max-age={ImageMaxAgeSeconds}";

                        await upstream.Content.CopyToAsync(http.Response.Body, cancellationToken);
                        return;
                    }
                }
            }
            catch (ApiException ex)
            {
                loggers.CreateLogger(typeof(FileDownload)).LogInformation(
                    ex, "알림 아이콘 {FileId} 를 가져오지 못했습니다. 그림자로 보냅니다.", id);
            }
        }

        // 캐시에 남기지 않는다. 다음 알림에서는 사진이 나올 수 있다.
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Redirect(FallbackAvatarPath);
    }

    private static Task HandleAsync(
        string fileId,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        // 경로 조작을 막는다. 파일 아이디는 언제나 GUID 다.
        if (!Guid.TryParse(fileId, out var id))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return RelayAsync(
            $"file/download/id/{id}", id, name, http, gateway, loggers, cancellationToken,
            // 이 갈래로 그림이 지나간다(얼굴·영정 사진·미디어 썸네일·공지의 img). 실제로 캐시할지는 형식을 보고 정한다(Cacheable).
            cacheable: true);
    }

    /// <summary>게이트웨이의 한 경로를 그대로 흘려보낸다. cacheable 은 이 갈래가 캐시를 허용하는지이고 실제 판정은 형식을 보고 한다. 파일 아이디가 GUID 인 갈래는 대부분이 이쪽이다.</summary>
    private static Task RelayAsync(
        string upstreamPath,
        Guid id,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        bool cacheable,
        string? fallbackPath = null)
        => RelayAsync(
            upstreamPath, id.ToString(), name, http, gateway, loggers,
            cancellationToken, cacheable, fallbackPath);

    /// <summary>중계 본체. tag 는 바이트를 가리키는 변하지 않는 이름(검증표·로그가 쓴다). GUID 가 아닌 열쇠는 접두사를 붙여 갈래끼리 검증표가 겹치지 않게 한다.</summary>
    private static async Task RelayAsync(
        string upstreamPath,
        string tag,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        bool cacheable,
        string? fallbackPath = null)
    {
        var logger = loggers.CreateLogger(typeof(FileDownload));

        HttpResponseMessage upstream;

        try
        {
            upstream = await gateway.SendRawAsync(
                HttpMethod.Get, upstreamPath, cancellationToken: cancellationToken);

            // 썸네일 등이 실패(404 등)했을 때 원본으로 안전하게 1회 폴백한다.
            if (!upstream.IsSuccessStatusCode && !string.IsNullOrEmpty(fallbackPath))
            {
                upstream.Dispose();
                upstream = await gateway.SendRawAsync(
                    HttpMethod.Get, fallbackPath, cancellationToken: cancellationToken);
            }
        }
        catch (ApiException ex)
        {
            // 여기서 잡지 않으면 스택 추적이 브라우저에 찍힌다. 개발 장비에서는 FileServer 가 운영 호스트로 302 를 주는데 그 호스트가 안 풀려 502 가 날 수 있다 — 운영에서는 정상이다.
            logger.LogWarning(ex, "첨부 {FileId} 를 가져오지 못했습니다 (게이트웨이 연결).", tag);

            http.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var _ = upstream;

        if (!upstream.IsSuccessStatusCode)
        {
            var authenticated = http.User.Identity?.IsAuthenticated == true;

            logger.LogInformation(
                "첨부 {FileId} 를 내려주지 못했습니다 ({Status}). 로그인 여부: {Auth}",
                tag, (int)upstream.StatusCode, authenticated);

            // 모든 실패를 404 로 뭉개지 않는다 — 익명에게만 404 를 주고, 로그인한 요청에는 인증 관련 상태를 그대로 넘긴다. 5xx 는 404 로 남긴다: FileServer 는 없는 아이디에도 500 을 주므로 502 로 올리면 지워진 첨부가 장애처럼 보인다.
            http.Response.StatusCode = authenticated
                && (int)upstream.StatusCode is
                    StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                ? (int)upstream.StatusCode
                : StatusCodes.Status404NotFound;

            return;
        }

        http.Response.ContentType =
            upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        if (upstream.Content.Headers.ContentLength is { } length)
        {
            http.Response.ContentLength = length;
        }

        http.Response.Headers.ContentDisposition = Disposition(name, upstream);

        if (Cacheable(cacheable, http.Response.ContentType))
        {
            // 이 아이디의 바이트는 절대 바뀌지 않으므로 검증표를 우리가 만들어도 된다.
            http.Response.Headers.ETag = $"\"{tag}\"";
            http.Response.Headers.CacheControl = $"private, max-age={ImageMaxAgeSeconds}";

            // 위쪽을 부른 뒤에 따진다 — 앞에서 끊으면 비공개로 되돌린 파일에 계속 304 를 준다. 아끼는 것은 판정이 아니라 바이트 전송뿐이다.
            if (NoneMatch(http.Request, tag))
            {
                http.Response.StatusCode = StatusCodes.Status304NotModified;
                http.Response.ContentLength = null;
                return;
            }
        }
        else
        {
            // 첨부는 공개 여부가 바뀔 수 있어 중간 캐시에 남으면 안 된다.
            http.Response.Headers.CacheControl = "private, no-store";
        }

        await upstream.Content.CopyToAsync(http.Response.Body, cancellationToken);
    }

    /// <summary>그림을 브라우저가 들고 있어도 되는 시간(초, 5분). 기준은 그림이 낡을까가 아니라 볼 자격이 없어졌는데 얼마나 더 보이느냐다 — private 이라 중간 캐시엔 안 남는다.</summary>
    private const int ImageMaxAgeSeconds = 300;

    /// <summary>이 응답을 브라우저가 들고 있어도 되는가. 형식으로 가른다 — 브라우저가 응답을 다루는 기준과 같다. 자료실 갈래(allowed=false)는 횟수를 세야 하므로 캐시하지 않는다.</summary>
    internal static bool Cacheable(bool allowed, string? contentType) =>
        allowed
        && contentType is not null
        && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>브라우저가 들고 있는 것이 이 파일과 같은가(If-None-Match). * 도 받는다.</summary>
    internal static bool NoneMatch(HttpRequest request, Guid id)
        => NoneMatch(request, id.ToString());

    /// <summary>브라우저가 들고 있는 것이 이 바이트와 같은가(If-None-Match). * 도 받는다.</summary>
    internal static bool NoneMatch(HttpRequest request, string tag)
    {
        if (request.Headers.IfNoneMatch.Count == 0)
        {
            return false;
        }

        var mine = $"\"{tag}\"";

        foreach (var header in request.Headers.IfNoneMatch)
        {
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            foreach (var candidate in header.Split(','))
            {
                var trimmed = candidate.Trim();

                // 약한 검증표(`W/"…"`)로 돌아오는 경우가 있다. 앞의 표시만 뗀다.
                if (trimmed.StartsWith("W/", StringComparison.Ordinal))
                {
                    trimmed = trimmed[2..];
                }

                if (trimmed == "*" || string.Equals(trimmed, mine, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>저장될 이름을 정한다. 우리가 아는 이름이 있으면 쓰고, 없으면 위쪽 헤더를 넘긴다. SetHttpFileName 을 써야 한글이 filename*=UTF-8 로 적힌다 — 손으로 적으면 브라우저마다 깨진다.</summary>
    private static string Disposition(string? name, HttpResponseMessage upstream)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return upstream.Content.Headers.ContentDisposition?.ToString()
                ?? "attachment";
        }

        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(name);
        return disposition.ToString();
    }
}
