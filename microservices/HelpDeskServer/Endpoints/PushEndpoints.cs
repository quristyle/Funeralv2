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

    // 구독을 추가합니다.
    //
    // 구독은 헬프데스크 내부 ID 로 저장된다(알림 발송이 그 ID 로 대상을 찾는다).
    // 그래서 연결이 없으면 구독할 수 없다 — 그 사실을 401 이 아니라 이유가 적힌 409 로 알린다.
    // 401 은 프론트의 인터셉터가 '토큰 만료' 로 보고 로그아웃시킬 수 있어 증상이 엉뚱해진다.
    group.MapPost("/subscribe", (PushSubscriptionDto dto, IPushSubscriptionStore store, HttpContext http) => {
      var me = http.GetHelpdeskPrincipal();

      if (!me.IsLinked || string.IsNullOrEmpty(me.LinkedUserType)) {
        return Results.Json(new {
          ok = false,
          message = "이 포털 계정에 연결된 헬프데스크 사용자가 없어 알림을 구독할 수 없습니다. "
                  + "헬프데스크 설정 › 계정 연결에서 이어 주세요."
        }, statusCode: StatusCodes.Status409Conflict);
      }

      store.Add(dto, me.HelpdeskUserId!.Value, me.LinkedUserType);

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
          var me = http.GetHelpdeskPrincipal();

          // 담당자는 다른 사용자를 지정해서 조회할 수 있다. 이 경우 자기 연결은 필요 없다.
          int? targetUserId = me.IsAdmin && userId.HasValue ? userId.Value : me.HelpdeskUserId;

          // 대상을 특정할 수 없다. 연결이 없는 계정은 받은 알림도 없으므로 빈 목록이 맞다.
          // (401 로 돌려주면 프론트 인터셉터가 토큰 만료로 보고 로그아웃시킨다)
          if (!targetUserId.HasValue) {
            return Results.Ok(new List<NotificationDto>());
          }

          var recipientId = targetUserId.Value;
          var query = db.PushMessageRecipients
              .AsNoTracking()
              .Include(r => r.PushMessage)
              .Where(r => r.UserId == recipientId);

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
      var me = http.GetHelpdeskPrincipal();

      // 연결이 없으면 받은 알림도 없다. 빈 목록으로 돌려준다(위 /notifications 주석 참고).
      if (!me.IsLinked) {
        return Results.Ok(new { ok = true, data = Array.Empty<object>() });
      }

      var userId = me.HelpdeskUserId!.Value;
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
