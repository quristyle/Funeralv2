using System.Security.Claims;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using HelpDeskServer.Services;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// funeralv2 계정 ↔ 헬프데스크 계정 매핑 관리 엔드포인트.
/// 로그인 아이디나 이메일이 서로 다른 사용자를 관리자가 직접 이어줄 때 쓴다.
/// </summary>
public static class AuthLinkEndpoints {
  /// <summary>매핑 관리 엔드포인트를 등록한다.</summary>
  public static void MapAuthLinkEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/api/auth-links").WithTags("AuthLinks").RequireAuthorization();

    /// <summary>등록된 매핑 목록을 반환한다.</summary>
    group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
      var links = await db.AuthUserLinks.AsNoTracking()
          .OrderBy(l => l.AuthUserId)
          .ToListAsync();

      var adminIds = links.Where(l => l.UserType == "admin").Select(l => l.HelpdeskUserId).ToList();
      var customerIds = links.Where(l => l.UserType == "customer").Select(l => l.HelpdeskUserId).ToList();

      var admins = await db.Admins.AsNoTracking()
          .Where(a => adminIds.Contains(a.Id))
          .ToDictionaryAsync(a => a.Id, a => a.UserName);
      var customers = await db.Customers.AsNoTracking()
          .Where(c => customerIds.Contains(c.Id))
          .ToDictionaryAsync(c => c.Id, c => c.UserName);

      return links.Select(l => new {
        l.Id,
        l.AuthUserId,
        l.UserType,
        l.HelpdeskUserId,
        l.CreatedAt,
        UserName = l.UserType == "admin"
            ? admins.GetValueOrDefault(l.HelpdeskUserId)
            : customers.GetValueOrDefault(l.HelpdeskUserId),
      }).ToList();
    }));

    // ClaimsPrincipal 로 받는다. HttpContext 를 유일한 매개변수로 쓰면 ASP.NET Core 가 핸들러를
    // RequestDelegate 로 간주해 반환한 IResult 를 버리고 빈 200 을 내보낸다.
    /// <summary>현재 토큰이 어떤 헬프데스크 계정으로 해석되는지 돌려준다. 연결 상태 점검용.</summary>
    // 연결이 없어도 200 으로 돌려준다.
    //
    // 전에는 uid 가 없으면 예외를 던졌다. 그러면 프론트는 "연결이 없다" 와 "서버가 죽었다" 를
    // 구분할 수 없고, 담당자 권한이 있는 계정조차 신원을 아예 받지 못해 화면이 통째로 잠겼다.
    // 이제 무엇을 할 수 있는지(isAdmin)와 무엇이 없는지(linked=false)를 함께 알려 준다.
    group.MapGet("/me", (ClaimsPrincipal principal, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      await Task.CompletedTask;

      var me = http.GetHelpdeskPrincipal();

      return new {
        linked = me.IsLinked,
        isAdmin = me.IsAdmin,
        // 담당자 권한이 연결이 아니라 포털 역할에서 왔는가. 화면 안내 문구가 이 값으로 갈린다.
        adminByRole = me.IsAdmin && !me.IsLinkedAdmin,
        helpdeskUserId = me.HelpdeskUserId,
        loginType = me.LinkedUserType,
        // 표시 이름은 JSini 계정을 우선한다. 헬프데스크 레코드의 이름은 참고용으로 함께 준다.
        userName = me.DisplayName,
        helpdeskUserName = principal.FindFirst("helpdesk_user_name")?.Value,
        companyId = me.CompanyId?.ToString(),
        jsiniUserId = me.JsiniUserId,
        jsiniUserName = me.DisplayName,
        jsiniEmail = me.Email,
        jsiniRoles = me.JsiniRoles,
      };
    }));

    /// <summary>매핑을 추가하거나 덮어쓴다.</summary>
    group.MapPost("/", (AppDbContext db, IFuneralAccountLinkService linkService, AuthLinkCreateDto dto, HttpContext http) =>
        ApiResponseBuilder.CreateAsync(async () => {
          var userType = dto.UserType?.ToLowerInvariant();
          if (userType is not ("admin" or "customer")) {
            throw new InvalidOperationException("userType 은 admin 또는 customer 여야 합니다.");
          }

          var exists = userType == "admin"
              ? await db.Admins.AnyAsync(a => a.Id == dto.HelpdeskUserId)
              : await db.Customers.AnyAsync(c => c.Id == dto.HelpdeskUserId);

          if (!exists) {
            throw new InvalidOperationException($"헬프데스크 {userType} 계정 #{dto.HelpdeskUserId} 을 찾을 수 없습니다.");
          }

          var link = await db.AuthUserLinks.FirstOrDefaultAsync(l => l.AuthUserId == dto.AuthUserId);
          if (link is null) {
            link = new AuthUserLink { AuthUserId = dto.AuthUserId };
            db.AuthUserLinks.Add(link);
          }

          link.UserType = userType;
          link.HelpdeskUserId = dto.HelpdeskUserId;
          link.CreatedAt = DateTime.UtcNow;
          link.CreatedBy = http.AuditUser();

          await db.SaveChangesAsync();
          linkService.InvalidateCache(dto.AuthUserId);

          return new { link.Id, link.AuthUserId, link.UserType, link.HelpdeskUserId };
        }, "계정 연결이 저장되었습니다.", 201));

    /// <summary>매핑을 제거한다.</summary>
    group.MapDelete("/{id:int}", (AppDbContext db, IFuneralAccountLinkService linkService, int id) =>
        ApiResponseBuilder.CreateAsync(async () => {
          var link = await db.AuthUserLinks.FirstOrDefaultAsync(l => l.Id == id);
          if (link is null) return null;

          db.AuthUserLinks.Remove(link);
          await db.SaveChangesAsync();
          linkService.InvalidateCache(link.AuthUserId);

          return new { link.Id };
        }, "계정 연결이 해제되었습니다."));
  }
}

/// <summary>계정 매핑 생성 요청.</summary>
/// <param name="AuthUserId">AuthServer 계정 식별자</param>
/// <param name="UserType">admin 또는 customer</param>
/// <param name="HelpdeskUserId">헬프데스크 내부 계정 ID</param>
public record AuthLinkCreateDto(string AuthUserId, string UserType, int HelpdeskUserId);
