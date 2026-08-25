using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// Q&amp;A 엔드포인트
/// </summary>
/// <remarks>
/// 누구나 질문하고 관리자가 답한다. 답글에 답글을 다는 것도 같은 방식이라
/// 깊이 제한이 없다.
///
/// 보이는 범위와 권한 판정은 모두 <see cref="IQnaService"/> 가 한다.
/// 화면은 서버가 내려준 값(`canManage` · `canEdit` · `isMine`)만 보고 버튼을 켠다.
///
/// 게이트웨이의 `/api/auth/**` 경로는 Anonymous 라 인증을 걸지 않는다.
/// 그래서 여기서 UserContext 가 없으면 401 을 돌려준다.
/// </remarks>
public static class QnaEndpoints
{
    public static void MapQnaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/qna").WithTags("Qna");

        // 질문 목록. 답글은 각 항목의 children 에 트리로 담겨 온다.
        group.MapGet("/", async (UserContext? user,
            [FromQuery] string? keyword, [FromQuery] string? filter,
            [FromQuery] int? page, [FromQuery] int? pageSize,
            [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.GetListAsync(
                user.UserId, keyword, filter, page ?? 1, pageSize ?? 20);

            return Results.Ok(ApiResponse<QnaListDto>.Ok(result));
        })
        .WithName("GetQnaList")
        .WithOpenApi();

        // 글 하나가 속한 스레드를 뿌리부터. 답글을 단 뒤 그 스레드만 다시 그릴 때 쓴다.
        group.MapGet("/{id}", async (string id, UserContext? user,
            [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var thread = await service.GetThreadAsync(user.UserId, id);
            return thread is null
                ? Results.NotFound(ApiResponse<QnaPostDto>.Fail("NOT_FOUND", "글을 찾을 수 없습니다."))
                : Results.Ok(ApiResponse<QnaPostDto>.Ok(thread));
        })
        .WithName("GetQnaThread")
        .WithOpenApi();

        // 질문 등록 (parentId 없음) · 답글 등록 (parentId 있음)
        group.MapPost("/", async ([FromBody] CreateQnaPostDto request, UserContext? user,
            [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var (result, post) = await service.CreateAsync(user.UserId, request);
            return result switch
            {
                QnaResult.Ok => Results.Ok(ApiResponse<QnaPostDto>.Ok(post!)),
                QnaResult.Invalid => Results.BadRequest(ApiResponse<QnaPostDto>.Fail(
                    "INVALID",
                    string.IsNullOrWhiteSpace(request.ParentId)
                        ? "제목과 내용을 입력하세요."
                        : "내용을 입력하세요.")),
                QnaResult.Forbidden => Results.Json(
                    ApiResponse<QnaPostDto>.Fail("FORBIDDEN", "글을 쓸 권한이 없습니다."),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.NotFound(ApiResponse<QnaPostDto>.Fail(
                    "NOT_FOUND", "답글을 달 글을 찾을 수 없습니다."))
            };
        })
        .WithName("CreateQnaPost")
        .WithOpenApi();

        // 수정. 본인 글이거나 관리자여야 한다.
        group.MapPut("/{id}", async (string id, [FromBody] UpdateQnaPostDto request,
            UserContext? user, [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.UpdateAsync(user.UserId, id, request);
            return Respond(result, "수정할 권한이 없습니다.");
        })
        .WithName("UpdateQnaPost")
        .WithOpenApi();

        // 삭제. 답글까지 함께 지운다.
        group.MapDelete("/{id}", async (string id, UserContext? user,
            [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.DeleteAsync(user.UserId, id);
            return Respond(result, "삭제할 권한이 없습니다.");
        })
        .WithName("DeleteQnaPost")
        .WithOpenApi();

        // 공개 여부 변경. 관리자만 부를 수 있다.
        group.MapPut("/{id}/visibility", async (string id,
            [FromBody] QnaVisibilityDto request, UserContext? user,
            [FromServices] IQnaService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.SetVisibilityAsync(user.UserId, id, request);
            return Respond(result, "공개 여부를 정할 권한이 없습니다.");
        })
        .WithName("SetQnaVisibility")
        .WithOpenApi();
    }

    /// <summary>쓰기 결과를 HTTP 응답으로 옮긴다.</summary>
    private static IResult Respond(QnaResult result, string forbiddenMessage) => result switch
    {
        QnaResult.Ok => Results.Ok(ApiResponse<bool>.Ok(true)),
        QnaResult.Invalid => Results.BadRequest(
            ApiResponse<bool>.Fail("INVALID", "내용을 입력하세요.")),
        QnaResult.Forbidden => Results.Json(
            ApiResponse<bool>.Fail("FORBIDDEN", forbiddenMessage),
            statusCode: StatusCodes.Status403Forbidden),
        _ => Results.NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "글을 찾을 수 없습니다."))
    };
}
