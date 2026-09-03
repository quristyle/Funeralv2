using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

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
        // 상태·호실 배정 검증에 걸리면 400 으로 사유를 돌려준다 (D-RS1 · D-RS6).
        group.MapPost("/", async ([FromBody] DeceasedCreateDto dto, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            try
            {
                return Results.Ok(await service.CreateDeceasedAsync(dto, user?.UserId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<DeceasedDto>.Fail(ex.Message));
            }
        })
        .WithName("CreateDeceased")
        .WithOpenApi();

        // 고인 정보 수정
        group.MapPut("/{id}", async (string id, [FromBody] DeceasedUpdateDto dto, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            try
            {
                var result = await service.UpdateDeceasedAsync(id, dto, user?.UserId);
                if (result == null)
                {
                    return Results.NotFound(ApiResponse<DeceasedDto>.Fail("수정할 고인 정보를 찾을 수 없습니다."));
                }
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<DeceasedDto>.Fail(ex.Message));
            }
        })
        .WithName("UpdateDeceased")
        .WithOpenApi();

        // 고인 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            var success = await service.DeleteDeceasedAsync(id, user?.UserId);
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
        //
        // [익명 라우트] 게이트웨이가 이 경로를 로그인 없이 통과시킨다(플레이어용).
        // 열쇠가 추측 가능한 장비 코드 하나뿐이라, 표출에 쓰지 않는 개인정보는
        // 내려보내지 않는다 — AnonymousDisplayProjection 주석 참고 (결정 D-M2).
        group.MapGet("/deviceCode/{deviceCode}", async (string deviceCode, [FromServices] IDeceasedService service) =>
        {
            var result = await service.GetDeceasedDetailByDeviceCodeAsync(deviceCode);
            if (result == null)
            {
                // 데이터가 없는 것을 오류가 아닌 정상 응답(null)으로 처리
                return Results.Ok(ApiResponse<DeceasedDetailDto?>.Ok(null, "해당 호실에 배정된 고인 정보가 없습니다."));
            }
            return Results.Ok(result.ToAnonymousDisplay());
        })
        .WithName("GetDeceasedDetailByDeviceCode")
        .WithOpenApi();

        // 장비코드로 입구 안내용 호실 및 고인 종합 상세 정보 목록 조회
        // [익명 라우트] 위와 같다.
        group.MapGet("/guide/deviceCode/{deviceCode}", async (string deviceCode, [FromServices] IDeceasedService service) =>
        {
            var result = await service.GetEntranceGuideRoomsByDeviceCodeAsync(deviceCode);
            return Results.Ok(result.ToAnonymousDisplay());
        })
        .WithName("GetEntranceGuideRoomsByDeviceCode")
        .WithOpenApi();

        // 장비코드로 키오스크용 건물 전체 호실 및 고인 종합 상세 정보 목록 조회
        // [익명 라우트] 위와 같다.
        group.MapGet("/kiosk/deviceCode/{deviceCode}", async (string deviceCode, [FromServices] IDeceasedService service) =>
        {
            var result = await service.GetKioskRoomsByDeviceCodeAsync(deviceCode);
            return Results.Ok(result.ToAnonymousDisplay());
        })
        .WithName("GetKioskRoomsByDeviceCode")
        .WithOpenApi();

        // 고인 종합 상세 정보 저장
        group.MapPut("/{id}/detail", async (string id, [FromBody] DeceasedDetailDto dto, [FromServices] IDeceasedService service) =>
        {
            try
            {
                var result = await service.SaveDeceasedDetailAsync(id, dto);
                if (result == null)
                {
                    return Results.NotFound(ApiResponse<DeceasedDetailDto>.Fail("저장할 고인 정보를 찾을 수 없습니다."));
                }
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<DeceasedDetailDto>.Fail(ex.Message));
            }
        })
        .WithName("SaveDeceasedDetail")
        .WithOpenApi();

        // 고인 종합 상세 정보 저장 (신규 등록 시 ID가 없을 때)
        group.MapPut("/detail", async ([FromBody] DeceasedDetailDto dto, [FromServices] IDeceasedService service) =>
        {
            try
            {
                var result = await service.SaveDeceasedDetailAsync(string.Empty, dto);
                if (result == null)
                {
                    return Results.NotFound(ApiResponse<DeceasedDetailDto>.Fail("저장할 고인 정보를 찾을 수 없습니다."));
                }
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<DeceasedDetailDto>.Fail(ex.Message));
            }
        })
        .WithName("SaveDeceasedDetailNew")
        .WithOpenApi();

        // 고인 호실 이동 — 배정만 바꾼다. 대상 호실 검증에 걸리면 400.
        group.MapPut("/{id}/room", async (string id, [FromQuery] string roomId, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("옮길 호실을 지정해야 합니다."));
            }

            try
            {
                var success = await service.MoveRoomAsync(id, roomId, user?.UserId);
                if (!success)
                {
                    return Results.NotFound(ApiResponse<bool>.Fail("호실을 옮길 고인 정보를 찾을 수 없습니다."));
                }
                return Results.Ok(true);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        })
        .WithName("MoveDeceasedRoom")
        .WithOpenApi();

        // 고인 출상 처리 — 상태 전환과 배정 해제만 한다.
        // 예전에는 화면이 전체 PUT 을 재구성해 보냈는데, 목록 DTO 에 없는
        // 칸(비고·주민번호 등)이 함께 지워지는 문제가 있었다 (47번 문서 0단계).
        group.MapPut("/{id}/depart", async (string id, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            var success = await service.DepartAsync(id, user?.UserId);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("출상 처리할 고인 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("Depart")
        .WithOpenApi();

        // 고인 출상 취소 처리 — 되돌아갈 호실에 다른 고인이 있으면 400.
        // 출상 취소는 관리자 역할만 (47번 문서 D-RS4).
        group.MapPut("/{id}/cancel-departure", async (string id, [FromServices] IDeceasedService service, UserContext? user) =>
        {
            if (user is null || !user.CanControlDevices)
            {
                return Results.Json(
                    ApiResponse<bool>.Fail("출상 취소 권한이 없습니다.", "ERR_FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                var success = await service.CancelDepartureAsync(id, user?.UserId);
                if (!success)
                {
                    return Results.NotFound(ApiResponse<bool>.Fail("출상 취소할 고인 정보를 찾을 수 없습니다."));
                }
                return Results.Ok(true);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        })
        .WithName("CancelDeparture")
        .WithOpenApi();
    }
}
