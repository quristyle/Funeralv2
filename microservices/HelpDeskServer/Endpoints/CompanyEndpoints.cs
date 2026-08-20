using HelpDeskServer.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using System.Linq.Dynamic.Core;

namespace HelpDeskServer.Endpoints;

/// <summary> 고객사 엔드포인트 </summary>
public static class CompanyEndpoints {

    /// <summary>
    /// 고객사 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder routes)    {
        var group = routes.MapGroup("/api/companys");

        // 전체 조회
        group.MapGet("/", (AppDbContext db, HttpContext http) =>        {
            var serviceName = http.Request.Headers["X-Service-Name"].ToString();
            var menuName = http.Request.Headers["X-Menu-Name"].ToString();


            return ApiResponseBuilder.CreateAsync(
                () => db.Companies.ToListAsync()
            );
        });

        // 상세 조회
        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.Companies.FirstOrDefaultAsync(c => c.Id == id)
        ));



        // 검색
        group.MapGet("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () =>        {
            var baseQuery = db.Companies.AsQueryable();

            // 포함 관계 필요하면 Include 이후 ApplyAll 호출
            //baseQuery = baseQuery;

            // ApplyAll 은 IQueryable 반환 (동적 타입 가능)
            var resultQuery = baseQuery.ApplyAll(http.Request.Query);

            // ToListAsync 은 dynamic IQueryable 에서도 작동
            var list = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());
            return list;
        }, "Company srch successfully.", 201));


        // 검색
        group.MapPost("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () =>        {

            // 1) JSON Body 읽기
            using var reader = new StreamReader(http.Request.Body);
            var body = await reader.ReadToEndAsync();

            IQueryCollection bodyQuery = new QueryCollection();
            if (!string.IsNullOrWhiteSpace(body))            {
                bodyQuery = JsonToQueryHelper.ConvertJsonToQuery(body);
            }

            // 2) GET QueryString 과 병합
            var finalQuery = QueryCollectionMerger.Merge(http.Request.Query, bodyQuery);

            // 3) DynamicFilterHelper 재사용

            var baseQuery = db.Companies.AsQueryable();

            // 포함 관계 필요하면 Include 이후 ApplyAll 호출
            //baseQuery = baseQuery.Include(c => c.Attachments);

            // ApplyAll 은 IQueryable 반환 (동적 타입 가능)
            //var resultQuery = baseQuery.ApplyAll(http.Request.Query);
            var resultQuery = baseQuery.ApplyAll(finalQuery);

            // ToListAsync 은 dynamic IQueryable 에서도 작동
            var list = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());
            return list;
        }, "Company search successfully.", 201));


        // 등록
        group.MapPost("/", (AppDbContext db, CompanyCreateDto companyDto) => ApiResponseBuilder.CreateAsync(async () =>        {
            var company = new CustomerCompany            {
                Name = companyDto.Name,
                ModifiedBy = companyDto.ModifiedBy,
                MenuContext = companyDto.MenuContext,
                // CreatedAt, ModifiedAt은 AppDbContext의 SaveChangesAsync에서 자동으로 설정됩니다.
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            // 회사 등록시 기본 고객사 접수자 자동 생성 (고객사 접수자 테이블)
            var customer = new Customer            {
                LoginId = "pub_"+company.Id.ToString(),
                UserName = companyDto.Name+"공통",
                Email = companyDto.Name + "@company.com",
                CompanyId = company.Id,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();


            return company;
        }, "Company created successfully.", 201));

        // 수정
        group.MapPut("/{id}", (AppDbContext db, int id, CustomerCompany input) => ApiResponseBuilder.CreateAsync(async () =>        {
            var company = await db.Companies.FindAsync(id);
            if (company is null) return null;

            company.Name = input.Name;
            // ModifiedBy, ModifiedAt은 AppDbContext의 SaveChangesAsync에서 자동으로 설정됩니다.

            await db.SaveChangesAsync();
            return company;
        }, "Company updated successfully."));

        // 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>        {
            var company = await db.Companies.FindAsync(id);
            if (company is null) return null;

            db.Companies.Remove(company);
            await db.SaveChangesAsync();
            // 삭제 성공 시 데이터는 없으므로 간단한 객체를 반환합니다.
            return new { DeletedId = id };
        }, "Company deleted successfully."));
    }
}
