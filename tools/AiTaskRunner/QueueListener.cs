using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AiTaskRunner;

/// <summary>
/// RabbitMQ ai_task 큐의 종을 듣는다. 메시지는 읽지 않고 「가서 물어봐라」만 알린다 — 실제 할 일은 서버의 claim 이 정한다.
/// 잃어도 안전망 조회가 집고, 두 번 와도 claim 이 빈손을 준다. 받자마자 ack 한다. 브로커가 없으면 Queue:Enabled 를 끄고 PollSeconds 를 짧게 둔다.
/// </summary>
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

        // 한 번에 하나만 집는다 — 깨어나기만 하므로 큰 값이 필요 없다.
        await channel.BasicQosAsync(0, 1, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            // 내용과 무관하게 ack 한다.
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
