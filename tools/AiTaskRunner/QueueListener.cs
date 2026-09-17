using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AiTaskRunner;

/// <summary>
/// 종을 듣는다 — RabbitMQ <c>ai_task</c> 큐.
/// </summary>
/// <remarks>
/// <para>
/// <b>메시지를 읽지도 않는다.</b> 본문에는 <c>taskKey</c> 하나뿐이고, 실제로
/// 무엇을 할지는 서버의 <c>claim</c> 이 정한다. 여기서 하는 일은
/// <b>「가서 물어봐라」를 알리는 것</b>뿐이다(설계 6.4).
/// </para>
/// <para>
/// 그래서 메시지를 잃어도 손실이 아니라 지연이다 — 안전망 조회가 집는다.
/// 두 번 와도 <c>claim</c> 이 빈손을 준다.
/// </para>
/// <para>
/// <b>받자마자 ack 한다.</b> 되돌린 메시지가 다시 배달되면 반쯤 고쳐진 저장소
/// 위에서 같은 지시가 다시 돈다 — 배포 소비자가 같은 이유로 그렇게 한다.
/// </para>
/// <para>
/// <b>브로커가 없어도 실행기는 돈다.</b> 로컬 개발 장비처럼 RabbitMQ 가 없는
/// 자리에서는 <c>Queue:Enabled</c> 를 끄고 <c>PollSeconds</c> 를 짧게 둔다.
/// </para>
/// </remarks>
public sealed class QueueListener(
    IOptions<RunnerOptions> optionsAccessor, ILogger<QueueListener> logger)
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    public async Task ListenAsync(Action ring, CancellationToken ct)
    {
        if (!_options.Queue.Enabled)
        {
            logger.LogInformation(
                "큐를 쓰지 않습니다. {Poll}초마다 서버에 물어봅니다.", _options.PollSeconds);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ring, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // 브로커가 내려가도 실행기는 살아 있어야 한다 — 폴링이 일을 찾는다.
                logger.LogWarning("큐에 붙지 못했습니다({Message}). 주기 조회로 계속합니다.", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task ConnectAsync(Action ring, CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _options.Queue.Host };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: _options.Queue.Name,
            durable: _options.Queue.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        // 한 번에 하나만 집어 간다. 우리는 메시지를 처리하는 것이 아니라
        // 깨어나기만 하므로 큰 값이 필요 없다.
        await channel.BasicQosAsync(0, 1, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            // 무엇이 있어도 ack 한다(위 머리말).
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            logger.LogDebug("종이 울렸습니다.");
            ring();
        };

        await channel.BasicConsumeAsync(
            queue: _options.Queue.Name, autoAck: false, consumer: consumer, cancellationToken: ct);

        logger.LogInformation("큐 {Queue}@{Host} 를 듣습니다.", _options.Queue.Name, _options.Queue.Host);

        await Task.Delay(Timeout.Infinite, ct);
    }
}
