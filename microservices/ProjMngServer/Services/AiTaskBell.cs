namespace ProjMngServer.Services;

/// <summary>
/// 맡아 둔 종을 뒤에서 울리는 일꾼.
/// </summary>
/// <remarks>
/// <para>
/// <b>사람이 기다리는 길에서 브로커를 떼어 놓으려고 둔다.</b> 「요청」은 DB 에
/// 한 문장을 적는 일이고 그것으로 이미 끝이다 — 종은 실행기를 <i>빨리</i>
/// 깨우는 편의일 뿐이라(설계 6.6), 그 편의 때문에 화면이 「보내는 중」으로
/// 묶여 있을 이유가 없다.
/// </para>
/// <para>
/// <b>한 번에 하나씩 울린다.</b> 종 하나가 AMQP 연결을 새로 여닫는 일이라,
/// 동시에 여러 개를 열면 브로커에 연결만 쌓인다. 줄을 세워도 사람은 기다리지
/// 않고, 실행기는 종이 늦게 와도 폴링으로 집는다.
/// </para>
/// <para>
/// 못 울린 종은 <b>다시 시도하지 않는다.</b> 재시도는 실행기의 안전망 폴링이
/// 이미 하고 있고(최대 1분), 여기서 또 하면 「같은 작업을 두 번 집어 가나」를
/// 두 곳에서 따져야 한다.
/// </para>
/// </remarks>
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
