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

        // 고인 목록 조회 (필터링 조건 수용)
        group.MapGet("/list", async ([AsParameters] DeceasedSearchDto searchDto, [FromServices] IDeceasedService service) =>
        {
            return await service.GetDeceasedListAsync(searchDto);
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

        // 고인 종합 상세 정보 조회
        group.MapGet("/{id}/detail", async (string id, [FromServices] IDeceasedService service) =>
        {
            var result = await service.GetDeceasedDetailAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeceasedDetailDto>.Fail("해당 고인의 상세 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetDeceasedDetail")
        .WithOpenApi();

        // 호실 ID로 현재 고인 상세 정보 조회
        group.MapGet("/roomId/{roomId}", async (string roomId, [FromServices] IDeceasedService service) =>
        {
            var result = await service.GetDeceasedDetailByRoomIdAsync(roomId);
            if (result == null)
            {
                // 데이터가 없는 것을 오류가 아닌 정상 응답(null)으로 처리
                return Results.Ok(ApiResponse<DeceasedDetailDto?>.Ok(null, "해당 호실에 배정된 고인 정보가 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetDeceasedDetailByRoomId")
        .WithOpenApi();

        // 고인 종합 상세 정보 저장
        group.MapPut("/{id}/detail", async (string id, [FromBody] DeceasedDetailDto dto, [FromServices] IDeceasedService service) =>
        {
            var result = await service.SaveDeceasedDetailAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeceasedDetailDto>.Fail("저장할 고인 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("SaveDeceasedDetail")
        .WithOpenApi();

        // 고인 종합 상세 정보 저장 (신규 등록 시 ID가 없을 때)
        group.MapPut("/detail", async ([FromBody] DeceasedDetailDto dto, [FromServices] IDeceasedService service) =>
        {
            var result = await service.SaveDeceasedDetailAsync(string.Empty, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeceasedDetailDto>.Fail("저장할 고인 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("SaveDeceasedDetailNew")
        .WithOpenApi();
    }
}
