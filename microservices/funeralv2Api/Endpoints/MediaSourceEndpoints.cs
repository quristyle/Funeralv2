using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 미디어 소스 (동영상, 음원 등) 관련 API 엔드포인트 정의
/// </summary>
public static class MediaSourceEndpoints
{
    public static void MapMediaSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/source").AddApiResponseWrapper();

        // 미디어 소스 목록 조회 (유형 필터 적용)
        group.MapGet("/list", async ([FromQuery] string? type, [FromServices] IMediaSourceService service) =>
        {
            return await service.GetMediaSourcesAsync(type);
        })
        .WithName("GetMediaSources")
        .WithOpenApi();

        // 미디어 소스 생성
        group.MapPost("/", async ([FromBody] MediaSourceCreateDto dto, [FromServices] IMediaSourceService service) =>
        {
            return await service.CreateMediaSourceAsync(dto);
        })
        .WithName("CreateMediaSource")
        .WithOpenApi();

        // 미디어 소스 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IMediaSourceService service) =>
        {
            var success = await service.DeleteMediaSourceAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 미디어 소스를 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteMediaSource")
        .WithOpenApi();
    }
}
