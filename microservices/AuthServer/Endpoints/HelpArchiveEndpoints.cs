using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 자료실 엔드포인트 (<c>/help/archive</c>)
/// </summary>
/// 관리자가 올리고 나머지는 읽고 내려받는다. 게이트웨이의 /api/auth/** 는 익명이라 여기서 UserContext 가 없으면 401 이다.
/// 쓰기 권한은 화면이 아니라 IHelpArchiveService 가 판정한다 — v-perm 은 버튼을 숨길 뿐이라 요청을 직접 보내면 통과한다.
public static class HelpArchiveEndpoints
{
    public static void MapHelpArchiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/help/archives").WithTags("HelpArchive");

        // 목록. 관리자에게는 비활성 항목까지 보인다.
        // 응답에 CanManage 를 함께 담아 화면이 등록 버튼을 켤지 정한다.
        group.MapGet("/", async (UserContext? user,
            [FromQuery] string? keyword, [FromQuery] string? category,
            [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.GetListAsync(user.UserId, keyword, category);
            return Results.Ok(ApiResponse<HelpArchiveListDto>.Ok(result));
        })
        .WithName("GetHelpArchives");

        group.MapGet("/{id}", async (string id, UserContext? user,
            [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var archive = await service.GetByIdAsync(user.UserId, id);
            return archive is null
                ? Results.NotFound(ApiResponse<HelpArchiveDto>.Fail("NOT_FOUND", "자료를 찾을 수 없습니다."))
                : Results.Ok(ApiResponse<HelpArchiveDto>.Ok(archive));
        })
        .WithName("GetHelpArchiveById");

        // ── 내려받기 ────────────────────────────────────────────
        // 다운로드 수를 세고 FileServer 로 302 로 넘긴다. 셀 수 없는 직접 접근을 막으려고 여기를 거친다.
        // 응답 봉투를 쓰지 않는다 — 브라우저가 그대로 따라가는 리다이렉트다.
        group.MapGet("/{id}/files/{fileId}/download", async (string id, string fileId,
            UserContext? user, [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var url = await service.ResolveDownloadAsync(user.UserId, id, fileId);
            return url is null
                ? Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "파일을 찾을 수 없습니다."))
                : Results.Redirect(url);
        })
        .WithName("DownloadHelpArchiveFile");

        group.MapPost("/", async ([FromBody] SaveHelpArchiveDto request, UserContext? user,
            [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(ApiResponse<HelpArchiveDto>.Fail("INVALID", "자료명을 입력하세요."));
            }

            var archive = await service.CreateAsync(request, user.UserId);
            return archive is null
                ? Results.Json(
                    ApiResponse<HelpArchiveDto>.Fail("FORBIDDEN", "자료를 등록할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden)
                : Results.Ok(ApiResponse<HelpArchiveDto>.Ok(archive));
        })
        .WithName("CreateHelpArchive");

        group.MapPut("/{id}", async (string id, [FromBody] SaveHelpArchiveDto request,
            UserContext? user, [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("INVALID", "자료명을 입력하세요."));
            }

            var result = await service.UpdateAsync(id, request, user.UserId);
            return result switch
            {
                HelpArchiveSaveResult.Ok => Results.Ok(ApiResponse<bool>.Ok(true)),
                HelpArchiveSaveResult.Forbidden => Results.Json(
                    ApiResponse<bool>.Fail("FORBIDDEN", "자료를 수정할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "자료를 찾을 수 없습니다."))
            };
        })
        .WithName("UpdateHelpArchive");

        group.MapDelete("/{id}", async (string id, UserContext? user,
            [FromServices] IHelpArchiveService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.DeleteAsync(id, user.UserId);
            return result switch
            {
                HelpArchiveSaveResult.Ok => Results.Ok(ApiResponse<bool>.Ok(true)),
                HelpArchiveSaveResult.Forbidden => Results.Json(
                    ApiResponse<bool>.Fail("FORBIDDEN", "자료를 삭제할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "자료를 찾을 수 없습니다."))
            };
        })
        .WithName("DeleteHelpArchive");
    }
}
