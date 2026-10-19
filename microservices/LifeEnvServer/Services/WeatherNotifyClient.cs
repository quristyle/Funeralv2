using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JSini.Shared.DTOs;

namespace LifeEnvServer.Services;

/// <summary>
/// 기상 이벤트를 NotificationServer 로 넘긴다. 수신 대상과 채널 판정은 알림 서버가 하고, 여기는 기준 충족 사실만 보낸다.
/// 루프백 직접 호출이라 신원 헤더는 X-User-Id: system:weather. 실패해도 판정 루프는 깨지 않는다(기록은 남긴다).
/// </summary>
public class WeatherNotifyClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WeatherNotifyClient> _logger;
    private readonly bool _enabled;

    public WeatherNotifyClient(HttpClient http, IConfiguration configuration, ILogger<WeatherNotifyClient> logger)
    {
        _http = http;
        _logger = logger;
        // 발송 스위치. 기본 켜짐 — 받는 사람은 날씨 스위치를 켠 구독자뿐이라 켜 두어도 조용하다.
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
                weatherKind = WeatherIconKinds.FromStandardCategory(category),
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

    /// <summary>
    /// 기상청이 발표한 특보 한 건을 알린다. 임계치 초과(NotifyAsync)와는 다른 사건이다.
    /// 실패는 로그만 남긴다 — 수집·매칭은 이미 끝났고 다음 사이클도 막지 않는다.
    /// </summary>
    public async Task NotifyWarningAsync(
        string title, string? command, string? warningNum,
        IReadOnlyCollection<string> locations, string? summary, DateTimeOffset? announcedAt)
    {
        if (!_enabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                title,
                command,
                warningNum,
                locations,
                summary,
                weatherKind = WeatherIconKinds.FromWarning(summary ?? title),
                announcedAt,
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/weather-warning", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("기상 특보 알림 발송 요청 완료: {Summary} ({Locations})",
                    summary ?? title, string.Join(", ", locations));
            }
            else
            {
                _logger.LogWarning("기상 특보 알림 발송 요청 실패: HTTP {Status} — NotificationServer 상태를 확인할 것",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "기상 특보 알림 발송 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }

    // ── 내 위치 날씨 ──────────────────────────────────────────────
    // 반대 방향: 내 위치 구독자 목록은 알림 서비스만 알므로 여기서 먼저 받아 와 날씨를 만들어 다시 보낸다.

    /// <summary>내 위치 날씨를 켜고 위치까지 잡아 둔 사람들. 못 받아 오면 빈 목록이다 — 던지지 않고 다음 바퀴에 다시 묻는다.</summary>
    public async Task<List<LocalWeatherSubscriber>> GetLocalSubscribersAsync(CancellationToken ct = default)
    {
        if (!_enabled) return [];

        try
        {
            using var response = await _http.GetAsync("/weather-local/subscribers", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("내 위치 날씨 대상 조회 실패: HTTP {Status}", (int)response.StatusCode);
                return [];
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<SubscriberEnvelope>(JsonOptions, ct);

            return envelope?.Data?.Result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "내 위치 날씨 대상 조회 예외 — NotificationServer 가 꺼져 있을 수 있다");
            return [];
        }
    }

    /// <summary>
    /// 한 사람에게 내 위치 날씨 한 통을 보내 달라고 한다. place 가 비어 있으면 알림 서버가 찾은 이름을 적어 둔다.
    /// </summary>
    public async Task NotifyLocalAsync(
        string ownerType, string ownerKey, string title, string? body, string? place,
        string weatherKind,
        CancellationToken ct = default)
    {
        if (!_enabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new { ownerType, ownerKey, title, body, place, weatherKind });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("/weather-local", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("내 위치 날씨 발송 요청 완료: {Owner} ({Place})", ownerKey, place);
            }
            else
            {
                _logger.LogWarning("내 위치 날씨 발송 요청 실패: HTTP {Status} — 대상 {Owner}",
                    (int)response.StatusCode, ownerKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "내 위치 날씨 발송 요청 예외 — 대상 {Owner}", ownerKey);
        }
    }

    /// <summary>
    /// 표준 봉투의 목록을 꺼낸다. data 는 배열이 아니라 { result: [...] } 로 한 겹 더 싼 객체다 — 배열로 읽으면 역직렬화가 통째로 던진다.
    /// </summary>
    private class SubscriberEnvelope
    {
        public SubscriberPayload? Data { get; set; }
    }

    private class SubscriberPayload
    {
        public List<LocalWeatherSubscriber>? Result { get; set; }
    }

    /// <summary>응답 칸 이름이 camelCase 라 이 옵션이 필요하다. 빠지면 값이 예외 없이 기본값(좌표 0,0)으로 채워진다.</summary>
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };
}

/// <summary>
/// 「내 위치 날씨」를 켠 사람 하나 (알림 서비스가 주는 모양).
/// </summary>
public class LocalWeatherSubscriber
{
    public string OwnerType { get; set; } = "jsini";
    public string OwnerKey { get; set; } = string.Empty;

    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>설정에 적혀 있던 지역 이름. 발송기가 다시 찾으므로 참고용이다.</summary>
    public string? Place { get; set; }

    /// <summary>받기로 한 시각들(KST, 쉼표). 비면 기본값 <c>7,18</c>.</summary>
    public string? Hours { get; set; }

    /// <summary>마지막으로 보낸 때(UTC). 같은 시각 칸을 두 번 울리지 않는 데 쓴다.</summary>
    public DateTime? LastSentAt { get; set; }
}
