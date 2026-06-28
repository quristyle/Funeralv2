using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 고인 정보 관리 API 엔드포인트 정의
/// </summary>
public static class DeceasedEndpoints
{
    public static void MapDeceasedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/deceased").AddApiResponseWrapper();

        // 고인 목록 조회
        group.MapGet("/list", async ([FromServices] IDeceasedService service) =>
        {
            return await service.GetDeceasedListAsync();
        })
        .WithName("GetDeceasedList")
        .WithOpenApi();

        // 고인 등록
        group.MapPost("/", async ([FromBody] DeceasedCreateDto dto, [FromServices] IDeceasedService service) =>
        {
            return await service.CreateDeceasedAsync(dto);
        })
        .WithName("CreateDeceased")
        .WithOpenApi();

        // 고인 정보 수정
        group.MapPut("/{id}", async (string id, [FromBody] DeceasedUpdateDto dto, [FromServices] IDeceasedService service) =>
        {
            var result = await service.UpdateDeceasedAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeceasedDto>.Fail("수정할 고인 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateDeceased")
        .WithOpenApi();

        // 고인 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IDeceasedService service) =>
        {
            var success = await service.DeleteDeceasedAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 고인 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteDeceased")
        .WithOpenApi();
    }
}
