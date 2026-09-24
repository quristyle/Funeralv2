using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AiTaskRunner;

/// <summary>
/// 「한 줄 물어보기」 — AI 서버의 무료 공급자가 전부 막혔을 때 쓰는 마지막 자리.
/// </summary>
/// <remarks>
/// <para>
/// <b>무엇을 위한 것인가.</b> 공통코드 · 역할 식별자 추천은 한 단어짜리 답이다.
/// AI 서버(<c>AIAgentServer</c>)가 로컬 LLM → Gemini → Groq → OpenRouter 를 다 돌고도
/// 한도에 막히면, 구독 인증이 끝나 있는 이 장비의 <c>agy</c> 에게 한 번 묻는다.
/// AI 서버는 컨테이너 안이라 <c>agy</c> 를 직접 띄울 수 없다 — 그래서 여기를 거친다.
/// </para>
/// <para>
/// <b>작업 대기열(<c>ai_task</c>)과 다른 길이다.</b> 그쪽은 DB 가 정본이고 큐는 종이지만,
/// 이것은 사람이 단추를 누르고 기다리는 동안의 <b>요청·응답(RPC)</b>이다.
/// 남길 기록이 없으므로 DB 를 거치지 않고, 메시지를 잃으면 부른 쪽이 시간 초과로 끝난다.
/// 큐도 <b>비내구(non-durable)</b>이고 메시지마다 유효 시간이 붙어 온다 —
/// 실행기가 내려가 있는 동안 쌓인 옛 질문을 나중에 풀어 CLI 한도를 태우지 않는다.
/// </para>
/// <para>
/// <b>담장 — 작업 실행보다 좁다.</b>
/// </para>
/// <list type="bullet">
///   <item><c>--dangerously-skip-permissions</c> 를 <b>주지 않는다.</b> 답은 글자뿐이라
///     도구가 필요 없다. 도구를 쓰려 들면 허락을 기다리다 제한 시간에 끊긴다.</item>
///   <item><c>--sandbox</c> · <c>--disable-slash-commands</c> 를 준다(설정의 <c>Args</c>).</item>
///   <item>매번 <b>빈 폴더</b>에서 띄우고 끝나면 지운다. 저장소를 보여 주지 않는다.</item>
///   <item>들어오는 글의 길이를 자른다(<see cref="QuickAskOptions.MaxPromptChars"/>).</item>
/// </list>
/// <para>
/// 질문 안의 「이름」은 사람이 친 글이다. 지시를 흉내 낸 글이 들어와도 위 넷 때문에
/// 할 수 있는 일은 「글자로 답하기」뿐이다.
/// </para>
/// </remarks>
public sealed class QuickAskWorker(
    IOptions<RunnerOptions> optionsAccessor, ILogger<QuickAskWorker> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var quick = _options.QuickAsk;

        if (!quick.Enabled || !_options.Queue.Enabled)
        {
            logger.LogInformation("한 줄 물어보기를 쓰지 않습니다.");
            return;
        }

        if (!_options.Adapters.TryGetValue(quick.Adapter, out var adapter)
            || string.IsNullOrWhiteSpace(adapter.Executable))
        {
            logger.LogWarning(
                "한 줄 물어보기: '{Adapter}' 어댑터가 없어 켜지 않습니다.", quick.Adapter);
            return;
        }

        logger.LogInformation(
            "한 줄 물어보기 대기 · 큐 {Queue} · CLI {Adapter} · 동시 {Parallel}",
            quick.QueueName, quick.Adapter, quick.MaxParallel);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(quick, adapter, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("한 줄 물어보기: 큐에 붙지 못했습니다({Message}). 30초 뒤 다시.", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task ListenAsync(QuickAskOptions quick, AdapterOptions adapter, CancellationToken ct)
    {
        // CLI 를 동시에 몇 개까지 띄울지. 한 번에 10초 남짓이라 크게 둘 필요가 없다.
        //
        // prefetch 만 올리면 안 된다 — 이 클라이언트는 기본으로 **받은 것을 하나씩
        // 차례로** 처리기에 넘긴다. 처리기가 CLI 를 기다리는 동안 다음 질문이 줄을 선다.
        var parallel = (ushort)Math.Clamp(quick.MaxParallel, 1, 10);

        var factory = new ConnectionFactory
        {
            HostName = _options.Queue.Host,
            ConsumerDispatchConcurrency = parallel,
        };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // **부르는 쪽(AIAgentServer 의 CliRelay)과 선언이 같아야 한다.**
        // 다르면 브로커가 PRECONDITION_FAILED 를 낸다.
        await channel.QueueDeclareAsync(
            queue: quick.QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.BasicQosAsync(0, parallel, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            // 받자마자 ack 한다. 되돌려 다시 배달되면 같은 질문에 CLI 를 두 번 띄운다 —
            // 부른 쪽은 이미 첫 답을 기다리다 끝났을 수도 있다.
            var body = ea.Body.ToArray();
            var replyTo = ea.BasicProperties.ReplyTo;
            var correlationId = ea.BasicProperties.CorrelationId;

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            var reply = await AnswerAsync(quick, adapter, body, ct);

            if (string.IsNullOrEmpty(replyTo)) return;

            var props = new BasicProperties { CorrelationId = correlationId };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: replyTo,
                mandatory: false,
                basicProperties: props,
                body: JsonSerializer.SerializeToUtf8Bytes(reply),
                cancellationToken: ct);
        };

        await channel.BasicConsumeAsync(quick.QueueName, autoAck: false, consumer, ct);

        // 연결이 끊기면 빠져나가 바깥 루프가 다시 붙는다.
        var closed = new TaskCompletionSource();
        connection.ConnectionShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await using (ct.Register(() => closed.TrySetCanceled(ct)))
        {
            await closed.Task;
        }
    }

    private async Task<QuickAskReply> AnswerAsync(
        QuickAskOptions quick, AdapterOptions adapter, byte[] body, CancellationToken ct)
    {
        QuickAskRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<QuickAskRequest>(body);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return QuickAskReply.Fail("질문이 비어 있습니다.");
        }

        var prompt = request.Prompt.Length > quick.MaxPromptChars
            ? request.Prompt[..quick.MaxPromptChars]
            : request.Prompt;

        // 매번 빈 폴더. agy 는 cwd 만으로는 작업 폴더를 못 찾으므로 --add-dir 로도 준다.
        var folder = Path.Combine(_options.WorkspaceRoot, "_quick", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var watch = Stopwatch.StartNew();

        try
        {
            var (ok, text) = await RunAsync(quick, adapter, folder, prompt, ct);

            logger.LogInformation(
                "한 줄 물어보기 {Result} · {Elapsed}ms",
                ok ? "답함" : "실패", watch.ElapsedMilliseconds);

            return ok ? QuickAskReply.Ok(text) : QuickAskReply.Fail(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 여기서 던지면 답이 안 가고, 부른 쪽은 시간 초과까지 기다린다.
            logger.LogWarning(ex, "한 줄 물어보기: CLI 를 띄우지 못했습니다.");
            return QuickAskReply.Fail($"{quick.Adapter} 을 띄우지 못했습니다. ({ex.Message})");
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex)
            {
                // 빈 폴더 하나라 남아도 해가 없다. WorkspaceSweeper 는 run-* 만 치운다.
                logger.LogDebug("한 줄 물어보기 폴더를 지우지 못했습니다: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// CLI 를 띄워 답을 받는다. <b>셸을 거치지 않는다</b> — <see cref="CliRunner"/> 와 같은 규칙이다.
    /// </summary>
    private async Task<(bool Ok, string Text)> RunAsync(
        QuickAskOptions quick, AdapterOptions adapter, string folder, string prompt, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(adapter.Executable)
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // 작업 실행용 Args(--dangerously-skip-permissions 가 든 것)를 쓰지 않는다.
        // 이 길은 자기 인자를 따로 든다.
        foreach (var arg in quick.Args)
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var arg in adapter.WorkspaceArgs)
        {
            psi.ArgumentList.Add(arg.Replace("{path}", folder));
        }

        // agy 는 -p 뒤에 플래그를 두면 그 플래그를 프롬프트로 먹는다. 맨 끝에 둔다.
        psi.ArgumentList.Add(adapter.PromptArgPrefix + prompt);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(quick.TimeoutSeconds, 10, 300));

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("프로세스를 띄우지 못했습니다.");

        proc.StandardInput.Close();

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(timeout);

        try
        {
            var stdout = proc.StandardOutput.ReadToEndAsync(timer.Token);
            var stderr = proc.StandardError.ReadToEndAsync(timer.Token);

            await proc.WaitForExitAsync(timer.Token);

            var output = (await stdout).Trim();

            if (proc.ExitCode != 0 || output.Length == 0)
            {
                var error = (await stderr).Trim();
                return (false,
                    $"{quick.Adapter} 이 답하지 못했습니다(종료 코드 {proc.ExitCode}). "
                    + Cut(error.Length > 0 ? error : output, 300));
            }

            return (true, Cut(output, 2000));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 이미 끝났다.
            }

            return (false, $"{quick.Adapter} 이 {timeout.TotalSeconds:0}초 안에 답하지 않았습니다.");
        }
    }

    private static string Cut(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}

/// <summary>AI 서버가 보내는 질문. 약속은 이 JSON 한 벌뿐이다.</summary>
public sealed record QuickAskRequest(string Prompt);

/// <summary>돌려주는 답.</summary>
public sealed record QuickAskReply(bool Success, string? Text, string? Error)
{
    public static QuickAskReply Ok(string text) => new(true, text, null);

    public static QuickAskReply Fail(string error) => new(false, null, error);
}
