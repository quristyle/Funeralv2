using AuthServer.Data;
using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 보고서 메일 발송기 — 때가 된 배치를 찾아 한 통씩 내보낸다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 AuthServer 인가]
/// </para>
///
/// <para>
/// 배치 줄과 역할·메뉴가 전부 이 서비스의 DB(<c>scom</c>)에 있다. 발송기를
/// 다른 서비스에 두면 그 표를 둘이 알게 되고, 읽기만 하려고 붙인 연결이 결국
/// 쓰기로 자란다. 보내는 일 자체는 여기서 하지 않는다 — 역할 이름을 그대로
/// NotificationServer 에 넘긴다(<see cref="ReportMailSender"/>).
/// </para>
///
/// <para>
/// [아무 줄도 없으면 아무 일도 안 한다]
/// </para>
///
/// <para>
/// 이 발송기가 켜져 있다고 메일이 나가지는 않는다. <b>사람이 배치를 만들고
/// 「사용」으로 둔 것만</b> 나간다. 그래서 올라가자마자 누군가의 메일함이
/// 울리는 일은 없고, 그래도 멈출 수 있어야 하므로 설정 하나를 둔다
/// (<c>ReportMail:Enabled</c>). 운영에서 메일이 쏟아질 때 DB 를 고치지 않고
/// 끌 수 있어야 한다.
/// </para>
///
/// <para>
/// [5분마다 깨어나되 한 칸에 한 번만 보낸다]
/// </para>
///
/// <para>
/// 고른 시각이 08:00 이면 08:00~08:55 의 어느 깨어남에서 한 번 나간다.
/// 그 칸에 이미 보냈는지는 <b>표에 적어 둔</b> <c>last_sent_at</c> 으로 가린다 —
/// 메모리에 기억하면 서비스를 다시 띄울 때마다 또 간다
/// (<c>LocalWeatherNotifyService</c> 가 같은 자리에서 같은 판단을 했다).
/// </para>
///
/// <para>
/// [실패해도 시각을 찍는다]
/// </para>
///
/// <para>
/// 보내기에 실패해도 <c>last_sent_at</c> 을 찍는다. 안 찍으면 <b>5분마다
/// 다시 시도</b>하게 되고, 받는 사람이 없는 배치 하나가 유예 세 시간 동안
/// 서른여섯 번의 실패 로그를 쌓는다. 까닭은 <c>last_result</c> 에 적히고
/// 화면이 그 줄을 보여 준다 — 고칠 사람이 읽을 자리는 거기다.
/// </para>
/// </remarks>
public class ReportMailDispatcher(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<ReportMailDispatcher> logger) : BackgroundService
{
    /// <summary>깨어나는 간격. 시각 칸 판정이 있어 촘촘해도 중복 발송이 없다.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>기동 직후에는 한 박자 쉰다 — DB 와 설정이 자리를 잡은 뒤에 본다.</summary>
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(40);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("ReportMail:Enabled", true))
        {
            logger.LogInformation("보고서 메일 발송기가 꺼져 있습니다 (ReportMail:Enabled = false).");
            return;
        }

        logger.LogInformation("보고서 메일 발송기 시작 — {Minutes}분마다 확인", Interval.TotalMinutes);

        try { await Task.Delay(Warmup, stoppingToken); }
        catch (OperationCanceledException) { return; }

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
                logger.LogError(ex, "보고서 메일 발송 중 오류");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>한 바퀴. 때가 된 배치를 찾아 보낸다.</summary>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 켜져 있는 것만 읽는다. 꺼 둔 배치는 몇 개든 비용이 들지 않아야 한다.
        var active = await db.Set<ReportMailSchedule>()
            .Include(s => s.Reports)
            .Include(s => s.Roles)
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        if (active.Count == 0) return;

        var now = AppTime.UtcNow;
        var due = active.Where(s => ReportMailSchedulePlan.IsDue(s, now)).ToList();

        if (due.Count == 0) return;

        var service = scope.ServiceProvider.GetRequiredService<IReportMailService>();
        var sender = scope.ServiceProvider.GetRequiredService<ReportMailSender>();

        var catalog = await service.GetCatalogAsync(ct);

        foreach (var schedule in due)
        {
            if (ct.IsCancellationRequested) break;

            var (ok, message) = await sender.SendAsync(schedule, catalog, "SCHEDULER", ct);

            // 성공이든 실패든 찍는다 — 머리말 참고.
            // `updated_by` 는 여기서 적지 않는다. DbContext 가 저장 직전에
            // 덮어쓴다(`ReportMailService` 머리말) — 발송기가 한 일이라는 것은
            // 이 로그와 `last_result` 가 말한다.
            schedule.LastSentAt = AppTime.UtcNow;
            schedule.LastResult = message;

            if (!ok)
            {
                logger.LogWarning(
                    "보고서 메일 배치 실패: {Name} ({Id}) — {Message}",
                    schedule.Name, schedule.Id, message);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
