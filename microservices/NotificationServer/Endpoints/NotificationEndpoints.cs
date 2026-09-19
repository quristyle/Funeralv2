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
        .WithName("GetVapidPublicKey");

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
        //
        // 화면이 "이 브라우저가 이미 구독 중인가" 를 알아야 버튼 상태를 정할 수 있다.
        group.MapGet("/subscriptions/me", async (
            UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await MyDevicesAsync(db, user.UserId);
            return Results.Ok(ApiResponse<object>.Ok(new { items = list, count = list.Count }));
        })
        .WithName("GetMySubscriptions");

        // ── 여러 사람의 알림 상태 (관리 화면) ───────────────
        //
        // 계정 관리 화면이 사람마다 「PWA 구독 · 푸시 · 이메일 · 기상특보」를
        // 함께 보여 준다. 사람마다 따로 물으면 계정 예순에 왕복이 예순이다.
        //
        // **누구를 볼지 인자로 받지 않는다.** 주인 종류 하나로 그 종류 전부를
        // 낸다 — 목록 화면은 조건을 바꿀 때마다 보는 사람이 달라지는데, 그때마다
        // 키 예순 개를 실어 보내면 조건 한 번에 URL 이 2KB 가 된다.
        //
        // **자기 것만 보는 길(`/preferences/me`)을 대신하지 않는다.** 그쪽은
        // 공개 키와 기기 목록까지 주고, 이쪽은 남의 것을 훑는 자리라 기기를
        // 수로 줄인다.
        group.MapGet("/preferences", async (
            UserContext? user,
            [FromQuery] string? ownerType,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var type = string.IsNullOrWhiteSpace(ownerType) ? "jsini" : ownerType;

            // 저장된 뜻. **행이 없는 사람은 여기 안 나온다** — 그 사람은
            // 기본값이고, 그 사실을 화면이 알아야 해서 `saved` 로 가른다.
            var prefs = await db.NotificationPreferences
                .AsNoTracking()
                .Where(p => p.OwnerType == type)
                .Select(p => new
                {
                    p.OwnerKey,
                    p.PushEnabled,
                    p.EmailEnabled,
                    p.WeatherEnabled,
                    p.UpdatedAt,
                })
                .ToListAsync(ct);

            // 기기 수는 **따로 센다.** 구독과 설정은 서로 없어도 되는 표라
            // (기기만 있고 설정이 없거나 그 반대) 조인으로 묶으면 한쪽이 빠진다.
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
                    Saved = true,
                    UpdatedAt = p.UpdatedAt,
                };
            }

            foreach (var d in devices)
            {
                // 기기는 있는데 설정을 한 번도 저장하지 않은 사람이 있다.
                // 그 줄을 여기서 만든다 — 기본값 + 기기 수다.
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

        // ── 내 알림 설정 화면이 한 번에 받는 상태 ───────────
        //
        // 공개 키 · 스위치 셋 · 기기 목록을 따로 부르면 순서에 따라 화면이 깜빡인다.
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
                // 공개 키는 비밀이 아니다 — 브라우저가 구독을 만들 때 쓰는 값이다.
                VapidPublicKey = v.IsConfigured ? v.PublicKey : null,
                Devices = await MyDevicesAsync(db, user.UserId)
            };

            return Results.Ok(ApiResponse<MyNotificationStateDto>.Ok(state));
        })
        .WithName("GetMyNotificationPreference");

        // ── 내 알림 설정 저장 ───────────────────────────────
        //
        // 자기 것만 바꾼다. 주인을 지정하는 인자를 두지 않는 것이 가장 확실한 방어다.
        group.MapPut("/preferences/me", async (
            [FromBody] UpdateNotificationPreferenceDto request,
            UserContext? user,
            [FromServices] INotificationPreferenceService prefs,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            if (request.PushEnabled is null &&
                request.EmailEnabled is null &&
                request.WeatherEnabled is null)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "바꿀 항목이 없습니다.", code: "INVALID"));
            }

            var saved = await prefs.SaveAsync("jsini", user.UserId, request, user.UserId, ct);
            return Results.Ok(ApiResponse<NotificationPreferenceDto>.Ok(saved));
        })
        .WithName("UpdateMyNotificationPreference");

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

            // **누가 보냈는지 함께 넘긴다.** 발송 뒤에 가장 먼저 묻는 것이고
            // 기록에 없으면 답할 길이 없다(PushSendLog 머리말).
            var result = await sender.SendAsync(request, user.UserId, ct);

            // 보낸 것이 하나도 없으면 성공으로 말하지 않는다. 이유는 result.Message 에 있다.
            return result.Sent > 0
                ? Results.Ok(ApiResponse<SendPushResultDto>.Ok(result))
                : Results.Json(
                    ApiResponse<SendPushResultDto>.Ok(result, result.Message ?? "보낸 알림이 없습니다."),
                    statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("SendPush");

        // ── 나에게 시험 발송 ────────────────────────────────
        //
        // 설정 화면의 [시험 발송] 이다. `/push` 로도 할 수 있지만 그러려면 화면이
        // 자기 주인 키를 알아야 하고, 남의 키를 적어 보낼 여지가 생긴다.
        // **대상을 서버가 정하는 길**을 따로 둔다.
        //
        // 켜짐 여부도 일부러 건너뛰지 않는다 — 실제로 알림이 가는 길과 같은 길을
        // 통과해야 시험의 뜻이 있다. 껐으면 202 와 그 이유가 돌아온다.
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
            if (string.IsNullOrWhiteSpace(message.Url)) message.Url = "/system/push/setting";

            var result = await sender.SendAsync(new SendPushDto
            {
                Owners = new List<OwnerRefDto>
                {
                    new() { OwnerType = "jsini", OwnerKey = user.UserId }
                },
                Message = message
            }, user.UserId, ct);

            return result.Sent > 0
                ? Results.Ok(ApiResponse<SendPushResultDto>.Ok(result))
                : Results.Json(
                    ApiResponse<SendPushResultDto>.Ok(result, result.Message ?? "보낸 알림이 없습니다."),
                    statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("SendTestPushToMe");

        // ── 보낸 기록 ───────────────────────────────────────
        //
        // [왜 이 서비스에 있나]
        //
        // 포털관리의 「푸시 현황」·「발송 이력」은 한동안 **헬프데스크 DB** 를
        // 읽었다(`helpdesk/dashboard/push-logs`). 그런데 거기에 쓰는 것은
        // 헬프데스크 자신의 발송 코드뿐이라, 포털에서 보낸 알림은 화면에
        // 한 줄도 안 나왔다 — 실제로 「메시지 발송」으로 보내고 나서
        // 「기록이 왜 없나」 하는 물음을 받았다.
        //
        // **보낸 쪽이 자기 기록을 갖고 그 기록을 낸다.**
        //
        // [봉투 모양을 헬프데스크에 맞춘다]
        //
        // 목록은 `{ data, totalcount, totalpagecount }` 다. 프론트의
        // `GetFlexibleCountedListAsync` 가 그 이름들을 찾아보게 되어 있어서,
        // 맞춰 두면 화면은 주소만 바꾸면 된다.

        group.MapGet("/push/logs", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] bool? isSuccess = null,
            [FromQuery] string? failureReason = null,
            [FromQuery] string? ownerKey = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var query = LogQuery(db, startDate, endDate);

            if (isSuccess.HasValue) query = query.Where(l => l.IsSuccess == isSuccess.Value);

            // 사유는 **부분 일치**다. 화면의 칸이 자유 입력이라 「구독」처럼
            // 한 토막만 치는 일이 많다.
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                query = query.Where(l => l.FailureReason != null && l.FailureReason.Contains(failureReason));
            }

            if (!string.IsNullOrWhiteSpace(ownerKey))
            {
                query = query.Where(l => l.OwnerKey == ownerKey);
            }

            var total = await query.CountAsync(ct);

            // 쪽 크기를 묶어 둔다. 화면이 실수로 0 이나 십만을 보내면 서버가
            // 통째로 들고 오게 된다.
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
                    SentBy = l.SentBy,
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
                .Where(l => l.SentAt >= from)
                .GroupBy(l => l.IsSuccess)
                .Select(g => new { Success = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var ok = rows.FirstOrDefault(r => r.Success)?.Count ?? 0;
            var ng = rows.FirstOrDefault(r => !r.Success)?.Count ?? 0;
            var total = ok + ng;

            // **여기만 `ApiResponse` 를 쓰지 않는다.**
            //
            // 그 봉투는 무엇을 담든 `data.result` **배열**로 만든다 — 객체 하나를
            // 줘도 원소 하나짜리 배열이 된다(ApiResponse.BuildSerializedData).
            // 목록에는 맞는 규칙인데, 값 한 벌을 받는 화면은 그 배열을 객체로
            // 읽지 못해 **타일이 통째로 안 그려진다**(실제로 그랬다).
            //
            // 아래 목록 엔드포인트와 같은 맨 봉투로 돌려준다.
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    totalSent = total,
                    successCount = ok,
                    failureCount = ng,
                    // **서버가 계산한다.** 화면 둘이 각자 나누면 0건일 때의 답이 갈린다.
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

            // **날짜로 묶는 일을 DB 에 시킨다.** 줄을 다 받아 와서 세면 기간이
            // 길어질수록 그대로 무거워진다.
            var grouped = await db.PushSendLogs
                .Where(l => l.SentAt >= from)
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
                .Where(l => l.SentAt >= from && !l.IsSuccess)
                .GroupBy(l => l.FailureReason)
                .Select(g => new { reason = g.Key ?? "(사유 없음)", count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(rows));
        })
        .WithName("GetPushFailureReasons");

        // ── 내 알림함 ───────────────────────────────────────
        //
        // 「발송 이력」과 **같은 표를 다른 각도로** 본다. 그쪽은 보낸 사람이
        // 「무엇이 어디로 갔나」를 보는 자리이고, 여기는 받은 사람이 「내게
        // 무엇이 왔나」를 보는 자리다.
        //
        // [줄을 묶어서 낸다]
        //
        // 표의 줄은 **기기 단위**다. 기기 둘을 쓰는 사람에게 같은 알림이 두 줄로
        // 보이면 안 되므로 `batch_id`(발송 한 번) 로 묶는다. 옛 줄에는 그 값이
        // 없어서 **줄 아이디를 열쇠로 삼는다** — 묶을 것이 없으면 그것이 곧 한 건이다.
        //
        // [못 간 것도 보여 준다]
        //
        // 푸시가 실패했거나 본인이 푸시를 꺼 두었어도 **내게 온 소식인 것은
        // 같다.** 알림함이 있는 값어치의 절반이 그것이다 — 알림을 못 받은
        // 사람이 나중에라도 여기서 본다.

        group.MapGet("/inbox", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int take = 500,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var mine = LogQuery(db, startDate, endDate)
                .Where(l => l.OwnerType == "jsini" && l.OwnerKey == user.UserId);

            // **줄을 받아 와서 묶는다.**
            //
            // DB 에서 묶으려 했는데(`GroupBy` + `Any`/`Max` 투영) EF 가 그
            // 조합을 번역하지 못해 요청이 통째로 죽었다(게이트웨이에는 502 로
            // 보인다). 여기서 받아 오는 것은 **한 사람의 한 달치**이고 기기
            // 수만큼만 늘어나는 양이라, 메모리에서 묶는 편이 안전하다.
            //
            // 상한은 그래도 건다 — 기간을 아주 넓게 잡는 사람이 있다.
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
                    })
                    .ToListAsync(ct))
                .GroupBy(l => l.Key)
                .Select(g =>
                {
                    // 한 대라도 갔으면 「도착」이다. 다 못 갔으면 그 까닭을
                    // 보여 준다 — 「안 왔는데 목록에는 있다」를 설명하는 자리다.
                    var delivered = g.Any(x => x.IsSuccess);

                    return new NotificationRowDto
                    {
                        Id = g.Key,
                        Title = g.First().Title,
                        Body = g.First().Body,
                        Url = g.First().Url,
                        CreatedAt = g.Max(x => x.SentAt),

                        // **한 기기라도 읽었으면 읽은 것이다.** 읽음은 사람의
                        // 상태이고, 읽음 처리가 그 묶음을 통째로 찍는다.
                        IsRead = g.Any(x => x.ReadAt != null),

                        Delivered = delivered,
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

        // 읽음 처리. 열쇠는 묶음이고, **그 묶음의 내 줄을 전부** 찍는다.
        group.MapPost("/inbox/{id}/read", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // **남의 알림을 찍지 못한다.** 열쇠만 보고 갱신하면 아이디를 아는
            // 사람이 남의 알림함을 건드릴 수 있다.
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
        .WithName("SendEmail");
    }

    /// <summary>
    /// 기간을 건 기록 질의. 세 통계와 목록이 같은 기준을 써야 해서 한 곳에 둔다 —
    /// 갈라 두면 「현황의 건수와 이력의 줄 수가 다르다」가 난다.
    /// </summary>
    /// <remarks>
    /// <paramref name="endDate"/> 는 <b>그 날의 끝까지</b> 넣는다. 날짜만 받는
    /// 칸이라 그대로 비교하면 마지막 날이 통째로 빠진다.
    /// </remarks>
    private static IQueryable<Entities.PushSendLog> LogQuery(
        AppDbContext db, DateTime? startDate, DateTime? endDate)
    {
        var query = db.PushSendLogs.AsQueryable();

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

    /// <summary>「최근 N 일」의 시작. 0 이나 음수가 와도 하루는 본다.</summary>
    private static DateTime Since(int days) =>
        DateTime.UtcNow.Date.AddDays(-Math.Max(1, days) + 1);

    /// <summary>
    /// 내 기기(구독) 목록. 최근 등록한 것이 위다.
    /// </summary>
    /// <remarks>
    /// <c>/subscriptions/me</c> 와 <c>/preferences/me</c> 가 같은 목록을 준다.
    /// 두 곳에 같은 질의를 적으면 한쪽만 고치는 일이 생기므로 한 곳으로 모았다.
    /// </remarks>
    private static async Task<List<PushDeviceDto>> MyDevicesAsync(AppDbContext db, string userId)
    {
        return await db.PushSubscriptions
            .Where(s => s.OwnerType == "jsini" && s.OwnerKey == userId)
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
