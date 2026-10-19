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
        // usageLocation 을 주면 그 사용처에 배정된 회사만 준다 — 장례식장 시스템 화면이 좁혀 쓴다. 안 주면 전부 준다.
        group.MapGet("/", async ([FromQuery] string? usageLocation, ICompanyService companyService) =>
        {
            var companies = await companyService.GetAllCompaniesAsync(usageLocation);
            return Results.Ok(companies);
        })
        .WithName("GetAllCompanies");

        // 소속 회사가 없는 사용자 조회 (추가 모달용)
        group.MapGet("/eligible-users", async (ICompanyService companyService) =>
        {
            var users = await companyService.GetEligibleUsersAsync();
            return Results.Ok(users);
        })
        .WithName("GetEligibleUsersForCompany");

        // 소속 회사 해제 (일괄)
        group.MapPost("/users/remove", async ([FromBody] List<string> userIds, ICompanyService companyService) =>
        {
            var success = await companyService.RemoveUsersFromCompanyAsync(userIds);
            return Results.Ok(success);
        })
        .WithName("RemoveUsersFromCompany");

        // 특정 회사 소속 사용자 목록 조회
        group.MapGet("/{companyId}/users", async (string companyId, ICompanyService companyService) =>
        {
            var users = await companyService.GetCompanyUsersAsync(companyId);
            return Results.Ok(users);
        })
        .WithName("GetCompanyUsers");

        // 회사에 사용자 추가 등록 (일괄)
        group.MapPost("/{companyId}/users", async (string companyId, [FromBody] List<string> userIds, ICompanyService companyService) =>
        {
            var success = await companyService.AssignUsersToCompanyAsync(companyId, userIds);
            return Results.Ok(success);
        })
        .WithName("AssignUsersToCompany");

        // 회사 차례 다시 매기기
        // **/{id} 보다 먼저 적는다.** 리터럴이 매개변수를 이겨 순서와 무관하지만 읽는 사람에게는 위가 낫다.
        // 화면이 보이는 줄 전부를 순서대로 싣는다(ICompanyService.ReorderCompaniesAsync 머리말).
        group.MapPost("/reorder", async ([FromBody] List<string> orderedIds, ICompanyService companyService) =>
        {
            var success = await companyService.ReorderCompaniesAsync(orderedIds);
            return success
                ? Results.Ok(true)
                : Results.NotFound(ApiResponse<object>.Fail("차례를 바꿀 회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("ReorderCompanies");

        // 특정 회사 상세 조회
        group.MapGet("/{id}", async (string id, ICompanyService companyService) =>
        {
            var company = await companyService.GetCompanyByIdAsync(id);
            return company is not null 
                ? Results.Ok(company) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("GetCompanyById");

        // 회사 등록
        group.MapPost("/", async (CompanyCreateDto createDto, ICompanyService companyService) =>
        {
            var result = await companyService.CreateCompanyAsync(createDto);
            return Results.Ok(result);
        })
        .WithName("CreateCompany");

        // 회사 정보 수정
        group.MapPut("/{id}", async (string id, CompanyCreateDto updateDto, ICompanyService companyService) =>
        {
            var success = await companyService.UpdateCompanyAsync(id, updateDto);
            return success 
                ? Results.Ok(true) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("UpdateCompany");

        // 회사 삭제
        group.MapDelete("/{id}", async (string id, ICompanyService companyService) =>
        {
            var success = await companyService.DeleteCompanyAsync(id);
            return success 
                ? Results.Ok(true) 
                : Results.NotFound(ApiResponse<object>.Fail("회사를 찾을 수 없습니다.", "404"));
        })
        .WithName("DeleteCompany");
    }
}
