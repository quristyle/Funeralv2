using HelpDeskServer.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using System.Linq.Dynamic.Core;

namespace HelpDeskServer.Endpoints;

/// <summary> 팀 엔드포인트 </summary>
public static class TeamEndpoints
{

    /// <summary>
    /// 팀 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapTeamEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/teams");

        // 전체 조회
        group.MapGet("/", (AppDbContext db, HttpContext http) =>
        {
            var serviceName = http.Request.Headers["X-Service-Name"].ToString();
            var menuName = http.Request.Headers["X-Menu-Name"].ToString();


            return ApiResponseBuilder.CreateAsync(
                () => db.Teams.ToListAsync()
            );
        });

        // 상세 조회
        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.Teams.FirstOrDefaultAsync(c => c.Id == id)
        ));



        // 검색
        group.MapGet("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var baseQuery = db.Teams.AsQueryable();

            // ApplyAll 은 IQueryable 반환 (동적 타입 가능)
            var resultQuery = baseQuery.ApplyAll(http.Request.Query);

            // ToListAsync 은 dynamic IQueryable 에서도 작동
            var list = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());
            return list;
        }, "Team srch successfully.", 201));


        // 검색
        group.MapPost("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () =>
        {

            // 1) JSON Body 읽기
            using var reader = new StreamReader(http.Request.Body);
            var body = await reader.ReadToEndAsync();

            IQueryCollection bodyQuery = new QueryCollection();
            if (!string.IsNullOrWhiteSpace(body))
            {
                bodyQuery = JsonToQueryHelper.ConvertJsonToQuery(body);
            }

            // 2) GET QueryString 과 병합
            var finalQuery = QueryCollectionMerger.Merge(http.Request.Query, bodyQuery);

            // 3) DynamicFilterHelper 재사용

            var baseQuery = db.Teams.AsQueryable();

            // 포함 관계 필요하면 Include 이후 ApplyAll 호출
            //baseQuery = baseQuery.Include(c => c.Attachments);

            // ApplyAll 은 IQueryable 반환 (동적 타입 가능)
            //var resultQuery = baseQuery.ApplyAll(http.Request.Query);
            var resultQuery = baseQuery.ApplyAll(finalQuery);

            // ToListAsync 은 dynamic IQueryable 에서도 작동
            var list = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());
            return list;
        }, "Team search successfully.", 201));


        // 등록
        group.MapPost("/", (AppDbContext db, TeamCreateDto teamDto) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var team = new Team
            {
                Name = teamDto.Name,
                ModifiedBy = teamDto.ModifiedBy,
                MenuContext = teamDto.MenuContext,
                // CreatedAt, ModifiedAt은 AppDbContext의 SaveChangesAsync에서 자동으로 설정됩니다.
            };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return team;
        }, "Team created successfully.", 201));

        // 수정
        group.MapPut("/{id}", (AppDbContext db, int id, Team input) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var team = await db.Teams.FindAsync(id);
            if (team is null) return null;

            team.Name = input.Name;
            // ModifiedBy, ModifiedAt은 AppDbContext의 SaveChangesAsync에서 자동으로 설정됩니다.

            await db.SaveChangesAsync();
            return team;
        }, "Team updated successfully."));

        // 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var team = await db.Teams.FindAsync(id);
            if (team is null) return null;

            db.Teams.Remove(team);
            await db.SaveChangesAsync();
            // 삭제 성공 시 데이터는 없으므로 간단한 객체를 반환합니다.
            return new { DeletedId = id };
        }, "Team deleted successfully."));

        // 팀-회사 관계 조회
        group.MapGet("/{id}/companies", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var companies = await db.TeamCompanies
                .Where(tc => tc.TeamId == id)
                .Select(tc => tc.Company)
                .ToListAsync();
            return companies;
        }, "Team companies retrieved successfully."));

        // 팀-회사 관계 업데이트
        group.MapPost("/{id}/companies", (AppDbContext db, int id, List<int> companyIds) => ApiResponseBuilder.CreateAsync(async () =>
        {
            // 기존 관계 삭제
            var existing = await db.TeamCompanies.Where(tc => tc.TeamId == id).ToListAsync();
            db.TeamCompanies.RemoveRange(existing);

            // 새로운 관계 추가
            if (companyIds != null && companyIds.Any())
            {
                var newRelations = companyIds.Select(cid => new TeamCompany
                {
                    TeamId = id,
                    CompanyId = cid
                });
                db.TeamCompanies.AddRange(newRelations);
            }

            await db.SaveChangesAsync();
            return new { Success = true };
        }, "Team companies updated successfully."));
    }
}