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
    //
    // 이 테이블(jsini.userproperty)은 사용자 키가 **정수**(admin.id / customer.id)다.
    // 포털 로그인 아이디는 문자열이라 그대로 넣을 수 없어, 연결이 없는 계정은 저장할 자리가 없다.
    // 어디에 둘지는 결정이 필요한 사안이라(docs/analysis/19 Q11) 여기서는 정직하게 처리한다.
    //   조회 — 기본값(빈 설정)을 주고 linked=false 를 함께 알린다
    //   저장 — 조용히 버리지 않고 이유가 적힌 409 를 돌려준다
    group.MapGet("/", async (AppDbContext db, HttpContext http) => {
      var (userId, userType) = GetUserInfo(http);
      if (userId == null) {
        return Results.Ok(new { ok = true, data = new Dictionary<string, string>(), linked = false });
      }

      var properties = await db.UserProperties
          .Where(p => p.UserId == userId && p.UserType == userType)
          .ToListAsync();

      // 결과를 { "key1": "value1", "key2": "value2" } 형태의 객체로 변환
      var result = properties.ToDictionary(p => p.Key, p => p.Value);

      return Results.Ok(new { ok = true, data = result, linked = true });
    });

    // 현재 로그인한 사용자의 속성을 업데이트(Upsert)합니다.
    group.MapPut("/", async (AppDbContext db, HttpContext http, [FromBody] Dictionary<string, string> newProperties) => {
      var (userId, userType) = GetUserInfo(http);
      if (userId == null) {
        return Results.Json(new {
          ok = false,
          message = "이 포털 계정에 연결된 헬프데스크 사용자가 없어 개인 설정을 저장할 수 없습니다. "
                  + "헬프데스크 설정 › 계정 연결에서 이어 주세요."
        }, statusCode: StatusCodes.Status409Conflict);
      }

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