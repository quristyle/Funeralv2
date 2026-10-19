using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 「처리 파일」의 추정 줄을 실제 파일에 대 보고 서버에 적는다. 경로가 호스트의 것이라 TargetStatusWorker 와 같은 까닭으로 여기 있다.
/// 실행기 본체와 한 바퀴를 같이 돌지 않는다 — 일이 몰리는 때 더 자주 도는 것을 막는다. 빈 바퀴가 대부분이라 로그는 Debug.
/// </summary>
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
                // 실패해도 다음 바퀴는 돈다. 못 본 줄은 「아직」인 채 남아 다시 물린다.
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
            // 하나씩 본다 — 한꺼번에 띄우면 git 프로세스가 디스크를 동시에 훑는다.
            var answers = await probe.CheckAsync(
                job.TargetPath, job.WorkspacePath, job.GitBranch, job.Paths, ct);

            if (answers.Count == 0)
            {
                // 아무 말도 하지 않는다 — 없다고 적으면 다른 장비의 멀쩡한 파일이 없는 경로가 된다.
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
