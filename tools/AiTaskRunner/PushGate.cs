using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// 커밋한 것을 <c>main</c> 에 올릴지 정하고, 통과한 것만 민다.
/// </summary>
/// <remarks>
/// <para>
/// 설계 9.3. <b>이 저장소에서 <c>main</c> push 는 곧 운영 배포다</b> —
/// 이미지 열둘이 말려 GHCR 에 올라가고 러너가 <c>pull</c>·<c>up -d</c> 한다.
/// </para>
/// <para>
/// <b>GitHub 의 CI 는 배포를 막지 않는다.</b> <c>ci.yml</c> 과 <c>deploy.yml</c>
/// 은 나란히 돌고, deploy 는 컨테이너 안에서 <c>publish</c> 만 하므로
/// 아키텍처 테스트가 거기서는 한 번도 돌지 않는다. <b>테스트가 빨간불이어도
/// 배포는 그냥 나간다.</b> 그래서 관문을 GitHub 에 맡길 수 없고, push 하기
/// 전에 이쪽에서 걸러야 한다.
/// </para>
/// <para>
/// <b>push 는 AI 가 아니라 이 코드가 한다.</b> AI 에게 맡기면 게이트를 통째로
/// 건너뛴 것이 된다 — 거부 목록에 <c>git push</c> 를 넣는 이유다.
/// </para>
/// </remarks>
public sealed class PushGate(RunnerOptions options, ILogger<PushGate> logger)
{
    /// <summary>
    /// AI 가 고쳐서는 안 되는 곳.
    /// </summary>
    /// <remarks>
    /// <b>배포 방식 자체를 AI 가 고치는 것을 막는다.</b> 워크플로를 고칠 수
    /// 있으면 게이트를 우회하는 길을 스스로 열 수 있고, 비밀값 파일은
    /// 애초에 올라가면 안 된다.
    /// </remarks>
    private static readonly string[] Forbidden =
    [
        ".github/", "deploy/", "scripts/secrets", "Directory.Build.props",
        "Directory.Packages.props", "global.json", "nuget.config",
    ];

    /// <summary>
    /// 커밋하고, 게이트를 통과하면 민다.
    /// </summary>
    public async Task<PushResult> RunAsync(
        ServerClient.Claim claim,
        Workspace.Prepared prepared,
        Func<string, Task> say,
        CancellationToken ct)
    {
        // worktree 가 아니면 push 할 것이 없다(폴더 대상).
        if (prepared.Branch is null || prepared.RepoPath is null)
        {
            return new PushResult { Skipped = "git 저장소 대상이 아닙니다." };
        }

        // ① 바뀐 것이 있나. **없으면 커밋도 push 도 하지 않는다** —
        //    빈 배포를 일으키지 않는다.
        //
        // **`git status --porcelain` 을 파싱하지 않는다.** 그 출력은 줄마다
        // 앞에 두 글자 상태와 공백이 붙는데(` M path`), 출력 전체를 다듬는
        // 순간 첫 줄의 앞 공백이 사라져 자리 수가 어긋난다. 그러면 경로의
        // **첫 글자가 잘린다** — `microservices/…` 가 `icroservices/…` 가 되어
        // 빌드 검사가 조용히 건너뛰어졌고, 더 나쁘게는 `.github/…` 가
        // `github/…` 가 되어 **금지 경로 검사가 뚫렸다.** 실제로 밟았다.
        //
        // 경로만 그대로 주는 명령 둘로 나눠 묻는다.
        var tracked = await GitAsync(prepared.Path, ct, "diff", "--name-only", "HEAD");
        var untracked = await GitAsync(prepared.Path, ct, "ls-files", "--others", "--exclude-standard");

        var changed = (tracked.Output + "\n" + untracked.Output)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (changed.Count == 0)
        {
            return new PushResult { Skipped = "바뀐 파일이 없습니다." };
        }

        // ② 금지 경로.
        var hit = changed.FirstOrDefault(
            f => Forbidden.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        if (hit is not null)
        {
            return new PushResult { Blocked = $"고치면 안 되는 경로가 바뀌었습니다: {hit}" };
        }

        // ③ 커밋. **AI 가 커밋을 안 했을 수도 있어** 여기서 한 번 더 한다.
        await say($"[게이트] 바뀐 파일 {changed.Count}개를 커밋합니다.");

        await GitAsync(prepared.Path, ct, "add", "-A");

        var title = (claim.Title ?? "AI 작업").Replace('\n', ' ');

        var commit = await GitAsync(prepared.Path, ct, "-c", "user.name=AI Task Runner",
            "-c", "user.email=ai-task@jsini.local",
            "commit", "-m", $"{title}\n\nAI 작업 #{claim.TaskKey} (run {claim.RunKey})");

        if (commit.ExitCode != 0 && !commit.Output.Contains("nothing to commit"))
        {
            return new PushResult { Blocked = $"커밋하지 못했습니다:\n{Head(commit.Output)}" };
        }

        var sha = (await GitAsync(prepared.Path, ct, "rev-parse", "HEAD")).Output.Trim();
        var diff = await GitAsync(prepared.Path, ct, "diff", "--stat", "HEAD~1", "HEAD");

        // 올리지 않기로 한 건은 여기까지. **커밋은 남는다** — 사람이 이어받는다.
        if (!claim.AutoPush)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Skipped = "올리기가 꺼져 있습니다. 커밋과 브랜치만 남겼습니다.",
            };
        }

        if (claim.Target?.AllowPush != true)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = "이 대상은 올리기를 허용하지 않습니다.",
            };
        }

        // ④ 빌드·테스트. **CPU 를 실제로 먹는 유일한 구간**이라 부르는 쪽이
        //    직렬로 묶는다(설계 9.4).
        var gate = claim.Target.GateMode ?? "build";

        if (gate != "none")
        {
            var check = await VerifyAsync(prepared.Path, changed, gate, say, ct);

            if (check is not null)
            {
                return new PushResult
                {
                    Committed = sha,
                    DiffStat = Trim(diff.Output),
                    Blocked = check,
                };
            }
        }

        // ⑤ 그 사이 누가 밀었을 수 있다. 당겨서 얹는다.
        await say("[게이트] git pull --rebase");

        var rebase = await GitAsync(prepared.Path, ct, "pull", "--rebase", "origin",
            claim.Target.PushRef ?? "main");

        if (rebase.ExitCode != 0)
        {
            // 충돌이다. **자동으로 풀지 않는다** — 사람이 봐야 한다.
            await GitAsync(prepared.Path, ct, "rebase", "--abort");

            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"그 사이 올라온 것과 충돌했습니다. 사람이 풀어야 합니다:\n{Head(rebase.Output)}",
            };
        }

        // ⑥ 되돌릴 때 필요한 값을 **밀기 전에** 적어 둔다.
        //    push 한 뒤에 읽으면 이미 새 배포가 그 값을 덮었을 수 있다.
        var previousTag = ReadTag();

        await say($"[게이트] 통과. {claim.Target.PushRef ?? "main"} 으로 올립니다 — 배포가 일어납니다.");

        var push = await GitAsync(prepared.Path, ct, "push", "origin",
            $"HEAD:{claim.Target.PushRef ?? "main"}");

        if (push.ExitCode != 0)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"올리지 못했습니다:\n{Head(push.Output)}",
            };
        }

        var pushed = (await GitAsync(prepared.Path, ct, "rev-parse", "HEAD")).Output.Trim();

        logger.LogWarning("작업 {TaskKey} 가 {Ref} 에 올라갔습니다 — 운영 배포가 시작됩니다: {Sha}",
            claim.TaskKey, claim.Target.PushRef ?? "main", pushed);

        return new PushResult
        {
            Committed = sha,
            Pushed = pushed,
            PreviousTag = previousTag,
            DiffStat = Trim(diff.Output),
        };
    }

    /// <summary>
    /// 빌드와 테스트. <b>통과하면 <c>null</c>, 아니면 막는 이유를 돌려준다.</b>
    /// </summary>
    /// <remarks>
    /// 바뀐 영역만 본다 — <c>web/</c> 이 안 바뀌었으면 프론트를 빌드하지 않는다.
    /// 운영 서버의 CPU 를 아끼는 자리다.
    /// </remarks>
    private async Task<string?> VerifyAsync(
        string path, IReadOnlyList<string> changed, string gate,
        Func<string, Task> say, CancellationToken ct)
    {
        var touchedWeb = changed.Any(f => f.StartsWith("web/", StringComparison.OrdinalIgnoreCase));

        var touchedBackend = changed.Any(f =>
            f.StartsWith("microservices/", StringComparison.OrdinalIgnoreCase)
            || f.StartsWith("ApiGateway/", StringComparison.OrdinalIgnoreCase));

        if (!touchedWeb && !touchedBackend)
        {
            await say("[게이트] 코드가 아닌 변경이라 빌드를 건너뜁니다.");
            return null;
        }

        if (touchedBackend)
        {
            // **솔루션 파일이 있는지 먼저 본다.** 없으면 빌드가
            // 「MSB1009: Project file does not exist」로 실패하는데, 그 문구는
            // 코드가 깨진 것처럼 읽힌다 — 실제로는 파일이 없는 것이다.
            // (이 파일은 한동안 .gitignore 의 `*.sln` 에 걸려 저장소에 없었고,
            //  그 때문에 CI 의 백엔드 잡도 계속 빨간불이었다.)
            var sln = Path.Combine(path, "jsini.sln");

            if (!File.Exists(sln))
            {
                return "백엔드 솔루션(jsini.sln)이 작업공간에 없어 빌드를 확인할 수 없습니다.";
            }

            await say("[게이트] 백엔드 빌드");

            var r = await RunAsync(options.DotnetPath, ct, path,
                "build", "jsini.sln", "-v", "q", "--nologo", "-m:2");

            if (r.ExitCode != 0)
            {
                return $"백엔드 빌드가 실패했습니다:\n{Head(r.Output)}";
            }
        }

        if (touchedWeb)
        {
            var web = Path.Combine(path, "web");

            await say("[게이트] 프론트 빌드");

            var b = await RunAsync(options.DotnetPath, ct, web, "build", "-v", "q", "--nologo", "-m:2");

            if (b.ExitCode != 0)
            {
                return $"프론트 빌드가 실패했습니다:\n{Head(b.Output)}";
            }

            if (gate == "test")
            {
                // **이 테스트가 막는 것들은 하나같이 빌드는 통과하고 런타임에
                // 조용히 틀리는 종류다.** 그래서 push 전에 돌린다.
                await say("[게이트] 아키텍처 테스트");

                var t = await RunAsync(options.DotnetPath, ct, web, "test", "-v", "q", "--nologo");

                if (t.ExitCode != 0)
                {
                    return $"아키텍처 테스트가 실패했습니다:\n{Head(t.Output)}";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 지금 운영에 떠 있는 <c>TAG</c>.
    /// </summary>
    /// <remarks>
    /// <b>되돌리기는 이 값을 이전 것으로 돌리고 <c>up</c> 하는 일이다.</b>
    /// 아무도 안 적어 두면 되돌릴 수가 없다(설계 9.3).
    /// 못 읽어도 push 를 막지 않는다 — 이 값이 없다고 배포를 멈출 일은 아니다.
    /// </remarks>
    private string? ReadTag()
    {
        try
        {
            var path = options.EnvFile;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("TAG=", StringComparison.OrdinalIgnoreCase))
                {
                    return line[4..].Trim();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("운영 TAG 를 읽지 못했습니다: {Message}", ex.Message);
        }

        return null;
    }

    private static string? Trim(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 480)];

    private static string Head(string text)
    {
        var lines = text.Split('\n');
        return string.Join('\n', lines.Take(20));
    }

    private static Task<(int ExitCode, string Output)> GitAsync(
        string workDir, CancellationToken ct, params string[] args)
        => RunAsync("git", ct, workDir, args);

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string exe, CancellationToken ct, string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var p = Process.Start(psi)!;

        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);

        return (p.ExitCode, (stdout + stderr).Trim());
    }
}

/// <summary>게이트를 지난 결과.</summary>
public sealed class PushResult
{
    /// <summary>커밋한 SHA. 게이트에 걸려도 커밋은 남는다.</summary>
    public string? Committed { get; set; }

    /// <summary><b>이 값이 있으면 운영 배포가 일어났다는 뜻이다.</b></summary>
    public string? Pushed { get; set; }

    /// <summary>push 직전의 운영 <c>TAG</c>. 되돌릴 때 필요하다.</summary>
    public string? PreviousTag { get; set; }

    public string? DiffStat { get; set; }

    /// <summary>게이트에 걸린 이유. 사람이 읽고 이어받는다.</summary>
    public string? Blocked { get; set; }

    /// <summary>애초에 올릴 이유가 없었던 경우.</summary>
    public string? Skipped { get; set; }

    public string? Note => Blocked ?? Skipped;
}
