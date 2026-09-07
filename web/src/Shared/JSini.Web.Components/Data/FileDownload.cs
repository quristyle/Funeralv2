using JSini.Web.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace JSini.Web.Components.Data;

/// <summary>
/// 첨부파일 내려받기를 <b>셸이 중계한다</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 서버가 준 주소를 그대로 쓰지 못하나]
/// </para>
///
/// <para>
/// 백엔드는 첨부마다 <c>/api/file/download/id/{fileId}</c> 를 함께 내려준다.
/// Vue 시절에는 브라우저가 게이트웨이와 같은 오리진(nginx 뒤)에 있어서 그
/// 주소를 <c>&lt;a href&gt;</c> 에 그대로 넣으면 됐다. 지금 브라우저가 보는
/// 것은 포털(:5557)이고 <b>거기에는 <c>/api</c> 가 없다</b> — 그대로 쓰면 404 다.
/// </para>
///
/// <para>
/// [익명과 로그인을 가르는 코드가 여기 없다 — 그게 요점이다]
/// </para>
///
/// <para>
/// 판정은 이미 FileServer 가 한다(<c>PublicFileAccessFilter</c>). 게이트웨이가
/// 붙여 주는 <c>X-User-Id</c> 가 있으면 로그인 사용자로 보고 통과시키고,
/// 없으면 <c>is_public</c> 이 켜진 파일만 통과시킨다. 그리고 공개 공지에 붙은
/// 첨부는 AuthServer 가 저장할 때마다 <c>is_public</c> 을 켜 준다
/// (<c>PublicFileSyncService</c> · 결정 D-S10).
/// </para>
///
/// <para>
/// 그래서 여기서 할 일은 <b>지금 요청의 신원을 그대로 흘려보내는 것</b>뿐이다.
/// 로그인한 사람의 요청이면 <c>AuthTokenHandler</c> 가 토큰을 붙이고, 로그인
/// 화면(익명)에서 온 요청이면 붙일 토큰이 없어 안 붙는다
/// (<c>TokenStore</c> 가 <c>HttpContext.User</c> 를 보는데 거기 클레임이 없다).
/// <b>둘을 우리가 가르지 않는다</b> — 가르는 코드를 여기 또 두면 언젠가
/// 백엔드 판정과 어긋나고, 어긋나는 쪽은 늘 「열려서는 안 되는데 열린」 쪽이다.
/// </para>
///
/// <para>
/// 열리는 범위가 넓어지지 않는다. 게이트웨이의 <c>/api/file/download/**</c> 는
/// 원래 <c>AuthorizationPolicy: Anonymous</c> 라 브라우저가 직접 부를 수 있던
/// 경로다. 이 중계는 그 경로를 포털 오리진에서 부를 수 있게 할 뿐이다.
/// </para>
/// </remarks>
public static class FileDownload
{
    /// <summary>내려받기 경로. 업무 모듈은 이 글자를 직접 적지 않고 <see cref="UrlFor"/> 를 쓴다.</summary>
    public const string Path = "/files";

    /// <summary>
    /// 첨부 하나의 내려받기 주소.
    /// </summary>
    /// <param name="fileId">FileServer 가 발급한 파일 아이디</param>
    /// <param name="fileName">
    /// 원본 파일 이름. 브라우저가 저장할 때 쓴다.
    ///
    /// <para>
    /// 주소에 이름을 실어 보내는 이유는 <b>FileServer 가 주는 이름을 믿을 수
    /// 없어서가 아니라</b>, 한글 이름이 <c>Content-Disposition</c> 을 거치며
    /// 깨져 오는 경우가 있어서다. 우리가 아는 이름이 있으면 그것을 쓴다.
    /// </para>
    /// </param>
    public static string UrlFor(string fileId, string? fileName = null)
    {
        var url = $"{Path}/{Uri.EscapeDataString(fileId)}";

        return string.IsNullOrWhiteSpace(fileName)
            ? url
            : $"{url}?name={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// DB 에 저장된 <c>/api/file/…</c> 주소를 중계 경로로 옮긴다.
    /// 파일 주소가 아니면 <b>그대로 돌려준다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// [저장된 값이 전부 상대경로다 — 운영 DB 에서 66건 확인]
    /// </para>
    ///
    /// <para>
    /// 영정 사진(<c>deceaseds.memorial_photo_url</c>) · 미디어 원본과 썸네일
    /// (<c>media_sources.url</c> · <c>thumbnailurl</c> · <c>aacurl</c> ·
    /// <c>oggurl</c> · <c>webmurl</c>)에 <c>/api/file/download/…</c> ·
    /// <c>/api/file/thumbnail/…</c> 이 그대로 들어 있다. <b>절대 URL 은 한 건도
    /// 없다.</b> 브라우저가 게이트웨이와 같은 오리진이던 Vue 시절에 만들어진
    /// 값이라, 포털(:5557)에서 <c>&lt;img src&gt;</c> 에 그대로 넣으면 전부 404 다.
    /// </para>
    ///
    /// <para>
    /// <b>DB 를 고치지 않는다.</b> 절대 URL 로 박아 넣으면 오리진이 환경마다
    /// 달라(운영 · 개발) 옮길 때마다 다시 깨진다. 저장 값은 상대경로로 두고
    /// <b>보여 줄 때</b> 옮긴다.
    /// </para>
    ///
    /// <para>
    /// 썸네일·중간 크기도 같은 자리로 보낸다. 중계는 원본을 주므로 그림이
    /// 조금 클 뿐 안 나오지는 않는다 — 크기별 중계를 따로 두는 것은 목록에
    /// 큰 그림이 실제로 문제가 될 때 한다.
    /// </para>
    /// </remarks>
    public static string? RelayUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var match = FileApiUrl.Match(url);

        return match.Success ? UrlFor(match.Groups["id"].Value) : url;
    }

    /// <summary>
    /// 게이트웨이의 파일 읽기 주소. <c>/id/</c> 가 있는 형태와 없는 형태가 둘 다 있다.
    /// </summary>
    private static readonly Regex FileApiUrl = new(
        @"^/api/file/(?:download|thumbnail|medium|large)/(?:id/)?(?<id>[0-9a-fA-F-]{36})",
        RegexOptions.Compiled);

    /// <summary>
    /// 자료실 첨부 하나의 내려받기 주소. <b>내려받은 횟수를 센다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 자료실과 플레이어 다운로드는 FileServer 로 바로 가지 않는다. AuthServer 의
    /// <c>auth/help/archives/{자료}/files/{파일}/download</c> 를 거쳐야 <b>내려받은
    /// 횟수가 올라가고</b>, 그쪽이 다시 FileServer 로 302 로 넘긴다. 브라우저가
    /// FileServer 를 직접 열면 셀 수가 없다 — 자료실 화면의 「내려받기」 칸이
    /// 그 숫자다.
    /// </para>
    ///
    /// <para>
    /// 그 경로는 <b>로그인해야 열린다</b>(AuthServer 가 <c>UserContext</c> 가
    /// 없으면 401). 자료실·플레이어 다운로드 둘 다 로그인 화면 뒤에 있으므로
    /// 맞는 동작이다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 내려받기 경로를 연다. <c>UseJSiniWebApp</c> 이 부른다.
    /// </summary>
    public static IEndpointRouteBuilder MapJSiniFileDownload(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{Path}/{{fileId}}", HandleAsync)
            // 로그인 화면의 공개 공지에서도 눌러야 한다. 무엇을 내려줄지는
            // 위 주석대로 FileServer 가 정하므로 여기서 막지 않는다.
            .AllowAnonymous()
            .WithName("JSiniFileDownload");

        // 자료실·플레이어. 횟수를 세는 경로를 거친다.
        //
        // **리터럴 `archive` 가 있어 위의 `{fileId}` 와 겹치지 않는다** —
        // 라우팅이 리터럴을 매개변수보다 먼저 고르고, 어차피 칸 수가 다르다.
        endpoints.MapGet($"{Path}/archive/{{archiveId}}/{{fileId}}", HandleArchiveAsync)
            .RequireAuthorization()
            .WithName("JSiniArchiveDownload");

        return endpoints;
    }

    /// <summary>
    /// 자료실 첨부를 중계한다. 횟수를 세는 경로로 들어가면 그쪽이 FileServer 로
    /// 302 를 주고, <c>HttpClient</c> 가 그 자리를 따라가 실제 바이트를 가져온다.
    /// </summary>
    /// <remarks>
    /// 넘어가는 곳이 <b>같은 게이트웨이</b>라 <c>Authorization</c> 헤더가 그대로
    /// 따라간다. 다른 호스트로 넘어가면 <c>HttpClient</c> 가 헤더를 떼는데,
    /// 그때는 FileServer 가 익명으로 보고 <c>is_public</c> 만 통과시킨다.
    /// </remarks>
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
            // **여기서는 캐시를 열지 않는다.** 이 경로의 목적 절반이 내려받은
            // 횟수를 세는 것이라, 브라우저가 캐시본을 쓰면 요청이 서버에 닿지
            // 않아 숫자가 멈춘다. 자료실 화면의 「내려받기」 칸이 그 숫자다.
            cacheable: false);
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
            // 이 갈래로 그림이 지나간다 — 헤더의 얼굴 · 영정 사진 · 장비 미리보기 ·
            // 미디어 썸네일 · 공지 본문의 <img>. 실제로 열지 말지는 형식을 보고
            // 정한다(Cacheable). 여기서는 "이 경로는 캐시를 허용한다" 까지만 말한다.
            cacheable: true);
    }

    /// <summary>
    /// 게이트웨이의 한 경로를 그대로 흘려보낸다. 두 갈래가 이것을 함께 쓴다 —
    /// FileServer 로 바로 가는 길과, 횟수를 세고 302 로 넘어가는 길.
    ///
    /// <para>
    /// <c>cacheable</c> 은 <b>이 갈래가</b> 캐시를 허용하는지다. 실제로 열리는지는
    /// 형식까지 보고 정한다(<see cref="Cacheable"/>) — 그림만 열린다.
    /// 자료실 갈래는 <c>false</c> 이고 이유는 그쪽 호출부 주석에 있다.
    /// </para>
    /// </summary>
    private static async Task RelayAsync(
        string upstreamPath,
        Guid id,
        string? name,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        bool cacheable)
    {
        var logger = loggers.CreateLogger(typeof(FileDownload));

        HttpResponseMessage upstream;

        try
        {
            upstream = await gateway.SendRawAsync(
                HttpMethod.Get, upstreamPath, cancellationToken: cancellationToken);
        }
        catch (ApiException ex)
        {
            // **여기서 잡지 않으면 스택 추적이 그대로 브라우저에 찍힌다.**
            // 첨부 하나를 못 받는 일로 내부 구조를 흘릴 이유가 없다.
            //
            // 개발 장비에서 자주 보게 된다: FileServer 는 로컬에 바이트가 없으면
            // `Storage:FallbackUrl`(운영 호스트)로 302 를 주는데, 그 호스트가
            // 개발망에서 안 풀린다. 운영에서는 풀리므로 정상 동작한다.
            // (루트 CLAUDE.md 의 「개발 장비에서 올린 파일은 운영에 바이트가 없다」)
            logger.LogWarning(ex, "첨부 {FileId} 를 가져오지 못했습니다 (게이트웨이 연결).", id);

            http.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var _ = upstream;

        if (!upstream.IsSuccessStatusCode)
        {
            var authenticated = http.User.Identity?.IsAuthenticated == true;

            logger.LogInformation(
                "첨부 {FileId} 를 내려주지 못했습니다 ({Status}). 로그인 여부: {Auth}",
                id, (int)upstream.StatusCode, authenticated);

            // **모든 실패를 404 로 뭉개면 안 된다** (실제로 밟음).
            //
            // 한동안 위쪽이 무엇을 답했든 404 를 내려주고 있었다. 「그 아이디의
            // 파일은 있다」를 익명에게 알려 주지 않으려는 것이었고, 그 판단은
            // 익명 요청에 대해서는 지금도 맞다 — FileServer 도 같은 이유로
            // 403 대신 404 를 준다(`PublicFileAccessFilter`).
            //
            // 문제는 **로그인한 요청까지 그렇게 했다**는 것이다. 헤더에 얼굴을
            // 띄우면서 이 경로가 화면마다 불리게 됐는데, AuthServer 가 잠깐
            // 내려가 있는 동안 얼굴이 깨졌고 브라우저 개발자 도구에는 404 만
            // 찍혔다. 그러면 「없는 파일을 가리키고 있다」로 읽힌다 —
            // 실제로는 그 파일이 멀쩡히 있고 인증이 잠깐 끊긴 것이었다.
            //
            // 그래서 **인증 관련 두 가지만** 로그인한 요청에 그대로 넘긴다.
            // 익명에게는 계속 404 다.
            //
            // 5xx 는 일부러 404 로 남겨 둔다. FileServer 는 <b>없는 아이디에도
            // 500 을 준다</b>(`download/id/{id}` 의 마지막 catch — 메타가 없으면
            // FileNotFoundException 이 아닌 예외가 난다). 그것을 502 로 올리면
            // 「지워진 첨부」가 전부 게이트웨이 장애처럼 보인다 — 고치는 자리는
            // FileServer 쪽이고, 여기서 뒤집으면 공지 첨부까지 함께 바뀐다.
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
            http.Response.Headers.ETag = $"\"{id}\"";
            http.Response.Headers.CacheControl = $"private, max-age={ImageMaxAgeSeconds}";

            // **위쪽을 부른 뒤에 따진다.** 앞에서 끊으면 판정(FileServer)을 건너뛰게
            // 되고, 비공개로 되돌린 파일에 계속 304 를 주어 브라우저가 캐시본을
            // 계속 쓴다 — 틀리는 방향이 「열려서는 안 되는데 열린」 쪽이다.
            // 여기서 아끼는 것은 판정이 아니라 **바이트 전송**뿐이다.
            if (NoneMatch(http.Request, id))
            {
                http.Response.StatusCode = StatusCodes.Status304NotModified;
                http.Response.ContentLength = null;
                return;
            }
        }
        else
        {
            // 첨부는 사람이 눌러 받는 것이고 공개 여부가 바뀔 수 있다.
            // 중간 캐시에 남으면 비공개로 되돌린 파일이 계속 나갈 수 있다.
            http.Response.Headers.CacheControl = "private, no-store";
        }

        await upstream.Content.CopyToAsync(http.Response.Body, cancellationToken);
    }

    /// <summary>
    /// 그림을 브라우저가 들고 있어도 되는 시간(초).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>바이트가 바뀔 걱정은 없다.</b> 주소의 열쇠가 파일 아이디이고 FileServer 는
    /// 덧쓰지 않는다 — 사진을 바꾸면 아이디가 새로 생긴다. 그래서 이 값을 정하는
    /// 기준은 「그림이 낡을까」가 아니라 <b>「볼 자격이 없어졌는데 얼마나 더
    /// 보이느냐」</b> 하나다.
    /// </para>
    ///
    /// <para>
    /// 5분으로 잡았다. 화면을 옮겨 다니는 동안은 한 번도 다시 받지 않고,
    /// 권한이 끊긴 뒤 남는 창은 그 사람 브라우저 안에서 5분이다 —
    /// <c>private</c> 이라 중간 캐시에는 애초에 안 남는다. 그 5분 사이에도
    /// 그 사람은 방금까지 볼 자격이 있던 사람이다.
    /// </para>
    /// </remarks>
    private const int ImageMaxAgeSeconds = 300;

    /// <summary>
    /// 이 응답을 브라우저가 들고 있어도 되는가.
    /// </summary>
    /// <param name="allowed">
    /// 이 경로가 캐시를 허용하는가. 자료실 갈래는 <c>false</c> 다 —
    /// <b>내려받은 횟수를 세는 경로</b>라서, 브라우저가 캐시본을 쓰면 요청이
    /// 서버에 닿지 않아 숫자가 올라가지 않는다.
    /// </param>
    /// <param name="contentType">위쪽이 준 형식.</param>
    /// <remarks>
    /// 가르는 기준이 <b>형식</b>인 이유는, 그것이 브라우저가 이 응답을 어떻게
    /// 다루는지와 정확히 같은 기준이기 때문이다. 화면에 박히는 것(<c>&lt;img&gt;</c>)은
    /// 목록을 다시 그릴 때마다 다시 요청되므로 캐시가 있어야 하고, 사람이 눌러
    /// 받는 첨부는 한 번 받으면 끝이라 캐시로 얻을 것이 없다.
    ///
    /// <para>
    /// 이름(<c>?name=</c>)이 있느냐로 가르지 않는다. 그쪽은 <b>부르는 화면이
    /// 넘겨 주기로 한 값</b>이라 빠뜨리면 조용히 판정이 뒤집힌다.
    /// </para>
    /// </remarks>
    internal static bool Cacheable(bool allowed, string? contentType) =>
        allowed
        && contentType is not null
        && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 브라우저가 들고 있는 것이 이 파일과 같은가 (<c>If-None-Match</c>).
    /// </summary>
    /// <remarks>
    /// <c>*</c> 도 받아 준다 — 「무엇이든 들고 있다」는 뜻이고, 이 주소에서
    /// 나올 수 있는 것은 그 아이디의 바이트 하나뿐이라 언제나 같다.
    /// </remarks>
    internal static bool NoneMatch(HttpRequest request, Guid id)
    {
        if (request.Headers.IfNoneMatch.Count == 0)
        {
            return false;
        }

        var mine = $"\"{id}\"";

        foreach (var header in request.Headers.IfNoneMatch)
        {
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            foreach (var tag in header.Split(','))
            {
                var trimmed = tag.Trim();

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

    /// <summary>
    /// 저장될 이름을 정한다. 우리가 아는 이름이 있으면 그것을 쓰고,
    /// 없으면 위쪽이 준 헤더를 그대로 넘긴다.
    /// </summary>
    /// <remarks>
    /// <c>ContentDispositionHeaderValue.SetHttpFileName</c> 이 한글 이름을
    /// <c>filename*=UTF-8''…</c> 로 적어 준다. 손으로 적으면 브라우저마다
    /// 다르게 읽어 이름이 깨진다.
    /// </remarks>
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
