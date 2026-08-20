using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
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
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var loginTypeClaim = http.User.FindFirst("login_type");
            if (loginTypeClaim?.Value != "admin")
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
