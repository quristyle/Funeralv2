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
                ? await push.SendAsync(new SendPushDto { Owners = owners, Message = message }, ct)
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
        .WithTags("Weather")
        .WithOpenApi();
    }
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
