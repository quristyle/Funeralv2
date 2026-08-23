using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HelpDeskServer.Dtos;
using HelpDeskServer.Utilities;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 관리자 관련 엔드포인트
/// </summary>
public static class AdminEndpoints {
  //public record AdminCreateDto([Required] string LoginId, [Required] string UserName, [Required] string Email, [Required] string Password, int TeamId, string? CreatedBy, string? MenuContext);

  /// <summary>
  /// 관리자 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapAdminEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/admins");

    group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Admins.Include(a => a.AdminTeams).ThenInclude(at => at.Team).ToListAsync()
    ));

    group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
        () => db.Admins.Include(a => a.AdminTeams).ThenInclude(at => at.Team).FirstOrDefaultAsync(a => a.Id == id)
    ));

    group.MapPost("/", (AppDbContext db, AdminCreateDto adminDto, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      // 담당자 등록은 '조직 데이터' 등록이지 계정 발급이 아니다.
      // 로그인 계정은 JSini 포털에서 만든다. 여기 비밀번호 칸은 채워야 하는 필수 컬럼이라
      // 아무도 모르는 임의값으로 채운다 — 이 값으로는 로그인할 수 없다.
      var passwordService = new PasswordService();
      var unusablePassword = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
      var admin = new Admin {
        LoginId = adminDto.LoginId,
        UserName = adminDto.UserName,
        Email = adminDto.Email,
        // 등록자는 로그인한 JSini 계정에서 정한다(요청 본문 값은 쓰지 않는다).
        CreatedBy = http.AuditUser(),
        MenuContext = adminDto.MenuContext,
        MustChangePassword = false
      };
      admin.PasswordHash = passwordService.HashPassword<Admin>(admin, unusablePassword);

      if (adminDto.TeamIds != null) {
        foreach (var teamId in adminDto.TeamIds) {
          admin.AdminTeams.Add(new AdminTeam { TeamId = teamId });
        }
      }

      db.Admins.Add(admin);
      await db.SaveChangesAsync();
      return new { admin };
    }, "Admin created successfully.", 201));

    // 비밀번호 변경(/change-password) 엔드포인트는 제거했다 (결정 Q4).
    //
    // 계정과 비밀번호는 JSini 관리 포털(AuthServer)이 단독으로 관리한다.
    // 헬프데스크 자체 로그인은 꺼져 있으므로(LocalLogin:Enabled, 기본 false)
    // 여기 저장된 비밀번호는 어디에서도 쓰이지 않는다.
    // 비밀번호 변경은 포털의 개인 설정 화면에서 한다.

    group.MapPut("/{id}", (AppDbContext db, int id, Admin input) => ApiResponseBuilder.CreateAsync(async () => {
      var admin = await db.Admins.Include(a => a.AdminTeams).FirstOrDefaultAsync(a => a.Id == id);
      if (admin is null) return null;

      admin.UserName = input.UserName;
      admin.Email = input.Email;
      admin.Photo = input.Photo;

      // Update teams: simple approach, remove existing and add new
      db.AdminTeams.RemoveRange(admin.AdminTeams);
      if (input.AdminTeams != null) {
        foreach (var at in input.AdminTeams) {
          admin.AdminTeams.Add(new AdminTeam { AdminId = id, TeamId = at.TeamId });
        }
      }

      admin.Photo = await FileUtil.SaveImageFromBase64("users/adm_user_" + id, input.Photo);

      await db.SaveChangesAsync();
      return admin;
    }, "Admin updated successfully."));

    group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var admin = await db.Admins.FindAsync(id);
      if (admin is null) return null;

      // Soft delete: set IsDeleted to true instead of removing from DB
      admin.IsDeleted = true;
      await db.SaveChangesAsync();
      return new { DeletedId = id };
    }, "Admin deleted successfully."));

    // 비밀번호 찾기(/find-password) 엔드포인트는 제거했다 (결정 D9-B).
    //
    // 인증 없이 loginId + email 만으로 그 계정의 비밀번호를 임의값으로 바꿔 버리는 동작이었다.
    // 임시 비밀번호는 등록된 메일로만 가므로 탈취까지는 어렵지만,
    // 피해자의 기존 비밀번호가 이미 바뀌어 있어 로그인이 막힌다(계정 잠금).
    // loginId·email 은 추측하기 쉬운 값이라 진입 장벽이 낮았다.
    //
    // 계정과 인증은 JSini 관리 포털이 일원 관리한다. 비밀번호 재설정도 포털에서 다룬다.
  }
}
