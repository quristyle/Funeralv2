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
    /// <b>AI 작업 지시에 함께 올린 파일</b> 하나의 주소.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 다른 갈래와 달리 <b>FileServer 가 아니라 ProjMngServer</b> 로 간다.
    /// 그 바이트는 <c>projmng.ai_task_file</c> 에 들어 있고, 왜 파일 서버가
    /// 아닌지는 <c>deploy/sql/projmng-ai-task-file-2026-09-25.sql</c> 머리말에
    /// 적었다 — 요지는 <b>실행기가 게이트웨이를 못 지난다</b>는 것이다.
    /// </para>
    /// <para>
    /// 열쇠가 GUID 가 아니라 <b>번호</b>라 자리가 갈린다. 같은 <c>{fileId}</c>
    /// 자리에 두면 「AI 첨부 12번」과 「파일 GUID」를 라우팅이 가를 수 없다.
    /// </para>
    /// </remarks>
    public static string AiTaskUrlFor(long fileKey, string? fileName = null)
    {
        var url = $"{Path}/ai-task/{fileKey}";

        return string.IsNullOrWhiteSpace(fileName)
            ? url
            : $"{url}?name={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// 첨부 하나의 썸네일(150x150 WebP) 주소.
    /// 레이아웃 아바타나 목록의 작은 미리보기처럼 작은 그림을 그릴 때 쓴다.
    /// </summary>
    public static string ThumbnailUrlFor(string fileId) =>
        $"{Path}/thumbnail/{Uri.EscapeDataString(fileId)}";

    /// <summary>
    /// 앱알림 아이콘용 프로필 사진 주소.
    /// <b>언제나 그림이 나온다</b> — 못 받으면 사람 형상 그림자로 갈린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ThumbnailUrlFor"/> 와 무엇이 다른가 — 그쪽은 못 받으면 404 를 주고,
    /// 화면은 그것을 보고 이름 첫 글자를 대신 그린다(<c>CurrentUser.MarkAvatarUnavailable</c>).
    /// <b>서비스워커에는 그렇게 되받을 자리가 없다.</b> 알림 아이콘은 브라우저가
    /// 혼자 받아 가고, 실패하면 알림이 아이콘 없이 뜰 뿐 아무도 알지 못한다.
    /// 그래서 이 갈래는 실패를 <b>여기서</b> 그림자로 바꾼다.
    /// </para>
    /// <para>
    /// 주소를 만드는 쪽은 알림 서비스다
    /// (<c>microservices/NotificationServer/Services/AvatarIconResolver.cs</c>) —
    /// 사진이 아예 없는 계정은 거기서 바로 <see cref="FallbackAvatarPath"/> 를 고르고,
    /// 이 갈래는 「사진은 있는데 못 받은」 경우를 맡는다.
    /// </para>
    /// </remarks>
    public static string AvatarUrlFor(string fileId, string? token = null) =>
        string.IsNullOrWhiteSpace(token)
            ? $"{Path}/avatar/{Uri.EscapeDataString(fileId)}"
            : $"{Path}/avatar/{Uri.EscapeDataString(fileId)}?{AvatarTokenKey}={Uri.EscapeDataString(token)}";

    /// <summary>
    /// 알림 아이콘 주소에 실려 오는 <b>열쇠</b>의 칸 이름.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 주소를 부르는 것은 화면이 아니라 <b>브라우저 자신</b>이다 — 알림을
    /// 띄우는 그 순간 서비스워커가 적어 준 주소를 직접 받아 가고, 그 요청은
    /// 서비스워커의 <c>fetch</c> 처리기도 거치지 않는다. 그래서 실을 수 있는
    /// 신원이 쿠키뿐인데, <b>알림은 포털에 로그인해 있지 않은 기기에도
    /// 도착한다</b> (구독은 오래 살아남고 인증 쿠키는 브라우저를 닫으면
    /// 사라진다). 그런 기기에서는 이 중계가 익명으로 올라가 프로필 사진을
    /// 받지 못하고 언제나 그림자가 됐다.
    /// </para>
    /// <para>
    /// 그래서 알림 서비스가 <b>그 사진 한 장만 여는 열쇠</b>를 주소에 실어
    /// 보내고(<c>NotificationServer/Services/AvatarIconToken.cs</c>), 이 중계는
    /// 그것을 게이트웨이로 그대로 넘긴다. <b>우리는 열쇠를 풀어 보지 않는다</b> —
    /// 서명을 아는 것은 게이트웨이이고, 여기에는 키가 없다. 가짜면 게이트웨이가
    /// 신원을 붙이지 않고, 그러면 사진이 안 나와 그림자로 갈린다.
    /// </para>
    /// </remarks>
    public const string AvatarTokenKey = "t";

    /// <summary>
    /// 사람 형상 그림자. 셸이 정적 파일로 들고 있다
    /// (<c>JSini.Web.Shell/wwwroot/avatar-fallback.png</c>).
    /// </summary>
    /// <remarks>
    /// 이 프로젝트가 아니라 셸의 <c>wwwroot</c> 에 두는 이유는 <b>서비스워커가
    /// 셸 오리진의 절대 경로로 이 그림을 받아 가기 때문</b>이다. RCL 에 두면
    /// 주소가 <c>/_content/…</c> 가 되어 알림 페이로드에 적히는 글자가 길어지고,
    /// 무엇보다 그 경로는 사람이 보고 무엇인지 알기 어렵다.
    /// </remarks>
    public const string FallbackAvatarPath = "/avatar-fallback.png";

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
    /// 사진·첨부 주소에서 <b>파일 아이디(GUID)</b>를 꺼낸다. 우리 파일이 아니면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 받는 모양이 둘이다 — 백엔드에 적혀 있는 Vue 시절 주소
    /// (<c>/api/file/download/id/{guid}</c>)와 이미 옮겨 놓은 셸 중계 경로
    /// (<c>/files/{guid}</c> · <c>/files/thumbnail/{guid}</c>)다. 저장된 값이
    /// 어느 쪽인지 시절마다 다르므로 <b>둘 다 받는다.</b>
    /// </para>
    /// <para>
    /// <b>우리 파일이 아닌 주소를 걸러 내는 자리이기도 하다.</b> 사진을 한 번도
    /// 올리지 않은 계정에는 서버가 바깥 기본 이미지 주소를 채워 준다
    /// (alipayobjects.com). 그것을 그대로 <c>&lt;img&gt;</c> 에 걸면 화면을 열
    /// 때마다 바깥으로 요청이 나가므로 <b>「사진 없음」으로 본다</b> — 화면은
    /// 그때 이름 첫 글자를 대신 그린다.
    /// </para>
    /// </remarks>
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

        // ② 백엔드 주소인 경우.
        //
        // **<see cref="FileApiUrl"/> 를 쓰지 않는다.** 그쪽은 앞머리를 박아 둔
        // (`^`) 정규식이라 `http://…/api/file/…` 처럼 오리진이 붙은 값을 놓친다.
        // 첨부를 옮기는 자리(`RelayUrl`)는 상대경로만 오는 것이 확인돼 있지만
        // (머리말의 66건), 프로필 사진은 계정 확장 속성이라 그 확인 밖이다.
        var match = AnyFileUrl.Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>어디에 박혀 있어도 찾아내는 파일 읽기 주소. <see cref="FileIdOf"/> 가 쓴다.</summary>
    private static readonly Regex AnyFileUrl = new(
        @"/api/file/(?:download|thumbnail|medium|large)/(?:id/)?(?<id>[0-9a-fA-F-]{36})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        // 썸네일 (150x150 WebP). 헤더 아바타 등 작은 그림에 쓴다.
        // 리터럴 `thumbnail` 이 있어 `{fileId}` 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/thumbnail/{{fileId}}", HandleThumbnailAsync)
            .AllowAnonymous()
            .WithName("JSiniFileThumbnail");

        // 앱알림 아이콘. 썸네일과 달리 **실패해도 404 를 주지 않는다**
        // (AvatarUrlFor 머리말). 리터럴 `avatar` 라 `{fileId}` 와 겹치지 않는다.
        endpoints.MapGet($"{Path}/avatar/{{fileId}}", HandleAvatarAsync)
            // 서비스워커가 받아 가는 자리다. 로그인 쿠키가 실려 오면 그 사람의
            // 신원으로 실제 사진이 나가고, 안 실려 오면 그림자가 나간다 —
            // 어느 쪽이든 그림 하나는 돌려준다는 것이 이 경로의 약속이다.
            .AllowAnonymous()
            .WithName("JSiniAvatarIcon");

        // AI 작업 지시에 함께 올린 파일. **파일 서버가 아니라 프로젝트관리**로
        // 간다(`AiTaskUrlFor` 머리말). 리터럴 `ai-task` 가 있어 `{fileId}` 와
        // 겹치지 않는다.
        endpoints.MapGet($"{Path}/ai-task/{{fileKey:long}}", HandleAiTaskAsync)
            // **로그인해야 한다.** 이 갈래에는 「공개 파일」이라는 개념이 없다 —
            // 지시에 붙은 화면 사진은 언제나 사내 자료다.
            .RequireAuthorization()
            .WithName("JSiniAiTaskFile");

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

    /// <summary>
    /// AI 작업 첨부를 중계한다. 그림이면 <c>&lt;img&gt;</c> 가, 그 밖에는
    /// 내려받기 링크가 이 주소를 부른다.
    /// </summary>
    /// <remarks>
    /// <b>무엇을 어떤 형식으로 내보낼지는 위쪽이 정한다</b>
    /// (<c>AiTaskFilesController.ContentAsync</c>) — 그림만 제 형식으로 나가고
    /// 나머지는 <c>application/octet-stream</c> 이다. 여기서 또 가르면 두
    /// 판정이 갈라지고, 갈라지는 쪽은 늘 「실행되어서는 안 되는데 실행되는」
    /// 쪽이다.
    /// </remarks>
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
            // 그림이면 캐시한다 — 판정은 위쪽이 준 형식이 한다(`Cacheable`).
            // 이 번호의 바이트는 덧쓰이지 않으므로 검증표를 우리가 만들어도 된다.
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

    /// <summary>
    /// 알림 아이콘용 프로필 사진. <b>못 받으면 사람 형상 그림자로 넘긴다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 그림자를 직접 흘려보내지 않고 <b>302 로 넘긴다.</b> 그 파일은 셸의
    /// <c>wwwroot</c> 에 있고 이 코드는 공용 컴포넌트 쪽이라, 여기서 바이트를
    /// 읽으려면 정적 파일의 실제 경로를 알아야 한다 — 그러면 컴포넌트가 셸의
    /// 파일 배치에 매인다. 그림 요청은 리다이렉트를 따라간다.
    /// </para>
    /// <para>
    /// 검증표(<c>ETag</c>)를 붙이지 않는다. 이 주소의 답은 <b>같은 아이디라도
    /// 바뀔 수 있다</b> — 신원이 실려 오느냐에 따라 사진과 그림자로 갈린다.
    /// </para>
    /// </remarks>
    private static async Task HandleAvatarAsync(
        string fileId,
        HttpContext http,
        GatewayClient gateway,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        // 알림 서비스가 실어 보낸 열쇠. 없으면(=화면이 부른 것이면) 지금 요청의
        // 신원이 그대로 올라간다 — 이 중계의 원래 규칙이다.
        var token = http.Request.Query[AvatarTokenKey].ToString();

        // `o=1` — 썸네일(WebP)을 건너뛰고 원본을 준다. 메일 본문에 싣는 사진이
        // 쓴다(AuthServer 의 가입 신청 알림). Outlook 은 WebP 를 그리지 못한다.
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

                    // **그림인 것까지 확인하고 흘려보낸다.** 위쪽이 200 과 함께
                    // 오류 봉투(JSON)를 주는 갈래가 있어서, 형식을 안 보면
                    // 알림에 글자 뭉치를 아이콘으로 물리게 된다.
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
            // 이 갈래로 그림이 지나간다 — 헤더의 얼굴 · 영정 사진 · 장비 미리보기 ·
            // 미디어 썸네일 · 공지 본문의 <img>. 실제로 열지 말지는 형식을 보고
            // 정한다(Cacheable). 여기서는 "이 경로는 캐시를 허용한다" 까지만 말한다.
            cacheable: true);
    }

    /// <summary>
    /// 게이트웨이의 한 경로를 그대로 흘려보낸다. 세 갈래가 이것을 함께 쓴다 —
    /// FileServer 로 바로 가는 길(원본·썸네일)과, 횟수를 세고 302 로 넘어가는 길.
    ///
    /// <para>
    /// <c>cacheable</c> 은 <b>이 갈래가</b> 캐시를 허용하는지다. 실제로 열리는지는
    /// 형식까지 보고 정한다(<see cref="Cacheable"/>) — 그림만 열린다.
    /// 자료실 갈래는 <c>false</c> 이고 이유는 그쪽 호출부 주석에 있다.
    /// </para>
    /// </summary>
    /// <summary>파일 아이디가 GUID 인 갈래. <b>대부분이 이쪽이다.</b></summary>
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

    /// <summary>
    /// 중계 본체.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>tag</c> 는 이 바이트를 가리키는 <b>변하지 않는 이름</b>이다.
    /// 검증표(<c>ETag</c>)와 로그가 그것을 쓴다.
    /// </para>
    /// <para>
    /// GUID 가 아닌 갈래가 생겨서 글자로 받는다 — AI 작업 첨부는 열쇠가
    /// <c>bigint</c> 다. <b>갈래끼리 겹치지 않게</b> 접두사를 붙여 만든다
    /// (<c>ai-task-12</c>) — 안 붙이면 GUID 갈래의 캐시본과 같은 검증표가 나올
    /// 수 있고, 그러면 브라우저가 엉뚱한 바이트를 들고 304 를 받는다.
    /// </para>
    /// </remarks>
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
            // **여기서 잡지 않으면 스택 추적이 그대로 브라우저에 찍힌다.**
            // 첨부 하나를 못 받는 일로 내부 구조를 흘릴 이유가 없다.
            //
            // 개발 장비에서 자주 보게 된다: FileServer 는 로컬에 바이트가 없으면
            // `Storage:FallbackUrl`(운영 호스트)로 302 를 주는데, 그 호스트가
            // 개발망에서 안 풀린다. 운영에서는 풀리므로 정상 동작한다.
            // (루트 CLAUDE.md 의 「개발 장비에서 올린 파일은 운영에 바이트가 없다」)
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
            http.Response.Headers.ETag = $"\"{tag}\"";
            http.Response.Headers.CacheControl = $"private, max-age={ImageMaxAgeSeconds}";

            // **위쪽을 부른 뒤에 따진다.** 앞에서 끊으면 판정(FileServer)을 건너뛰게
            // 되고, 비공개로 되돌린 파일에 계속 304 를 주어 브라우저가 캐시본을
            // 계속 쓴다 — 틀리는 방향이 「열려서는 안 되는데 열린」 쪽이다.
            // 여기서 아끼는 것은 판정이 아니라 **바이트 전송**뿐이다.
            if (NoneMatch(http.Request, tag))
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
        => NoneMatch(request, id.ToString());

    /// <summary>
    /// 브라우저가 들고 있는 것이 이 바이트와 같은가 (<c>If-None-Match</c>).
    /// </summary>
    /// <remarks>
    /// <c>*</c> 도 받아 준다 — 「무엇이든 들고 있다」는 뜻이고, 이 주소에서
    /// 나올 수 있는 것은 그 이름의 바이트 하나뿐이라 언제나 같다.
    /// </remarks>
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
