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

    group.MapPost("/", (AppDbContext db, AdminCreateDto adminDto) => ApiResponseBuilder.CreateAsync(async () => {
      var passwordService = new PasswordService();
      var tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
      var admin = new Admin {
        LoginId = adminDto.LoginId,
        UserName = adminDto.UserName,
        Email = adminDto.Email,
        CreatedBy = adminDto.CreatedBy ?? "system",
        MenuContext = adminDto.MenuContext,
        MustChangePassword = true
      };
      admin.PasswordHash = passwordService.HashPassword<Admin>(admin, tempPassword);

      if (adminDto.TeamIds != null) {
        foreach (var teamId in adminDto.TeamIds) {
          admin.AdminTeams.Add(new AdminTeam { TeamId = teamId });
        }
      }

      db.Admins.Add(admin);
      await db.SaveChangesAsync();
      return new { admin, tempPassword };
    }, "Admin created successfully.", 201));

    group.MapPost("/change-password", async (HttpContext http, AppDbContext db, AdminChangePasswordDto changePasswordDto) => {
      var passwordService = new PasswordService();

      /*
                          new Claim(JwtRegisteredClaimNames.Sub, login_id),
                          new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                          new Claim("uid", user_uid),
                          new Claim("login_type", login_type)
                          */

      var loginId = http.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
      var uid = http.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
      var loginType = http.User.Claims.FirstOrDefault(c => c.Type == "login_type")?.Value;



      Console.WriteLine($"change-password uid : {uid}");
      Console.WriteLine($"change-password loginId : {loginId}");
      Console.WriteLine($"change-password loginType : {loginType}");


      Console.WriteLine($"change-password changePasswordDto.OldPassword : {changePasswordDto.OldPassword}");
      Console.WriteLine($"change-password changePasswordDto.NewPassword : {changePasswordDto.NewPassword}");



      if (loginId == null) {
        return Results.Unauthorized();
      }

      if (loginType == "admin") {
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id + "" == uid);
        if (admin == null) {
          return Results.NotFound("Admin not found.");
        }

        if (!passwordService.VerifyPassword(admin, changePasswordDto.OldPassword)) {
          return Results.BadRequest("Invalid old password.");
        }

        admin.PasswordHash = passwordService.HashPassword<Admin>(admin, changePasswordDto.NewPassword);
        admin.MustChangePassword = false;
        await db.SaveChangesAsync();

      }
      else {


        var customer = await db.Customers.FirstOrDefaultAsync(a => a.Id + "" == uid);
        if (customer == null) {
          return Results.NotFound("Customer not found.");
        }

        if (!passwordService.VerifyPassword(customer, changePasswordDto.OldPassword)) {
          return Results.BadRequest("Invalid old password.");
        }

        customer.PasswordHash = passwordService.HashPassword<Customer>(customer, changePasswordDto.NewPassword);
        //customer.MustChangePassword = false;
        await db.SaveChangesAsync();

      }
      return Results.Ok("Password changed successfully.");
    }).RequireAuthorization();

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

    group.MapPost("/find-password", (AppDbContext db, FindPasswordDto findPasswordDto , IRabbitMqConnectionProvider provider, ILoggerFactory loggerFactory, IConfiguration configuration, IPushSubscriptionStore store, IWebPushService sender  ) => ApiResponseBuilder.CreateAsync(async () => {
      var customer = await db.Customers.FirstOrDefaultAsync(a => a.LoginId == findPasswordDto.LoginId && a.Email == findPasswordDto.Email);
      var admin = await db.Admins.FirstOrDefaultAsync(a => a.LoginId == findPasswordDto.LoginId && a.Email == findPasswordDto.Email);


        var tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

      if (customer is null) {
        if (admin is null) return null;

        var passwordService = new PasswordService();
        admin.PasswordHash = passwordService.HashPassword<Admin>(admin, tempPassword);
        admin.MustChangePassword = true;

        await db.SaveChangesAsync();

      }
      else {

        var passwordService = new PasswordService();
        customer.PasswordHash = passwordService.HashPassword<Customer>(customer, tempPassword);
        //customer.MustChangePassword = true;

        await db.SaveChangesAsync();
      }


        await EMailUtil.SendEmailJinNets(findPasswordDto.Email, "임시 비밀번호 발급", $"임시 비밀번호: {tempPassword}\n로그인 후 비밀번호를 변경해주세요.", provider, loggerFactory, configuration);

        return new { tempPassword = "이메일로 발송되었습니다." };


    }, "Password reset successfully."));
  }
}
