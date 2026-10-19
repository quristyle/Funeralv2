namespace ProjMngServer.Services;

/// <summary>맡아 둔 종을 뒤에서 울리는 일꾼. 「요청」은 DB 에 한 문장을 적는 것으로 이미 끝나고, 종은 실행기를 빨리 깨우는 편의일 뿐이다 — 그래서 사람이 기다리는 길에서 브로커를 떼어 놓는다.</summary>
public sealed class AiTaskBell(AiTaskQueue queue, ILogger<AiTaskBell> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AI 작업 종지기 시작.");

        try
        {
            await foreach (var taskKey in queue.Bells.ReadAllAsync(stoppingToken))
            {
                // RingAsync 는 스스로 삼킨다 — 여기까지 예외가 올라오지 않는다.
                // 그래도 감싼다. 이 반복문이 죽으면 **다음 종부터 전부** 안 울린다.
                try
                {
                    await queue.RingAsync(taskKey, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "종을 울리지 못했습니다. (task {TaskKey})", taskKey);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 서비스가 내려간다. 정상이다 — 남은 종은 실행기의 폴링이 집는다.
        }
    }
}
