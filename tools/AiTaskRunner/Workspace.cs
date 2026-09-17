using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// 실행 한 번이 일할 자리를 만든다.
/// </summary>
/// <remarks>
/// <para>
/// <b>원본에서 직접 돌리지 않는다</b>가 공통 원칙이고, 「어떻게 비껴가나」가
/// 대상 종류마다 다르다(설계 7.3).
/// </para>
/// <list type="bullet">
///   <item><description><c>repo</c> — <c>git worktree</c></description></item>
///   <item><description><c>folder</c> + <c>copy</c> — 복사본</description></item>
///   <item><description><c>folder</c> + <c>inplace</c> — 격리 없음(예외)</description></item>
/// </list>
/// </remarks>
public sealed class Workspace(RunnerOptions options, ILogger<Workspace> logger)
{
    public async Task<Prepared> PrepareAsync(
        ServerClient.Claim claim, Func<string, Task> say, CancellationToken ct)
    {
        var target = claim.Target
            ?? throw new InvalidOperationException("대상이 없는 작업은 돌릴 수 없습니다.");

        var path = target.TargetPath
            ?? throw new InvalidOperationException("대상 경로가 비어 있습니다.");

        if (!Directory.Exists(path))
        {
            // **장비를 짚어 준다.** 그냥 「폴더가 없습니다」로 두면 「내 PC 에는
            // 분명히 있는데」가 되고, 다른 장비의 실행기가 집어 간 것이라는
            // 진짜 원인이 안 읽힌다 — 실제로 그렇게 한 번 헤맸다.
            throw new DirectoryNotFoundException(
                $"이 장비({options.Name})에는 대상 폴더가 없습니다: {path} "
                + "— 대상의 「장비」가 맞게 지정돼 있는지 확인하십시오.");
        }

        // worktree 의 `.git` 은 폴더가 아니라 **파일**이다. 폴더만 보면
        // worktree 안에서 돌 때 저장소가 아닌 것으로 읽힌다.
        var dotGit = Path.Combine(path, ".git");
        var isRepo = Directory.Exists(dotGit) || File.Exists(dotGit);

        var isolation = target.IsolationMode ?? (isRepo ? "worktree" : "copy");

        // 저장소로 등록해 놓고 실제로는 아니면 **여기서 말한다.** 그냥 넘기면
        // 당기지도 올리지도 않은 채 성공으로 끝나고, 사람은 최신에서 돌았다고
        // 믿는다.
        if (!isRepo && target.TargetKind == "repo")
        {
            throw new InvalidOperationException(
                $"저장소로 등록된 대상인데 {path} 에 .git 이 없습니다. "
                + "대상의 종류나 경로를 확인하십시오.");
        }

        // ── git 저장소면 먼저 당긴다 ────────────────────────
        //
        // **시작 전 pull 은 결정된 요구사항이다.** 뒤처진 기준에서 만든 커밋을
        // main 에 밀면 그 사이 남이 올린 것을 되돌리는 커밋이 될 수 있고,
        // 그것이 곧 배포다(설계 7.3).
        var baseSha = (string?)null;

        if (isRepo)
        {
            var dirty = await GitAsync(path, ct, "status", "--porcelain");

            if (!string.IsNullOrWhiteSpace(dirty.Output))
            {
                throw new InvalidOperationException(
                    $"정본이 깨끗하지 않습니다. 사람이 먼저 정리해야 합니다:\n{dirty.Output}");
            }

            await say("[준비] git fetch origin");

            var fetch = await GitAsync(path, ct, "fetch", "origin");

            if (fetch.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"origin 에서 가져오지 못했습니다. 최신에서 시작할 수 없습니다:\n{fetch.Output}");
            }

            // **가지를 짚어서 당긴다.** 인자 없는 `git pull` 은 그 가지에
            // upstream 설정이 있어야 돌고, 없으면 「no tracking information」
            // 으로 죽는다 — 최신이 아니라 설정이 없다는 뜻인데 그 문구로는
            // 읽히지 않는다.
            var head = await GitAsync(path, ct, "rev-parse", "--abbrev-ref", "HEAD");
            var current = head.Output.Trim();

            if (current is { Length: > 0 } && current != "HEAD")
            {
                await say($"[준비] git pull --ff-only origin {current}");

                var pull = await GitAsync(path, ct, "pull", "--ff-only", "origin", current);

                if (pull.ExitCode != 0)
                {
                    // **원본 직접은 여기서 일한다.** 뒤처진 자리에서 고치고
                    // 올리면 그 사이 남이 올린 것을 되돌린다 — 막는다.
                    if (isolation == "inplace")
                    {
                        throw new InvalidOperationException(
                            $"정본({current})을 최신으로 맞추지 못했습니다:\n{pull.Output}");
                    }

                    // worktree · 복사본은 방금 받은 origin/<ref> 에서 갈라지므로
                    // 정본의 가지가 뒤처져 있어도 작업 자체는 최신에서 시작한다.
                    // 멈출 일은 아니지만 **조용히 넘기지도 않는다.**
                    await say($"[준비] 정본 {current} 를 당기지 못했습니다 "
                              + "(작업은 origin 에서 갈라집니다): " + Head(pull.Output));
                }
            }
            else
            {
                await say("[준비] 정본이 어느 가지도 보고 있지 않아 당기지 않았습니다.");
            }

            baseSha = (await GitAsync(path, ct, "rev-parse", "HEAD")).Output.Trim();

            // **당긴 결과를 적어 둔다.** 「pull 했다」만 남으면 어디에서
            // 시작했는지가 기록에 없다.
            await say($"[준비] 시작 기준 {current} {Short(baseSha)}");
        }

        Directory.CreateDirectory(options.WorkspaceRoot);

        // ── 원본 직접 ───────────────────────────────────────
        if (isolation == "inplace")
        {
            await say("[준비] 원본에서 직접 돕니다. 되돌리려면 사람이 해야 합니다.");

            return new Prepared
            {
                Path = path,
                BaseSha = baseSha,
                Branch = null,
                Disposable = false,
            };
        }

        var dir = Path.Combine(options.WorkspaceRoot, $"run-{claim.RunKey}");

        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        // ── worktree ────────────────────────────────────────
        if (isolation == "worktree" && isRepo)
        {
            var branch = $"ai/{claim.TaskKey}-{claim.RunKey}";
            var from = string.IsNullOrWhiteSpace(claim.TargetRef)
                ? $"origin/{target.DefaultRef ?? "main"}"
                : claim.TargetRef;

            await say($"[준비] worktree {dir} ({branch} ← {from})");

            var add = await GitAsync(path, ct, "worktree", "add", dir, "-b", branch, from);

            if (add.ExitCode != 0)
            {
                throw new InvalidOperationException($"worktree 를 만들지 못했습니다:\n{add.Output}");
            }

            return new Prepared
            {
                Path = dir,
                BaseSha = baseSha,
                Branch = branch,
                RepoPath = path,
                Disposable = true,
            };
        }

        // ── 복사본 ──────────────────────────────────────────
        //
        // 상한을 넘으면 **미리 거절한다.** 복사에 몇 분을 쓰다 타임아웃으로
        // 죽는 것보다 낫다.
        if (target.MaxSizeMb is { } cap and > 0)
        {
            var mb = await SizeMbAsync(path, ct);

            if (mb > cap)
            {
                throw new InvalidOperationException(
                    $"대상이 {mb}MB 로 상한({cap}MB)을 넘습니다. 복사하지 않습니다.");
            }
        }

        await say($"[준비] 복사본 {dir}");

        var copy = await RunAsync("rsync", ct, workDir: null,
            "-a", "--exclude", ".git", path.TrimEnd('/') + "/", dir + "/");

        if (copy.ExitCode != 0)
        {
            throw new InvalidOperationException($"복사하지 못했습니다:\n{copy.Output}");
        }

        return new Prepared { Path = dir, BaseSha = baseSha, Disposable = true };
    }

    /// <summary>
    /// 바뀐 것 요약. <b>git 인 자리면 격리 방식과 무관하게 낸다</b> —
    /// 원본 직접도 저장소이므로 무엇이 바뀌었는지 말할 수 있다.
    /// </summary>
    public async Task<string?> DiffStatAsync(Prepared prepared, CancellationToken ct)
    {
        var dotGit = Path.Combine(prepared.Path, ".git");

        if (!Directory.Exists(dotGit) && !File.Exists(dotGit))
        {
            return null;
        }

        var r = await GitAsync(prepared.Path, ct, "diff", "--stat", "HEAD");
        var text = r.Output.Trim();

        return text.Length == 0 ? null : text[..Math.Min(text.Length, 480)];
    }

    /// <summary>
    /// 치운다. <b>바뀐 것이 있으면 남긴다</b> — 사람이 이어받을 자리다.
    /// </summary>
    public async Task CleanupAsync(Prepared prepared, bool keep, CancellationToken ct)
    {
        if (!prepared.Disposable || keep)
        {
            return;
        }

        try
        {
            if (prepared.Branch is not null && prepared.RepoPath is not null)
            {
                await GitAsync(prepared.RepoPath, ct, "worktree", "remove", "--force", prepared.Path);
                await GitAsync(prepared.RepoPath, ct, "branch", "-D", prepared.Branch);
            }
            else if (Directory.Exists(prepared.Path))
            {
                Directory.Delete(prepared.Path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("작업공간을 치우지 못했습니다: {Message}", ex.Message);
        }
    }

    private static string Short(string? sha) =>
        sha is { Length: > 7 } ? sha[..7] : sha ?? "?";

    /// <summary>긴 git 출력에서 앞 몇 줄만. 로그 한 줄이 화면을 덮지 않게 한다.</summary>
    private static string Head(string text) =>
        string.Join(' ', text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3));

    private static Task<(int ExitCode, string Output)> GitAsync(
        string workDir, CancellationToken ct, params string[] args)
        => RunAsync("git", ct, workDir, args);

    /// <summary>
    /// 짧은 명령 하나. <b>셸을 거치지 않는다</b> — 인자를 하나씩 넣는다.
    /// </summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(
        string exe, CancellationToken ct, string? workDir, params string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        if (workDir is not null)
        {
            psi.WorkingDirectory = workDir;
        }

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

    private static async Task<long> SizeMbAsync(string path, CancellationToken ct)
    {
        var r = await RunAsync("du", ct, null, "-sm", path);
        var head = r.Output.Split('\t', ' ')[0];

        return long.TryParse(head, out var mb) ? mb : 0;
    }

    public sealed class Prepared
    {
        /// <summary>CLI 가 돌 자리.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>어느 커밋에서 갈라졌나. 뒤처진 기준을 나중에 알아보려면 필요하다.</summary>
        public string? BaseSha { get; set; }

        public string? Branch { get; set; }

        /// <summary>worktree 를 매단 정본 경로.</summary>
        public string? RepoPath { get; set; }

        /// <summary>끝나고 치워도 되는 자리인가. 원본 직접이면 거짓이다.</summary>
        public bool Disposable { get; set; }
    }
}
