using System;
using System.Threading.Tasks;
using FileServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileServer.Endpoints;

/// <summary>
/// 파일 읽기 엔드포인트의 익명 접근을 막는 필터.
/// </summary>
/// <remarks>
/// 게이트웨이의 파일 라우트들(<c>/api/file/download|thumbnail|medium|large|resize/**</c>)은
/// <c>AuthorizationPolicy: Anonymous</c> 다. 브라우저가 <c>&lt;img src&gt;</c> 로 직접 부르는 경로라
/// 토큰을 붙일 수 없어서 그렇게 열어 둔 것인데, 그 결과 <b>파일 아이디만 알면 누구나</b>
/// 헬프데스크 첨부까지 내려받을 수 있었다. "아무도 아이디를 모른다" 에 의존하는 상태였다.
///
/// 여기서 다시 판정한다. 게이트웨이는 JWT 가 유효하면 <c>X-User-Id</c> 를 붙여 주고
/// (외부에서 보낸 같은 이름의 헤더는 무조건 지운다 — ApiGateway/Program.cs),
/// 없으면 붙이지 않는다. 그러므로
///
/// <list type="bullet">
///   <item>헤더가 있으면 = 로그인 사용자 → 통과. 지금까지의 동작과 같다.</item>
///   <item>헤더가 없으면 = 익명 → <c>is_public</c> 이 켜진 파일만 통과.</item>
/// </list>
///
/// 막을 때는 403 이 아니라 404 를 준다. 403 은 "그 아이디의 파일은 있다" 를 알려 주기 때문이다.
///
/// <para>
/// 스위치는 <c>Files:RequirePublicFlagForAnonymous</c> 다. <b>켜 두었다.</b>
/// 끄면 파일 아이디만 알면 누구나 남의 첨부를 내려받는 상태로 되돌아간다.
/// </para>
/// <para>
/// <b>왜 처음에는 껐다가 켤 수 있게 됐는가.</b> 포털은 토큰을 <c>Authorization</c> 헤더로 붙이는데
/// <c>&lt;img src="/api/file/thumbnail/{id}"&gt;</c> 같은 태그에는 브라우저가 그 헤더를 붙여 주지 않는다.
/// 그래서 <b>포털 안에서 사진을 보는 요청도 여기서는 익명과 구별되지 않았고</b>,
/// 이 판정을 켜면 공지 첨부 · 본문 이미지 · 아바타 · 빈소 현황의 고인 사진이 모두 깨졌다.
/// </para>
/// <para>
/// 그래서 <b>브라우저가 스스로 보내는 인증 수단</b>을 붙였다. 로그인할 때 같은 토큰을
/// <c>jsini_file_at</c> 쿠키로도 심고(<c>HttpOnly · SameSite=Lax · Path=/api/file</c>),
/// 게이트웨이가 <b>파일 읽기 경로에서만</b> 그것을 <c>X-User-Id</c> 의 근거로 받는다.
/// 쓰기(업로드·삭제)에는 받지 않는다 — 받으면 CSRF 로 남이 파일을 지울 수 있다.
/// 짝이 되는 코드는 AuthServer/Endpoints/AuthEndpoints.cs 와 ApiGateway/Program.cs 의
/// <c>OnMessageReceived</c> 다. 자세한 경위는 docs/analysis/27-jsini-site-brand.md 5절.
/// </para>
/// <para>
/// <b>브라우저가 아닌 클라이언트 — 사이니지 플레이어 (2026-09-03).</b>
/// 위의 쿠키는 <b>로그인하는 브라우저</b>만의 이야기였다. 빈소의 사이니지 플레이어는
/// 브라우저도 아니고 로그인도 하지 않는다(장비 코드만 들고 익명으로 부른다).
/// 그래서 이 판정을 켠 순간 <b>플레이어의 모든 미디어가 404</b> 가 되었고,
/// 영정 화면이 이름만 있는 검은 화면으로 보였다 — DB 의 파일 174개가 전부
/// <c>is_public = false</c> 였다(공개 0건).
/// </para>
/// <para>
/// 그래서 <c>Files:AnonymousReadablePaths</c> 를 두었다. 저장 경로 앞머리가 이 목록에
/// 걸리면 익명도 읽는다. 기본값은 <c>funeralv2/</c> 하나다 — 그 아래 있는 것은
/// <b>빈소 화면에 띄우기 위해 올린 것</b>(영정 · 배경 · 리본 · 영상 · 음원)뿐이고,
/// 실제로 미디어 외 파일이 하나도 없다(2026-09-03 확인). 반면
/// <c>helpdesk-request/</c> · <c>notice/</c> · <c>qna/</c> · <c>profile/</c> 은 그대로 막힌다 —
/// 이 판정을 만든 이유가 그것들이었다.
/// </para>
/// <para>
/// <b>비밀 문서를 <c>funeralv2/</c> 아래에 두면 안 된다.</b> 그 순간 아이디를 아는 사람이
/// 내려받을 수 있게 된다. 계약서 같은 것이 생기면 다른 영역에 올리거나 이 목록에서 뺀다.
/// </para>
/// </remarks>
public sealed class PublicFileAccessFilter : IEndpointFilter
{
    /// <summary>이 판정을 켜는 설정 키.</summary>
    public const string EnabledKey = "Files:RequirePublicFlagForAnonymous";

    /// <summary>
    /// 익명으로도 읽을 수 있는 저장 경로 앞머리 목록 설정 키.
    /// 사이니지 플레이어처럼 로그인하지 않는 표출 장비를 위한 것이다.
    /// </summary>
    public const string AnonymousPathsKey = "Files:AnonymousReadablePaths";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue(EnabledKey, false))
        {
            return await next(context);
        }

        // 게이트웨이가 검증해 붙인 헤더. 있으면 로그인 사용자다.
        if (!string.IsNullOrEmpty(http.Request.Headers["X-User-Id"].ToString()))
        {
            return await next(context);
        }

        var id = ResolveFileId(http);
        if (id is null)
        {
            return NotFound();
        }

        var fileService = http.RequestServices.GetRequiredService<IFileService>();
        var metadata = await fileService.GetMetadataAsync(id.Value);
        if (metadata is null)
        {
            return NotFound();
        }

        // is_public 이 켜진 파일, 또는 빈소 화면에 띄우려고 올린 영역의 파일은 익명도 읽는다.
        if (metadata.IsPublic || IsAnonymousReadablePath(configuration, metadata.Path))
        {
            return await next(context);
        }

        return NotFound();
    }

    /// <summary>
    /// 저장 경로가 <see cref="AnonymousPathsKey"/> 의 앞머리 목록에 걸리는지.
    /// </summary>
    /// <remarks>
    /// 목록이 비어 있으면 아무것도 열지 않는다 — 설정을 지웠을 때 조용히 열리는 쪽이
    /// 아니라 조용히 닫히는 쪽으로 기울여 둔다.
    /// <para>
    /// <c>..</c> 가 든 경로는 아예 거부한다. 지금은 업로드가 경로를 직접 만들어서
    /// 그런 값이 들어올 수 없지만, 앞머리 비교는 <c>funeralv2/../notice/x</c> 같은 값에
    /// 속기 때문에 여기서 막아 둔다.
    /// </para>
    /// </remarks>
    private static bool IsAnonymousReadablePath(IConfiguration configuration, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains(".."))
        {
            return false;
        }

        var prefixes = configuration.GetSection(AnonymousPathsKey).Get<string[]>();
        if (prefixes is null)
        {
            return false;
        }

        foreach (var prefix in prefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                continue;
            }

            if (path.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 라우트에서 파일 아이디를 뽑는다. <c>{id}</c> 인 경로와 <c>{fileName}</c> 인 경로가 섞여 있고,
    /// 후자는 <c>{guid}</c> 와 <c>{guid}.jpg</c>(영상 썸네일) 두 형태를 받는다.
    /// </summary>
    private static Guid? ResolveFileId(HttpContext http)
    {
        if (http.Request.RouteValues.TryGetValue("id", out var raw)
            && Guid.TryParse(raw?.ToString(), out var id))
        {
            return id;
        }

        if (http.Request.RouteValues.TryGetValue("fileName", out var rawName))
        {
            var name = rawName?.ToString() ?? string.Empty;
            if (name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^4];
            }

            if (Guid.TryParse(name, out var fromName))
            {
                return fromName;
            }
        }

        return null;
    }

    private static IResult NotFound() =>
        Results.NotFound(ApiResponse<object>.Fail("ERR_FILE_NOT_FOUND", "파일을 찾을 수 없습니다."));
}
