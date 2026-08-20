using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using HelpDeskServer.Models;
using HelpDeskServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// Web Push 관련 엔드포인트
/// </summary>
public static class PushEndpoints {
  /// <summary>
  /// Web Push 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapPushEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/api/push");

    //구독을 추가합니다.
    group.MapPost("/subscribe", (PushSubscriptionDto dto, IPushSubscriptionStore store, HttpContext http) => {
      var uidClaim = http.User.FindFirst("uid");
      var loginTypeClaim = http.User.FindFirst("login_type");

      if (uidClaim is null || loginTypeClaim is null || !int.TryParse(uidClaim.Value, out var userId)) {
        return Results.Unauthorized();
      }

      var userType = loginTypeClaim.Value; // "admin" or "customer"

      Console.WriteLine($"/api/push/subscribe for {userType} ID: {userId}");
      store.Add(dto, userId, userType);

      return Results.Created("/api/push/subscribe", new { ok = true });

    }).RequireAuthorization();

    //구독을 취소합니다.
    group.MapPost("/unsubscribe", async (PushSubscriptionDto dto, IPushSubscriptionStore store) => {
      Console.WriteLine("/api/push/unsubscribe");
      if (string.IsNullOrWhiteSpace(dto.Endpoint)) {
        return Results.BadRequest("Endpoint is required.");
      }
      await store.RemoveByEndpointAsync(dto.Endpoint);
      return Results.Ok(new { ok = true });
    });

    //모든 사용자에게 푸시 알림을 보냅니다.
    group.MapPost("/notify", async (PushMessageDto message, IPushSubscriptionStore store, IWebPushService sender, CancellationToken ct) => {
      Console.WriteLine("/api/push/notify");
      var subs = await store.GetAllAsync();
      var sent = await sender.BroadcastAsync(subs, message, ct);
      return Results.Ok(new { sent, total = subs.Count });
    });

    // 관리자 그룹에게만 푸시 알림을 보냅니다.
    group.MapPost("/notify-admins", async (PushMessageDto message, IPushSubscriptionStore store, IWebPushService sender, CancellationToken ct) => {
      Console.WriteLine("/api/push/notify-admins");
      var adminSubs = await store.GetAdminSubscriptionsAsync();
      var sent = await sender.BroadcastAsync(adminSubs, message, ct);
      return Results.Ok(new { sent, total = adminSubs.Count });
    })
    .RequireAuthorization(); // 인증된 사용자만 호출 가능

    // 특정 팀에 속한 관리자 그룹에게만 푸시 알림을 보냅니다.
    group.MapPost("/notify-team/{teamId:int}", async (int teamId, PushMessageDto message, IPushSubscriptionStore store, IWebPushService sender, CancellationToken ct) => {
      Console.WriteLine($"/api/push/notify-team/{teamId}");
      var teamSubs = await store.GetSubscriptionsByTeamAsync(teamId);
      var sent = await sender.BroadcastAsync(teamSubs, message, ct);
      return Results.Ok(new { sent, total = teamSubs.Count });
    })
    .RequireAuthorization(); // 인증된 사용자만 호출 가능

    // 특정 고객사(Company)의 모든 사용자에게 푸시 알림을 보냅니다.
    group.MapPost("/notify-company/{companyId:int}", async (int companyId, PushMessageDto message, IPushSubscriptionStore store, IWebPushService sender, CancellationToken ct) => {
      Console.WriteLine($"/api/push/notify-company/{companyId}");
      var companySubs = await store.GetSubscriptionsByCompanyAsync(companyId);
      var sent = await sender.BroadcastAsync(companySubs, message, ct);
      return Results.Ok(new { sent, total = companySubs.Count });
    })
    .RequireAuthorization(); // 인증된 사용자만 호출 가능

    // 특정 사용자 한 명에게만 푸시 알림을 보냅니다.
    group.MapPost("/notify-user/{userType}/{userId:int}", async (string userType, int userId, PushMessageDto message, IPushSubscriptionStore store, IWebPushService sender, CancellationToken ct) => {
      Console.WriteLine($"/api/push/notify-user/{userType}/{userId}");
      var userSubs = await store.GetSubscriptionsByUserAsync(userId, userType);
      var sent = await sender.BroadcastAsync(userSubs, message, ct);
      return Results.Ok(new { sent, total = userSubs.Count });
    })
    .RequireAuthorization(); // 인증된 사용자만 호출 가능

    // 현재 브라우저의 구독 정보가 서버에 등록되어 있는지 확인합니다.
    group.MapGet("/is-subscribed", async (IPushSubscriptionStore store, [FromQuery] string endpoint) => {
      if (string.IsNullOrWhiteSpace(endpoint)) {
        return Results.BadRequest(new { isSubscribed = false, message = "Endpoint is required." });
      }

      var subscribed = await store.IsSubscribedAsync(endpoint);
      return Results.Ok(new { isSubscribed = subscribed });
    });

    // 현재 로그인한 사용자의 알림 목록을 기간별로 조회합니다. 관리자는 특정 사용자를 지정하여 조회할 수 있습니다.
    group.MapGet("/notifications", async (
        AppDbContext db,
        HttpContext http,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] bool? isRead,
        [FromQuery] int? userId) => {
          var uidClaim = http.User.FindFirst("uid");
          var loginTypeClaim = http.User.FindFirst("login_type");

          if (uidClaim is null || !int.TryParse(uidClaim.Value, out var currentUserId)) {
            return Results.Unauthorized();
          }

          int targetUserId = currentUserId;
          // 관리자이고, 다른 사용자 ID를 조회하려는 경우 대상 ID를 변경
          if (loginTypeClaim?.Value == "admin" && userId.HasValue) {
            targetUserId = userId.Value;
          }

          var query = db.PushMessageRecipients
              .AsNoTracking()
              .Include(r => r.PushMessage)
              .Where(r => r.UserId == targetUserId);

          if (startDate.HasValue) {
            query = query.Where(r => r.CreatedAt >= startDate.Value.ToUniversalTime());
          }

          if (endDate.HasValue) {
            var inclusiveEndDate = endDate.Value.ToUniversalTime().Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.CreatedAt <= inclusiveEndDate);
          }

          if (isRead.HasValue) {
            query = query.Where(r => r.IsRead == isRead.Value);
          }

          var notifications = await query
              .OrderByDescending(r => r.CreatedAt)
              .Select(r => new NotificationDto {
                Id = r.Id,
                Message = r.PushMessage.Body,
                ReceivedAt = r.CreatedAt,
                IsRead = r.IsRead,
                Url = r.PushMessage.Url,
                Endpoint = r.Endpoint // Endpoint 필드 추가
              })
              .ToListAsync();

          return Results.Ok(notifications);
        }).RequireAuthorization();

    // 현재 로그인한 사용자의 모든 알림 목록을 조회합니다.
    group.MapGet("/my-notifications", async (IPushSubscriptionStore store, AppDbContext db, HttpContext http) => {
      var uidClaim = http.User.FindFirst("uid");
      if (uidClaim is null || !int.TryParse(uidClaim.Value, out var userId)) {
        return Results.Unauthorized();
      }

      var notifications = await db.PushMessageRecipients
          .Include(r => r.PushMessage)
          .Where(r => r.UserId == userId)
          .OrderByDescending(r => r.CreatedAt)
          .Select(r => new {
            r.Id,
            r.PushMessage.Title,
            r.PushMessage.Body,
            r.PushMessage.Url,
            r.IsRead,
            r.ReadAt,
            r.CreatedAt
          })
          .ToListAsync();

      return Results.Ok(new { ok = true, data = notifications });
    }).RequireAuthorization();

    // 특정 알림을 '읽음'으로 표시합니다.
    group.MapPost("/notifications/{id:int}/read", async (int id, AppDbContext db, HttpContext http) => {
      var uidClaim = http.User.FindFirst("uid");
      // 서비스 워커에서 호출하는 경우 인증 정보가 없을 수 있습니다.
      // 인증 정보가 있는 경우에만 권한을 확인합니다.

      /*
      if (uidClaim is not null && int.TryParse(uidClaim.Value, out var userId))
      {
          var recipient = await db.PushMessageRecipients.FindAsync(id);
          if (recipient is null) return Results.NotFound();

          // 자신의 알림이 아닌 경우 접근을 거부합니다.
          if (recipient.UserId != userId)
          {
              return Results.Forbid();
          }
      }
      */

      // 대상 알림을 찾아 '읽음'으로 표시합니다.
      await db.PushMessageRecipients.Where(r => r.Id == id)
          .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsRead, true).SetProperty(b => b.ReadAt, DateTime.Now));

      await db.SaveChangesAsync();

      return Results.Ok(new { ok = true });
    }).RequireAuthorization();

    // 특정 알림이 클라이언트에 '수신됨'을 표시합니다. (서비스 워커에서 호출)
    group.MapPost("/notifications/{id:int}/delivered", async (int id, AppDbContext db) => {
      var recipient = await db.PushMessageRecipients.FindAsync(id);
      if (recipient is null) {
        return Results.NotFound(new { ok = false, message = "Recipient not found." });
      }

      recipient.IsDelivered = true;
      recipient.DeliveredAt = DateTime.UtcNow;
      await db.SaveChangesAsync();

      return Results.Ok(new { ok = true });
    });
  }
}
