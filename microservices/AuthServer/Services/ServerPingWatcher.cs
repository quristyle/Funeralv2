using System.Text;
using System.Text.Json;
using AuthServer.Data;
using AuthServer.Endpoints;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 내부 장비 Ping 감시기 — 주기마다 Ping 을 쳐서 두절·되풀이·복구를 <c>SERVER_PING</c> 이벤트로 알린다.
/// DB(scom.server_ping_targets)에 등록된 활성 장비들을 동적으로 읽어오며, 각 장비별로 독립된 실패 횟수 및 두절 상태를 유지한다.
/// 새 화면에서 장비가 추가·수정·비활성화되면 다음 주기에 자동으로 반영된다.
/// 받는 사람과 길(앱 푸시·메일)은 알림관리 정책이 정하고, 여기서 싣는 역할은 정책이 없을 때의 받는 사람이다.
/// 상태는 메모리에만 있어 두절 중에 서버가 다시 뜨면 두절 알림이 한 번 더 간다.
/// </summary>
public sealed class ServerPingWatcher(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ServerPingWatcher> logger) : BackgroundService
{
    /// <summary>알림 서버를 부르는 통 이름.</summary>
    public const string NotifyClient = "server-ping-notify";

    /// <summary>정책이 역할을 안 걸었을 때 받는 역할.</summary>
    private const string FallbackRole = "SYSTEM_ADMINISTRATOR";

    private const string MonitorUrl = "/admin/status/server-ping";

    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(30);

    private sealed class TargetWatchState(string ip, string name, int timeoutMs, ServerPingAlertPlan plan)
    {
        public string Ip { get; } = ip;
        public string Name { get; set; } = name;
        public int TimeoutMs { get; set; } = timeoutMs;
        public ServerPingAlertPlan Plan { get; } = plan;
    }

    // IP 기준 감시 상태 맵 (연속 실패 횟수 및 두절 시점 유지)
    private readonly Dictionary<string, TargetWatchState> _targetStates = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = configuration.GetSection("ServerPing");

        if (!section.GetValue("Enabled", true))
        {
            logger.LogInformation("내부 장비 Ping 감시기가 꺼져 있습니다 (ServerPing:Enabled = false).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, section.GetValue("IntervalSeconds", 30)));
        var defaultTimeoutMs = Math.Clamp(section.GetValue("TimeoutMs", 2000), 500, 10000);
        var failThreshold = section.GetValue("FailThreshold", 3);
        var repeatMinutes = section.GetValue("RepeatMinutes", 60);

        logger.LogInformation(
            "내부 장비 Ping 감시기 시작 — {Seconds}초마다 · 연속 {Threshold}회 실패 시 두절 · 되풀이 {Repeat}분 (DB scom.server_ping_targets 연동)",
            interval.TotalSeconds,
            failThreshold,
            repeatMinutes);

        try { await Task.Delay(Warmup, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);

        do
        {
            // 매 주기마다 DB 및 설정을 통해 활성 장비 목록 갱신
            await SyncWatchTargetsAsync(section, defaultTimeoutMs, failThreshold, repeatMinutes, stoppingToken);

            var targets = _targetStates.Values.ToList();

            foreach (var target in targets)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var result = await ServerPingEndpoints.PingServerAsync(target.Ip, target.TimeoutMs, stoppingToken, target.Name);
                    var now = AppTime.UtcNowOffset;
                    var alert = target.Plan.Observe(result.IsAlive, now);

                    if (alert.Kind != ServerPingAlertKind.None)
                    {
                        await NotifyAsync(target.Ip, target.Name, alert, now, result, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 특정 장비 검사 중 오류가 나도 다른 장비는 돌아야 한다.
                    logger.LogError(ex, "내부 장비({Name} {Ip}) Ping 감시 중 오류", target.Name, target.Ip);
                }
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task SyncWatchTargetsAsync(
        IConfigurationSection section, int defaultTimeoutMs, int failThreshold, int repeatMinutes, CancellationToken ct)
    {
        var activeIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. DB (scom.server_ping_targets) 조회
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbTargets = await db.ServerPingTargets
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .ToListAsync(ct);

            foreach (var item in dbTargets)
            {
                var ip = item.Ip.Trim();
                if (string.IsNullOrEmpty(ip)) continue;

                activeIps.Add(ip);
                var timeoutMs = item.TimeoutMs > 0 ? item.TimeoutMs : defaultTimeoutMs;
                var name = !string.IsNullOrWhiteSpace(item.Name) ? item.Name.Trim() : ServerPingEndpoints.GetDefaultDeviceName(ip);

                if (_targetStates.TryGetValue(ip, out var existing))
                {
                    existing.Name = name;
                    existing.TimeoutMs = timeoutMs;
                }
                else
                {
                    _targetStates[ip] = new TargetWatchState(
                        ip, name, timeoutMs, new ServerPingAlertPlan(failThreshold, TimeSpan.FromMinutes(repeatMinutes)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ping 대상 장비 DB 목록 조회 실패 — 기존 등록 장비 및 설정 파일로 유지합니다.");
        }

        // DB에 장비가 하나도 없거나 DB 접속 실패 시 appsettings.json fallback
        if (activeIps.Count == 0)
        {
            var fallbackTargets = ResolveFallbackTargets(section, defaultTimeoutMs, failThreshold, repeatMinutes);
            foreach (var fb in fallbackTargets)
            {
                activeIps.Add(fb.Ip);
                if (_targetStates.TryGetValue(fb.Ip, out var existing))
                {
                    existing.Name = fb.Name;
                    existing.TimeoutMs = fb.TimeoutMs;
                }
                else
                {
                    _targetStates[fb.Ip] = fb;
                }
            }
        }

        // 비활성화되었거나 삭제된 장비는 모니터링 목록에서 제거
        var removedIps = _targetStates.Keys.Where(ip => !activeIps.Contains(ip)).ToList();
        foreach (var ip in removedIps)
        {
            _targetStates.Remove(ip);
        }
    }

    private static List<TargetWatchState> ResolveFallbackTargets(
        IConfigurationSection section, int defaultTimeoutMs, int failThreshold, int repeatMinutes)
    {
        var targets = new List<TargetWatchState>();
        var seenIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var targetsSection = section.GetSection("Targets");
        if (targetsSection.Exists())
        {
            foreach (var child in targetsSection.GetChildren())
            {
                var ip = child["Ip"]?.Trim();
                if (string.IsNullOrEmpty(ip) || !seenIps.Add(ip)) continue;

                var name = child["Name"]?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = ServerPingEndpoints.GetDefaultDeviceName(ip);
                }

                targets.Add(new TargetWatchState(
                    ip, name, defaultTimeoutMs, new ServerPingAlertPlan(failThreshold, TimeSpan.FromMinutes(repeatMinutes))));
            }
        }

        // 기본 장비: 그리드 중계 장비(192.168.219.229) & jin sm 장비(192.168.219.224)
        if (seenIps.Add(ServerPingEndpoints.DefaultTargetIp))
        {
            targets.Add(new TargetWatchState(
                ServerPingEndpoints.DefaultTargetIp,
                ServerPingEndpoints.GetDefaultDeviceName(ServerPingEndpoints.DefaultTargetIp),
                defaultTimeoutMs,
                new ServerPingAlertPlan(failThreshold, TimeSpan.FromMinutes(repeatMinutes))));
        }

        if (seenIps.Add(ServerPingEndpoints.JinSmTargetIp))
        {
            targets.Add(new TargetWatchState(
                ServerPingEndpoints.JinSmTargetIp,
                ServerPingEndpoints.GetDefaultDeviceName(ServerPingEndpoints.JinSmTargetIp),
                defaultTimeoutMs,
                new ServerPingAlertPlan(failThreshold, TimeSpan.FromMinutes(repeatMinutes))));
        }

        return targets;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    /// <summary>알림 서버의 /notifications/push 로 보낸다. 메일은 이벤트의 email_from_push 로 저쪽이 함께 낸다.</summary>
    private async Task NotifyAsync(
        string targetIp, string deviceName, ServerPingAlert alert, DateTimeOffset now, ServerPingResultDto result, CancellationToken ct)
    {
        var (title, body) = Compose(targetIp, deviceName, alert, now, result);

        logger.LogWarning("내부 장비 Ping 알림 [{Kind}] {Title} — {Body}", alert.Kind, title, body);

        var client = httpFactory.CreateClient(NotifyClient);

        // 받는 사람은 알림관리 정책에 맡긴다 — 정책이 비었을 때만 FallbackRole(SYSTEM_ADMINISTRATOR) 로 간다.
        var payload = new
        {
            category = NotificationEvents.ServerPing,
            eventCode = NotificationEvents.ServerPing,
            title,
            body,
            linkUrl = MonitorUrl,
            roles = new[] { FallbackRole },
        };

        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var resp = await client.PostAsync("/notifications/push", content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var errorText = await resp.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "내부 장비 Ping 알림 발송 실패 (HTTP {Status}): {Error}",
                    (int)resp.StatusCode, errorText);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "내부 장비 Ping 알림 발송 중 통신 실패");
        }
    }

    /// <summary>알림 글. 시각은 한국 시각으로 적는다.</summary>
    internal static (string Title, string Body) Compose(
        string targetIp, string deviceName, ServerPingAlert alert, DateTimeOffset now, ServerPingResultDto result)
    {
        var label = string.IsNullOrWhiteSpace(deviceName) ? targetIp : $"{deviceName}({targetIp})";
        var since = alert.DownSince ?? now;
        var sinceText = AppTime.ToKorea(since.UtcDateTime).ToString("MM-dd HH:mm");
        var elapsed = FormatDuration(now - since);

        return alert.Kind switch
        {
            ServerPingAlertKind.Down => (
                $"[서버 두절] {label} 응답 없음",
                $"{sinceText}부터 통신이 되지 않습니다. ({result.Message})"),
            ServerPingAlertKind.StillDown => (
                $"[서버 두절 지속] {label} {elapsed}째 응답 없음",
                $"{sinceText}부터 통신이 되지 않습니다. ({result.Message})"),
            ServerPingAlertKind.Recovered => (
                $"[서버 복구] {label} 통신 정상화",
                $"{AppTime.ToKorea(now.UtcDateTime):MM-dd HH:mm} 응답을 다시 받았습니다. 두절 시간 약 {elapsed} ({sinceText}부터)."),
            _ => (string.Empty, string.Empty),
        };
    }

    internal static (string Title, string Body) Compose(
        string targetIp, ServerPingAlert alert, DateTimeOffset now, ServerPingResultDto result)
        => Compose(targetIp, ServerPingEndpoints.GetDefaultDeviceName(targetIp), alert, now, result);

    internal static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1)) return "1분 미만";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes}분";
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;
        return minutes == 0 ? $"{hours}시간" : $"{hours}시간 {minutes}분";
    }
}
