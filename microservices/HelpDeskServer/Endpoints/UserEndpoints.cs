using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using HelpDeskServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 사용자 관련 엔드포인트
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        /// <summary>
        /// 관리자가 조회할 수 있는 모든 사용자(관리자, 고객) 목록을 반환합니다.
        /// </summary>
        // 담당자 권한이면 볼 수 있다. 전에는 헬프데스크 계정 연결이 admin 인 경우만 통과시켜서,
        // 포털에서 관리자 역할을 받은 계정도 연결이 없으면 403 이었다.
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            if (!http.GetHelpdeskPrincipal().IsAdmin)
            {
                return Results.Forbid();
            }

            var admins = await db.Admins
                                 .AsNoTracking()
                                 .Select(a => new UserDto
                                 {
                                     UserId = a.Id,
                                     UserName = a.UserName + " (관리자)",
                                     UserType = "admin"
                                 })
                                 .ToListAsync();

            var customers = await db.Customers
                                    .AsNoTracking()
                                    .Select(c => new UserDto
                                    {
                                        UserId = c.Id,
                                        UserName = c.UserName,
                                        UserType = "customer"
                                    })
                                    .ToListAsync();

            var allUsers = admins.Concat(customers).OrderBy(u => u.UserName);

            return Results.Ok(allUsers);
        });
    }
}
