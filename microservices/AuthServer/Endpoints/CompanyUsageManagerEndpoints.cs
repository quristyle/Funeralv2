using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>회사 사용처의 처리 담당자. 관리 길은 메뉴 권한(MenuViewAccess)으로, 바꾸는 길은 AdminPathGuardMiddleware 로도 막는다.</summary>
/// <c>/mine</c> 만 로그인한 누구나 부른다 — 자기 것만 돌려주고, 헬프데스크 같은 업무 화면이 「내가 맡은 회사」를 미리 고르는 데 쓴다.
public static class CompanyUsageManagerEndpoints
{
    /// <summary>관리 화면의 열쇠. 화면의 <c>RouteKey</c> 와 같아야 한다.</summary>
    private const string RouteKey = "admin.company.usage-manager";

    private static readonly string[] Paths = ["/admin/company/usage-manager"];

    public static void MapCompanyUsageManagerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/company-usage-managers")
            .WithTags("Company Usage Manager")
            .AddApiResponseWrapper();

        group.MapGet("/", async (
            [FromQuery] string companyId,
            [FromQuery] string? codeValue,
            UserContext? user, HttpContext http, MenuViewAccess access,
            CompanyUsageManagerService service, CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;
            return Results.Ok(await service.ListAsync(companyId, codeValue, ct));
        })
        .WithName("GetCompanyUsageManagers");

        group.MapPost("/", async (
            [FromBody] CompanyUsageManagerAddDto body,
            UserContext? user, HttpContext http, MenuViewAccess access,
            CompanyUsageManagerService service, CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            try
            {
                return Results.Ok(await service.AddAsync(body, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "400"));
            }
        })
        .WithName("AddCompanyUsageManagers");

        group.MapPost("/remove", async (
            [FromBody] List<int> ids,
            UserContext? user, HttpContext http, MenuViewAccess access,
            CompanyUsageManagerService service, CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;
            return Results.Ok(await service.RemoveAsync(ids ?? [], ct));
        })
        .WithName("RemoveCompanyUsageManagers");

        group.MapGet("/mine", async (
            [FromQuery] string codeValue,
            UserContext? user,
            CompanyUsageManagerService service, CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();
            return Results.Ok(await service.MyCompanyIdsAsync(user.UserId, codeValue ?? string.Empty, ct));
        })
        .WithName("GetMyUsageManagedCompanies");

        // 서비스 간 호출 전용(헬프데스크가 새 요청을 맡은 담당자에게 알린다). 게이트웨이를 거친 요청은 X-User-Role 이 늘 붙으므로 그것으로 가른다.
        group.MapGet("/of", async (
            [FromQuery] string companyId,
            [FromQuery] string codeValue,
            [FromQuery] string? role,
            UserContext? user, HttpContext http,
            CompanyUsageManagerService service, CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();
            if (http.Request.Headers.ContainsKey("X-User-Role"))
            {
                return Results.Json(
                    ApiResponse<object>.Fail("서비스 간 호출만 받습니다.", "403"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await service.ManagerLoginIdsAsync(
                companyId ?? string.Empty, codeValue ?? string.Empty, role, ct));
        })
        .WithName("GetCompanyUsageManagerLoginIds");
    }

    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await access.CanViewAsync(roles, RouteKey, Paths, ct)) return null;

        return Results.Json(
            ApiResponse<object>.Fail("이 화면을 볼 권한이 없습니다. 메뉴 권한에서 역할을 확인하세요.", "403"),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
