using System.Text;
using System.Text.Json;
using AuthServer.Endpoints;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;

namespace AuthServer.Services;

/// <summary>
/// 내부 장비 Ping 감시기 — 주기마다 Ping 을 쳐서 두절·되풀이·복구를 <c>SERVER_PING</c> 이벤트로 알린다.
/// 받는 사람과 길(앱 푸시·메일)은 알림관리 정책이 정하고, 여기서 싣는 역할은 정책이 없을 때의 받는 사람이다.
/// 상태는 메모리에만 있어 두절 중에 서버가 다시 뜨면 두절 알림이 한 번 더 간다.
/// </summary>
public sealed class ServerPingWatcher(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<ServerPingWatcher> logger) : BackgroundService
{
    /// <summary>알림 서버를 부르는 통 이름.</summary>
    public const string NotifyClient = "server-ping-notify";

    /// <summary>정책이 역할을 안 걸었을 때 받는 역할.</summary>
    private const string FallbackRole = "SYSTEM_ADMINISTRATOR";

    private const string MonitorUrl = "/admin/status/server-ping";

    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = configuration.GetSection("ServerPing");

        if (!section.GetValue("Enabled", true))
        {
            logger.LogInformation("내부 장비 Ping 감시기가 꺼져 있습니다 (ServerPing:Enabled = false).");
            return;
        }

        var targetIp = section["TargetIp"] is { Length: > 0 } ip ? ip : ServerPingEndpoints.DefaultTargetIp;
        var interval = TimeSpan.FromSeconds(Math.Max(5, section.GetValue("IntervalSeconds", 30)));
        var timeoutMs = Math.Clamp(section.GetValue("TimeoutMs", 2000), 500, 10000);
        var plan = new ServerPingAlertPlan(
            section.GetValue("FailThreshold", 3),
            TimeSpan.FromMinutes(section.GetValue("RepeatMinutes", 60)));

        logger.LogInformation(
            "내부 장비 Ping 감시기 시작 — {Ip} · {Seconds}초마다 · 연속 {Threshold}회 실패 시 두절 · 되풀이 {Repeat}분",
            targetIp, interval.TotalSeconds, plan.FailThreshold, plan.RepeatInterval.TotalMinutes);

        try { await Task.Delay(Warmup, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                var result = await ServerPingEndpoints.PingServerAsync(targetIp, timeoutMs, stoppingToken);
                var now = AppTime.UtcNowOffset;
                var alert = plan.Observe(result.IsAlive, now);

                if (alert.Kind != ServerPingAlertKind.None)
                {
                    await NotifyAsync(targetIp, alert, now, result, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 한 바퀴가 실패해도 다음 바퀴는 돌아야 한다.
                logger.LogError(ex, "내부 장비 Ping 감시 중 오류");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    /// <summary>알림 서버의 /notifications/push 로 보낸다. 메일은 이벤트의 email_from_push 로 저쪽이 함께 낸다.</summary>
    private async Task NotifyAsync(
        string targetIp, ServerPingAlert alert, DateTimeOffset now, ServerPingResultDto result, CancellationToken ct)
    {
        var (title, body) = Compose(targetIp, alert, now, result);

        logger.LogWarning("내부 장비 Ping 알림 [{Kind}] {Title} — {Body}", alert.Kind, title, body);

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                roles = new[] { FallbackRole },
                message = new
                {
                    title,
                    body,
                    url = MonitorUrl,
                    category = NotificationEvents.ServerPing,
                    eventCode = NotificationEvents.ServerPing,

                    // 같은 장비의 두절·복구는 기기에서 한 장으로 겹친다(복구가 두절 알림을 덮는다).
                    tag = $"server-ping:{targetIp}",
                    topic = $"server-ping-{targetIp.Replace('.', '-')}",
                    ttlSeconds = 3600,
                },
            });

            var http = httpFactory.CreateClient(NotifyClient);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/notifications/push")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-User-Id", "system:server-ping");

            using var response = await http.SendAsync(request, ct);

            // 202 = 앱 푸시도 메일도 한 통도 안 나갔다(구독 없음·이벤트 꺼짐 등). 까닭은 발송 이력에 남는다.
            if ((int)response.StatusCode == 202)
            {
                logger.LogWarning("내부 장비 Ping 알림: 받은 사람이 없습니다 ({Kind}).", alert.Kind);
            }
            else if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("내부 장비 Ping 알림 요청 실패: HTTP {Status} ({Kind})", (int)response.StatusCode, alert.Kind);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "내부 장비 Ping 알림 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }

    /// <summary>알림 글. 시각은 한국 시각으로 적는다.</summary>
    internal static (string Title, string Body) Compose(
        string targetIp, ServerPingAlert alert, DateTimeOffset now, ServerPingResultDto result)
    {
        var since = alert.DownSince ?? now;
        var sinceText = AppTime.ToKorea(since.UtcDateTime).ToString("MM-dd HH:mm");
        var elapsed = FormatDuration(now - since);

        return alert.Kind switch
        {
            ServerPingAlertKind.Down => (
                $"[서버 두절] 내부 장비 {targetIp} 응답 없음",
                $"{sinceText}부터 통신이 되지 않습니다. ({result.Message})"),
            ServerPingAlertKind.StillDown => (
                $"[서버 두절 지속] 내부 장비 {targetIp} {elapsed}째 응답 없음",
                $"{sinceText}부터 통신이 되지 않습니다. ({result.Message})"),
            ServerPingAlertKind.Recovered => (
                $"[서버 복구] 내부 장비 {targetIp} 통신 정상화",
                $"{AppTime.ToKorea(now.UtcDateTime):MM-dd HH:mm} 응답을 다시 받았습니다. 두절 시간 약 {elapsed} ({sinceText}부터)."),
            _ => (string.Empty, string.Empty),
        };
    }

    internal static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1)) return "1분 미만";
        var hours = (int)span.TotalHours;
        return hours > 0 ? $"{hours}시간 {span.Minutes}분" : $"{span.Minutes}분";
    }
}
