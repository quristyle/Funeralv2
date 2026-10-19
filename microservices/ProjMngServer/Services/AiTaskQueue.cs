using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using RabbitMQ.Client;

namespace ProjMngServer.Services;

/// <summary>AI 작업을 깨우는 종 — RabbitMQ <c>ai_task</c> 큐. 정본은 DB 이고 큐는 종이다. 메시지에는 <c>taskKey</c> 하나만 싣는다 — 지시문도 상태도 싣지 않는다.</summary>
public sealed class AiTaskQueue(IConfiguration configuration, ILogger<AiTaskQueue> logger)
{
    private readonly string _host =
        configuration["AiTasks:QueueHost"] is { Length: > 0 } h ? h : "localhost";

    private readonly string _queue =
        configuration["AiTasks:QueueName"] is { Length: > 0 } q ? q : "ai_task";

    /// <summary>큐를 durable 로 선언할지. 넣는 쪽과 집는 쪽이 같아야 한다 — 다르면 브로커가 <c>PRECONDITION_FAILED</c> 를 낸다.</summary>
    private readonly bool _durable = configuration.GetValue("AiTasks:Durable", true);

    /// <summary>종을 울릴지. 기능 자체의 켬/끔(<c>AiTasks:Enabled</c>)과 다른 값이다. 브로커가 없는 장비에서는 이것만 끈다 — 기능은 멀쩡히 돌고 지연이 조회 주기만큼 늘 뿐이다.</summary>
    private readonly bool _enabled =
        configuration.GetValue("AiTasks:Enabled", true)
        && configuration.GetValue("AiTasks:QueueEnabled", true);

    /// <summary>브로커를 기다리는 한도. 사람이 단추를 누른 길에서 떼어 놓았어도 무한정 매달리게 두지 않는다 — 못 닿으면 빨리 포기하고 로그를 남긴다.</summary>
    private readonly TimeSpan _ringTimeout =
        TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("AiTasks:RingTimeoutSeconds", 5)));

    /// <summary>아직 못 울린 종. 「요청」을 화면에서 떼어 놓는 자리다 — <see cref="RingAsync"/> 를 요청 처리 안에서 기다리면 AMQP 연결을 새로 여는 시간이 요청에 붙는다.</summary>
    private readonly Channel<long> _bells =
        Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>울릴 차례를 기다리는 종. <see cref="AiTaskBell"/> 만 읽는다.</summary>
    public ChannelReader<long> Bells => _bells.Reader;

    /// <summary>종을 울려 달라고 맡긴다 — 기다리지 않는다. 브로커와 이야기하는 일은 <see cref="AiTaskBell"/> 이 뒤에서 한다.</summary>
    public void Ring(long taskKey)
    {
        if (!_enabled)
        {
            logger.LogDebug("큐를 쓰지 않습니다. 실행기의 주기 조회가 집습니다. (task {TaskKey})", taskKey);
            return;
        }

        // 무제한 채널이라 닫히지 않는 한 실패하지 않는다. 그래도 값을 본다 —
        // 못 넣었다면 그 사실이 로그에 남아야 「왜 1분 뒤에 도나」를 설명한다.
        if (!_bells.Writer.TryWrite(taskKey))
        {
            logger.LogWarning("종을 맡기지 못했습니다. 실행기의 주기 조회가 집을 것입니다. (task {TaskKey})", taskKey);
        }
    }

    /// <summary>「이 번호를 봐라」를 큐에 넣는다. 실패해도 던지지 않는다 — 요청은 이미 DB 에 적혀 있고 실행기의 안전망 폴링이 집으며, 던지면 저장은 됐는데 실패라고 말하는 어긋남이 생긴다.</summary>
    public async Task<bool> RingAsync(long taskKey, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            logger.LogDebug("큐를 쓰지 않습니다. 실행기의 주기 조회가 집습니다. (task {TaskKey})", taskKey);
            return false;
        }

        try
        {
            // **기다리는 데 한도를 둔다.** 기본값(30초)으로 두면 브로커에 못 닿는
            // 동안 이 한 번이 종 줄 전체를 붙잡는다.
            var factory = new ConnectionFactory
            {
                HostName = _host,
                RequestedConnectionTimeout = _ringTimeout,
                SocketReadTimeout = _ringTimeout,
                SocketWriteTimeout = _ringTimeout,
            };

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(_ringTimeout);
            ct = limit.Token;

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
