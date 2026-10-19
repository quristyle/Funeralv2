using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 오래된 작업공간을 치운다. 복사본(폴더 대상)은 끝나도 지우지 않는다 — 그 폴더가 결과의 전부라서.
/// 기본 14일이 지난 것만 지우고, 지울 때마다 로그를 남긴다.
/// </summary>
public sealed class WorkspaceSweeper(
    IOptions<RunnerOptions> optionsAccessor, ILogger<WorkspaceSweeper> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var keep = TimeSpan.FromDays(Math.Max(1, _options.KeepWorkspaceDays));

        logger.LogInformation("작업공간 청소: {Days}일 지난 것을 치웁니다.", keep.TotalDays);

        // 뜨자마자 돌리지 않는다. 기동 직후는 밀린 작업을 집는 때라 디스크 훑기를 겹치지 않는다.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Sweep(keep);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "작업공간 청소 한 바퀴가 실패했습니다.");
            }
        }
    }

    private void Sweep(TimeSpan keep)
    {
        if (!Directory.Exists(_options.WorkspaceRoot))
        {
            return;
        }

        var deadline = DateTime.UtcNow - keep;
        var removed = 0;

        foreach (var dir in Directory.EnumerateDirectories(_options.WorkspaceRoot))
        {
            var name = Path.GetFileName(dir);

            // 실행마다 만든 것만 본다. 사람이 둔 폴더를 건드리지 않는다.
            if (!name.StartsWith("run-", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) > deadline)
                {
                    continue;
                }

                Directory.Delete(dir, recursive: true);
                removed++;

                logger.LogInformation("오래된 작업공간을 치웠습니다: {Dir}", dir);
            }
            catch (Exception ex)
            {
                logger.LogWarning("치우지 못했습니다 ({Dir}): {Message}", dir, ex.Message);
            }
        }

        // 지시문 파일도 같이. 작업공간 밖에 두므로 따로 본다.
        var prompts = Path.Combine(_options.WorkspaceRoot, "prompts");

        if (Directory.Exists(prompts))
        {
            foreach (var file in Directory.EnumerateFiles(prompts, "*.md"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) <= deadline)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // 한 파일이 안 지워졌다고 나머지를 멈추지 않는다.
                }
            }
        }

        if (removed > 0)
        {
            logger.LogInformation("작업공간 {Count}개를 치웠습니다.", removed);
        }
    }
}
