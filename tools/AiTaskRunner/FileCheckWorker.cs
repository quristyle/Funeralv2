using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 「처리 파일」의 <b>추정 줄을 실제 파일에 대 보고</b> 서버에 적어 둔다.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TargetStatusWorker"/> 와 짝이고 <b>같은 까닭으로 여기 있다</b> —
/// 서버(ProjMngServer)는 컨테이너 안에서 돌고 대상 경로는 호스트의 것이다.
/// </para>
/// <para>
/// <b>실행기 본체와 한 바퀴를 같이 돌지 않는다.</b> 본체의 주기는 「집을 일이
/// 있나」고 큐가 울리면 그것을 건너뛴다 — 거기에 얹으면 일이 몰릴 때 가장
/// 자주 돌고 한가할 때 안 돈다.
/// </para>
/// <para>
/// <b>빈손이 기본이다.</b> 대 볼 것은 작업이 끝난 직후에만 생기므로 거의
/// 모든 바퀴가 빈 목록을 받는다 — 그래서 로그를 Debug 로 둔다.
/// </para>
/// </remarks>
public sealed class FileCheckWorker(
    IOptions<RunnerOptions> optionsAccessor,
    ServerClient server,
    FileProbe probe,
    ILogger<FileCheckWorker> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RunnerToken))
        {
            // 본체가 이미 LogCritical 로 말했다. 여기서 또 지르지 않는다.
            return;
        }

        if (_options.FileCheckSeconds <= 0)
        {
            logger.LogInformation("Runner:FileCheckSeconds 가 0 이라 처리 파일을 대 보지 않습니다.");
            return;
        }

        logger.LogInformation(
            "처리 파일 확인: {Sec}초마다 결과문에서 뽑은 경로를 실제 파일에 대 봅니다.",
            _options.FileCheckSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.FileCheckSeconds));

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
                // 한 바퀴가 실패해도 다음 바퀴는 돈다. 못 대 본 줄은
                // 「아직」인 채 남아 다시 물린다.
                logger.LogWarning(ex, "처리 파일 확인 한 바퀴가 실패했습니다. 계속 돕니다.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RoundAsync(CancellationToken ct)
    {
        var jobs = await server.FileCheckJobsAsync(ct);

        if (jobs.Count == 0)
        {
            return;
        }

        var reports = new List<ServerClient.FileCheckReport>(jobs.Count);

        foreach (var job in jobs)
        {
            // **하나씩 본다.** 한꺼번에 띄우면 git 프로세스 열이 동시에
            // 디스크를 훑는다 — 대상 상태 감시가 그렇게 하는 것과 같은 규칙이다.
            var answers = await probe.CheckAsync(
                job.TargetPath, job.WorkspacePath, job.GitBranch, job.Paths, ct);

            if (answers.Count == 0)
            {
                // 이 장비의 대상이 아니다. **아무 말도 하지 않는다** — 없다고
                // 적으면 다른 장비에서 멀쩡히 고친 파일이 「없는 경로」가 된다.
                continue;
            }

            reports.Add(new ServerClient.FileCheckReport
            {
                RunKey = job.RunKey,
                Results =
                [
                    .. answers.Select(a => new ServerClient.FileCheckAnswer
                    {
                        Raw = a.Raw,
                        Path = a.Path,
                        State = a.Result switch
                        {
                            FileProbe.State.Found => "found",
                            FileProbe.State.Ambiguous => "ambiguous",
                            _ => "missing",
                        },
                    }),
                ],
            });
        }

        await server.ReportFileChecksAsync(reports, ct);

        if (reports.Count > 0)
        {
            logger.LogInformation(
                "처리 파일을 실행 {Count}건에 대 봤습니다 ({Rows}줄).",
                reports.Count, reports.Sum(r => r.Results.Count));
        }
    }
}
