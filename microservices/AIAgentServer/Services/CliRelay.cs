using System.Text.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AIAgentServer.Services;

/// <summary>
/// 무료 공급자가 전부 막혔을 때 <b>운영 서버 호스트의 안티그래비티 CLI(<c>agy</c>)</b>에게
/// 한 줄 추천을 대신 묻는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 직접 부르지 않나.</b> 이 서비스는 컨테이너 안이고 <c>agy</c> 와 그 로그인 자격은
/// 호스트에 있다. 호스트에는 이미 AI 작업 실행기(<c>tools/AiTaskRunner</c>)가 systemd 담장 안에서
/// 상주하므로, 그쪽에 RabbitMQ 로 묻고 답을 받는다(요청·응답, direct reply-to).
/// 실행기는 받는 포트가 없다는 원칙을 그대로 지킨다 — 둘 다 브로커로 나가기만 한다.
/// </para>
/// <para>
/// 공통코드 추천뿐 아니라 대화의 무료 공급자 폴백에도 쓴다. 대화 스트림은 공급자
/// 응답 전에 CLI 호출을 끝낸 뒤 한 번에 돌려준다.
/// </para>
/// <para>
/// <b>실행기가 없으면 곧바로 안다.</b> 큐를 선언하면 브로커가 듣는 쪽 수를 알려 준다.
/// 0 이면 보내지 않고 바로 실패한다 — 보내 놓고 제한 시간을 다 기다리게 하지 않는다.
/// </para>
/// </remarks>
public sealed class CliRelay(IConfiguration configuration, ILogger<CliRelay> logger)
{
    private const string DirectReplyTo = "amq.rabbitmq.reply-to";
    private static readonly string[] DefaultAdapters = ["antigravity", "claude", "copilot"];
    private static readonly HashSet<string> SupportedAdapters =
        new(DefaultAdapters, StringComparer.OrdinalIgnoreCase);

    /// <summary>켜져 있는지. 브로커가 없는 장비(로컬 개발)에서는 끈다.</summary>
    public bool Enabled { get; } = configuration.GetValue("AI:CliRelay:Enabled", true);

    /// <summary>화면·로그에 보일 이름.</summary>
    public string DisplayName { get; } =
        configuration["AI:CliRelay:DisplayName"] is { Length: > 0 } n ? n : "안티그래비티 CLI";

    private readonly string[] _adapters =
        configuration.GetSection("AI:CliRelay:Adapters").Get<string[]>() ?? DefaultAdapters;

    private readonly string _host =
        configuration["AI:CliRelay:QueueHost"] is { Length: > 0 } h ? h : "localhost";

    /// <summary><b>실행기의 <c>Runner:QuickAsk:QueueName</c> 과 같아야 한다.</b></summary>
    private readonly string _queue =
        configuration["AI:CliRelay:QueueName"] is { Length: > 0 } q ? q : "ai_quick";

    private readonly TimeSpan _timeout =
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("AI:CliRelay:TimeoutSeconds", 70), 10, 300));

    private readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 한 번 묻는다. 실패는 <see cref="AiProviderException"/> 으로 던진다.
    /// </summary>
    public async Task<CliRelayAnswer> AskAsync(string prompt, CancellationToken ct = default)
    {
        if (!Enabled)
        {
            throw new AiProviderException(
                $"{DisplayName} 대체가 꺼져 있습니다.", "antigravity-cli");
        }

        AiProviderException? lastFailure = null;
        foreach (var adapter in _adapters.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!SupportedAdapters.Contains(adapter))
            {
                logger.LogWarning("지원하지 않는 CLI 대체 항목을 건너뜁니다: {Adapter}", adapter);
                continue;
            }

            try
            {
                var text = await AskAdapterAsync(adapter, prompt, ct);
                return new CliRelayAnswer(text, adapter, DisplayNameFor(adapter));
            }
            catch (AiProviderException ex)
            {
                lastFailure = ex;
                logger.LogWarning("{Adapter} 대체가 실패했습니다. {Reason}", adapter, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                lastFailure = new AiProviderException(
                    $"{DisplayNameFor(adapter)} 중계에 실패했습니다.", $"{adapter}-cli",
                    isConnectFailure: true);
                logger.LogWarning(
                    ex, "{Adapter} 중계에 실패했습니다. 다음 CLI 를 시도합니다.", adapter);
            }
        }

        throw lastFailure ?? new AiProviderException(
            "사용할 수 있는 CLI 대체가 설정되지 않았습니다.", "antigravity-cli");
    }

    private async Task<string> AskAdapterAsync(
        string adapter, string prompt, CancellationToken ct)
    {
        var key = $"{adapter}-cli";
        IConnection connection;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _host,
                RequestedConnectionTimeout = _connectTimeout,
                SocketReadTimeout = _connectTimeout,
                SocketWriteTimeout = _connectTimeout,
            };

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(_connectTimeout);
            connection = await factory.CreateConnectionAsync(limit.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("{Relay}: 브로커({Host})에 닿지 못했습니다. {Message}", DisplayName, _host, ex.Message);
            throw new AiProviderException(
                $"{DisplayNameFor(adapter)} 에 물을 통로(브로커)에 닿지 못했습니다.",
                key, isConnectFailure: true);
        }

        await using (connection)
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            // **실행기(QuickAskWorker)와 선언이 같아야 한다.** 비내구다 — 요청·응답이라 남길 것이 없다.
            var declared = await channel.QueueDeclareAsync(
                queue: _queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            if (declared.ConsumerCount == 0)
            {
                logger.LogWarning("{Adapter}: 듣는 실행기가 없습니다(큐 {Queue}).", adapter, _queue);
                throw new AiProviderException(
                    $"{DisplayNameFor(adapter)} 을 돌릴 실행기가 떠 있지 않습니다.",
                    key, isConnectFailure: true);
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var answer = new TaskCompletionSource<QuickAskReply?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // direct reply-to 는 **보내기 전에** 같은 채널에서 듣고 있어야 한다.
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) =>
            {
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    try
                    {
                        answer.TrySetResult(JsonSerializer.Deserialize<QuickAskReply>(ea.Body.Span));
                    }
                    catch (JsonException)
                    {
                        answer.TrySetResult(null);
                    }
                }

                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(DirectReplyTo, autoAck: true, consumer, ct);

            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = DirectReplyTo,

                // 실행기가 늦게 집어도 부른 쪽이 이미 떠났으면 CLI 를 띄우지 않게 한다.
                Expiration = ((long)_timeout.TotalMilliseconds).ToString(),
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queue,
                mandatory: false,
                basicProperties: props,
                body: JsonSerializer.SerializeToUtf8Bytes(new QuickAskRequest(adapter, prompt)),
                cancellationToken: ct);

            QuickAskReply? reply;

            try
            {
                reply = await answer.Task.WaitAsync(_timeout, ct);
            }
            catch (TimeoutException)
            {
                throw new AiProviderException(
                    $"{DisplayNameFor(adapter)} 이 {_timeout.TotalSeconds:0}초 안에 답하지 않았습니다.",
                    key);
            }

            if (reply is null)
            {
                throw new AiProviderException(
                    $"{DisplayNameFor(adapter)} 의 답을 읽지 못했습니다.", key);
            }

            if (!reply.Success || string.IsNullOrWhiteSpace(reply.Text))
            {
                throw new AiProviderException(
                    $"{DisplayNameFor(adapter)} 이 답하지 못했습니다. {reply.Error}".Trim(), key);
            }

            return reply.Text;
        }
    }

    private string DisplayNameFor(string adapter) => adapter.ToLowerInvariant() switch
    {
        "antigravity" => DisplayName,
        "claude" => "Claude CLI",
        "copilot" => "Copilot CLI",
        _ => adapter,
    };

    // 실행기와 오가는 약속. **실행기의 QuickAskWorker 쪽 레코드와 이름·모양이 같아야 한다.**
    // 공유 프로젝트로 묶지 않는다 — 실행기는 서버 코드를 참조하지 않는다(그쪽 csproj 주석).
    private sealed record QuickAskRequest(string Adapter, string Prompt);

    private sealed record QuickAskReply(bool Success, string? Text, string? Error);
}

public sealed record CliRelayAnswer(string Text, string Adapter, string DisplayName);
