using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 회사·부서·사람 단계로 역할을 걸고 푸는 엔드포인트.
///
/// <para>
/// 세 단계 모두 같은 통로를 쓴다(<c>kind</c> 로 구분). 화면이 어디에 놓든 같은 요청을
/// 보내면 되므로 드래그드롭 처리가 한 갈래로 끝난다.
/// </para>
/// </summary>
public static class RoleScopeEndpoints
{
    /// <summary>역할 범위 엔드포인트를 등록한다.</summary>
    public static void MapRoleScopeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/role-scope").WithTags("RoleScope");

        /// <summary>회사 하나의 조직 트리와 각 단계에 걸린 역할.</summary>
        group.MapGet("/tree", async ([FromQuery] string companyId, [FromServices] IRoleAssignmentService service) =>
        {
            try
            {
                return Results.Ok(ApiResponse<RoleScopeTreeDto>.Ok(await service.GetScopeTreeAsync(companyId)));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ApiResponse<object>.Fail(ex.Message, "B404"));
            }
        })
        .WithName("GetRoleScopeTree")
        .WithOpenApi();

        /// <summary>대상에 역할을 건다. 이미 걸려 있으면 그대로 둔다.</summary>
        group.MapPost("/assign", async ([FromBody] RoleAssignRequest request, [FromServices] IRoleAssignmentService service) =>
        {
            try
            {
                await service.AssignAsync(ParseKind(request.Kind), request.TargetId, request.RoleId);
                return Results.Ok(ApiResponse<bool>.Ok(true, "역할을 지정했습니다."));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(ex.Message, "B404"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(ex.Message, "B400"));
            }
        })
        .WithName("AssignRoleScope")
        .WithOpenApi();

        /// <summary>대상에서 역할을 푼다. 걸려 있지 않아도 오류가 아니다.</summary>
        group.MapPost("/remove", async ([FromBody] RoleAssignRequest request, [FromServices] IRoleAssignmentService service) =>
        {
            try
            {
                await service.RemoveAsync(ParseKind(request.Kind), request.TargetId, request.RoleId);
                return Results.Ok(ApiResponse<bool>.Ok(true, "역할을 해제했습니다."));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(ex.Message, "B400"));
            }
        })
        .WithName("RemoveRoleScope")
        .WithOpenApi();

        /// <summary>
        /// 그 계정에 실제로 적용되는 역할과 그것이 온 단계.
        /// 화면이 "이 역할은 부서에서 물려받은 것" 이라고 알려 줄 때 쓴다.
        /// </summary>
        group.MapGet("/effective", async ([FromQuery] string accountId, [FromServices] IRoleAssignmentService service) =>
        {
            return Results.Ok(ApiResponse<EffectiveRolesDto>.Ok(await service.ResolveEffectiveRolesAsync(accountId)));
        })
        .WithName("GetEffectiveRoles")
        .WithOpenApi();

        /// <summary>
        /// 검색용 사람 목록. 회사·부서 이름까지 함께 담아 한 줄로 훑을 수 있게 한다.
        /// </summary>
        group.MapGet("/accounts", async ([FromServices] IRoleAssignmentService service) =>
        {
            return Results.Ok(ApiResponse<List<AccountPickDto>>.Ok(await service.GetAccountPickListAsync()));
        })
        .WithName("GetRoleScopeAccounts")
        .WithOpenApi();

        /// <summary>그 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.</summary>
        group.MapGet("/menus", async ([FromQuery] string accountId, [FromServices] IRoleAssignmentService service) =>
        {
            return Results.Ok(ApiResponse<AccountMenuAccessDto>.Ok(await service.GetMenuAccessAsync(accountId)));
        })
        .WithName("GetAccountMenuAccess")
        .WithOpenApi();
    }

    private static RoleScopeKind ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "company" => RoleScopeKind.Company,
        "department" => RoleScopeKind.Department,
        "account" => RoleScopeKind.Account,
        _ => throw new InvalidOperationException("kind 는 company · department · account 중 하나여야 합니다."),
    };
}
