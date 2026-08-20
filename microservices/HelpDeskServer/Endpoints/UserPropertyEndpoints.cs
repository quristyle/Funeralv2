using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 사용자 속성 관련 엔드포인트
/// </summary>
public static class UserPropertyEndpoints {
  /// <summary>
  /// 사용자 속성 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapUserPropertyEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/api/user-properties");//.RequireAuthorization();

    // 현재 로그인한 사용자의 모든 속성을 조회합니다.
    group.MapGet("/", async (AppDbContext db, HttpContext http) => {
      var (userId, userType) = GetUserInfo(http);
      if (userId == null) return Results.Unauthorized();

      var properties = await db.UserProperties
          .Where(p => p.UserId == userId && p.UserType == userType)
          .ToListAsync();

      // 결과를 { "key1": "value1", "key2": "value2" } 형태의 객체로 변환
      var result = properties.ToDictionary(p => p.Key, p => p.Value);

      return Results.Ok(new { ok = true, data = result });
    });

    // 현재 로그인한 사용자의 속성을 업데이트(Upsert)합니다.
    group.MapPut("/", async (AppDbContext db, HttpContext http, [FromBody] Dictionary<string, string> newProperties) => {
      var (userId, userType) = GetUserInfo(http);
      if (userId == null) return Results.Unauthorized();

      var existingProperties = await db.UserProperties
          .Where(p => p.UserId == userId && p.UserType == userType && newProperties.Keys.Contains(p.Key))
          .ToListAsync();

      foreach (var kvp in newProperties) {
        var existing = existingProperties.FirstOrDefault(p => p.Key == kvp.Key);
        if (existing != null) {
          // 속성이 존재하면 값 업데이트
          existing.Value = kvp.Value;
        }
        else {
          // 속성이 없으면 새로 추가
          db.UserProperties.Add(new UserProperty {
            UserId = userId.Value,
            UserType = userType!,
            Key = kvp.Key,
            Value = kvp.Value
          });
        }
      }

      await db.SaveChangesAsync();
      return Results.Ok(new { ok = true, message = "Properties updated successfully." });
    });
  }

  private static (int? UserId, string? UserType) GetUserInfo(HttpContext http) {
    var uidClaim = http.User.FindFirst("uid");
    var loginTypeClaim = http.User.FindFirst("login_type");

    if (uidClaim is null || loginTypeClaim is null || !int.TryParse(uidClaim.Value, out var userId)) {
      return (null, null);
    }
    return (userId, loginTypeClaim.Value);
  }
}