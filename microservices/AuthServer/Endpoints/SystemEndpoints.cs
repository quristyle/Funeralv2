using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using JSini.Shared.DTOs;
using static AuthServer.Services.I18nResourceService;

namespace AuthServer.Endpoints;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system");

        // 사용자 계정(Account) 관리
        group.MapGet("/account/list", async ([FromServices] IUserService userService) =>
        {
            var accounts = await userService.GetAccountsAsync();
            return Results.Ok(ApiResponse<List<AccountDto>>.Ok(accounts));
        })
        .WithName("GetAccountList")
        .WithOpenApi();

        group.MapPost("/account", async ([FromBody] CreateAccountDto request, [FromServices] IUserService userService) =>
        {
            var account = await userService.CreateAccountAsync(request);
            return Results.Ok(ApiResponse<AccountDto>.Ok(account));
        })
        .WithName("CreateAccount")
        .WithOpenApi();

        group.MapPut("/account/{id}", async (string id, [FromBody] UpdateAccountDto request, [FromServices] IUserService userService) =>
        {
            var success = await userService.UpdateAccountAsync(id, request);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("계정을 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateAccount")
        .WithOpenApi();

        group.MapDelete("/account/{id}", async (string id, [FromServices] IUserService userService) =>
        {
            var success = await userService.DeleteAccountAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("계정을 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteAccount")
        .WithOpenApi();

        // 역할(Role) 관리
        group.MapGet("/role/id-exists", async ([FromQuery] string id, [FromServices] IRoleService roleService) =>
        {
            var exists = await roleService.IsIdExistsAsync(id);
            return Results.Ok(ApiResponse<bool>.Ok(exists));
        })
        .WithName("IsRoleIdExists")
        .WithOpenApi();

        group.MapGet("/role/list", async ([FromServices] IRoleService roleService) =>
        {
            var roles = await roleService.GetRoleListAsync();
            //return Results.Ok(ApiResponse<PagedResult<RoleDto>>.Ok(roles.ToPagedResult()));
            return Results.Ok(ApiResponse<List<RoleDto>>.Ok(roles));
        })
        .WithName("GetRoleList")
        .WithOpenApi();

        group.MapPost("/role", async ([FromBody] CreateRoleDto request, [FromServices] IRoleService roleService) =>
        {
            try
            {
                var role = await roleService.CreateRoleAsync(request);
                return Results.Ok(ApiResponse<RoleDto>.Ok(role));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "400"));
            }
        })
        .WithName("CreateRole")
        .WithOpenApi();

        group.MapPut("/role/{id}", async (string id, [FromBody] CreateRoleDto request, [FromServices] IRoleService roleService) =>
        {
            var success = await roleService.UpdateRoleAsync(id, request);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("역할을 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateRole")
        .WithOpenApi();

        group.MapDelete("/role/{id}", async (string id, [FromServices] IRoleService roleService) =>
        {
            var success = await roleService.DeleteRoleAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("역할을 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteRole")
        .WithOpenApi();

        // 부서(Department) 관리
        group.MapGet("/dept/list", async ([FromQuery] string? companyId, UserContext? userContext, [FromServices] IDepartmentService deptService) =>
        {
            var depts = await deptService.GetDeptListAsync(companyId, userContext);
            return Results.Ok(ApiResponse<List<DepartmentDto>>.Ok(depts));
        })
        .WithName("GetDeptList")
        .WithOpenApi();

        group.MapPost("/dept", async (UserContext? userContext, [FromBody] CreateDepartmentDto request, [FromServices] IDepartmentService deptService) =>
        {
            var dept = await deptService.CreateDeptAsync(request, userContext);
            return Results.Ok(ApiResponse<DepartmentDto>.Ok(dept));
        })
        .WithName("CreateDept")
        .WithOpenApi();

        group.MapPut("/dept/{id}", async (string id, UserContext? userContext, [FromBody] CreateDepartmentDto request, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.UpdateDeptAsync(id, request, userContext);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("부서를 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateDept")
        .WithOpenApi();

        group.MapDelete("/dept/{id}", async (string id, UserContext? userContext, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.DeleteDeptAsync(id, userContext);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("부서를 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteDept")
        .WithOpenApi();

        // 부서 소속 사용자 매핑 관리
        group.MapGet("/dept/{id}/users", async (string id, [FromServices] IDepartmentService deptService) =>
        {
            var users = await deptService.GetDeptUsersAsync(id);
            return Results.Ok(ApiResponse<IEnumerable<AccountDto>>.Ok(users));
        })
        .WithName("GetDeptUsers")
        .WithOpenApi();

        group.MapGet("/dept/eligible-users", async ([FromQuery] string? companyId, [FromServices] IDepartmentService deptService) =>
        {
            var users = await deptService.GetEligibleUsersAsync(companyId);
            return Results.Ok(ApiResponse<IEnumerable<AccountDto>>.Ok(users));
        })
        .WithName("GetEligibleUsersForDept")
        .WithOpenApi();

        group.MapPost("/dept/{id}/users", async (string id, [FromBody] List<string> userIds, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.AssignUsersToDeptAsync(id, userIds);
            return Results.Ok(ApiResponse<bool>.Ok(success, "사용자가 부서에 등록되었습니다."));
        })
        .WithName("AssignUsersToDept")
        .WithOpenApi();

        group.MapPost("/dept/users/remove", async ([FromBody] List<string> userIds, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.RemoveUsersFromDeptAsync(userIds);
            return Results.Ok(ApiResponse<bool>.Ok(success, "사용자의 부서 소속이 해제되었습니다."));
        })
        .WithName("RemoveUsersFromDept")
        .WithOpenApi();

        // 부서 및 사용자 노드 이동 (조직도 드래그앤드롭용)
        group.MapPost("/dept/{id}/move", async (string id, [FromQuery] string? parentId, UserContext? userContext, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.MoveDeptAsync(id, parentId, userContext);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true, "부서가 이동되었습니다.")) : Results.BadRequest(ApiResponse<object>.Fail("부서 이동에 실패했습니다. (순환 참조 등이 의심됩니다.)", "400"));
        })
        .WithName("MoveDept")
        .WithOpenApi();

        group.MapPost("/dept/user/move", async ([FromQuery] string accountId, [FromQuery] string? departmentId, UserContext? userContext, [FromServices] IDepartmentService deptService) =>
        {
            var success = await deptService.MoveUserDeptAsync(accountId, departmentId, userContext);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true, "사용자 부서가 이동되었습니다.")) : Results.BadRequest(ApiResponse<object>.Fail("사용자 부서 이동에 실패했습니다.", "400"));
        })
        .WithName("MoveUserDept")
        .WithOpenApi();

        // 시스템 메뉴 관리
        group.MapGet("/menu/list", async ([FromServices] ISystemMenuService menuService) =>
        {
            var menus = await menuService.GetMenuListAsync();
            return Results.Ok(ApiResponse<List<SystemMenuDto>>.Ok(menus));
        })
        .WithName("GetSystemMenuList")
        .WithOpenApi();

        group.MapGet("/menu/name-exists", async ([FromQuery] string name, [FromQuery] string? id, [FromServices] ISystemMenuService menuService) =>
        {
            var exists = await menuService.IsNameExistsAsync(name, id);
            return Results.Ok(ApiResponse<bool>.Ok(exists));
        })
        .WithName("IsMenuNameExists")
        .WithOpenApi();

        group.MapGet("/menu/path-exists", async ([FromQuery] string path, [FromQuery] string? id, [FromServices] ISystemMenuService menuService) =>
        {
            var exists = await menuService.IsPathExistsAsync(path, id);
            return Results.Ok(ApiResponse<bool>.Ok(exists));
        })
        .WithName("IsMenuPathExists")
        .WithOpenApi();

        group.MapPost("/menu", async ([FromBody] CreateSystemMenuDto request, [FromServices] ISystemMenuService menuService) =>
        {
            var menu = await menuService.CreateMenuAsync(request);
            return Results.Ok(ApiResponse<SystemMenuDto>.Ok(menu));
        })
        .WithName("CreateSystemMenu")
        .WithOpenApi();

        group.MapPost("/menu/move", async ([FromBody] MoveSystemMenuRequest request, [FromServices] IMenuService menuService) =>
        {
            try
            {
                var success = await menuService.MoveMenuAsync(request.MenuId, request.NewParentId, request.NewOrderNo);
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("메뉴 이동 실패", "B400", realMessage: ex.Message));
            }
        })
        .WithName("MoveSystemMenu")
        .WithOpenApi();

        // 트리 그리드에서 드래그로 자리를 옮기면 형제 여러 개의 순번이 함께 바뀐다.
        // 화면이 확정한 배치를 그대로 받아 한 번의 왕복으로 저장한다.
        group.MapPost("/menu/reorder", async ([FromBody] List<MenuOrderDto> items, [FromServices] IMenuService menuService) =>
        {
            try
            {
                await menuService.ReorderMenusAsync(items);
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("메뉴 순서 저장 실패", "B400", realMessage: ex.Message));
            }
        })
        .WithName("ReorderSystemMenus")
        .WithOpenApi();

        group.MapPut("/menu/{id}", async (string id, [FromBody] CreateSystemMenuDto request, [FromServices] ISystemMenuService menuService) =>
        {
            var success = await menuService.UpdateMenuAsync(id, request);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("메뉴를 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateSystemMenu")
        .WithOpenApi();

        group.MapDelete("/menu/{id}", async (string id, [FromServices] ISystemMenuService menuService) =>
        {
            var success = await menuService.DeleteMenuAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("메뉴를 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteSystemMenu")
        .WithOpenApi();

        // 다국어(I18n) 관리
        group.MapGet("/i18n/list", async ([FromServices] II18nResourceService i18nService) =>
        {
            var resources = await i18nService.GetAllResourcesAsync();
            //return Results.Ok(ApiResponse<PagedResult<I18nResourceDto>>.Ok(resources.ToPagedResult())); // 실제 페이징처리할 페이지의 경우 pagedResult 를 사용.
            return Results.Ok(ApiResponse<List<I18nResourceDto>>.Ok(resources));
        })
        .WithName("GetI18nList")
        .WithOpenApi();

        group.MapGet("/i18n/paged", async ([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? locale, [FromQuery] string? key, [FromQuery] string? value, [FromQuery] string? category, [FromServices] II18nResourceService i18nService) =>
        {
            var searchParams = new SearchI18nParams { Page = page, PageSize = pageSize, Locale = locale, Key = key, Value = value, Category = category };
            var result = await i18nService.GetPagedResourcesAsync(searchParams);
            //return Results.Ok(ApiResponse<List<I18nResourceDto>>.Ok(result));
            return Results.Ok(ApiResponse<PagedResourceDto<I18nResourceDto>>.Ok(result));
        })
        .WithName("GetI18nPaged")
        .WithOpenApi();

        group.MapGet("/i18n/{locale}", async (string locale, [FromServices] II18nResourceService i18nService) =>
        {
            var resources = await i18nService.GetResourcesByLocaleAsync(locale);
            return Results.Ok(ApiResponse<List<I18nResourceDto>>.Ok(resources));
        })
        .WithName("GetI18nByLocale")
        .WithOpenApi();

        group.MapPost("/i18n", async ([FromBody] CreateI18nResourceDto request, [FromServices] II18nResourceService i18nService) =>
        {
            var resource = await i18nService.CreateResourceAsync(request);
            return Results.Ok(ApiResponse<I18nResourceDto>.Ok(resource));
        })
        .WithName("CreateI18nResource")
        .WithOpenApi();

        group.MapPut("/i18n/{id}", async (int id, [FromBody] CreateI18nResourceDto request, [FromServices] II18nResourceService i18nService) =>
        {
            var success = await i18nService.UpdateResourceAsync(id, request);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("다국어 자원을 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateI18nResource")
        .WithOpenApi();

        group.MapDelete("/i18n/{id}", async (int id, [FromServices] II18nResourceService i18nService) =>
        {
            var success = await i18nService.DeleteResourceAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("다국어 자원을 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteI18nResource")
        .WithOpenApi();

        group.MapPost("/i18n/ensure", async ([FromBody] EnsureI18nRequest request, [FromServices] II18nResourceService i18nService) =>
        {
            await i18nService.EnsureResourceExistsAsync(request.Locale, request.Key, request.DefaultValue);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("EnsureI18nResource")
        .WithOpenApi();

        // BizSelect 설정 관리
        group.MapGet("/biz-select/configs", async ([FromServices] IBizSelectConfigService configService) =>
        {
            var configs = await configService.GetAllConfigsAsync();
            return Results.Ok(ApiResponse<List<BizSelectConfigDto>>.Ok(configs.ToList()));
        })
        .WithName("GetBizSelectConfigs")
        .WithOpenApi();

        group.MapPost("/biz-select/config", async ([FromBody] BizSelectConfigCreateDto request, [FromServices] IBizSelectConfigService configService) =>
        {
            var config = await configService.CreateConfigAsync(request);
            return Results.Ok(ApiResponse<BizSelectConfigDto>.Ok(config));
        })
        .WithName("CreateBizSelectConfig")
        .WithOpenApi();

        group.MapPut("/biz-select/config/{id}", async (string id, [FromBody] BizSelectConfigCreateDto request, [FromServices] IBizSelectConfigService configService) =>
        {
            var success = await configService.UpdateConfigAsync(id, request);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("설정을 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateBizSelectConfig")
        .WithOpenApi();

        group.MapDelete("/biz-select/config/{id}", async (string id, [FromServices] IBizSelectConfigService configService) =>
        {
            var success = await configService.DeleteConfigAsync(id);
            return success ? Results.Ok(ApiResponse<bool>.Ok(true)) : Results.NotFound(ApiResponse<object>.Fail("설정을 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteBizSelectConfig")
        .WithOpenApi();
    }
}

public class EnsureI18nRequest
{
    public string Locale { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
}

public class MoveSystemMenuRequest
{
    public string MenuId { get; set; } = string.Empty;
    public string? NewParentId { get; set; }
    public int NewOrderNo { get; set; }
}
