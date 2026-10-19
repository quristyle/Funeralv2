using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// 커밋한 것을 main 에 올릴지 정하고 통과한 것만 민다. 이 저장소의 main push 는 곧 운영 배포다.
/// GitHub CI 는 배포를 막지 않으므로(deploy 는 테스트를 돌리지 않는다) 관문을 push 전 이쪽에서 건다. push 는 AI 가 아니라 이 코드가 한다.
/// </summary>
public sealed class PushGate(RunnerOptions options, Workspace workspace, ILogger<PushGate> logger)
{
    /// <summary>AI 가 고쳐서는 안 되는 곳. 배포 방식과 비밀값 파일을 고치면 게이트를 우회할 길이 생기므로 막는다.</summary>
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
        // 올리기가 켜져 있으면 조용히 넘기지 않는다.
        var wantsPush = claim.AutoPush && claim.Target?.AllowPush == true;

        // 커밋은 AI 가 돈 자리에서 한다. 복사본은 .git 이 없어 그곳에선 못 하므로, 결과를 정본으로 되돌려 정본에서 커밋한다.
        var gitPath = prepared.Path;

        if (prepared.Branch is null && !IsRepo(gitPath))
        {
            if (!prepared.SourceIsRepo || string.IsNullOrWhiteSpace(prepared.SourcePath))
            {
                // 정본도 저장소가 아니면 원격이 없어 push 할 수 없다 — 여기서 정직하게 막는다.
                return wantsPush
                    ? new PushResult
                    {
                        Blocked = "올리기가 켜져 있지만 대상이 git 저장소가 아닙니다. "
                                  + "올릴 원격이 없으니 대상 경로를 저장소로 바꾸거나 올리기를 끄십시오.",
                    }
                    : new PushResult { Skipped = "git 저장소 대상이 아닙니다." };
            }

            // 올릴 생각이 없으면 되돌리지 않는다 — 사본을 그대로 두는 것이 copy 격리의 요점이다(사람이 보고 옮긴다).
            if (!wantsPush)
            {
                return new PushResult { Skipped = "올리기가 꺼져 있습니다. 복사본에 그대로 두었습니다." };
            }

            if (await workspace.SyncBackAsync(prepared, say, ct) is { } fail)
            {
                return new PushResult { Blocked = fail };
            }

            gitPath = prepared.SourcePath;
        }

        // 올릴 가지: worktree 는 이 실행만의 가지, 그 밖에는 그 자리가 체크아웃한 가지.
        var branch = prepared.Branch;

        if (branch is null)
        {
            var head = await GitAsync(gitPath, ct, "rev-parse", "--abbrev-ref", "HEAD");

            branch = head.ExitCode == 0 && head.Output.Trim() is { Length: > 0 } name and not "HEAD"
                ? name
                : null;
        }

        if (branch is null)
        {
            return wantsPush
                ? new PushResult { Blocked = "올리기가 켜져 있지만 어느 가지도 보고 있지 않아 올릴 곳을 정할 수 없습니다." }
                : new PushResult { Skipped = "git 저장소 대상이 아닙니다." };
        }

        // ① 바뀐 것이 있나. 없으면 커밋도 push 도 하지 않는다 — 빈 배포를 일으키지 않는다.
        // git status --porcelain 은 파싱하지 않는다 — 출력 전체를 다듬으면 첫 줄 앞 공백이 사라져 경로 첫 글자가 잘리고, 금지 경로 검사가 뚫린다. 경로만 주는 명령 둘로 나눠 묻는다.
        var tracked = await GitAsync(gitPath, ct, "diff", "--name-only", prepared.BaseSha);
        var untracked = await GitAsync(gitPath, ct, "ls-files", "--others", "--exclude-standard");

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

        // ③ 커밋. AI 가 커밋을 안 했을 수도 있어 여기서 한 번 더 한다.
        await say($"[게이트] 바뀐 파일 {changed.Count}개를 커밋합니다.");

        await GitAsync(gitPath, ct, "add", "-A");

        var title = (claim.Title ?? "AI 작업").Replace('\n', ' ');

        var commit = await GitAsync(gitPath, ct, "-c", "user.name=AI Task Runner",
            "-c", "user.email=ai-task@jsini.local",
            "commit", "-m", $"{title}\n\nAI 작업 #{claim.TaskKey} (run {claim.RunKey})");

        if (commit.ExitCode != 0 && !commit.Output.Contains("nothing to commit"))
        {
            return new PushResult { Blocked = $"커밋하지 못했습니다:\n{Head(commit.Output)}" };
        }

        var sha = (await GitAsync(gitPath, ct, "rev-parse", "HEAD")).Output.Trim();
        var diff = await GitAsync(gitPath, ct, "diff", "--stat", prepared.BaseSha, "HEAD");

        // 올리지 않기로 한 건은 여기서 끝난다 — 커밋은 남고 사람이 이어받는다. worktree 면 따로 난 가지, 원본 직접이면 정본 가지에 커밋이 얹힌 것이다.
        var where = prepared.Branch is not null
            ? $"브랜치 {branch} 에 커밋만 남겼습니다."
            : $"정본({gitPath})의 {branch} 에 커밋만 남겼습니다. 아직 올라가지 않았습니다.";

        if (!claim.AutoPush)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Skipped = $"올리기가 꺼져 있습니다. {where}",
            };
        }

        if (claim.Target?.AllowPush != true)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"이 대상은 올리기를 허용하지 않습니다. {where}",
            };
        }

        var pushRef = claim.Target.PushRef ?? "main";

        // ③-2 원본 직접일 때 — 지금 가지가 올릴 가지와 같아야 한다. 아니면 HEAD:main 으로 엉뚱한 가지의 이력이 main 에 올라가 운영 배포가 된다.
        if (prepared.Branch is null && !string.Equals(branch, pushRef, StringComparison.Ordinal))
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"정본이 {branch} 를 보고 있는데 올릴 곳은 {pushRef} 입니다. "
                          + "원본 직접 격리에서는 두 가지가 같아야 올립니다.",
            };
        }

        // ④ 빌드·테스트. **CPU 를 실제로 먹는 유일한 구간**이라 부르는 쪽이
        //    직렬로 묶는다(설계 9.4).
        var gate = claim.Target.GateMode ?? "build";

        if (gate != "none")
        {
            var check = await VerifyAsync(gitPath, changed, gate, say, ct);

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

        // ⑤ 그 사이 누가 밀었을 수 있다. 당겨서 얹는다. git pull --rebase 는 쓰지 않는다 — FETCH_HEAD 파일을 자물쇠 없이 덮어써서 겹친 실행끼리 섞여 죽는다.
        // fetch 로 받고 원격 추적 가지를 짚어 얹는다. 받는 자리는 git 이 잠금으로 갱신하므로 겹쳐도 섞이지 않는다.
        await say($"[게이트] git fetch origin {pushRef} · git rebase origin/{pushRef}");

        var fetch = await GitAsync(
            gitPath, ct, "fetch", "origin", $"+refs/heads/{pushRef}:refs/remotes/origin/{pushRef}");

        if (fetch.ExitCode != 0)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"올릴 곳({pushRef})의 최신을 받지 못했습니다:\n{Head(fetch.Output)}",
            };
        }

        var rebase = await GitAsync(gitPath, ct, "rebase", $"origin/{pushRef}");

        if (rebase.ExitCode != 0)
        {
            // 충돌이다. **자동으로 풀지 않는다** — 사람이 봐야 한다.
            await GitAsync(gitPath, ct, "rebase", "--abort");

            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"그 사이 올라온 것과 충돌했습니다. 사람이 풀어야 합니다:\n{Head(rebase.Output)}",
            };
        }

        // ⑥ 되돌릴 때 필요한 값을 밀기 전에 적어 둔다 — push 한 뒤에 읽으면 새 배포가 그 값을 덮었을 수 있다.
        var previousTag = ReadTag();

        await say($"[게이트] 통과. {pushRef} 으로 올립니다 — 배포가 일어납니다.");

        var push = await GitAsync(gitPath, ct, "push", "origin", $"HEAD:{pushRef}");

        if (push.ExitCode != 0)
        {
            return new PushResult
            {
                Committed = sha,
                DiffStat = Trim(diff.Output),
                Blocked = $"올리지 못했습니다:\n{Head(push.Output)}",
            };
        }

        var pushed = (await GitAsync(gitPath, ct, "rev-parse", "HEAD")).Output.Trim();

        logger.LogWarning("작업 {TaskKey} 가 {Ref} 에 올라갔습니다 — 운영 배포가 시작됩니다: {Sha}",
            claim.TaskKey, pushRef, pushed);

        return new PushResult
        {
            Committed = sha,
            Pushed = pushed,
            PreviousTag = previousTag,
            DiffStat = Trim(diff.Output),
        };
    }

    /// <summary>빌드와 테스트. 통과하면 null, 아니면 막는 이유를 돌려준다. 바뀐 영역만 본다(web/ 이 안 바뀌었으면 프론트를 빌드하지 않는다).</summary>
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
            // 솔루션 파일이 있는지 먼저 본다 — 없으면 빌드가 MSB1009 로 실패해 코드가 깨진 것처럼 읽힌다.
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
                // 이 테스트가 막는 것들은 빌드는 통과하고 런타임에 조용히 틀리는 종류다 — 그래서 push 전에 돌린다.
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

    /// <summary>지금 운영에 떠 있는 TAG. 되돌리기는 이 값을 이전 것으로 돌리고 up 하는 일이라 미리 적어 둔다. 못 읽어도 push 를 막지 않는다.</summary>
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

    /// <summary>worktree 의 <c>.git</c> 은 폴더가 아니라 <b>파일</b>이다.</summary>
    private static bool IsRepo(string path)
    {
        var dotGit = Path.Combine(path, ".git");
        return Directory.Exists(dotGit) || File.Exists(dotGit);
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
