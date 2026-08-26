using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Options;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 알림 엔드포인트 (<c>/api/notification/*</c>)
/// </summary>
/// <remarks>
/// 이 서비스는 **보내는 일만** 한다 (결정 D8-A).
///
/// <para>
/// 누구에게 보낼지는 부르는 쪽이 정한다. 헬프데스크가 "이 팀의 관리자" 를 알고 싶으면
/// 자기 DB 에서 골라 주인 키 목록을 넘긴다 — 이 서비스는 팀도 회사도 모른다.
/// 그래야 포털·장례식장도 같은 서비스를 쓸 수 있다.
/// </para>
///
/// <para>
/// <b>알림 목록(읽음·전달 표시)은 여기 없다.</b> 그것은 헬프데스크의 화면 기능이고
/// 헬프데스크 테이블을 읽는다. 옮기면 도메인 로직이 따라와야 해서 남겨 두었다.
/// </para>
/// </remarks>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");

        // ── 화면이 구독을 만들 때 필요한 공개 키 ─────────────
        //
        // 공개 키는 비밀이 아니다. 브라우저가 구독을 만들 때 쓰는 값이라 내려가야 한다.
        // enabled 가 거짓이면 화면이 구독 버튼을 숨기면 된다.
        group.MapGet("/vapid-public-key", ([FromServices] IOptions<VapidOptions> vapid) =>
        {
            var v = vapid.Value;
            return Results.Ok(ApiResponse<VapidPublicKeyDto>.Ok(new VapidPublicKeyDto
            {
                PublicKey = v.PublicKey,
                Enabled = v.IsConfigured
            }));
        })
        .WithName("GetVapidPublicKey")
        .WithOpenApi();

        // ── 구독 등록 ───────────────────────────────────────
        //
        // 같은 브라우저가 다시 구독하면 같은 endpoint 가 온다. 새로 만들지 않고 갱신한다 —
        // 새로 만들면 같은 기기에 여러 번 보내게 된다.
        group.MapPost("/subscriptions", async (
            [FromBody] SubscribeDto request,
            UserContext? user,
            HttpContext http,
            [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Endpoint) ||
                string.IsNullOrWhiteSpace(request.P256dh) ||
                string.IsNullOrWhiteSpace(request.Auth))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "구독 정보(endpoint·p256dh·auth)가 온전하지 않습니다.",
                    code: "INVALID_SUBSCRIPTION"));
            }

            // 주인을 지정하지 않으면 로그인한 계정으로 본다.
            //
            // **남의 이름으로 구독을 만들지 못하게 한다.** 다른 주인을 지정하는 것은
            // 헬프데스크처럼 자기 신원 체계를 쓰는 서비스가 서버 대 서버로 부를 때만
            // 필요한데, 지금은 그 경로가 없으므로 막아 둔다.
            var ownerType = string.IsNullOrWhiteSpace(request.OwnerType) ? "jsini" : request.OwnerType;
            var ownerKey = string.IsNullOrWhiteSpace(request.OwnerKey) ? user.UserId : request.OwnerKey;

            if (ownerType != "jsini" || ownerKey != user.UserId)
            {
                return Results.Json(ApiResponse<bool>.Fail(
                    message: "다른 사람 이름으로 구독을 만들 수 없습니다.",
                    code: "FORBIDDEN"), statusCode: StatusCodes.Status403Forbidden);
            }

            var existing = await db.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);

            if (existing is null)
            {
                db.PushSubscriptions.Add(new Entities.PushSubscription
                {
                    Endpoint = request.Endpoint,
                    P256dh = request.P256dh,
                    Auth = request.Auth,
                    OwnerType = ownerType,
                    OwnerKey = ownerKey,
                    Source = request.Source,
                    UserAgent = http.Request.Headers.UserAgent.ToString()
                });
            }
            else
            {
                // 키가 갱신될 수 있고, 같은 브라우저를 다른 계정이 쓸 수도 있다.
                existing.P256dh = request.P256dh;
                existing.Auth = request.Auth;
                existing.OwnerType = ownerType;
                existing.OwnerKey = ownerKey;
                existing.Source = request.Source ?? existing.Source;
                existing.UserAgent = http.Request.Headers.UserAgent.ToString();
                existing.FailureCount = 0;
            }

            await db.SaveChangesAsync();
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("Subscribe")
        .WithOpenApi();

        // ── 구독 해제 ───────────────────────────────────────
        group.MapDelete("/subscriptions", async (
            [FromQuery] string endpoint,
            UserContext? user,
            [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "endpoint 가 필요합니다.", code: "INVALID"));
            }

            // 자기 구독만 지울 수 있다. endpoint 만 알면 남의 구독을 끊을 수 있으면 안 된다.
            var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s =>
                s.Endpoint == endpoint && s.OwnerType == "jsini" && s.OwnerKey == user.UserId);

            if (sub is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "구독을 찾을 수 없습니다.", code: "NOT_FOUND"));
            }

            db.PushSubscriptions.Remove(sub);
            await db.SaveChangesAsync();
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("Unsubscribe")
        .WithOpenApi();

        // ── 내 구독 확인 ────────────────────────────────────
        //
        // 화면이 "이 브라우저가 이미 구독 중인가" 를 알아야 버튼 상태를 정할 수 있다.
        group.MapGet("/subscriptions/me", async (
            UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await db.PushSubscriptions
                .Where(s => s.OwnerType == "jsini" && s.OwnerKey == user.UserId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Endpoint,
                    s.Source,
                    s.UserAgent,
                    s.LastSentAt,
                    s.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(ApiResponse<object>.Ok(new { items = list, count = list.Count }));
        })
        .WithName("GetMySubscriptions")
        .WithOpenApi();

        // ── 푸시 발송 ───────────────────────────────────────
        group.MapPost("/push", async (
            [FromBody] SendPushDto request,
            UserContext? user,
            [FromServices] IPushSender sender,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Message?.Title))
            {
                return Results.BadRequest(ApiResponse<SendPushResultDto>.Fail(
                    message: "알림 제목이 필요합니다.", code: "INVALID"));
            }

            var result = await sender.SendAsync(request, ct);

            // 보낸 것이 하나도 없으면 성공으로 말하지 않는다. 이유는 result.Message 에 있다.
            return result.Sent > 0
                ? Results.Ok(ApiResponse<SendPushResultDto>.Ok(result))
                : Results.Json(
                    ApiResponse<SendPushResultDto>.Ok(result, result.Message ?? "보낸 알림이 없습니다."),
                    statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("SendPush")
        .WithOpenApi();

        // ── 이메일 발송 ─────────────────────────────────────
        //
        // 큐에 넣는 것까지가 이 서비스의 일이다. 실제 발송은 배포 장비의 스크립트가 한다.
        group.MapPost("/email", async (
            [FromBody] SendEmailDto request,
            UserContext? user,
            [FromServices] IEmailQueueSender sender,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await sender.SendAsync(request, ct);

            return result.Queued
                ? Results.Ok(ApiResponse<SendEmailResultDto>.Ok(result))
                : Results.BadRequest(ApiResponse<SendEmailResultDto>.Fail(
                    message: result.Message ?? "메일 발송 요청에 실패했습니다.",
                    code: "EMAIL_QUEUE_FAILED"));
        })
        .WithName("SendEmail")
        .WithOpenApi();
    }
}
