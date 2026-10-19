using System.Text;

using Microsoft.Extensions.Options;

namespace AiTaskRunner;

/// <summary>
/// 실행기 본체 — 종을 듣고, 집고, 돌리고, 보고한다. 큐는 지연을 줄이는 종일 뿐이고 일을 찾는 것은 주기 조회다.
/// 브로커가 없으면 Queue:Enabled 를 끄고 PollSeconds 를 짧게 둔다.
/// </summary>
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

    /// <summary>이 장비가 돌릴 CLI. 어댑터에 적힌 것이 그대로다(RunnerOptions.RunnableKinds).</summary>
    private IReadOnlyList<string> Kinds => _options.RunnableKinds;

    /// <summary>지금 도는 것. 동시 상한을 이것으로 센다.</summary>
    private readonly SemaphoreSlim _slots =
        new(Math.Max(1, optionsAccessor.Value.MaxParallel));

    /// <summary>게이트는 직렬이다. CLI 는 기다리는 시간이 대부분이라 겹쳐도 되지만, 빌드·테스트는 CPU 를 먹으므로 직렬로 묶는다.</summary>
    private readonly SemaphoreSlim _gate = new(1);

    /// <summary>종이 울렸다는 표시. 폴링이 기다리다 이것을 보면 바로 깬다.</summary>
    private readonly SemaphoreSlim _bell = new(0);

    /// <summary>같은 대상을 두 실행이 동시에 건드리지 않게 막는다. 까닭과 범위는 TargetGate 에 있다.</summary>
    private readonly TargetGate _targets = new();

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

        WarnAboutAdapters();

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
                // 포기하지 않으면 서버가 내려가 있는 동안 보고마다 타임아웃을 기다려 작업이 느려진다.
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

        // 치울 때 붙이는 이름. 어느 실행이 남긴 것인지 있어야 사람이 stash 목록에서 알아본다.
        var parkLabel = $"ai-run-{claim.RunKey} (작업 {claim.TaskKey}) "
                        + (claim.Title ?? "AI 작업").Replace('\n', ' ');

        // ── 같은 대상의 차례를 받는다 ───────────────────────
        // 원본 직접은 실행이 끝날 때까지, 나머지는 준비가 끝날 때까지 대상을 잡는다(TargetGate).
        // 앞엣것은 같은 파일을 두 실행이 고치는 것을, 뒤엣것은 정본의 fetch·pull·worktree add 가 겹쳐 index.lock 을 못 잡는 것을 막는다.
        var targetPath = claim.Target?.TargetPath;
        var inplace = claim.Target is { } t && Workspace.IsolationOf(t) == "inplace";

        var hold = await _targets.HoldAsync(
            targetPath,
            () => SayAsync(inplace
                ? "[대기] 같은 대상에서 다른 작업이 돌고 있습니다. 끝나면 이어서 시작합니다."
                : "[대기] 같은 정본을 다른 작업이 준비 중입니다. 잠시 기다립니다."),
            ct);

        // 원본 직접이 아니면 준비가 끝나는 대로 놓는다(try 안에서 놓으므로 여기서는 들고만 있는다).
        var held = true;

        async Task ReleaseAsync()
        {
            if (!held)
            {
                return;
            }

            held = false;
            await hold.DisposeAsync();
        }

        try
        {
            prepared = await workspace.PrepareAsync(claim, SayAsync, ct);

            if (!inplace)
            {
                // worktree·복사본은 여기서부터 자기 자리에서만 돈다. 계속 잡고 있으면 같은 정본의 다른 작업이 CLI 가 도는 30분 내내 줄을 선다.
                await ReleaseAsync();
            }

            // 지시문을 파일로도 남긴다 — 전달 방식과 무관하게 늘 한다. 작업공간 밖에 둔다 — 안에 두면 git add -A 가 집어 커밋에 섞인다.
            var promptDir = Path.Combine(_options.WorkspaceRoot, "prompts");
            Directory.CreateDirectory(promptDir);

            var promptPath = Path.Combine(promptDir, $"task-{claim.TaskKey}-run-{claim.RunKey}.md");

            // 지시에 함께 올라온 파일을 받아 지시문 끝에 경로로 적는다 — 안 적으면 AI 는 그 파일이 있는 줄 모른다.
            var instruction = await WithAttachmentsAsync(claim, promptDir, SayAsync, ct);

            // BOM 을 붙이지 않는다 — 붙이면 프롬프트 첫 글자가 보이지 않는 문자가 된다.
            await File.WriteAllTextAsync(
                promptPath, instruction, new UTF8Encoding(false), ct);

            await SayAsync($"[시작] {adapter.Executable} · {prepared.Path}");

            var result = await cli.RunAsync(
                adapter,
                prepared.Path,
                instruction,
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

            // ── 모델이 손을 뗀 **그 자리에서** 무엇이 바뀌었는지 묻는다 ──
            // 모델이 손을 뗀 그 자리에서 바뀐 것을 묻는다 — 게이트보다 앞이다. 게이트는 커밋·rebase 를 하므로 뒤에 물으면 남이 올린 것까지 섞인다.
            // 여기서 물으면 AI 가 남긴 것만 센다(커밋 여부와 무관, 커밋 전의 새 파일도 ls-files --others 로 포함).
            var changedFiles = await workspace.ChangedFilesAsync(prepared, ct);

            // 몇 줄이 바뀌었나도 여기서 떠 둔다 — 게이트가 커밋한 뒤의 diff --stat HEAD 는 비어 돌아온다.
            var diffBeforeGate = await workspace.DiffStatAsync(prepared, ct);

            if (changedFiles is { Length: > 0 })
            {
                await SayAsync($"[파일] 이번에 건드린 파일 {ChangedFileCount(changedFiles)}개를 적어 둡니다.");
                await FlushAsync(true);
            }

            // ── push 게이트 ─────────────────────────────────
            // 성공했을 때만 본다. 게이트는 직렬이다 — 빌드·테스트가 CPU 를 먹는 유일한 구간이라 다섯 건이 동시에 들어가면 운영 응답이 느려진다.
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
                // 켜 둔 것이 왜 안 됐는지는 말해야 한다 — 그냥 넘기면 「올리기를 켰는데 아무 일도 없었다」로만 보인다.
                await SayAsync("[게이트] 실행이 성공으로 끝나지 않아 올리지 않았습니다.");
                await FlushAsync(true);
            }

            // 게이트가 센 것을 먼저 쓴다(커밋 뒤의 diff --stat <기준> HEAD 라 더 정확하다). 게이트를 안 탔으면 CLI 직후에 뜬 값, 그것도 없으면 지금 다시 묻는다.
            var diff = push?.DiffStat ?? diffBeforeGate ?? await workspace.DiffStatAsync(prepared, ct);

            // 정본을 다음 실행에 넘기기 전에 치운다. 원본 직접으로 돌았고 게이트가 커밋까지 못 갔으면 고친 파일이 정본에 남아 뒤의 실행이 모두 「정본이 깨끗하지 않습니다」로 죽는다.
            // 치우기는 버리는 것이 아니라 stash 로 옮기는 것이며, 취소되지 않는다.
            await workspace.ParkAsync(prepared, parkLabel, SayAsync, CancellationToken.None);

            // 작업공간을 남길지.
            //
            // 복사본(폴더 대상)은 언제나 남긴다 — diff 를 낼 수 없어 「바뀐 것이 있으면 남긴다」로 두면 AI 가 만든 파일이 지워진다.
            // 원본을 고치지 않는 것이 copy 의 요점이라 사람이 옮길 때까지 남는다. 대신 쌓이므로 치우는 규칙은 WorkspaceSweeper 가 맡는다.
            keepWorkspace = diff is { Length: > 0 } || prepared.Branch is null;

            // 이미 올라간 것은 정본에 있다 — 작업공간을 남길 이유가 없다.
            if (push?.Pushed is { Length: > 0 })
            {
                keepWorkspace = false;
            }

            // 원본 직접은 치우는 자리가 아니다 — 「남겼습니다」를 적으면 임시 자리가 생긴 것처럼 읽힌다.
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
                changedFiles,
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

            // 예외로 끝난 실행도 정본에 손을 댔을 수 있다. 같은 이유로 치운다.
            if (prepared is not null)
            {
                await workspace.ParkAsync(prepared, parkLabel, SayAsync, CancellationToken.None);
            }

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

            // 치운 뒤에 놓는다 — 원본 직접은 정본을 stash 로 옮기고 CleanupAsync 까지 끝나야 다음 실행이 깨끗한 자리를 본다.
            // 준비만 잡았던 경우에는 이미 놓았으니 두 번 놓지 않는다.
            await ReleaseAsync();

            if (gone)
            {
                logger.LogWarning("서버가 실행 {RunKey} 를 모릅니다. 보고 없이 끝냈습니다.", claim.RunKey);
            }
        }
    }

    /// <summary>어댑터를 훑어 이상한 것을 시작하자마자 말한다. 어댑터가 하나도 없음(설정이 안 읽힘)과 적힌 실행 파일이 없음(깔기 전에 어댑터만 넣음)은 시켜 보기 전엔 드러나지 않는다.</summary>
    private void WarnAboutAdapters()
    {
        if (Kinds.Count == 0)
        {
            logger.LogCritical(
                "Runner:Adapters 가 비어 있습니다. 이 장비는 어떤 작업도 집지 않습니다.");
            return;
        }

        foreach (var kind in Kinds)
        {
            var path = _options.Adapters[kind].Executable;

            if (!File.Exists(path))
            {
                logger.LogWarning(
                    "'{Kind}' 어댑터의 실행 파일이 없습니다: {Path}. 그 종류로 시킨 건은 전부 실패합니다.",
                    kind, path);
            }
        }

        // 끈 것도 적어 둔다. **「왜 코파일럿이 안 돌지」의 답이 여기 있을 수 있다.**
        var off = _options.Adapters
            .Where(a => string.IsNullOrWhiteSpace(a.Value.Executable))
            .Select(a => a.Key)
            .ToList();

        if (off.Count > 0)
        {
            logger.LogInformation(
                "실행 파일이 비어 있어 끈 어댑터: [{Off}]. 그 종류로 시킨 건은 이 장비가 집지 않습니다.",
                string.Join(",", off));
        }
    }

    /// <summary>지시에 붙어 온 파일을 작업공간 밖의 폴더에 내려 받고, 그 경로를 지시문 끝에 적어 돌려준다.
    /// 같은 이름은 번호를 앞에 붙이고(휴대폰 사진 image.jpg 가 겹치는 일), 못 받은 것도 「못 받았다」고 적는다 — 지우면 사람은 붙여 보냈는데 안 봤다고 읽는다.</summary>
    private async Task<string> WithAttachmentsAsync(
        ServerClient.Claim claim, string promptDir, Func<string, Task> say, CancellationToken ct)
    {
        var instruction = claim.Instruction ?? string.Empty;

        if (claim.Files.Count == 0)
        {
            return instruction;
        }

        var dir = Path.Combine(promptDir, $"task-{claim.TaskKey}-run-{claim.RunKey}-files");
        Directory.CreateDirectory(dir);

        var lines = new StringBuilder();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var got = 0;

        foreach (var file in claim.Files)
        {
            var name = SafeLocalName(file.FileNm);

            // 같은 이름이 두 번 오면 번호를 앞에 붙인다.
            if (!taken.Add(name))
            {
                name = $"{file.FileKey}-{name}";
                taken.Add(name);
            }

            var path = Path.Combine(dir, name);

            if (await server.DownloadFileAsync(claim.RunKey, claim.Token, file.FileKey, path, ct))
            {
                got++;
                lines.AppendLine(
                    $"- `{path}` — {(file.IsImage ? "그림" : "파일")} · 원래 이름 `{file.FileNm}` · {Human(file.ByteSize)}");
            }
            else
            {
                lines.AppendLine($"- (받지 못함) 원래 이름 `{file.FileNm}` · {Human(file.ByteSize)}");
            }
        }

        await say($"[첨부] {claim.Files.Count}개 중 {got}개를 받았습니다 · {dir}");

        return $"""
            {instruction}

            ---

            ## 함께 올라온 파일

            아래 파일이 이 지시와 함께 올라왔다. **필요하면 읽어서 참고한다** —
            그림이면 그대로 보고, 문서면 열어 본다. 작업공간 안이 아니라 밖에
            있으므로 커밋에 섞이지 않는다.

            {lines.ToString().TrimEnd()}
            """;
    }

    /// <summary>내려 받을 이름을 안전하게 만든다. 서버도 한 번 걸렀지만 여기서 또 보는 것은 이 값이 Path.Combine 에 그대로 들어가기 때문이다.</summary>
    private static string SafeLocalName(string? name)
    {
        var n = Path.GetFileName(name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(n))
        {
            return "file";
        }

        var bad = Path.GetInvalidFileNameChars().ToHashSet();
        var sb = new StringBuilder(n.Length);

        foreach (var c in n)
        {
            sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        n = sb.ToString().TrimStart('.');

        return n.Length == 0 ? "file" : n;
    }

    /// <summary>사람이 읽는 크기.</summary>
    private static string Human(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    /// <summary>로그 한 줄에 적을 개수. 「외 N개」 줄은 뺀다 — 파일이 아니라 상한에 접힌 나머지 말이다.</summary>
    private static int ChangedFileCount(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => !line.StartsWith('…'));

    private Task FailAsync(ServerClient.Claim claim, string message, CancellationToken ct)
        => server.CompleteAsync(claim.RunKey, claim.Token, new
        {
            status = "failed",
            exitCode = -1,
            error = message,
        }, ct);
}
