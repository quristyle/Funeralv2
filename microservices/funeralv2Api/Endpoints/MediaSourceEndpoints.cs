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

        // 미디어 소스 상세 조회
        group.MapGet("/{id}", async (string id, [FromServices] IMediaSourceService service) =>
        {
            var result = await service.GetMediaSourceByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<MediaSourceDto>.Fail("미디어 소스 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetMediaSourceById")
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

        // 미디어 소스 변환 상태 업데이트 (FileServer 등에서 비동기 처리 완료 후 알림용)
        group.MapPatch("/{id}/status", async (string id, [FromBody] MediaSourceStatusUpdateDto dto, [FromServices] IMediaSourceService service) =>
        {
            var result = await service.UpdateMediaSourceStatusAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<MediaSourceDto>.Fail("상태를 업데이트할 미디어 소스를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateMediaSourceStatus")
        .WithOpenApi();

        // 미디어 소스 썸네일 재추출
        group.MapPost("/{id}/retry/thumbnail", async (string id, [FromServices] IMediaSourceService service) =>
        {
            var success = await service.RetryThumbnailAsync(id);
            if (!success)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("썸네일 재추출 처리에 실패했습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("RetryMediaSourceThumbnail")
        .WithOpenApi();

        // 미디어 소스 WebM 재변환
        group.MapPost("/{id}/retry/webm", async (string id, [FromServices] IMediaSourceService service) =>
        {
            var success = await service.RetryWebmAsync(id);
            if (!success)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("WebM 재변환 처리에 실패했습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("RetryMediaSourceWebm")
        .WithOpenApi();

        // 미디어 소스 정보 수정
        group.MapPut("/{id}", async (string id, [FromBody] MediaSourceUpdateDto dto, [FromServices] IMediaSourceService service) =>
        {
            var result = await service.UpdateMediaSourceAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<MediaSourceDto>.Fail("수정할 미디어 소스를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateMediaSource")
        .WithOpenApi();

        // 미디어 소스 Audio 재변환
        group.MapPost("/{id}/retry/audio", async (string id, [FromServices] IMediaSourceService service) =>
        {
            var success = await service.RetryAudioAsync(id);
            if (!success)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("오디오 재변환 처리에 실패했습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("RetryMediaSourceAudio")
        .WithOpenApi();
    }
}
