using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using Funeralv2.Shared.DTOs;

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
            var role = await roleService.CreateRoleAsync(request);
            return Results.Ok(ApiResponse<RoleDto>.Ok(role));
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
        group.MapGet("/dept/list", async (UserContext? userContext, [FromServices] IDepartmentService deptService) =>
        {
            var depts = await deptService.GetDeptListAsync(userContext);
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
            //return Results.Ok(ApiResponse<PagedResult<I18nResourceDto>>.Ok(resources.ToPagedResult()));
            return Results.Ok(ApiResponse<List<I18nResourceDto>>.Ok(resources));
        })
        .WithName("GetI18nList")
        .WithOpenApi();

        group.MapGet("/i18n/paged", async ([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? locale, [FromQuery] string? key, [FromQuery] string? value, [FromQuery] string? category, [FromServices] II18nResourceService i18nService) =>
        {
            var searchParams = new SearchI18nParams { Page = page, PageSize = pageSize, Locale = locale, Key = key, Value = value, Category = category };
            var result = await i18nService.GetPagedResourcesAsync(searchParams);
            return Results.Ok(ApiResponse<List<I18nResourceDto>>.Ok(result));
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
