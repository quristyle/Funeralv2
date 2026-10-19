using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>F.A.Q 엔드포인트. 관리자만 등록·수정·삭제하고 나머지는 읽는다. 게이트웨이의 <c>/api/auth/**</c> 는 익명이라 권한을 여기서 직접 본다.</summary>
public static class FaqEndpoints
{
    public static void MapFaqEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/faqs").WithTags("Faqs");

        // 목록. 관리자에게는 비활성 항목까지 보인다.
        // 응답에 CanManage 를 함께 담아 화면이 등록 버튼을 켤지 정한다.
        group.MapGet("/", async (UserContext? user,
            [FromQuery] string? keyword, [FromQuery] string? category,
            [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.GetListAsync(user.UserId, keyword, category);
            return Results.Ok(ApiResponse<FaqListDto>.Ok(result));
        })
        .WithName("GetFaqs");

        group.MapGet("/{id}", async (string id, UserContext? user,
            [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var faq = await service.GetByIdAsync(user.UserId, id);
            return faq is null
                ? Results.NotFound(ApiResponse<FaqDto>.Fail("NOT_FOUND", "F.A.Q 를 찾을 수 없습니다."))
                : Results.Ok(ApiResponse<FaqDto>.Ok(faq));
        })
        .WithName("GetFaqById");

        group.MapPost("/", async ([FromBody] SaveFaqDto request, UserContext? user,
            [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(ApiResponse<FaqDto>.Fail("INVALID", "질문을 입력하세요."));
            }

            var faq = await service.CreateAsync(request, user.UserId);
            return faq is null
                ? Results.Json(
                    ApiResponse<FaqDto>.Fail("FORBIDDEN", "F.A.Q 를 등록할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden)
                : Results.Ok(ApiResponse<FaqDto>.Ok(faq));
        })
        .WithName("CreateFaq");

        group.MapPut("/{id}", async (string id, [FromBody] SaveFaqDto request,
            UserContext? user, [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("INVALID", "질문을 입력하세요."));
            }

            var result = await service.UpdateAsync(id, request, user.UserId);
            return result switch
            {
                FaqSaveResult.Ok => Results.Ok(ApiResponse<bool>.Ok(true)),
                FaqSaveResult.Forbidden => Results.Json(
                    ApiResponse<bool>.Fail("FORBIDDEN", "F.A.Q 를 수정할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "F.A.Q 를 찾을 수 없습니다."))
            };
        })
        .WithName("UpdateFaq");

        group.MapDelete("/{id}", async (string id, UserContext? user,
            [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.DeleteAsync(id, user.UserId);
            return result switch
            {
                FaqSaveResult.Ok => Results.Ok(ApiResponse<bool>.Ok(true)),
                FaqSaveResult.Forbidden => Results.Json(
                    ApiResponse<bool>.Fail("FORBIDDEN", "F.A.Q 를 삭제할 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "F.A.Q 를 찾을 수 없습니다."))
            };
        })
        .WithName("DeleteFaq");
    }
}
