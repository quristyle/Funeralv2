using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업을 깨우는 종 — RabbitMQ <c>ai_task</c> 큐.
/// </summary>
/// <remarks>
/// <para>
/// <b>정본은 DB 다. 큐는 종(bell)이다</b>(설계 6.4). 메시지에는
/// <c>taskKey</c> 하나만 싣는다 — 지시문도 상태도 싣지 않는다.
/// </para>
/// <para>
/// 그래서 이중 쓰기 함정이 없다. 메시지를 잃으면 실행기의 안전망 폴링이
/// 최대 1분 뒤에 집고, 두 번 오면 <c>claim</c> 이 빈손을 준다. 큐가
/// 신뢰성을 책임지지 않는 대신 DB 가 책임진다.
/// </para>
/// <para>
/// <b><c>run_script</c> 큐에 얹지 않는다.</b> 그 큐는 배포와 헬프데스크 메일이
/// 이미 나눠 쓰고 있어 「넣을 수 있으면 무엇이든 실행시킬 수 있는」 상태이고,
/// 30분짜리 AI 작업이 <c>prefetch</c> 를 잡고 있으면 <b>배포가 줄을 선다.</b>
/// </para>
/// <para>
/// 발행 코드는 <c>AuthServer.ReleaseService.PublishAsync</c> 와 같은 모양이다.
/// 그쪽이 먼저 자리를 잡았고, 여기서 새로 발명할 것이 없다.
/// </para>
/// </remarks>
public sealed class AiTaskQueue(IConfiguration configuration, ILogger<AiTaskQueue> logger)
{
    private readonly string _host =
        configuration["AiTasks:QueueHost"] is { Length: > 0 } h ? h : "localhost";

    private readonly string _queue =
        configuration["AiTasks:QueueName"] is { Length: > 0 } q ? q : "ai_task";

    /// <summary>
    /// 큐를 <c>durable</c> 로 선언할지. <b>넣는 쪽과 집는 쪽이 같아야 한다</b> —
    /// 다르면 브로커가 <c>PRECONDITION_FAILED</c> 를 낸다.
    /// </summary>
    private readonly bool _durable = configuration.GetValue("AiTasks:Durable", true);

    /// <summary>
    /// 종을 울릴지. <b>기능 자체의 켬/끔(<c>AiTasks:Enabled</c>)과 다른 값이다.</b>
    /// </summary>
    /// <remarks>
    /// 브로커가 없는 장비(로컬 개발)에서는 이것만 끈다. 요청은 그대로
    /// 저장되고 실행기의 주기 조회가 집으므로 <b>기능은 멀쩡히 돈다</b> —
    /// 지연이 조회 주기만큼 늘 뿐이다(설계 6.6).
    /// </remarks>
    private readonly bool _enabled =
        configuration.GetValue("AiTasks:Enabled", true)
        && configuration.GetValue("AiTasks:QueueEnabled", true);

    /// <summary>
    /// 「이 번호를 봐라」를 큐에 넣는다.
    /// </summary>
    /// <remarks>
    /// <b>실패해도 던지지 않는다.</b> 브로커가 내려가 있어도 요청은 이미 DB 에
    /// 적혀 있고, 실행기의 안전망 폴링이 그것을 집는다. 여기서 던지면
    /// 「요청은 저장됐는데 화면은 실패라고 말하는」 어긋남이 생긴다.
    /// </remarks>
    // 던지지 않는 이유: 요청은 이미 DB 에 있고 폴링이 집으므로, 종 실패는 지연일 뿐 실패가 아니다.
    public async Task<bool> RingAsync(long taskKey, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            logger.LogDebug("큐를 쓰지 않습니다. 실행기의 주기 조회가 집습니다. (task {TaskKey})", taskKey);
            return false;
        }

        try
        {
            var factory = new ConnectionFactory { HostName = _host };

            await using var connection = await factory.CreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.QueueDeclareAsync(
                queue: _queue,
                durable: _durable,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { taskKey }));

            var props = new BasicProperties();

            if (_durable)
            {
                // durable 큐일 때만 뜻이 있다. non-durable 큐에 persistent 를 붙여도
                // 브로커가 재시작되면 큐 자체가 사라진다.
                props.Persistent = true;
            }

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queue,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            return true;
        }
        catch (Exception ex)
        {
            // 남기되 막지 않는다. 안전망 폴링이 늦게라도 집는다.
            logger.LogWarning(ex,
                "큐에 넣지 못했습니다. 실행기의 주기 조회가 집을 것입니다. (task {TaskKey})", taskKey);

            return false;
        }
    }
}
