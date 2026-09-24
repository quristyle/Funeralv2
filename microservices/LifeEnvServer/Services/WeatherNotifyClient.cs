using System.Net.Http.Json;
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

    /// <summary>
    /// <b>기상청이 발표한 특보</b> 한 건을 알린다. 위 <see cref="NotifyAsync"/> 와 다른 사건이다 —
    /// 저쪽은 우리가 정한 임계치(WeatherStandard)를 실황이 넘은 것이고, 이쪽은 특보 자체다.
    /// </summary>
    /// <remarks>
    /// 환경설정 화면의 「기상 특보」 칸이 가리키는 것이 바로 이쪽인데도
    /// 오랫동안 발송하는 쪽이 없었다 — 수집기가 <c>weather_location_warnings</c> 에
    /// 매칭만 남기고(<c>is_notified = false</c>) 거기서 끊겨 있었다.
    ///
    /// <para>실패는 로그만 남긴다. 수집·매칭은 이미 끝났고 다음 사이클도 막지 않는다.</para>
    /// </remarks>
    /// <param name="title">기상청 특보 제목(t1)</param>
    /// <param name="command">발표 · 변경 · 해제</param>
    /// <param name="warningNum">특보 번호(제01-198호). 같은 건의 갱신을 묶는 열쇠다.</param>
    /// <param name="locations">매칭된 우리 관리 지역 이름들</param>
    /// <param name="summary">통보문에서 뽑은 특보 종류 요약</param>
    /// <param name="announcedAt">발표 시각</param>
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
    //
    // 위의 둘과 방향이 하나 더 있다. 저 둘은 **보내기만** 하지만 이쪽은 먼저
    // **받아 온다** — 누가 켰고 좌표가 어디인지는 알림 서비스만 알기 때문이다.
    // 그 목록으로 날씨를 만들어 다시 보낸다.

    /// <summary>
    /// 「내 위치 날씨」를 켜고 위치까지 잡아 둔 사람들. 못 받아 오면 <b>빈 목록</b>이다.
    /// </summary>
    /// <remarks>
    /// 던지지 않는다. 알림 서비스가 잠깐 내려가 있는 것과 「켠 사람이 없다」는
    /// 발송기 입장에서 같은 일이다 — 다음 바퀴에 다시 묻는다.
    /// </remarks>
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

    /// <summary>한 사람에게 「내 위치 날씨」 한 통을 보내 달라고 한다.</summary>
    /// <param name="place">
    /// 찾아낸 지역 이름. 설정에 이름이 비어 있으면 알림 서비스가 이것을 적어 둔다 —
    /// 사람이 처음 위치를 잡을 때는 이름을 모르는 채 저장될 수 있다.
    /// </param>
    public async Task NotifyLocalAsync(
        string ownerType, string ownerKey, string title, string? body, string? place,
        CancellationToken ct = default)
    {
        if (!_enabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new { ownerType, ownerKey, title, body, place });
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
    /// 서비스들이 쓰는 표준 봉투에서 목록을 꺼낸다.
    /// </summary>
    /// <remarks>
    /// <b><c>data</c> 가 배열이 아니다.</b> 공용 봉투(<c>JSini.Shared.DTOs.ApiResponse</c>)는
    /// 목록을 내보낼 때 <c>data</c> 를 <c>{ result: [...], page: { total } }</c> 로 한 겹 더
    /// 싼다. 처음에 <c>data</c> 를 바로 배열로 읽게 해 두었더니 역직렬화가 통째로 던졌고,
    /// 로그에는 「NotificationServer 가 꺼져 있을 수 있다」만 남아 <b>서버가 멀쩡히 200 을
    /// 주고 있는데도 꺼진 것처럼 보였다.</b>
    /// </remarks>
    private class SubscriberEnvelope
    {
        public SubscriberPayload? Data { get; set; }
    }

    private class SubscriberPayload
    {
        public List<LocalWeatherSubscriber>? Result { get; set; }
    }

    /// <summary>
    /// 응답의 칸 이름이 camelCase 라 그대로 읽는다. 이것을 빠뜨리면 값이 예외 없이
    /// <b>전부 기본값</b>으로 채워져, 좌표 <c>0,0</c> 인 사람이 잔뜩 생긴 것처럼 보인다.
    /// </summary>
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
