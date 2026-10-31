using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Options;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>알림 엔드포인트(/api/notification/*). 보내는 일만 한다. 알림 목록(읽음·전달)은 헬프데스크 기능이라 여기 없다.</summary>
public static class NotificationEndpoints
{
    /// <summary>포털 계정의 주인 종류. 게이트웨이가 주는 <c>X-User-Id</c> 가 곧 주인 키다.</summary>
    private const string OwnerTypePortal = "jsini";

    private const string ChannelPush = Entities.PushSendLog.ChannelPush;
    private const string ChannelEmail = Entities.PushSendLog.ChannelEmail;

    /// <summary>이 사람에게 온 줄.</summary>
    private const string DirectionReceived = "received";

    /// <summary>이 사람이 보낸 줄.</summary>
    private const string DirectionSent = "sent";

    /// <summary>계정 하나의 앱 현황 화면 주소(상대 경로). 구독 알림이 이 주소를 달고 간다.
    /// 로그인 아이디는 EscapeDataString 으로 감싼다(/ 나 ? 가 들어가면 경로가 어긋난다).</summary>
    private static string AccountAppUrl(string loginId) =>
        $"/admin/system/account/{Uri.EscapeDataString(loginId)}/app";

    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");

        // 공개 키는 비밀이 아니다. enabled 가 거짓이면 화면이 구독 버튼을 숨긴다.
        group.MapGet("/vapid-public-key", ([FromServices] IOptions<VapidOptions> vapid) =>
        {
            var v = vapid.Value;
            return Results.Ok(ApiResponse<VapidPublicKeyDto>.Ok(new VapidPublicKeyDto
            {
                PublicKey = v.PublicKey,
                Enabled = v.IsConfigured
            }));
        })
        .WithName("GetVapidPublicKey");

        // ── 구독 등록 ───────────────────────────────────────
        // 같은 브라우저는 같은 endpoint 가 오므로 새로 만들지 않고 갱신한다(새로 만들면 같은 기기에 여러 번 간다).
        group.MapPost("/subscriptions", async (
            [FromBody] SubscribeDto request,
            UserContext? user,
            HttpContext http,
            [FromServices] AppDbContext db,
            [FromServices] IPushSender pushSender) =>
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

            // 주인을 적지 않으면 로그인 계정으로 본다. 남의 이름으로 구독을 만들지 못하게 막는다.
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

            bool isNew = existing is null;

            if (existing is null)
            {
                var created = new Entities.PushSubscription
                {
                    Endpoint = request.Endpoint,
                    P256dh = request.P256dh,
                    Auth = request.Auth,
                    OwnerType = ownerType,
                    OwnerKey = ownerKey,
                    Source = request.Source,
                    UserAgent = http.Request.Headers.UserAgent.ToString()
                };
                ApplyMetadata(created, request.Metadata);
                db.PushSubscriptions.Add(created);
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
                ApplyMetadata(existing, request.Metadata);
            }

            await db.SaveChangesAsync();

            if (isNew)
            {
                var superAdmins = await (
                    from ra in db.RoleAccounts
                    where ra.RoleId == "SYSTEM_ADMINISTRATOR" && !ra.IsDeleted
                    join a in db.Accounts on ra.AccountId equals a.Id
                    where !a.IsDeleted && a.UserId != user.UserId
                    select a.UserId
                ).ToListAsync();

                if (superAdmins.Count > 0)
                {
                    var pushDto = new SendPushDto
                    {
                        Owners = superAdmins.Select(id => new OwnerRefDto { OwnerType = "jsini", OwnerKey = id }).ToList(),
                        Message = new PushMessageDto
                        {
                            Title = "새로운 알림 구독",
                            Body = $"{user.UserId} 님이 새 기기에서 알림을 구독했습니다.",
                            // 누르면 그 사람의 [앱 현황]으로 간다.
                            Url = AccountAppUrl(user.UserId),
                            // 새 기기를 등록한 사람의 프로필 사진을 알림 아이콘으로 쓴다.
                            IconOwnerKey = user.UserId,
                            // 하루 뒤에 배달되면 알림창만 채우므로 수명을 짧게 준다.
                            TtlSeconds = 3600,
                            Category = PushCategories.Subscription,
                        }
                    };
                    
                    try
                    {
                        await pushSender.SendAsync(pushDto, "system");
                    }
                    catch
                    {
                        // 알림 발송 실패가 구독 저장을 깨뜨리지 않도록 삼킨다.
                    }
                }
            }

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("Subscribe");

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
        .WithName("Unsubscribe");

        // ── 내 구독 확인 ────────────────────────────────────
        // 화면이 이 브라우저의 구독 여부로 버튼 상태를 정한다.
        group.MapGet("/subscriptions/me", async (
            UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await DevicesAsync(db, OwnerTypePortal, user.UserId);
            return Results.Ok(ApiResponse<object>.Ok(new { items = list, count = list.Count }));
        })
        .WithName("GetMySubscriptions");

        // ── 여러 사람의 알림 상태 (관리 화면) ───────────────
        // 주인 종류 하나로 그 종류 전부를 낸다(키를 인자로 받으면 URL 이 길어진다).
        // 기기 목록까지 주는 /preferences/me 와 달리 기기는 수로 줄인다.
        group.MapGet("/preferences", async (
            UserContext? user,
            [FromQuery] string? ownerType,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var type = string.IsNullOrWhiteSpace(ownerType) ? "jsini" : ownerType;

            // 행이 없는 사람은 나오지 않는다. saved 로 기본값과 가른다.
            var prefs = await db.NotificationPreferences
                .AsNoTracking()
                .Where(p => p.OwnerType == type)
                .Select(p => new
                {
                    p.OwnerKey,
                    p.PushEnabled,
                    p.EmailEnabled,
                    p.WeatherEnabled,
                    p.NoteEmailEnabled,
                    p.CommentPushEnabled,
                    p.CommentEmailEnabled,
                    p.UpdatedAt,
                })
                .ToListAsync(ct);

            // 기기 수는 따로 센다(구독과 설정은 서로 없어도 되는 표라 조인하면 한쪽이 빠진다).
            var devices = await db.PushSubscriptions
                .AsNoTracking()
                .Where(s => s.OwnerType == type)
                .GroupBy(s => s.OwnerKey)
                .Select(g => new
                {
                    OwnerKey = g.Key,
                    Count = g.Count(),
                    LastSentAt = g.Max(x => x.LastSentAt),
                })
                .ToListAsync(ct);

            var byOwner = new Dictionary<string, OwnerNotificationStateDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in prefs)
            {
                byOwner[p.OwnerKey] = new OwnerNotificationStateDto
                {
                    OwnerKey = p.OwnerKey,
                    PushEnabled = p.PushEnabled,
                    EmailEnabled = p.EmailEnabled,
                    WeatherEnabled = p.WeatherEnabled,
                    NoteEmailEnabled = p.NoteEmailEnabled,
                    CommentPushEnabled = p.CommentPushEnabled,
                    CommentEmailEnabled = p.CommentEmailEnabled,
                    Saved = true,
                    UpdatedAt = p.UpdatedAt,
                };
            }

            foreach (var d in devices)
            {
                // 기기는 있는데 설정을 한 번도 저장하지 않은 사람 — 기본값과 기기 수로 줄을 만든다.
                if (!byOwner.TryGetValue(d.OwnerKey, out var state))
                {
                    state = new OwnerNotificationStateDto { OwnerKey = d.OwnerKey };
                    byOwner[d.OwnerKey] = state;
                }

                state.DeviceCount = d.Count;
                state.LastSentAt = d.LastSentAt;
            }

            var items = byOwner.Values.OrderBy(s => s.OwnerKey, StringComparer.OrdinalIgnoreCase).ToList();

            return Results.Ok(ApiResponse<List<OwnerNotificationStateDto>>.Ok(items));
        })
        .WithName("GetNotificationPreferences");

        // ── 계정 하나의 앱 현황 (관리 화면) ─────────────────
        // 구독·설정·발송 기록·쪽지를 한 번에 읽는다(따로 부르면 화면이 맞추고 왕복도 넷이 된다).
        // 관리 화면에서만 부른다(화면은 포털관리 메뉴 권한 안에 있다).
        group.MapGet("/owners/{ownerKey}/app-status", async (
            string ownerKey,
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] IOptions<VapidOptions> vapid,
            [FromQuery] string? ownerType = null,
            [FromQuery] int days = 30,
            [FromQuery] int take = 100,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(ownerKey))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "누구의 현황인지 주지 않았습니다.", code: "INVALID"));
            }

            var type = string.IsNullOrWhiteSpace(ownerType) ? OwnerTypePortal : ownerType.Trim();
            var key = ownerKey.Trim();
            var from = Since(days);
            var rows = Math.Clamp(take, 1, 500);

            var status = new OwnerAppStatusDto
            {
                OwnerType = type,
                OwnerKey = key,
                Days = Math.Max(1, days),
                PushAvailable = vapid.Value.IsConfigured,
                Preference = await prefs.GetAsync(type, key, ct),
                Devices = await DevicesAsync(db, type, key),
            };

            // ── 계정 요약 ────────────────────────────────
            // 계정을 지워도 구독·기록은 남으므로 없을 수 있다(그때는 「없는 아이디」로 말한다).
            var account = await db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == key && !a.IsDeleted)
                .Select(a => new { a.Id, a.UserName, a.RealName, a.DepartmentId })
                .FirstOrDefaultAsync(ct);

            if (account is not null)
            {
                status.AccountFound = true;
                status.UserName = string.IsNullOrWhiteSpace(account.RealName)
                    ? account.UserName
                    : account.RealName;

                status.Email = await db.AccountProfileDetails
                    .AsNoTracking()
                    .Where(d => d.AccountId == account.Id && d.DetailType == "Email"
                                && !d.IsDeleted && d.Content != "")
                    .OrderByDescending(d => d.IsPrimary)
                    .Select(d => d.Content)
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrWhiteSpace(account.DepartmentId))
                {
                    status.DeptName = await db.Departments
                        .AsNoTracking()
                        .Where(d => d.Id == account.DepartmentId && !d.IsDeleted)
                        .Select(d => d.Name)
                        .FirstOrDefaultAsync(ct);
                }
            }

            // ── 길 × 방향 집계 ───────────────────────────
            // 받은 것은 owner_key, 보낸 것은 sent_by 로 가른다. 한 줄이 기기 하나라 닿으려 한 곳의 수를 센다.
            var received = await db.PushSendLogs
                .AsNoTracking()
                .Where(l => l.SentAt >= from && l.OwnerType == type && l.OwnerKey == key)
                .GroupBy(l => l.Channel)
                .Select(g => new
                {
                    Channel = g.Key,
                    Total = g.Count(),
                    Success = g.Count(x => x.IsSuccess),
                    Read = g.Count(x => x.ReadAt != null),
                    LastAt = g.Max(x => (DateTime?)x.SentAt),
                })
                .ToListAsync(ct);

            var sent = await db.PushSendLogs
                .AsNoTracking()
                .Where(l => l.SentAt >= from && l.SentBy == key)
                .GroupBy(l => l.Channel)
                .Select(g => new
                {
                    Channel = g.Key,
                    Total = g.Count(),
                    Success = g.Count(x => x.IsSuccess),
                    Read = g.Count(x => x.ReadAt != null),
                    LastAt = g.Max(x => (DateTime?)x.SentAt),
                })
                .ToListAsync(ct);

            // 줄이 없는 칸도 0 으로 세운다(빼면 화면이 그 칸을 못 그린다).
            foreach (var channel in new[] { ChannelPush, ChannelEmail })
            {
                foreach (var (direction, source) in new[]
                         {
                             (DirectionReceived, received),
                             (DirectionSent, sent),
                         })
                {
                    var hit = source.FirstOrDefault(r => r.Channel == channel);

                    status.Channels.Add(new AppChannelStatDto
                    {
                        Channel = channel,
                        Direction = direction,
                        Total = hit?.Total ?? 0,
                        Success = hit?.Success ?? 0,
                        Failure = (hit?.Total ?? 0) - (hit?.Success ?? 0),
                        Read = hit?.Read ?? 0,
                        LastAt = hit?.LastAt,
                    });
                }
            }

            // ── 최근 기록 ────────────────────────────────
            // 양쪽을 따로 읽어 합친다(한 벌로 읽으면 자기가 자기에게 보낸 줄이 뭉쳐 방향을 잃는다).
            var receivedRows = await db.PushSendLogs
                .AsNoTracking()
                .Where(l => l.SentAt >= from && l.OwnerType == type && l.OwnerKey == key)
                .OrderByDescending(l => l.SentAt)
                .Take(rows)
                .Select(l => new AppDeliveryRowDto
                {
                    Id = l.Id,
                    SentAt = l.SentAt,
                    Channel = l.Channel,
                    Direction = DirectionReceived,
                    Counterpart = l.SentBy,
                    Title = l.Title,
                    Body = l.Body,
                    Url = l.Url,
                    Success = l.IsSuccess,
                    FailureReason = l.FailureReason,
                    ReadAt = l.ReadAt,
                })
                .ToListAsync(ct);

            var sentRows = await db.PushSendLogs
                .AsNoTracking()
                .Where(l => l.SentAt >= from && l.SentBy == key)
                .OrderByDescending(l => l.SentAt)
                .Take(rows)
                .Select(l => new AppDeliveryRowDto
                {
                    Id = l.Id,
                    SentAt = l.SentAt,
                    Channel = l.Channel,
                    Direction = DirectionSent,
                    Counterpart = l.OwnerKey,
                    Title = l.Title,
                    Body = l.Body,
                    Url = l.Url,
                    Success = l.IsSuccess,
                    FailureReason = l.FailureReason,
                    ReadAt = l.ReadAt,
                })
                .ToListAsync(ct);

            status.Deliveries = receivedRows
                .Concat(sentRows)
                .OrderByDescending(r => r.SentAt)
                .Take(rows)
                .ToList();

            // ── 쪽지 ─────────────────────────────────────
            // 포털 계정끼리만 주고받으므로 주인 종류를 보지 않는다.
            if (type == OwnerTypePortal)
            {
                var inbox = db.Notes.AsNoTracking()
                    .Where(n => n.ReceiverKey == key && !n.IsDeleted && !n.ReceiverDeleted);

                var outbox = db.Notes.AsNoTracking()
                    .Where(n => n.SenderKey == key && !n.IsDeleted && !n.SenderDeleted);

                status.Notes = new AppNoteStatDto
                {
                    Received = await inbox.CountAsync(n => n.SentAt >= from, ct),
                    Sent = await outbox.CountAsync(n => n.SentAt >= from, ct),
                    MailForwarded = await inbox.CountAsync(n => n.SentAt >= from && n.EmailSent, ct),

                    // 안 읽은 것은 기간으로 자르지 않는다(오래된 안 읽은 것이 가장 중요하다).
                    UnreadTotal = await inbox.CountAsync(n => n.ReadAt == null, ct),

                    LastReceivedAt = await inbox.MaxAsync(n => (DateTime?)n.SentAt, ct),
                    LastSentAt = await outbox.MaxAsync(n => (DateTime?)n.SentAt, ct),
                };
            }

            return Results.Ok(ApiResponse<OwnerAppStatusDto>.Ok(status));
        })
        .WithName("GetOwnerAppStatus");

        // ── 내 알림 설정 화면이 한 번에 받는 상태 ───────────
        // 공개 키·스위치·기기 목록을 한 번에 내린다(따로 부르면 화면이 깜빡인다).
        group.MapGet("/preferences/me", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] IOptions<VapidOptions> vapid,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var v = vapid.Value;
            var state = new MyNotificationStateDto
            {
                OwnerType = "jsini",
                OwnerKey = user.UserId,
                Preference = await prefs.GetAsync("jsini", user.UserId, ct),
                PushAvailable = v.IsConfigured,
                VapidPublicKey = v.IsConfigured ? v.PublicKey : null,
                Devices = await DevicesAsync(db, OwnerTypePortal, user.UserId)
            };

            return Results.Ok(ApiResponse<MyNotificationStateDto>.Ok(state));
        })
        .WithName("GetMyNotificationPreference");

        // ── 내 알림 설정 저장 ───────────────────────────────
        // 자기 것만 바꾼다. 주인을 지정하는 인자가 없는 것이 방어다.
        group.MapPut("/preferences/me", async (
            [FromBody] UpdateNotificationPreferenceDto request,
            UserContext? user,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] ILocationTrackService tracks,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // NoteEmailEnabled 도 이 검사에 있어야 한다(빠지면 그 스위치만 눌러도 400). 항목을 늘리면 여기도 늘린다.
            if (request.PushEnabled is null &&
                request.EmailEnabled is null &&
                request.WeatherEnabled is null &&
                request.NoteEmailEnabled is null &&
                request.CommentPushEnabled is null &&
                request.CommentEmailEnabled is null &&
                request.WeatherLocalEnabled is null &&
                request.WeatherHours is null &&
                request.WeatherPlace is null &&
                (request.WeatherLat is null || request.WeatherLon is null))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "바꿀 항목이 없습니다.", code: "INVALID"));
            }

            var saved = await prefs.SaveAsync("jsini", user.UserId, request, user.UserId, ct);

            // ── 지나온 자리를 한 점 쌓는다 ──────────────────
            // 「방금 쟀다」가 실려 있을 때만 쌓는다(설정 화면은 스위치에도 좌표를 실어 보낸다).
            // 설정 저장 뒤에 둔다(앞에 두면 기록 실패가 스위치 고장처럼 보인다).
            if (request.WeatherLocated == true
                && request.WeatherLat is { } lat && request.WeatherLon is { } lon)
            {
                await tracks.RecordAsync(
                    "jsini", user.UserId, lat, lon,
                    request.WeatherAccuracy, request.WeatherPlace, user.UserId, ct);
            }

            return Results.Ok(ApiResponse<NotificationPreferenceDto>.Ok(saved));
        })
        .WithName("UpdateMyNotificationPreference");

        // ── 내가 받을 수 있는 알림 하나하나 ─────────────────
        // 정책이 허락한 것만 내린다. 끄기만 할 수 있고 정책 밖의 것을 켤 길은 없다.
        group.MapGet("/preferences/me/events", async (
            UserContext? user,
            [FromServices] IMyNotificationEventService events,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await events.GetAsync(user.UserId, ct);
            return Results.Ok(ApiResponse<List<MyNotificationEventDto>>.Ok(list));
        })
        .WithName("GetMyNotificationEvents");

        group.MapPut("/preferences/me/events/{eventCode}", async (
            string eventCode,
            [FromBody] SaveMyNotificationEventDto request,
            UserContext? user,
            [FromServices] IMyNotificationEventService events,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            if (request.PushEnabled is null && request.EmailEnabled is null)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "바꿀 항목이 없습니다.", code: "INVALID"));
            }

            var saved = await events.SaveAsync(user.UserId, eventCode.Trim().ToUpperInvariant(), request, ct);

            return saved is null
                ? Results.NotFound(ApiResponse<bool>.Fail(
                    message: "본인이 설정할 수 있는 알림이 아닙니다.", code: "NOT_FOUND"))
                : Results.Ok(ApiResponse<MyNotificationEventDto>.Ok(saved));
        })
        .WithName("SaveMyNotificationEvent");

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

            // 누가 보냈는지 함께 넘긴다(기록에 없으면 나중에 답할 길이 없다).
            var result = await sender.SendAsync(request, user.UserId, ct);

            // 보낸 것이 없으면 성공으로 말하지 않는다(이유는 result.Message).
            // 메일이 나갔으면 성공으로 센다(구독 기기가 없어도 부르는 쪽이 경고를 쌓지 않게).
            return result.Sent > 0 || result.Mailed > 0
                ? Results.Ok(ApiResponse<SendPushResultDto>.Ok(result))
                : Results.Json(
                    ApiResponse<SendPushResultDto>.Ok(result, result.Message ?? "보낸 알림이 없습니다."),
                    statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("SendPush");

        // ── 나에게 시험 발송 ────────────────────────────────
        // 대상을 서버가 정한다(주인 키를 화면이 적으면 남의 키가 들어온다). 켜짐 여부도 건너뛰지 않는다(실제 발송과 같은 길을 지나야 시험의 뜻이 있다).
        group.MapPost("/push/test", async (
            [FromBody] PushMessageDto? request,
            UserContext? user,
            [FromServices] IPushSender sender,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var message = request ?? new PushMessageDto();
            if (string.IsNullOrWhiteSpace(message.Title)) message.Title = "JSini 포털 시험 알림";
            if (string.IsNullOrWhiteSpace(message.Body)) message.Body = "이 알림이 보이면 설정이 정상입니다.";
            // 누르면 「내 알림 설정」 화면으로 간다.
            if (string.IsNullOrWhiteSpace(message.Url)) message.Url = "/funeral/setting/notification";

            // 시험 알림은 수명을 5분으로 준다(늦게 도착하면 뜻이 없다). 열쇠를 줘 여러 번 눌러도 줄은 하나만 남긴다.
            message.TtlSeconds ??= 300;
            message.Topic ??= "push-test";

            // 구분은 부르는 쪽 말을 듣지 않는다(이 주소로 온 것은 전부 시험이다).
            message.Category = PushCategories.Test;

            var result = await sender.SendAsync(new SendPushDto
            {
                Owners = new List<OwnerRefDto>
                {
                    new() { OwnerType = "jsini", OwnerKey = user.UserId }
                },
                Message = message
            }, user.UserId, ct);

            return result.Sent > 0 || result.Mailed > 0
                ? Results.Ok(ApiResponse<SendPushResultDto>.Ok(result))
                : Results.Json(
                    ApiResponse<SendPushResultDto>.Ok(result, result.Message ?? "보낸 알림이 없습니다."),
                    statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("SendTestPushToMe");

        // ── 보낸 기록 ───────────────────────────────────────
        // 보낸 쪽의 기록을 낸다. 목록 봉투 {data, totalcount, totalpagecount} 는 프론트가 찾는 이름이라 그대로 맞춘다.

        // ── 기기에 닿았다 ───────────────────────────────────
        // 익명이다(서비스워커가 부르고 토큰이 없다). 묶음 아이디와 구독 주소로 찾고, 닿았다고 찍는 것뿐이라 새어도 잃을 것이 없다.
        // 주인은 묻지 않는다(구독 주소가 곧 기기다).
        group.MapPost("/push/delivered", async (
            [FromBody] PushDeliveredDto request,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            var nid = (request.Nid ?? string.Empty).Trim();
            var endpoint = (request.Endpoint ?? string.Empty).Trim();

            // 없는 값에도 참을 돌려준다(404 로 가르면 묶음 아이디로 알림을 찾아볼 수 있다).
            if (nid.Length == 0 || nid.Length > 64
                || endpoint.Length == 0 || endpoint.Length > 1000)
            {
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }

            // 묶음 × 기기로 한 줄만 찍는다(묶음만 찍으면 한 대만 받아도 넷 다 닿은 것이 된다).
            // 이미 찍힌 것은 두고 처음 닿은 때를 남긴다(같은 푸시로 두 번 깨어나기도 한다).
            var rows = await db.PushSendLogs
                .Where(l => l.BatchId == nid && l.Endpoint == endpoint
                            && l.DeliveredAt == null)
                .ToListAsync(ct);

            if (rows.Count > 0)
            {
                var now = AppTime.UtcNow;
                foreach (var row in rows) row.DeliveredAt = now;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .AllowAnonymous()
        .WithName("MarkPushDelivered");

        group.MapGet("/push/logs", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] bool? isSuccess = null,
            [FromQuery] string? failureReason = null,
            [FromQuery] string? ownerKey = null,
            [FromQuery] string? channel = null,
            [FromQuery] string? category = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            // 갈래를 주지 않으면 푸시만 본다(메일 줄은 channel=email 로 따로 본다).
            var query = LogQuery(db, startDate, endDate,
                string.IsNullOrWhiteSpace(channel) ? ChannelPush : channel.Trim());

            if (isSuccess.HasValue) query = query.Where(l => l.IsSuccess == isSuccess.Value);

            // 사유는 부분 일치다(화면 칸이 자유 입력이라 한 토막만 치는 일이 많다).
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                query = query.Where(l => l.FailureReason != null && l.FailureReason.Contains(failureReason));
            }

            if (!string.IsNullOrWhiteSpace(ownerKey))
            {
                query = query.Where(l => l.OwnerKey == ownerKey);
            }

            query = ByCategory(query, category);

            var total = await query.CountAsync(ct);

            // 쪽 크기를 묶어 둔다(0 이나 십만을 받으면 통째로 들고 오게 된다).
            var size = Math.Clamp(pageSize, 1, 500);
            var skip = Math.Max(0, page - 1) * size;

            var rows = await query
                .OrderByDescending(l => l.SentAt)
                .Skip(skip)
                .Take(size)
                .Select(l => new PushLogRowDto
                {
                    Id = l.Id,
                    SentAt = l.SentAt,
                    TargetUser = l.OwnerKey,
                    OwnerType = l.OwnerType,
                    Title = l.Title,
                    Body = l.Body,
                    Success = l.IsSuccess,
                    FailureReason = l.FailureReason,
                    Category = l.Category,
                    SentBy = l.SentBy,
                    DeliveredAt = l.DeliveredAt,
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                success = true,
                data = rows,
                totalcount = total,
                totalpagecount = (int)Math.Ceiling(total / (double)size),
            });
        })
        .WithName("GetPushLogs");

        // 현황 화면의 타일 넷.
        group.MapGet("/push/stats", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int days = 7,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var from = Since(days);
            var rows = await db.PushSendLogs
                .Where(l => l.SentAt >= from && l.Channel == ChannelPush)
                .GroupBy(l => l.IsSuccess)
                .Select(g => new { Success = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var ok = rows.FirstOrDefault(r => r.Success)?.Count ?? 0;
            var ng = rows.FirstOrDefault(r => !r.Success)?.Count ?? 0;
            var total = ok + ng;

            // 이 엔드포인트만 ApiResponse 를 쓰지 않는다(값 한 벌을 받는 화면이 타일을 그릴 수 있게 맨 봉투로 돌려준다).
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    totalSent = total,
                    successCount = ok,
                    failureCount = ng,
                    // 서버가 계산한다(화면이 각자 나누면 0건일 때의 답이 갈린다).
                    successRate = total == 0 ? 0d : Math.Round(ok * 100d / total, 1),
                },
            });
        })
        .WithName("GetPushStats");

        // 성공률 추이. 하루 한 점이다.
        group.MapGet("/push/trend", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int days = 30,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var from = Since(days);

            // 날짜로 묶는 일은 DB 에 시킨다(줄을 다 받아 세면 기간이 길수록 무거워진다).
            var grouped = await db.PushSendLogs
                .Where(l => l.SentAt >= from && l.Channel == ChannelPush)
                .GroupBy(l => l.SentAt.Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Sent = g.Count(),
                    Success = g.Count(x => x.IsSuccess),
                })
                .OrderBy(g => g.Day)
                .ToListAsync(ct);

            var points = grouped.Select(g => new
            {
                period = g.Day.ToString("yyyy-MM-dd"),
                sent = g.Sent,
                success = g.Success,
                successRate = g.Sent == 0 ? 0d : Math.Round(g.Success * 100d / g.Sent, 1),
            });

            return Results.Ok(ApiResponse<object>.Ok(points));
        })
        .WithName("GetPushTrend");

        // 실패 사유별 건수. 사유 글자는 PushSender 의 상수라 갈래가 몇 개뿐이다.
        group.MapGet("/push/failure-reasons", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int days = 7,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var from = Since(days);
            var rows = await db.PushSendLogs
                .Where(l => l.SentAt >= from && !l.IsSuccess && l.Channel == ChannelPush)
                .GroupBy(l => l.FailureReason)
                .Select(g => new { reason = g.Key ?? "(사유 없음)", count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows));
        })
        .WithName("GetPushFailureReasons");

        // ── 내 알림함 ───────────────────────────────────────
        // 발송 이력과 같은 표를 받는 사람 쪽에서 본다. 기기 단위 줄은 batch_id 로 묶고, 옛 줄은 줄 아이디가 곧 한 건이다.
        // 실패하거나 본인이 끈 것도 내게 온 소식이므로 보여 준다.

        group.MapGet("/inbox", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? category = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int take = 500,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var mine = LogQuery(db, startDate, endDate)
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId)

                // 치운 줄도 남긴다(이 표는 발송 기록이기도 해서 치운 것이 「푸시 현황」 건수를 깎으면 안 된다).
                .Where(l => l.DeletedAt == null);

            mine = ByCategory(mine, category);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var escaped = keyword.Trim()
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");
                var pattern = $"%{escaped}%";
                mine = mine.Where(l =>
                    (l.Title != null && EF.Functions.ILike(l.Title, pattern, "\\"))
                    || (l.Body != null && EF.Functions.ILike(l.Body, pattern, "\\")));
            }

            // 안 읽은 것만 보기는 서버에서 거른다(상한 take 를 읽은 줄이 먼저 먹지 않게).
            // 묶음 단위로 본다(기기 하나만 찍힌 줄이 있으면 그 묶음은 읽은 것이다).
            if (unreadOnly)
            {
                var readBatches = mine.Where(l => l.ReadAt != null).Select(l => l.BatchId ?? l.Id);
                mine = mine.Where(l => !readBatches.Contains(l.BatchId ?? l.Id));
            }

            // 줄을 받아 메모리에서 묶는다(EF 가 GroupBy 를 번역하지 못해 502 가 났다). 상한은 그래도 건다.
            var rows = (await mine
                    .OrderByDescending(l => l.SentAt)
                    .Take(Math.Clamp(take, 1, 2000) * 4)
                    .Select(l => new
                    {
                        Key = l.BatchId ?? l.Id,
                        l.Title,
                        l.Body,
                        l.Url,
                        l.SentAt,
                        l.ReadAt,
                        l.IsSuccess,
                        l.FailureReason,
                        l.Category,
                        l.Icon,
                    })
                    .ToListAsync(ct))
                .GroupBy(l => l.Key)
                .Select(g =>
                {
                    // 한 대라도 갔으면 도착이다. 다 못 갔으면 까닭을 보여 준다.
                    var delivered = g.Any(x => x.IsSuccess);

                    return new NotificationRowDto
                    {
                        Id = g.Key,
                        Title = g.First().Title,
                        Body = g.First().Body,
                        Url = g.First().Url,
                        Category = g.First().Category,
                        CreatedAt = g.Max(x => x.SentAt),

                        // 한 기기라도 읽었으면 읽은 것이다(읽음 처리는 묶음을 통째로 찍는다).
                        IsRead = g.Any(x => x.ReadAt != null),

                        Delivered = delivered,

                        // 비어 있지 않은 줄을 고른다(칸이 생기기 전 줄이 섞이면 First() 가 빈 줄을 집을 수 있다).
                        Icon = g.Select(x => x.Icon).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i)),

                        FailureReason = delivered
                            ? null
                            : g.Select(x => x.FailureReason).FirstOrDefault(r => r != null),
                    };
                })
                .OrderByDescending(r => r.CreatedAt)
                .Take(Math.Clamp(take, 1, 2000))
                .ToList();

            return Results.Ok(ApiResponse<List<NotificationRowDto>>.Ok(rows));
        })
        .WithName("GetMyInbox");

        // 안 읽은 건수. 상단 종의 숫자이므로 목록과 같은 길(LogQuery, 푸시만)로 센다.
        // 목록이 기간을 안 주므로 세도 기간을 주지 않는다.
        group.MapGet("/inbox/unread-count", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // 목록과 한 글자도 어긋나면 안 된다(치운 줄을 여기서만 안 거르면 종 숫자가 목록보다 많아진다).
            var mine = LogQuery(db, null, null)
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && l.DeletedAt == null);

            var readBatches = mine.Where(l => l.ReadAt != null).Select(l => l.BatchId ?? l.Id);
            var unreadQuery = mine.Where(l => !readBatches.Contains(l.BatchId ?? l.Id));

            var count = await unreadQuery
                .Select(l => l.BatchId ?? l.Id)
                .Distinct()
                .CountAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(new { Unread = count }));
        })
        .WithName("GetNotificationUnreadCount");

        group.MapPost("/inbox/read-all", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // 치운 것은 목록에 없으니 찍을 것도 없다(안 거르면 치워 둔 옛 줄 수백 개를 갱신하게 된다).
            var mine = await db.PushSendLogs
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && l.ReadAt == null && l.DeletedAt == null)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var row in mine)
            {
                row.ReadAt = now;
            }

            if (mine.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("MarkAllInboxRead");

        group.MapPost("/inbox/{id}/read", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // 열쇠만 보고 갱신하지 않는다(아이디만 아는 사람이 남의 알림함을 건드릴 수 있다).
            var mine = await db.PushSendLogs
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && (l.BatchId == id || l.Id == id))
                .ToListAsync(ct);

            if (mine.Count == 0)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "그런 알림이 없습니다.", code: "NOT_FOUND"));
            }

            var now = DateTime.UtcNow;
            foreach (var row in mine.Where(r => r.ReadAt is null))
            {
                row.ReadAt = now;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("MarkInboxRead");

        // 알림함에서 한 건을 치운다(읽음으로는 부족하다 — 읽음은 안 읽은 것만 보면 도로 나온다).
        // 줄은 지우지 않고 deleted_at 만 찍는다(이 표는 발송 기록이기도 하다). 묶음 단위로 그 사람의 줄을 찍는다.
        group.MapDelete("/inbox/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var mine = await db.PushSendLogs
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && (l.BatchId == id || l.Id == id))
                .ToListAsync(ct);

            if (mine.Count == 0)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "그런 알림이 없습니다.", code: "NOT_FOUND"));
            }

            // 두 번 눌러도 처음 시각을 남긴다(다시 찍으면 치운 시각이 밀린다).
            var now = DateTime.UtcNow;
            foreach (var row in mine.Where(r => r.DeletedAt is null))
            {
                row.DeletedAt = now;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("DeleteInbox");

        // 고른 묶음만 읽음으로 찍는다(한 건짜리 길을 되풀이하면 왕복이 늘고 중간에 끊기면 어디까지 찍혔는지 알 수 없다).
        // 묶음 단위·소유자 확인은 한 건짜리 길과 같다.
        group.MapPost("/inbox/read-many", async (
            [FromBody] InboxBulkDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var keys = Keys(request);
            if (keys.Count == 0)
            {
                return Results.Ok(ApiResponse<int>.Ok(0));
            }

            var mine = await db.PushSendLogs
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && l.ReadAt == null
                            && (keys.Contains(l.BatchId!) || keys.Contains(l.Id)))
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var row in mine)
            {
                row.ReadAt = now;
            }

            if (mine.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            // 센 것은 묶음 수다(줄 수를 돌려주면 「3건 골랐는데 5건 처리」가 된다).
            return Results.Ok(ApiResponse<int>.Ok(
                mine.Select(r => r.BatchId ?? r.Id).Distinct().Count()));
        })
        .WithName("MarkInboxReadMany");

        // 고른 것 여럿을 한꺼번에 치운다(줄은 지우지 않고 deleted_at 을 찍는다). 절반만 지워지는 것이 가장 나쁘므로 저장은 한 번이다.
        // DELETE 가 아니라 POST 다(본문을 싣는 DELETE 는 중간 프록시가 몸체를 다르게 다룬다).
        group.MapPost("/inbox/delete-many", async (
            [FromBody] InboxBulkDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var keys = Keys(request);
            if (keys.Count == 0)
            {
                return Results.Ok(ApiResponse<int>.Ok(0));
            }

            var mine = await db.PushSendLogs
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId
                            && l.DeletedAt == null
                            && (keys.Contains(l.BatchId!) || keys.Contains(l.Id)))
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var row in mine)
            {
                row.DeletedAt = now;
            }

            if (mine.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(ApiResponse<int>.Ok(
                mine.Select(r => r.BatchId ?? r.Id).Distinct().Count()));
        })
        .WithName("DeleteInboxMany");

        // 큐에 넣는 것까지가 이 서비스의 일이다.
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
        .WithName("SendEmail");
    }

    /// <summary>기간을 건 기록 질의. 세 통계와 목록이 같은 기준을 쓴다. endDate 는 그 날의 끝까지 넣는다.</summary>
    private static IQueryable<Entities.PushSendLog> LogQuery(
        AppDbContext db, DateTime? startDate, DateTime? endDate, string? channel = ChannelPush)
    {
        var query = db.PushSendLogs.AsQueryable();

        // 길을 안 거르면 메일이 푸시 통계에 섞이므로, 갈래를 주지 않으면 푸시만 본다.
        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(l => l.Channel == channel);
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.SentAt >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.SentAt < endDate.Value.ToUniversalTime().AddDays(1));
        }

        return query;
    }

    /// <summary>알림구분으로 거른다. 코드값은 부분 일치가 아니라 정확히 맞춘다(AI_TASK 를 AI 로 찾으면 다른 구분이 딸려 온다).
    /// PushCategories.Unset 은 구분이 안 붙은 줄을 찾는 이름표다.</summary>
    private static IQueryable<Entities.PushSendLog> ByCategory(
        IQueryable<Entities.PushSendLog> query, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return query;

        if (string.Equals(category.Trim(), PushCategories.Unset, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(l => l.Category == null || l.Category == "");
        }

        var wanted = PushCategories.Normalize(category);
        return wanted is null ? query : query.Where(l => l.Category == wanted);
    }

    /// <summary>「최근 N 일」의 시작. 0 이나 음수가 와도 하루는 본다.</summary>
    private static DateTime Since(int days) =>
        DateTime.UtcNow.Date.AddDays(-Math.Max(1, days) + 1);

    /// <summary>묶음 요청에서 쓸 수 있는 열쇠만 고른다. 빈 값은 떨어뜨리고 중복은 한 번만 남긴다.</summary>
    private static List<string> Keys(InboxBulkDto request) =>
        [.. (request.Ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)];

    /// <summary>한 사람의 기기(구독) 목록. 최근 등록순. 주인을 인자로 받는다(본인으로 못 박으면 계정 앱 현황이 남의 기기를 보여 줄 수 없다).</summary>
    /// <summary>기기 목록이 도달 기록을 세는 기간(일, 30). 처음부터 세면 한 달 전에 죽은 기기가 「30건 중 28건 도달」로 보인다.</summary>
    private const int DeviceDeliveryDays = 30;

    private static async Task<List<PushDeviceDto>> DevicesAsync(
        AppDbContext db, string ownerType, string ownerKey)
    {
        var devices = await DeviceRowsAsync(db, ownerType, ownerKey);
        if (devices.Count == 0) return devices;

        // 성공한 줄만 센다(못 보낸 줄은 그 기기 몫이 아니라 분모에 넣으면 멀쩡한 기기가 낮게 보인다).
        var from = Since(DeviceDeliveryDays);

        var counts = await db.PushSendLogs
            .AsNoTracking()
            .Where(l => l.SentAt >= from && l.Channel == ChannelPush
                        && l.OwnerType == ownerType && l.OwnerKey == ownerKey
                        && l.IsSuccess && l.Endpoint != null)
            .GroupBy(l => l.Endpoint!)
            .Select(g => new
            {
                Endpoint = g.Key,
                Sent = g.Count(),
                Delivered = g.Count(x => x.DeliveredAt != null),
                LastDelivered = g.Max(x => x.DeliveredAt),
            })
            .ToListAsync();

        var byEndpoint = counts.ToDictionary(c => c.Endpoint, StringComparer.Ordinal);

        foreach (var device in devices)
        {
            if (!byEndpoint.TryGetValue(device.Endpoint, out var hit)) continue;

            device.RecentSentCount = hit.Sent;
            device.RecentDeliveredCount = hit.Delivered;
            device.LastDeliveredAt = hit.LastDelivered;
        }

        return devices;
    }

    /// <summary>구독 표에서 기기 줄만 읽어 온다. 도달 기록은 부르는 쪽이 얹는다.</summary>
    private static async Task<List<PushDeviceDto>> DeviceRowsAsync(
        AppDbContext db, string ownerType, string ownerKey)
    {
        return await db.PushSubscriptions
            .Where(s => s.OwnerType == ownerType && s.OwnerKey == ownerKey)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new PushDeviceDto
            {
                Endpoint = s.Endpoint,
                Source = s.Source,
                UserAgent = s.UserAgent,
                LastSentAt = s.LastSentAt,
                CreatedAt = s.CreatedAt,
                FailureCount = s.FailureCount,
                Metadata = new DeviceMetadataDto
                {
                    DeviceType = s.DeviceType,
                    Platform = s.Platform,
                    PlatformVersion = s.PlatformVersion,
                    DeviceVendor = s.DeviceVendor,
                    DeviceModel = s.DeviceModel,
                    Browser = s.Browser,
                    BrowserVersion = s.BrowserVersion,
                    BrowserEngine = s.BrowserEngine,
                    IsMobile = s.IsMobile,
                    IsStandalone = s.IsStandalone,
                    DisplayMode = s.DisplayMode,
                    ScreenWidth = s.ScreenWidth,
                    ScreenHeight = s.ScreenHeight,
                    ViewportWidth = s.ViewportWidth,
                    ViewportHeight = s.ViewportHeight,
                    DevicePixelRatio = s.DevicePixelRatio,
                    ColorDepth = s.ColorDepth,
                    HardwareConcurrency = s.HardwareConcurrency,
                    DeviceMemoryGb = s.DeviceMemoryGb,
                    MaxTouchPoints = s.MaxTouchPoints,
                    Language = s.Language,
                    Languages = s.Languages,
                    TimeZone = s.TimeZone,
                    ConnectionType = s.ConnectionType,
                    EffectiveConnectionType = s.EffectiveConnectionType,
                    UserAgentDataJson = s.UserAgentDataJson
                }
            })
            .ToListAsync();
    }

    private static void ApplyMetadata(Entities.PushSubscription target, DeviceMetadataDto metadata)
    {
        target.DeviceType = metadata.DeviceType;
        target.Platform = metadata.Platform;
        target.PlatformVersion = metadata.PlatformVersion;
        target.DeviceVendor = metadata.DeviceVendor;
        target.DeviceModel = metadata.DeviceModel;
        target.Browser = metadata.Browser;
        target.BrowserVersion = metadata.BrowserVersion;
        target.BrowserEngine = metadata.BrowserEngine;
        target.IsMobile = metadata.IsMobile;
        target.IsStandalone = metadata.IsStandalone;
        target.DisplayMode = metadata.DisplayMode;
        target.ScreenWidth = metadata.ScreenWidth;
        target.ScreenHeight = metadata.ScreenHeight;
        target.ViewportWidth = metadata.ViewportWidth;
        target.ViewportHeight = metadata.ViewportHeight;
        target.DevicePixelRatio = metadata.DevicePixelRatio;
        target.ColorDepth = metadata.ColorDepth;
        target.HardwareConcurrency = metadata.HardwareConcurrency;
        target.DeviceMemoryGb = metadata.DeviceMemoryGb;
        target.MaxTouchPoints = metadata.MaxTouchPoints;
        target.Language = metadata.Language;
        target.Languages = metadata.Languages;
        target.TimeZone = metadata.TimeZone;
        target.ConnectionType = metadata.ConnectionType;
        target.EffectiveConnectionType = metadata.EffectiveConnectionType;
        target.UserAgentDataJson = metadata.UserAgentDataJson;
    }
}
