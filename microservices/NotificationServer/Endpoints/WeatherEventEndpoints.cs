using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>기상 이벤트 알림 발송. LifeEnvServer 가 기준 충족을 기록한 직후 루프백으로 부른다. 받는 사람은 날씨 스위치를 켠 주인들이다.
/// 카카오 알림톡은 기본 꺼짐이고, 수신 번호 출처가 정해져야 켤 수 있다.</summary>
public static class WeatherEventEndpoints
{
    public static void MapWeatherEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/weather-event", async (
            [FromBody] WeatherEventDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] IPushSender push,
            [FromServices] IKakaoAlimtalkSender kakao,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WeatherEvent");
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.StandardName))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("기준 이름이 필요합니다.", "INVALID"));
            }

            // 날씨 스위치를 켠 주인이 없으면 오류가 아니라 보낼 곳이 없는 것이다.
            var owners = await db.NotificationPreferences
                .Where(p => p.WeatherEnabled && !p.IsDeleted)
                .Select(p => new OwnerRefDto { OwnerType = p.OwnerType, OwnerKey = p.OwnerKey })
                .ToListAsync(ct);

            var valueText = $"{request.MeasuredValue:0.#}{request.Unit}";
            var message = new PushMessageDto
            {
                Title = $"[기상] {request.StandardName}",
                Body = $"{request.Location} · 측정 {valueText} — 기준 충족",
                Url = "/life/weather/events",
                Icon = WeatherPushIcons.ForKind(request.WeatherKind),
                // 한 시간 지난 실황은 알리지 않는다(늦게 켜진 브라우저에 쌓였다가 한꺼번에 쏟아진다).
                // 같은 기준은 줄에서 최신 한 건만 남으므로 Tag 는 주지 않는다(겹침은 Topic).
                TtlSeconds = 3600,
                Topic = $"weather-standard:{request.StandardName}",
                Category = PushCategories.Weather,
                EventCode = NotificationEvents.Weather,
            };

            var pushResult = owners.Count > 0
                ? await push.SendAsync(new SendPushDto { Owners = owners, Message = message }, sentBy: null, ct)
                : new SendPushResultDto { Sent = 0, Message = "날씨 알림을 켠 사람이 없습니다." };

            // 카카오 알림톡은 기본 꺼짐이고, 켜져도 수신 번호가 없으면 0건이다.
            var kakaoSent = await kakao.SendWeatherAsync(request, ct);

            logger.LogInformation(
                "기상 알림 발송: {Standard} ({Location}) 대상 {Owners}명 · 푸시 {Sent}건 · 알림톡 {Kakao}건",
                request.StandardName, request.Location, owners.Count, pushResult.Sent, kakaoSent);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                targets = owners.Count,
                pushSent = pushResult.Sent,
                kakaoSent,
                detail = pushResult.Message,
            }));
        })
        .WithName("SendWeatherEvent")
        .WithTags("Weather");

        // ── 기상 특보 ──────────────────────────────────────────────────
        // 위 /weather-event 와 다르다(관리 지역에 걸린 기상청 특보). 받는 사람은 날씨 스위치(weather_enabled) 주인이다.
        app.MapPost("/weather-warning", async (
            [FromBody] WeatherWarningEventDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] IPushSender push,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("WeatherWarning");
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Summary))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("특보 제목이나 요약이 필요합니다.", "INVALID"));
            }

            var owners = await db.NotificationPreferences
                .Where(p => p.WeatherEnabled && !p.IsDeleted)
                .Select(p => new OwnerRefDto { OwnerType = p.OwnerType, OwnerKey = p.OwnerKey })
                .ToListAsync(ct);

            var message = new PushMessageDto
            {
                Title = BuildWarningTitle(request),
                Body = BuildWarningBody(request),
                Url = "/life/weather/warning",
                Icon = WeatherPushIcons.ForKind(request.WeatherKind),
                // 같은 특보 번호의 발표·변경·해제는 한 줄로 겹친다. 번호가 없으면 태그를 주지 않는다(빈 태그는 서로 다른 특보를 합친다).
                Tag = string.IsNullOrWhiteSpace(request.WarningNum) ? null : $"weather-warning:{request.WarningNum}",
                // 특보도 한 시간이면 낡는다. 태그가 줄에서의 겹침 열쇠다.
                TtlSeconds = 3600,
                Category = PushCategories.Weather,
                EventCode = NotificationEvents.Weather,
            };

            var pushResult = owners.Count > 0
                ? await push.SendAsync(new SendPushDto { Owners = owners, Message = message }, sentBy: null, ct)
                : new SendPushResultDto { Sent = 0, Message = "날씨 알림을 켠 사람이 없습니다." };

            logger.LogInformation(
                "기상 특보 알림 발송: {Title} ({Locations}) 대상 {Owners}명 · 푸시 {Sent}건",
                message.Title, string.Join(", ", request.Locations), owners.Count, pushResult.Sent);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                targets = owners.Count,
                pushSent = pushResult.Sent,
                detail = pushResult.Message,
            }));
        })
        .WithName("SendWeatherWarning")
        .WithTags("Weather");

        // ── 내 위치 날씨 ──────────────────────────────────────────────
        // 정해진 시각마다 가는 사람별 날씨다. 대상은 생활과환경이 목록으로 준다.
        // 위경도 목록이라 X-User-Id: system:weather 만 신원으로 받는다(보통 계정으로 열면 동료 위치가 샌다).
        app.MapGet("/weather-local/subscribers", async (
            UserContext? user,
            [FromServices] INotificationPreferenceService prefs,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();
            if (!user.UserId.StartsWith("system:", StringComparison.Ordinal))
            {
                return Results.Json(
                    ApiResponse<object>.Fail("시스템 호출만 받습니다.", "E403"), statusCode: 403);
            }

            var items = await prefs.GetLocalWeatherSubscribersAsync(ct);
            return Results.Ok(ApiResponse<List<LocalWeatherSubscriberDto>>.Ok(items));
        })
        .WithName("GetLocalWeatherSubscribers")
        .WithTags("Weather");

        app.MapPost("/weather-local", async (
            [FromBody] SendLocalWeatherDto request,
            UserContext? user,
            [FromServices] IPushSender push,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("LocalWeather");
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.OwnerKey))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("받는 사람이 필요합니다.", "INVALID"));
            }

            var ownerType = string.IsNullOrWhiteSpace(request.OwnerType) ? "jsini" : request.OwnerType;

            var message = new PushMessageDto
            {
                Title = string.IsNullOrWhiteSpace(request.Title) ? "내 위치 날씨" : request.Title,
                Body = request.Body,
                Url = "/life/weather/dashboard",
                Icon = WeatherPushIcons.ForKind(request.WeatherKind),
                // 같은 사람의 날씨 알림은 한 줄로 겹친다 — 알림창에 같은 모양이 쌓이지 않게.
                Tag = "weather-local",
                // 정해진 시각 알림이라 그 시간대를 넘기면 버린다(저녁에 뜨는 아침 날씨는 소음이다).
                TtlSeconds = 10800,
                Category = PushCategories.Weather,
                EventCode = NotificationEvents.Weather,
            };

            var result = await push.SendAsync(
                new SendPushDto
                {
                    Owners = [new OwnerRefDto { OwnerType = ownerType, OwnerKey = request.OwnerKey }],
                    Message = message,
                },
                sentBy: null, ct);

            // 보낸 것이 없어도 시각을 찍는다(안 찍으면 발송기가 5분마다 같은 칸을 헛발송한다).
            await prefs.MarkLocalWeatherSentAsync(ownerType, request.OwnerKey, request.Place, ct);

            logger.LogInformation(
                "내 위치 날씨 발송: {Owner} ({Place}) · 푸시 {Sent}건 {Detail}",
                request.OwnerKey, request.Place, result.Sent, result.Message);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                pushSent = result.Sent,
                detail = result.Message,
            }));
        })
        .WithName("SendLocalWeather")
        .WithTags("Weather");
    }

    internal static class WeatherPushIcons
    {
        public static string ForKind(string? kind) =>
            $"/weather-icons/{WeatherIconKinds.Normalize(kind)}.svg";
    }

    /// <summary>알림 제목. [기상특보] 강풍주의보 해제 꼴. 기상청 제목(t1)은 뭉뚱그려서 특보 종류를 쓴다.</summary>
    private static string BuildWarningTitle(WeatherWarningEventDto request)
    {
        var kind = !string.IsNullOrWhiteSpace(request.Summary) ? request.Summary!.Trim() : request.Title.Trim();
        var command = request.Command?.Trim();

        // 종류에 이미 「해제」 같은 말이 들어 있으면 덧붙이지 않는다.
        if (!string.IsNullOrEmpty(command) && !kind.Contains(command))
        {
            kind = $"{kind} {command}";
        }

        return string.IsNullOrWhiteSpace(kind) ? "[기상특보]" : $"[기상특보] {kind}";
    }

    /// <summary>알림 본문. 지역이 먼저다(「내 지역이 걸렸나」가 첫 물음이다).</summary>
    private static string BuildWarningBody(WeatherWarningEventDto request)
    {
        var parts = new List<string>();

        if (request.Locations.Count > 0)
        {
            // 지역이 많으면 알림이 잘린다. 셋까지만 적고 나머지는 수로 말한다.
            var shown = request.Locations.Take(3);
            var rest = request.Locations.Count - 3;
            parts.Add(rest > 0
                ? $"{string.Join(", ", shown)} 외 {rest}곳"
                : string.Join(", ", shown));
        }

        if (!string.IsNullOrWhiteSpace(request.WarningNum)) parts.Add(request.WarningNum!.Trim());
        if (request.AnnouncedAt is { } at) parts.Add(at.ToOffset(TimeSpan.FromHours(9)).ToString("MM-dd HH:mm"));

        return parts.Count > 0 ? string.Join(" · ", parts) : "관리 지역에 기상 특보가 발표되었습니다.";
    }
}

/// <summary>LifeEnvServer 가 보내는 기상 특보 한 건. WeatherEventDto 와 따로 둔다(임계치 초과와 기상청 발표는 다른 사건이라 칸이 늘 빈다).</summary>
public class WeatherWarningEventDto
{
    /// <summary>기상청이 준 특보 제목(t1). 예: <c>기상특보 발표</c></summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>발표 · 변경 · 해제 · 대체</summary>
    public string? Command { get; set; }

    /// <summary>특보 번호. 예: <c>제01-198호</c>. 같은 건의 갱신을 묶는 열쇠다.</summary>
    public string? WarningNum { get; set; }

    /// <summary>매칭된 우리 관리 지역 이름들. 사람이 알아보는 것은 등록해 둔 이름이다.</summary>
    public List<string> Locations { get; set; } = new();

    /// <summary>특보 종류 요약(강풍주의보 · 풍랑주의보 꼴). 통보문 형식이 어긋나면 비어 있을 수 있다.</summary>
    public string? Summary { get; set; }

    /// <summary>날씨 상태 아이콘 분류.</summary>
    public string? WeatherKind { get; set; }

    /// <summary>발표 시각</summary>
    public DateTimeOffset? AnnouncedAt { get; set; }
}

/// <summary>LifeEnvServer 가 보내는 기상 이벤트 한 건</summary>
public class WeatherEventDto
{
    /// <summary>기준 이름 (예: 강풍주의, 폭염)</summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>관측 지점 (예: 울산 남구)</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>측정 분류 (WIND · RAIN · SNOW · HEAT · COLD …)</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>날씨 상태 아이콘 분류.</summary>
    public string? WeatherKind { get; set; }

    /// <summary>측정값</summary>
    public double MeasuredValue { get; set; }

    /// <summary>단위 (m/s · mm · ℃ …)</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>기준 충족 판정 시각</summary>
    public DateTimeOffset EventTime { get; set; }
}
