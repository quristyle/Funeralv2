using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// F.A.Q 엔드포인트
/// </summary>
/// <remarks>
/// F.A.Q 는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
/// 관리자만 등록·수정·삭제하고 나머지 사용자는 읽는다.
///
/// 게이트웨이의 `/api/auth/**` 경로는 Anonymous 라 인증을 걸지 않는다.
/// 그래서 여기서 UserContext 가 없으면 401 을 돌려준다.
/// (게이트웨이가 토큰을 검증한 뒤에만 X-User-* 헤더를 붙이고,
///  외부에서 보낸 같은 이름의 헤더는 지운다.)
///
/// 쓰기 권한은 화면이 아니라 <see cref="IFaqService"/> 가 판정한다.
/// 화면의 `v-perm` 은 버튼을 숨기는 장치일 뿐이라 요청을 직접 보내면 통과한다.
/// </remarks>
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
        .WithName("GetFaqs")
        .WithOpenApi();

        group.MapGet("/{id}", async (string id, UserContext? user,
            [FromServices] IFaqService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var faq = await service.GetByIdAsync(user.UserId, id);
            return faq is null
                ? Results.NotFound(ApiResponse<FaqDto>.Fail("NOT_FOUND", "F.A.Q 를 찾을 수 없습니다."))
                : Results.Ok(ApiResponse<FaqDto>.Ok(faq));
        })
        .WithName("GetFaqById")
        .WithOpenApi();

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
        .WithName("CreateFaq")
        .WithOpenApi();

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
        .WithName("UpdateFaq")
        .WithOpenApi();

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
        .WithName("DeleteFaq")
        .WithOpenApi();
    }
}
