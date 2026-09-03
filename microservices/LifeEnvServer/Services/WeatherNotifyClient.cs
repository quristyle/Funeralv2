using System.Text;
using System.Text.Json;

namespace LifeEnvServer.Services;

/// <summary>
/// 기상 이벤트를 NotificationServer 로 넘기는 클라이언트 (결정 D-G1a · 2026-09-04).
/// </summary>
/// <remarks>
/// <para>
/// GHUB 원본은 판정한 자리에서 직접 발송(이메일·푸시·카카오)까지 했다. 이식하면서
/// 발송을 떼어 두었는데, 사용자 결정으로 **NotificationServer 로 보내 그쪽에서 처리**한다.
/// 수신 대상 판정(날씨 스위치)·채널(웹푸시 · 알림톡 확장점)은 전부 그쪽에 있다 —
/// 여기는 "기준이 충족됐다" 는 사실만 넘긴다.
/// </para>
/// <para>
/// 루프백 직접 호출이다(게이트웨이를 거치지 않는다). 서비스들이 전부 루프백에만 묶여
/// 있으므로 같은 장비 안 호출은 게이트웨이 신원과 같은 신뢰로 본다 —
/// 신원 헤더는 <c>X-User-Id: system:weather</c> 로 남긴다.
/// </para>
/// <para>
/// <b>실패해도 판정 루프를 깨지 않는다.</b> NotificationServer 가 내려가 있으면
/// 이번 이벤트의 알림만 빠지고 기록은 남는다 — 원본의 IsNotified 의미(기록 시 true,
/// 중복 억제용)를 그대로 두는 이유이기도 하다(38번 문서 4절 세 번째 항목).
/// </para>
/// </remarks>
public class WeatherNotifyClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WeatherNotifyClient> _logger;
    private readonly bool _enabled;

    public WeatherNotifyClient(HttpClient http, IConfiguration configuration, ILogger<WeatherNotifyClient> logger)
    {
        _http = http;
        _logger = logger;
        // 발송 스위치. 기본 켜짐 — 사용자 결정(2026-09-04)으로 발송이 표준 동작이다.
        // 받는 사람은 어차피 날씨 스위치를 켠 구독자뿐이라 켜 두어도 조용하다.
        _enabled = configuration.GetValue("Notify:WeatherEnabled", true);
        var baseUrl = configuration["Notify:BaseUrl"] ?? "http://127.0.0.1:5460";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _http.DefaultRequestHeaders.Add("X-User-Id", "system:weather");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>기준 충족 한 건을 알린다. 실패는 로그만 남긴다.</summary>
    public async Task NotifyAsync(
        string standardName, string location, string category,
        double measuredValue, string unit, DateTimeOffset eventTime)
    {
        if (!_enabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                standardName,
                location,
                category,
                measuredValue,
                unit,
                eventTime,
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/weather-event", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("기상 알림 발송 요청 완료: {Standard} ({Location})", standardName, location);
            }
            else
            {
                _logger.LogWarning("기상 알림 발송 요청 실패: HTTP {Status} — NotificationServer 상태를 확인할 것",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // 알림이 빠져도 판정·기록은 계속되어야 한다.
            _logger.LogWarning(ex, "기상 알림 발송 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }
}
