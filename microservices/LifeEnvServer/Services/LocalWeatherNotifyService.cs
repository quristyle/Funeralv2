using LifeEnvServer.Utilities;

namespace LifeEnvServer.Services;

/// <summary>
/// 「내 위치 날씨」 발송기 — 사람마다 정해 둔 시각에 그 사람 좌표의 날씨를 보낸다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 이 일이 알림 서비스가 아니라 여기 있나.</b> 알림 서비스는 누가 켰는지만
/// 안다. 어느 격자로 물을지(위경도 → nx·ny), 기상청이 준 코드값이 무슨 날씨인지,
/// 그것을 한 줄로 어떻게 줄일지는 전부 날씨를 아는 쪽의 몫이다. 그래서 켠 사람
/// 목록을 받아 와(<c>GET /weather-local/subscribers</c>) 문장을 만들어
/// 돌려준다(<c>POST /weather-local</c>) — 기상 이벤트와 같은 방향이다.
/// </para>
///
/// <para>
/// <b>브라우저 위치를 여기서 받지 않는다.</b> 웹은 백그라운드에서 위치를 읽을 수
/// 없다 — 앱이 열려 있을 때 사람이 단추를 눌러야 준다. 그래서 위치는 화면이 한 번
/// 받아 저장해 두고(<c>scom.notification_preferences.weather_lat/lon</c>), 발송기는
/// 그 <b>저장된 좌표</b>를 쓴다. 이사를 가거나 출장을 가면 화면에서 다시 잡아야
/// 하고, 설정 화면이 마지막으로 잡은 날짜를 보여 주는 것이 그 때문이다.
/// </para>
///
/// <para>
/// <b>5분마다 깨어나되 한 시각 칸에 한 번만 보낸다.</b> 사람이 고른 시각이 07 시면
/// 07:00~07:55 사이의 어느 깨어남에서 한 번 나가고, 그 시각 칸에 이미 보냈는지는
/// 알림 서비스가 찍어 둔 <c>weather_local_sent_at</c> 로 판정한다. 메모리에 기억해
/// 두면 서비스를 다시 띄울 때마다 같은 사람에게 또 간다.
/// </para>
/// </remarks>
public class LocalWeatherNotifyService : BackgroundService
{
    /// <summary>깨어나는 간격. 시각 칸 판정이 있으므로 촘촘해도 중복 발송이 없다.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 고른 시각이 지난 뒤 <b>이만큼까지만</b> 보낸다.
    /// </summary>
    /// <remarks>
    /// 서비스가 몇 시간 내려가 있다가 올라왔을 때, 지나간 시각 칸의 알림을 뒤늦게
    /// 보내면 <b>밤 열한 시에 아침 날씨</b>가 온다. 그 시각의 날씨를 알려 주는 것이
    /// 목적이므로 늦은 것은 보내지 않는 편이 맞다.
    /// </remarks>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(55);

    /// <summary>시각을 고르지 않은 사람에게 적용할 기본값(KST).</summary>
    private static readonly int[] DefaultHours = [7, 18];

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LocalWeatherNotifyService> _logger;

    public LocalWeatherNotifyService(
        IServiceScopeFactory scopes, ILogger<LocalWeatherNotifyService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("내 위치 날씨 발송기 시작 — {Interval}분마다 확인", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 한 바퀴가 실패해도 다음 바퀴는 돌아야 한다.
                _logger.LogError(ex, "내 위치 날씨 발송 중 오류");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var notify = scope.ServiceProvider.GetRequiredService<WeatherNotifyClient>();

        var subscribers = await notify.GetLocalSubscribersAsync(ct);
        if (subscribers.Count == 0) return;

        var nowKst = Kst.Now;
        var due = subscribers.Where(s => IsDue(s, nowKst)).ToList();
        if (due.Count == 0) return;

        var weather = scope.ServiceProvider.GetRequiredService<PointWeatherService>();

        foreach (var subscriber in due)
        {
            if (ct.IsCancellationRequested) break;

            if (!GridConverter.IsInKorea(subscriber.Lat, subscriber.Lon))
            {
                _logger.LogWarning(
                    "내 위치 날씨 건너뜀 — 한반도 밖 좌표: {Owner} ({Lat}, {Lon})",
                    subscriber.OwnerKey, subscriber.Lat, subscriber.Lon);
                continue;
            }

            var point = await weather.GetAsync(subscriber.Lat, subscriber.Lon, ct);

            // 기상청이 아무것도 주지 않았으면 보내지 않는다. 「자료를 받지 못했습니다」만
            // 적힌 알림은 받는 사람이 할 수 있는 일이 없고, 시각 칸도 찍히지 않아
            // 다음 깨어남에서 다시 시도한다.
            if (point.Now is null && point.Days.Count == 0)
            {
                _logger.LogWarning("내 위치 날씨 건너뜀 — 기상청 자료 없음: {Owner}", subscriber.OwnerKey);
                continue;
            }

            var place = point.Place ?? subscriber.Place;

            await notify.NotifyLocalAsync(
                subscriber.OwnerType,
                subscriber.OwnerKey,
                title: string.IsNullOrWhiteSpace(place) ? "내 위치 날씨" : $"[날씨] {place}",
                body: point.Summary,
                place: point.Place,
                ct);
        }
    }

    /// <summary>
    /// 지금이 이 사람의 발송 시각인가.
    /// </summary>
    /// <remarks>
    /// 판정이 둘이다 — <b>고른 시각인가</b>(그리고 너무 늦지 않았나)와
    /// <b>이 시각 칸에 이미 보냈나</b>. 뒤엣것이 없으면 5분마다 같은 알림이 나간다.
    /// </remarks>
    private static bool IsDue(LocalWeatherSubscriber subscriber, DateTime nowKst)
    {
        var hours = ParseHours(subscriber.Hours);
        if (!hours.Contains(nowKst.Hour)) return false;

        // 시각 칸의 시작(예: 오늘 07:00 KST)을 UTC 로 환산해 마지막 발송과 견준다.
        var slotKst = nowKst.Date.AddHours(nowKst.Hour);
        if (nowKst - slotKst > Grace) return false;

        var slotUtc = Kst.ToUtc(slotKst).UtcDateTime;
        return subscriber.LastSentAt is not { } last || last < slotUtc;
    }

    /// <summary>
    /// <c>"7,18"</c> 을 시각 목록으로. 비었거나 못 읽으면 기본값이다.
    /// </summary>
    private static HashSet<int> ParseHours(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [.. DefaultHours];

        var hours = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var h) ? h : -1)
            .Where(h => h is >= 0 and <= 23)
            .ToHashSet();

        return hours.Count > 0 ? hours : [.. DefaultHours];
    }
}
