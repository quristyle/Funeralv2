using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 공지 엔드포인트
/// </summary>
/// <remarks>
/// 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
///
/// 팝업 조회는 두 갈래다.
///   GET /notices/popup/public  로그인 전에도 볼 수 있는 공지만. 인증 불필요.
///   GET /notices/popup         전부. 로그인한 사용자용.
///
/// 게이트웨이의 `/api/auth/**` 경로는 Anonymous 라 인증을 걸지 않는다.
/// 인증이 필요한 엔드포인트는 여기서 UserContext 가 없으면 401 을 돌려준다.
/// (게이트웨이가 토큰을 검증한 뒤에만 X-User-* 헤더를 붙이고,
///  외부에서 보낸 같은 이름의 헤더는 지운다.)
/// </remarks>
public static class NoticeEndpoints
{
    public static void MapNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notices").WithTags("Notices");

        // ── 팝업 조회 ──────────────────────────────────────────

        // 로그인 전 화면에서 부른다. 인증 없이 열려 있다.
        group.MapGet("/popup/public", async ([FromServices] INoticeService service) =>
        {
            var notices = await service.GetPopupAsync(publicOnly: true);
            return Results.Ok(ApiResponse<List<NoticeDto>>.Ok(notices));
        })
        .WithName("GetPublicPopupNotices")
        .WithOpenApi();

        // 로그인한 사용자용. 공개 공지까지 함께 내려준다.
        group.MapGet("/popup", async (UserContext? user, [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            var notices = await service.GetPopupAsync(publicOnly: false);
            return Results.Ok(ApiResponse<List<NoticeDto>>.Ok(notices));
        })
        .WithName("GetPopupNotices")
        .WithOpenApi();

        // ── 관리 ───────────────────────────────────────────────

        group.MapGet("/", async (UserContext? user, [FromQuery] string? keyword,
            [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            var notices = await service.GetAllAsync(keyword);
            return Results.Ok(ApiResponse<List<NoticeDto>>.Ok(notices));
        })
        .WithName("GetNotices")
        .WithOpenApi();

        group.MapGet("/{id}", async (string id, UserContext? user,
            [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            var notice = await service.GetByIdAsync(id);
            return notice is null
                ? Results.NotFound(ApiResponse<NoticeDto>.Fail("NOT_FOUND", "공지를 찾을 수 없습니다."))
                : Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
        })
        .WithName("GetNoticeById")
        .WithOpenApi();

        group.MapPost("/", async ([FromBody] SaveNoticeDto request, UserContext? user,
            [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(ApiResponse<NoticeDto>.Fail("INVALID", "제목을 입력하세요."));
            }

            var notice = await service.CreateAsync(request, user.UserId);
            return Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
        })
        .WithName("CreateNotice")
        .WithOpenApi();

        group.MapPut("/{id}", async (string id, [FromBody] SaveNoticeDto request,
            UserContext? user, [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("INVALID", "제목을 입력하세요."));
            }

            var ok = await service.UpdateAsync(id, request, user.UserId);
            return ok
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "공지를 찾을 수 없습니다."));
        })
        .WithName("UpdateNotice")
        .WithOpenApi();

        group.MapDelete("/{id}", async (string id, UserContext? user,
            [FromServices] INoticeService service) =>
        {
            if (user is null) return Results.Unauthorized();
            var ok = await service.DeleteAsync(id, user.UserId);
            return ok
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "공지를 찾을 수 없습니다."));
        })
        .WithName("DeleteNotice")
        .WithOpenApi();
    }
}
