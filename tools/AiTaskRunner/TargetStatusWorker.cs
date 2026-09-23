using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 대상들을 주기적으로 들여다보고 서버에 적어 둔다.
/// </summary>
/// <remarks>
/// <para>
/// 「대상 git 상태」 화면(<c>/projmng/ai/target-status</c>)이 읽는 값을 만드는
/// 쪽이다. <b>서버가 할 수 없는 일이라 여기 있다</b> — ProjMngServer 는
/// 컨테이너 안에서 돌고 대상 경로는 호스트의 것이다(설계 11.7).
/// </para>
/// <para>
/// <b>실행기 본체와 한 바퀴를 같이 돌지 않는다.</b> <see cref="RunnerWorker"/>
/// 의 주기는 「집을 일이 있나」를 묻는 주기(기본 60초)고 큐가 울리면 그것을
/// 건너뛴다. 감시를 거기 얹으면 <b>일이 몰릴 때 가장 자주 돌고 한가할 때
/// 안 돈다</b> — 필요한 것과 정확히 반대다.
/// </para>
/// <para>
/// 여기서 하는 일은 전부 읽기다(<see cref="GitProbe"/>). <b>작업이 도는
/// 대상도 건너뛰지 않는다</b> — 그때의 상태가 사람이 제일 보고 싶어 하는
/// 것이고, 읽기는 그 작업을 방해하지 않는다(<c>--no-optional-locks</c>).
/// </para>
/// </remarks>
public sealed class TargetStatusWorker(
    IOptions<RunnerOptions> optionsAccessor,
    ServerClient server,
    GitProbe probe,
    ILogger<TargetStatusWorker> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    /// <summary>
    /// 한 바퀴의 간격. <b>보는 주기가 아니라 「볼 때가 됐나」를 확인하는 주기</b>다.
    /// </summary>
    /// <remarks>
    /// 실제로 들여다보는 간격은 <see cref="RunnerOptions.StatusSeconds"/> 고,
    /// 이 짧은 바퀴는 화면의 <b>「지금 확인」을 빨리 받기 위한 것</b>이다 —
    /// 그 표시는 DB 에 찍히므로 물어보지 않으면 알 수 없다.
    /// </remarks>
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

        // 뜨자마자 한 번 본다. **기동 직후가 사람이 제일 궁금해하는 때다** —
        // 실행기를 올려 놓고 화면을 여는 순서로 일이 벌어진다.
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
                // 감시가 멈추면 화면이 옛 값을 계속 보여 준다. 한 바퀴가
                // 실패해도 다음 바퀴는 돈다.
                logger.LogWarning(ex, "대상 상태 한 바퀴가 실패했습니다. 계속 돕니다.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// 한 바퀴 — 볼 것을 고르고, 보고, 올린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 고르는 기준이 셋이다. <b>한 번도 안 봤거나</b>, 화면이 <b>「지금 확인」을
    /// 눌렀거나</b>, 마지막으로 본 지 <see cref="RunnerOptions.StatusSeconds"/>
    /// 가 지났거나.
    /// </para>
    /// <para>
    /// 「지금 확인」이 필요한 이유는 이 화면이 대개 <b>무언가를 고친 직후</b>에
    /// 열리기 때문이다. 주기만 두면 사람이 화면을 보며 몇 분을 기다린다.
    /// </para>
    /// </remarks>
    private async Task RoundAsync(CancellationToken ct)
    {
        var targets = await server.ProbeTargetsAsync(ct);

        if (targets.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
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
            // **하나씩 본다.** 한꺼번에 띄우면 대상이 열이고 저장소가 크면
            // git 프로세스 열이 동시에 디스크를 훑는다 — 이 감시가 정작
            // 일하는 작업의 발목을 잡는 그림이다.
            var snap = await probe.ProbeAsync(t.TargetPath, ct);

            snap.TargetKey = t.TargetKey;
            snapshots.Add(snap);
        }

        await server.ReportTargetStatusAsync(snapshots, ct);

        logger.LogDebug("대상 {Count}건의 상태를 보고했습니다.", snapshots.Count);
    }
}
