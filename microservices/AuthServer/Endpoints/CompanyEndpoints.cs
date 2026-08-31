using AuthServer.DTOs;
using AuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace AuthServer.Endpoints;

public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        // SystemEndpoints와 일관성을 유지하기 위해 /system 하위로 그룹화합니다.
        var group = app.MapGroup("/system/companies")
                       .WithTags("Company Management")
                       .AddApiResponseWrapper();

        // 회사 전체 목록 조회
        //
        // `usageLocation` 을 주면 그 사용처(COMPANY_USAGE_LOCATION)에 배정된 회사만 준다.
        // 장례식장 관리시스템 화면들이 FUNERAL_HOME_MANAGEMENT_SYSTEM 으로 좁혀 쓴다.
        // 안 주면 예전처럼 전부 준다 — 회사 관리 화면이 그대로 동작해야 한다.
        group.MapGet("/", async ([FromQuery] string? usageLocation, ICompanyService companyService) =>
        {
            var companies = await companyService.GetAllCompaniesAsync(usageLocation);
            return Results.Ok(companies);
        })
        .WithName("GetAllCompanies")
        .WithOpenApi();

        // 소속 회사가 없는 사용자 조회 (추가 모달용)
        group.MapGet("/eligible-users", async (ICompanyService companyService) =>
        {
            var users = await companyService.GetEligibleUsersAsync();
            return Results.Ok(users);
        })
        .WithName("GetEligibleUsersForCompany")
        .WithOpenApi();

        // 소속 회사 해제 (일괄)
        group.MapPost("/users/remove", async ([FromBody] List<string> userIds, ICompanyService companyService) =>
        {
            var success = await companyService.RemoveUsersFromCompanyAsync(userIds);
            return Results.Ok(success);
        })
        .WithName("RemoveUsersFromCompany")
        .WithOpenApi();

        // 특정 회사 소속 사용자 목록 조회
        group.MapGet("/{companyId}/users", async (string companyId, ICompanyService companyService) =>
        {
            var users = await companyService.GetCompanyUsersAsync(companyId);
            return Results.Ok(users);
        })
        .WithName("GetCompanyUsers")
        .WithOpenApi();

        // 회사에 사용자 추가 등록 (일괄)
        group.MapPost("/{companyId}/users", async (string companyId, [FromBody] List<string> userIds, ICompanyService companyService) =>
        {
            var success = await companyService.AssignUsersToCompanyAsync(companyId, userIds);
            return Results.Ok(success);
        })
        .WithName("AssignUsersToCompany")
        .WithOpenApi();

        // 특정 회사 상세 조회
        group.MapGet("/{id}", async (string id, ICompanyService companyService) =>
        {
            var company = await companyService.GetCompanyByIdAsync(id);
            return company is not null 
                ? Results.Ok(company) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("GetCompanyById")
        .WithOpenApi();

        // 회사 등록
        group.MapPost("/", async (CompanyCreateDto createDto, ICompanyService companyService) =>
        {
            var result = await companyService.CreateCompanyAsync(createDto);
            return Results.Ok(result);
        })
        .WithName("CreateCompany")
        .WithOpenApi();

        // 회사 정보 수정
        group.MapPut("/{id}", async (string id, CompanyCreateDto updateDto, ICompanyService companyService) =>
        {
            var success = await companyService.UpdateCompanyAsync(id, updateDto);
            return success 
                ? Results.Ok(true) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateCompany")
        .WithOpenApi();

        // 회사 삭제
        group.MapDelete("/{id}", async (string id, ICompanyService companyService) =>
        {
            var success = await companyService.DeleteCompanyAsync(id);
            return success 
                ? Results.Ok(true) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteCompany")
        .WithOpenApi();
    }
}
