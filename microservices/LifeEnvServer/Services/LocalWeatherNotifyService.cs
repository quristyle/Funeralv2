using LifeEnvServer.Utilities;
using JSini.Shared.DTOs;

namespace LifeEnvServer.Services;

/// <summary>
/// 「내 위치 날씨」 발송기. 사람마다 정한 시각에 그 사람의 저장된 좌표 날씨를 보낸다.
/// 누가 켰는지는 알림 서버가, 좌표→격자와 날씨 문장은 여기가 맡는다. 5분마다 깨어나되 같은 시각 칸에는 한 번만 보낸다.
/// 브라우저 위치는 백그라운드에서 못 읽으므로 화면이 저장해 둔 좌표를 쓴다.
/// </summary>
public class LocalWeatherNotifyService : BackgroundService
{
    /// <summary>깨어나는 간격. 시각 칸 판정이 있으므로 촘촘해도 중복 발송이 없다.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>고른 시각이 지난 뒤 55분까지만 보낸다. 올라온 뒤 지난 칸을 뒤늦게 보내면 밤에 아침 날씨가 온다.</summary>
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

            var point = await weather.GetAsync(subscriber.Lat, subscriber.Lon, ct: ct);

            // 기상청이 아무것도 안 주면 보내지 않는다. 「자료를 받지 못했습니다」만 적힌 알림은 할 일이 없고, 시각 칸도 찍히지 않아 다음 깨어남에 다시 시도한다.
            if (point.Now is null && point.Days.Count == 0)
            {
                _logger.LogWarning("내 위치 날씨 건너뜀 — 기상청 자료 없음: {Owner}", subscriber.OwnerKey);
                continue;
            }

            var place = point.Place ?? subscriber.Place;
            var condition = point.Now?.Condition;
            if (string.IsNullOrWhiteSpace(condition))
                condition = point.Days.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Condition))?.Condition;

            await notify.NotifyLocalAsync(
                subscriber.OwnerType,
                subscriber.OwnerKey,
                title: string.IsNullOrWhiteSpace(place) ? "내 위치 날씨" : $"[날씨] {place}",
                body: point.Summary,
                place: point.Place,
                weatherKind: WeatherIconKinds.FromCondition(condition),
                ct);
        }
    }

    /// <summary>지금이 이 사람의 발송 시각인가. 고른 시각이 맞는지와 이 시각 칸에 이미 보냈는지를 둘 다 본다 — 뒤엣것이 없으면 5분마다 같은 알림이 나간다.</summary>
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
