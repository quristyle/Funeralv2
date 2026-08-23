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


        // 등록·수정·삭제 엔드포인트는 제거했다.
        //
        // 회사 **관리 화면(`/helpdesk/org/company`)이 없어졌다.** 회사는 포털
        // (`/system/company`)에서 관리하고, 헬프데스크에 있던 9건은 포털 회사 데이터로
        // 옮겼다(각 행의 remark 에 `helpdesk:company:<원본ID>` 를 남겼다).
        //
        // 조회는 남긴다 — 요청 화면들의 회사 셀렉트와 팀-회사 매핑 화면이 쓴다.
        // 요청·팀 데이터가 헬프데스크 회사 ID 를 참조하므로 그 값을 계속 읽어야 한다.
        //
        // 헬프데스크 DB(`jsini.company`)는 손대지 않았다.
        //
        // 여기 있던 등록 로직은 회사를 만들 때 `pub_<회사ID>` 공용 고객까지 함께
        // 만들었다. 그 계정은 관리자가 회사를 대신해 요청을 등록할 때 쓰인다
        // (요청 등록 화면의 getCustomerByLoginId 참고). 포털에서 만든 회사에는
        // 그 공용 고객이 생기지 않는다 — 판단이 필요한 지점이라 아래 문서에 적었다.
    }
}
