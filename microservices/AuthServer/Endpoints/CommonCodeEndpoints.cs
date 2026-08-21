using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

public static class CommonCodeEndpoints
{
    public static void MapCommonCodeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/common-code").WithTags("Common Code Management");

        // --- 그룹 관리 ---
        group.MapGet("/groups", async (ICommonCodeService codeService) =>
        {
            var groups = await codeService.GetGroupsAsync();
            return Results.Ok(ApiResponse<IEnumerable<CommonCodeGroupDto>>.Ok(groups));
        });

        group.MapPost("/groups", async (CommonCodeGroupCreateDto createDto, ICommonCodeService codeService) =>
        {
            var result = await codeService.CreateGroupAsync(createDto);
            return Results.Ok(ApiResponse<CommonCodeGroupDto>.Ok(result, "그룹이 생성되었습니다."));
        });

        group.MapPut("/groups/{id}", async (string id, CommonCodeGroupCreateDto updateDto, ICommonCodeService codeService) =>
        {
            var success = await codeService.UpdateGroupAsync(id, updateDto);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("그룹을 찾을 수 없습니다.", "404"));
        });

        group.MapDelete("/groups/{id}", async (string id, ICommonCodeService codeService) =>
        {
            var success = await codeService.DeleteGroupAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("그룹을 찾을 수 없습니다.", "404"));
        });

        // --- 코드 관리 ---
        group.MapGet("/{groupCode}", async (string groupCode, [FromQuery] bool hierarchical, ICommonCodeService codeService) =>
        {
            var codes = await codeService.GetCodesByGroupAsync(groupCode, hierarchical);
            return Results.Ok(ApiResponse<IEnumerable<CommonCodeDto>>.Ok(codes));
        });

        group.MapPost("/", async (CommonCodeCreateDto createDto, ICommonCodeService codeService) =>
        {
            var result = await codeService.CreateCodeAsync(createDto);
            return Results.Ok(ApiResponse<CommonCodeDto>.Ok(result, "코드가 생성되었습니다."));
        });

        group.MapPut("/{id}", async (string id, CommonCodeCreateDto updateDto, ICommonCodeService codeService) =>
        {
            var success = await codeService.UpdateCodeAsync(id, updateDto);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("코드를 찾을 수 없습니다.", "404"));
        });

        group.MapDelete("/{id}", async (string id, ICommonCodeService codeService) =>
        {
            var success = await codeService.DeleteCodeAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("코드를 찾을 수 없습니다.", "404"));
        });
    }
}
