using System.Text;

using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 실행기 본체 — 종을 듣고, 집고, 돌리고, 보고한다.
/// </summary>
/// <remarks>
/// <para>
/// 받는 포트가 없다. 큐와 서버로 <b>나가기만</b> 한다.
/// </para>
/// <para>
/// <b>큐가 없어도 돈다.</b> 큐는 지연을 줄이는 종일 뿐이고, 실제로 일을 찾는
/// 것은 주기 조회다(설계 6.6). 로컬 개발 장비처럼 브로커가 없는 자리에서는
/// <c>Queue:Enabled</c> 를 끄고 <c>PollSeconds</c> 를 짧게 두면 그대로 돌아간다.
/// </para>
/// </remarks>
public sealed class RunnerWorker(
    IOptions<RunnerOptions> optionsAccessor,
    ServerClient server,
    Workspace workspace,
    CliRunner cli,
    PushGate gate,
    QueueListener queue,
    ILogger<RunnerWorker> logger) : BackgroundService
{
    private readonly RunnerOptions _options = optionsAccessor.Value;

    /// <summary>
    /// 이 장비가 돌릴 CLI. <b>중복을 턴다.</b>
    /// </summary>
    /// <remarks>
    /// .NET 설정은 배열을 <b>칸 번호로 겹친다</b> — 기본 파일에
    /// <c>["claude","antigravity"]</c> 가 있고 Local 에 <c>["antigravity"]</c> 를
    /// 적으면 0번만 덮이고 1번이 남아 <c>["antigravity","antigravity"]</c> 가 된다.
    /// 실제로 시작 로그에 그렇게 찍혔다. 여기서 한 번 정리한다.
    /// </remarks>
    private string[] Kinds => [.. _options.Kinds.Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>지금 도는 것. 동시 상한을 이것으로 센다.</summary>
    private readonly SemaphoreSlim _slots =
        new(Math.Max(1, optionsAccessor.Value.MaxParallel));

    /// <summary>
    /// <b>게이트는 직렬이다.</b> CLI 는 대개 기다리는 시간이라 다섯이 겹쳐도
    /// 괜찮지만, 빌드·테스트는 CPU 를 실제로 먹는다(설계 9.4).
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1);

    /// <summary>종이 울렸다는 표시. 폴링이 기다리다 이것을 보면 바로 깬다.</summary>
    private readonly SemaphoreSlim _bell = new(0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RunnerToken))
        {
            logger.LogCritical("Runner:RunnerToken 이 없습니다. 서버가 집어가기를 거절합니다.");
            return;
        }

        logger.LogInformation(
            "실행기 {Name} 시작 · 서버 {Url} · CLI [{Kinds}] · 동시 {Max} · 조회 {Poll}초",
            _options.Name, _options.ServerUrl, string.Join(",", Kinds),
            _options.MaxParallel, _options.PollSeconds);

        // 종을 듣는다. 못 붙어도 계속 간다 — 폴링이 있다.
        _ = queue.ListenAsync(() => _bell.Release(), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PickAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "한 바퀴가 실패했습니다. 계속 돕니다.");
            }

            // 종이 울리면 바로 깨고, 안 울려도 주기마다 한 번 본다.
            try
            {
                await _bell.WaitAsync(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("실행기를 멈춥니다.");
    }

    private async Task PickAndRunAsync(CancellationToken ct)
    {
        var free = _slots.CurrentCount;

        if (free <= 0)
        {
            return;
        }

        var claims = await server.ClaimAsync(free, ct);

        foreach (var claim in claims)
        {
            await _slots.WaitAsync(ct);

            // 건마다 따로 돈다. 하나가 오래 걸려도 다음 건을 막지 않는다.
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunOneAsync(claim, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "작업 {TaskKey} 가 예외로 끝났습니다.", claim.TaskKey);
                }
                finally
                {
                    _slots.Release();
                }
            }, ct);
        }
    }

    private async Task RunOneAsync(ServerClient.Claim claim, CancellationToken ct)
    {
        logger.LogInformation("작업 {TaskKey} 「{Title}」 를 {Kind} 로 시작합니다.",
            claim.TaskKey, claim.Title, claim.RunnerKind);

        var kind = claim.RunnerKind ?? "claude";

        if (!_options.Adapters.TryGetValue(kind, out var adapter))
        {
            await FailAsync(claim, $"이 장비에는 '{kind}' 어댑터가 없습니다.", ct);
            return;
        }

        // ── 보고 준비 ───────────────────────────────────────
        var buffer = new List<LogLine>();
        var nextSeq = 0;
        var lastFlush = DateTime.UtcNow;
        var giveUp = false;
        var fails = 0;

        async Task FlushAsync(bool force)
        {
            if (giveUp || buffer.Count == 0)
            {
                return;
            }

            if (!force && buffer.Count < _options.FlushLines
                && (DateTime.UtcNow - lastFlush).TotalSeconds < _options.FlushSeconds)
            {
                return;
            }

            var batch = buffer.ToList();
            buffer.Clear();
            lastFlush = DateTime.UtcNow;

            var ok = await server.LogsAsync(claim.RunKey, claim.Token, batch, ct);

            if (ok)
            {
                fails = 0;
            }
            else if (++fails >= 5)
            {
                // 포기하지 않으면 서버가 내려가 있는 동안 보고마다 타임아웃을
                // 기다려 **작업 자체가 느려진다**(release-run.sh 와 같은 규칙).
                giveUp = true;
                logger.LogWarning("연속 {Fails}회 실패로 로그 보고를 포기합니다. 작업은 계속합니다.", fails);
            }
        }

        async Task SayAsync(string text)
        {
            buffer.Add(new LogLine { Seq = ++nextSeq, Stream = "system", Text = text });
            await FlushAsync(false);
        }

        // ── 하트비트 · 취소 확인 ────────────────────────────
        var canceled = false;
        var gone = false;

        using var beatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var beat = Task.Run(async () =>
        {
            while (!beatCts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatSeconds), beatCts.Token);

                var r = await server.HeartbeatAsync(claim.RunKey, claim.Token, beatCts.Token);

                if (r is null)
                {
                    // 서버가 이 실행을 모른다. **보고만 멈추고 작업은 계속한다.**
                    gone = true;
                    return;
                }

                if (r.Value)
                {
                    canceled = true;
                    return;
                }
            }
        }, beatCts.Token);

        Workspace.Prepared? prepared = null;
        var keepWorkspace = false;

        try
        {
            prepared = await workspace.PrepareAsync(claim, SayAsync, ct);

            // 지시문을 파일로도 남긴다. **전달 방식과 무관하게 늘 한다** —
            // 그 파일 하나면 사람이 같은 명령을 그대로 다시 칠 수 있다.
            //
            // **작업공간 「밖」에 둔다.** 안에 두면 `git add -A` 가 그것까지
            // 집어 커밋에 섞인다 — 실제로 `.ai-task/task-8.md` 가 함께
            // 올라갔다. worktree 의 `info/exclude` 는 정본과 공유되는 파일이라
            // 거기에 적는 것은 정본을 건드리는 일이 된다. 밖에 두는 편이 깔끔하다.
            var promptDir = Path.Combine(_options.WorkspaceRoot, "prompts");
            Directory.CreateDirectory(promptDir);

            var promptPath = Path.Combine(promptDir, $"task-{claim.TaskKey}-run-{claim.RunKey}.md");

            // BOM 을 붙이지 않는다. 붙이면 프롬프트 첫 글자가 보이지 않는
            // 문자로 시작한다.
            await File.WriteAllTextAsync(
                promptPath, claim.Instruction ?? string.Empty, new UTF8Encoding(false), ct);

            await SayAsync($"[시작] {adapter.Executable} · {prepared.Path}");

            var result = await cli.RunAsync(
                adapter,
                prepared.Path,
                claim.Instruction ?? string.Empty,
                promptPath,
                TimeSpan.FromMinutes(claim.TimeoutMinutes > 0 ? claim.TimeoutMinutes : adapter.TimeoutMinutes),
                async line =>
                {
                    line.Seq = ++nextSeq;
                    buffer.Add(line);
                    await FlushAsync(false);
                },
                () => Task.FromResult(canceled),
                ct);

            await FlushAsync(true);

            // ── push 게이트 ─────────────────────────────────
            //
            // **성공했을 때만 본다.** 실패한 결과를 올릴 이유가 없다.
            // 그리고 **게이트는 직렬이다** — 빌드·테스트가 CPU 를 실제로
            // 먹는 유일한 구간이라, 다섯 건이 동시에 그 단계에 들어가면
            // 운영 응답이 눈에 띄게 느려진다(설계 9.4).
            PushResult? push = null;

            if (!result.Canceled && !result.TimedOut && result.ExitCode == 0)
            {
                await _gate.WaitAsync(ct);

                try
                {
                    await SayAsync("[게이트] 검사 차례를 받았습니다.");
                    push = await gate.RunAsync(claim, prepared, SayAsync, ct);
                }
                finally
                {
                    _gate.Release();
                }

                if (push.Note is { } note)
                {
                    await SayAsync($"[게이트] 올리지 않았습니다 — {note}");
                }

                await FlushAsync(true);
            }
            else if (claim.AutoPush)
            {
                // **켜 둔 것이 왜 안 됐는지는 말해야 한다.** 그냥 넘기면
                // 「올리기를 켰는데 아무 일도 없었다」로만 보인다.
                await SayAsync("[게이트] 실행이 성공으로 끝나지 않아 올리지 않았습니다.");
                await FlushAsync(true);
            }

            var diff = push?.DiffStat ?? await workspace.DiffStatAsync(prepared, ct);

            // 작업공간을 남길지.
            //
            // **복사본(폴더 대상)은 언제나 남긴다.** git 이 아니라 diff 를 낼 수
            // 없는데, 「바뀐 것이 있으면 남긴다」로만 두면 **AI 가 만든 파일이
            // 통째로 지워진다** — 실제로 그렇게 한 번 날렸다. 원본을 고치지
            // 않는 것이 copy 의 요점이므로, 그 결과물은 사람이 보고 옮길
            // 때까지 남아 있어야 한다.
            //
            // 대신 쌓인다. 치우는 규칙은 따로 필요하다(설계 7.3 — 성공 후 N일).
            keepWorkspace = diff is { Length: > 0 } || prepared.Branch is null;

            // 이미 올라간 것은 정본에 있다. 작업공간을 남길 이유가 없다.
            if (push?.Pushed is { Length: > 0 })
            {
                keepWorkspace = false;
            }

            // 원본 직접은 애초에 치우는 자리가 아니다(Disposable=false).
            // 거기에 「남겼습니다」를 적으면 임시 자리가 생긴 것처럼 읽힌다.
            if (keepWorkspace && prepared.Disposable)
            {
                await SayAsync($"[남김] 결과가 {prepared.Path} 에 있습니다.");
            }

            var status = result.Canceled ? "canceled"
                : result.TimedOut ? "timeout"
                : result.ExitCode == 0 ? "succeeded" : "failed";

            await SayAsync($"[끝] {status} (exit {result.ExitCode}, {result.LineCount}줄)");
            await FlushAsync(true);

            beatCts.Cancel();

            await server.CompleteAsync(claim.RunKey, claim.Token, new
            {
                status,
                exitCode = result.ExitCode,
                error = status == "succeeded" ? null : $"{status} (exit {result.ExitCode})",
                branch = prepared.Branch,
                baseSha = prepared.BaseSha,
                diffStat = diff,
                resultText = result.ResultText,
                sessionId = result.SessionId,
                sessionKind = kind,
                workspacePath = prepared.Path,
                pushedCommit = push?.Pushed,
                previousTag = push?.PreviousTag,
            }, CancellationToken.None);

            logger.LogInformation("작업 {TaskKey} 끝: {Status}", claim.TaskKey, status);
        }
        catch (Exception ex)
        {
            await SayAsync($"[오류] {ex.Message}");
            await FlushAsync(true);

            beatCts.Cancel();
            await FailAsync(claim, ex.Message, CancellationToken.None);
        }
        finally
        {
            beatCts.Cancel();

            if (prepared is not null)
            {
                await workspace.CleanupAsync(prepared, keepWorkspace, CancellationToken.None);
            }

            if (gone)
            {
                logger.LogWarning("서버가 실행 {RunKey} 를 모릅니다. 보고 없이 끝냈습니다.", claim.RunKey);
            }
        }
    }

    private Task FailAsync(ServerClient.Claim claim, string message, CancellationToken ct)
        => server.CompleteAsync(claim.RunKey, claim.Token, new
        {
            status = "failed",
            exitCode = -1,
            error = message,
        }, ct);
}
