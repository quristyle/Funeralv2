using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 기상 이벤트 알림 발송 (결정 D-G1a · 2026-09-04).
/// </summary>
/// <remarks>
/// <para>
/// GHUB 이식 때 판정·기록만 옮기고 발송은 남겨 두었던 것(38번 문서 4절)이다.
/// 사용자 결정: <b>발송은 NotificationServer 로 보내 여기서 처리한다.</b>
/// LifeEnvServer 의 기상 감시가 기준 충족을 기록한 직후 이 엔드포인트를 부른다.
/// </para>
/// <para>
/// <b>수신 대상은 역할이 아니라 구독이다</b> — 내 알림 설정의 날씨 스위치
/// (<c>scom.notification_preferences.weather_enabled</c>, 29번 문서 8절).
/// 발송은 그 주인들의 웹푸시 구독으로 나간다(푸시 스위치를 끈 사람은
/// <see cref="PushSender"/> 가 이미 거른다).
/// </para>
/// <para>
/// <b>부르는 쪽</b>: LifeEnvServer 가 루프백으로 직접 부른다(게이트웨이를 거치지 않는다).
/// 신원 헤더는 <c>X-User-Id: system:weather</c> 다 — 서비스들은 전부 루프백에만 묶여
/// 있어(각 appsettings 의 kestrel 주석) 같은 장비 안의 호출은 게이트웨이 신원과 같은
/// 신뢰 수준으로 본다.
/// </para>
/// <para>
/// 카카오 알림톡(D-G1b)은 <see cref="IKakaoAlimtalkSender"/> 확장점으로 준비만 되어 있다
/// — 기본 꺼짐이고, 켜려면 비즈뿌리오 자격증명과 **수신 전화번호의 출처**(포털 계정에는
/// 아직 전화번호가 없다)가 먼저 정해져야 한다.
/// </para>
/// </remarks>
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

            // 날씨 스위치를 켠 주인들. 아무도 안 켰으면 보낼 곳이 없는 것이지 오류가 아니다.
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
            };

            var pushResult = owners.Count > 0
                ? await push.SendAsync(new SendPushDto { Owners = owners, Message = message }, sentBy: null, ct)
                : new SendPushResultDto { Sent = 0, Message = "날씨 알림을 켠 사람이 없습니다." };

            // 카카오 알림톡 (D-G1b) — 기본 꺼짐. 켜져 있어도 수신 번호가 없으면 0건이다.
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
        //
        // 위 /weather-event 와 **다른 것**이다. 저쪽은 우리가 정한 기준
        // (WeatherStandard, 예: 풍속 14m/s 이상)을 실황이 넘었다는 뜻이고,
        // 이쪽은 **기상청이 실제로 발표한 특보**가 우리 관리 지역에 걸렸다는 뜻이다.
        // 측정값·단위가 없고 대신 특보 종류와 발표/해제 구분이 있어 본문이 다르다.
        //
        // 받는 사람은 둘 다 같다 — 날씨 스위치(weather_enabled)를 켠 사람.
        // 환경설정 화면의 칸 이름이 「기상 특보」인 것도 이쪽을 가리킨다.
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
                // 같은 특보 번호의 발표 → 변경 → 해제는 한 줄로 겹쳐 보이는 편이 낫다.
                // 번호가 없으면 태그를 주지 않는다 — 빈 태그로 묶으면 서로 다른 특보가 합쳐진다.
                Tag = string.IsNullOrWhiteSpace(request.WarningNum) ? null : $"weather-warning:{request.WarningNum}",
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
    }

    /// <summary>
    /// 알림 제목. <c>[기상특보] 강풍주의보 해제</c> 꼴이다.
    /// </summary>
    /// <remarks>
    /// 특보 종류(<c>Summary</c>)가 제목보다 훨씬 쓸모 있다 — 기상청이 주는
    /// 제목(t1)은 「기상특보 발표」처럼 뭉뚱그린 말이라 그것만으로는
    /// 무슨 특보인지 알 수 없다. 그래서 종류를 알면 그것을 앞세운다.
    /// </remarks>
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

    /// <summary>
    /// 알림 본문. <b>지역이 먼저다</b> — 「내 지역이 걸렸나」가 첫 물음이라서다.
    /// </summary>
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

/// <summary>LifeEnvServer 가 보내는 기상 특보 한 건</summary>
/// <remarks>
/// <see cref="WeatherEventDto"/> 와 따로 두는 까닭은 <b>둘이 다른 사건</b>이기 때문이다.
/// 저쪽은 우리가 정한 임계치를 실황이 넘은 것이고, 이쪽은 기상청이 발표한 특보다.
/// 한 DTO 에 억지로 합치면 측정값 칸이 늘 비어 있거나 특보 종류 칸이 늘 비게 된다.
/// </remarks>
public class WeatherWarningEventDto
{
    /// <summary>기상청이 준 특보 제목(t1). 예: <c>기상특보 발표</c></summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>발표 · 변경 · 해제 · 대체</summary>
    public string? Command { get; set; }

    /// <summary>특보 번호. 예: <c>제01-198호</c>. 같은 건의 갱신을 묶는 열쇠다.</summary>
    public string? WarningNum { get; set; }

    /// <summary>
    /// 매칭된 <b>우리 관리 지역</b> 이름들. 특보 구역명이 아니다 —
    /// 사람이 알아보는 것은 등록해 둔 지역 이름이다.
    /// </summary>
    public List<string> Locations { get; set; } = new();

    /// <summary>
    /// 특보 종류 요약. 통보문에서 뽑은 <c>강풍주의보 · 풍랑주의보</c> 꼴이다.
    /// 비어 있을 수 있다(통보문 형식이 어긋난 경우).
    /// </summary>
    public string? Summary { get; set; }

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

    /// <summary>측정값</summary>
    public double MeasuredValue { get; set; }

    /// <summary>단위 (m/s · mm · ℃ …)</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>기준 충족 판정 시각</summary>
    public DateTimeOffset EventTime { get; set; }
}
