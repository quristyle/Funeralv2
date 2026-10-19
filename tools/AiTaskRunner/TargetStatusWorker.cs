using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 대상들의 git 상태를 주기적으로 들여다보고 서버에 적어 둔다. 서버는 호스트 경로를 볼 수 없어 여기 있다(설계 11.7).
/// 작업이 도는 대상도 건너뛰지 않는다 — 읽기만 하고(--no-optional-locks) 작업을 방해하지 않는다.
/// </summary>
public sealed class TargetStatusWorker(
    IOptions<RunnerOptions> optionsAccessor,
    ServerClient server,
    GitProbe probe,
    ILogger<TargetStatusWorker> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    /// <summary>한 바퀴의 간격. 보는 주기가 아니라 「볼 때가 됐나」를 확인하는 주기다(실제 간격은 RunnerOptions.StatusSeconds).</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RunnerToken))
        {
            // 본체가 이미 LogCritical 로 말했다. 여기서 또 지르지 않는다.
            return;
        }

        if (_options.StatusSeconds <= 0)
        {
            logger.LogInformation("Runner:StatusSeconds 가 0 이라 대상 상태를 보지 않습니다.");
            return;
        }

        logger.LogInformation(
            "대상 상태 감시: {Sec}초마다 봅니다 (확인 요청은 {Tick}초 안에 받습니다).",
            _options.StatusSeconds, Tick.TotalSeconds);

        // 뜨자마자 한 번 본다 — 기동 직후가 사람이 제일 궁금해하는 때다.
        using var timer = new PeriodicTimer(Tick);

        do
        {
            try
            {
                await RoundAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // 감시가 멈추면 화면이 옛 값을 보여 준다. 한 바퀴가 실패해도 다음 바퀴는 돈다.
                logger.LogWarning(ex, "대상 상태 한 바퀴가 실패했습니다. 계속 돕니다.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// 한 바퀴 — 볼 것을 고르고, 보고, 올린다. 고르는 기준은 한 번도 안 봤거나, 화면이 「지금 확인」을 눌렀거나, 마지막으로 본 지 StatusSeconds 가 지났거나.
    /// 「지금 확인」은 화면이 대개 무언가를 고친 직후에 열리기 때문에 있다.
    /// </summary>
    private async Task RoundAsync(CancellationToken ct)
    {
        var targets = await server.ProbeTargetsAsync(ct);

        if (targets.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var due = TimeSpan.FromSeconds(_options.StatusSeconds);

        var picked = targets
            .Where(t => t.ProbeRequested
                        || t.ProbedAt is null
                        || now - t.ProbedAt.Value >= due)
            .ToList();

        if (picked.Count == 0)
        {
            return;
        }

        var snapshots = new List<GitProbe.Snapshot>(picked.Count);

        foreach (var t in picked)
        {
            // 하나씩 본다 — 한꺼번에 띄우면 git 프로세스 열이 디스크를 동시에 훑어 정작 일하는 작업의 발목을 잡는다.
            var snap = await probe.ProbeAsync(t.TargetPath, ct);

            snap.TargetKey = t.TargetKey;
            snapshots.Add(snap);
        }

        await server.ReportTargetStatusAsync(snapshots, ct);

        logger.LogDebug("대상 {Count}건의 상태를 보고했습니다.", snapshots.Count);
    }
}
