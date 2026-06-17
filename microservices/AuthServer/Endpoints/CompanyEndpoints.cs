using AuthServer.DTOs;
using AuthServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        // SystemEndpoints와 일관성을 유지하기 위해 /system 하위로 그룹화합니다.
        var group = app.MapGroup("/system/companies").WithTags("Company Management");

        // 회사 전체 목록 조회
        group.MapGet("/", async (ICompanyService companyService) =>
        {
            var companies = await companyService.GetAllCompaniesAsync();
            return Results.Ok(ApiResponse<PagedResult<CompanyDto>>.Success(companies.ToPagedResult()));
        })
        .WithName("GetAllCompanies")
        .WithOpenApi();

        // 특정 회사 상세 조회
        group.MapGet("/{id}", async (string id, ICompanyService companyService) =>
        {
            var company = await companyService.GetCompanyByIdAsync(id);
            return company is not null 
                ? Results.Ok(ApiResponse<CompanyDto>.Success(company)) 
                : Results.NotFound(ApiResponse<object>.Errors(404, "회사를 찾을 수 없습니다."));
        })
        .WithName("GetCompanyById")
        .WithOpenApi();

        // 회사 등록
        group.MapPost("/", async (CompanyCreateDto createDto, ICompanyService companyService) =>
        {
            var result = await companyService.CreateCompanyAsync(createDto);
            return Results.Ok(ApiResponse<CompanyDto>.Success(result, "회사가 성공적으로 등록되었습니다."));
        })
        .WithName("CreateCompany")
        .WithOpenApi();

        // 회사 정보 수정
        group.MapPut("/{id}", async (string id, CompanyCreateDto updateDto, ICompanyService companyService) =>
        {
            var success = await companyService.UpdateCompanyAsync(id, updateDto);
            return success 
                ? Results.Ok(ApiResponse<bool>.Success(true, "회사 정보가 수정되었습니다.")) 
                : Results.NotFound(ApiResponse<object>.Errors(404, "회사를 찾을 수 없습니다."));
        })
        .WithName("UpdateCompany")
        .WithOpenApi();

        // 회사 삭제
        group.MapDelete("/{id}", async (string id, ICompanyService companyService) =>
        {
            var success = await companyService.DeleteCompanyAsync(id);
            return success 
                ? Results.Ok(ApiResponse<bool>.Success(true, "회사가 삭제되었습니다.")) 
                : Results.NotFound(ApiResponse<object>.Errors(404, "회사를 찾을 수 없습니다."));
        })
        .WithName("DeleteCompany")
        .WithOpenApi();
    }
}
