using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>실행 한 번이 일할 자리를 만든다. 원본에서 직접 돌리지 않는 것이 원칙이며, 비껴가는 법은 대상 종류마다 다르다 — repo: git worktree, folder+copy: 복사본, folder+inplace: 격리 없음(예외).</summary>
public sealed class Workspace(RunnerOptions options, ILogger<Workspace> logger)
{
    /// <summary>이 대상이 어떤 격리로 도는가. RunnerWorker 의 잠금 범위가 이 값으로 정해지므로 두 곳이 다른 답을 내면 안 된다. worktree 의 .git 은 폴더가 아니라 파일이다.</summary>
    public static string IsolationOf(ServerClient.Target target)
    {
        if (target.IsolationMode is { Length: > 0 } chosen)
        {
            return chosen;
        }

        if (target.TargetPath is not { Length: > 0 } path)
        {
            return "copy";
        }

        var dotGit = Path.Combine(path, ".git");

        return Directory.Exists(dotGit) || File.Exists(dotGit) ? "worktree" : "copy";
    }

    public async Task<Prepared> PrepareAsync(
        ServerClient.Claim claim, Func<string, Task> say, CancellationToken ct)
    {
        var target = claim.Target
            ?? throw new InvalidOperationException("대상이 없는 작업은 돌릴 수 없습니다.");

        var path = target.TargetPath
            ?? throw new InvalidOperationException("대상 경로가 비어 있습니다.");

        if (!Directory.Exists(path))
        {
            // 장비를 짚어 준다 — 「폴더가 없습니다」만 두면 다른 장비의 실행기가 집어 간 진짜 원인이 안 읽힌다.
            throw new DirectoryNotFoundException(
                $"이 장비({options.Name})에는 대상 폴더가 없습니다: {path} "
                + "— 대상의 「장비」가 맞게 지정돼 있는지 확인하십시오.");
        }

            // worktree 의 .git 은 파일이라, 폴더만 보면 저장소가 아닌 것으로 읽힌다.
        var dotGit = Path.Combine(path, ".git");
        var isRepo = Directory.Exists(dotGit) || File.Exists(dotGit);

        var isolation = IsolationOf(target);

            // 저장소로 등록했는데 아니면 여기서 말한다 — 그냥 넘기면 당기지도 올리지도 않은 채 성공으로 끝난다.
        if (!isRepo && target.TargetKind == "repo")
        {
            throw new InvalidOperationException(
                $"저장소로 등록된 대상인데 {path} 에 .git 이 없습니다. "
                + "대상의 종류나 경로를 확인하십시오.");
        }

        // ── git 저장소면 먼저 당긴다 ────────────────────────
        // 시작 전 pull 은 필수다 — 뒤처진 기준에서 만든 커밋을 main 에 밀면 남의 변경을 되돌리는 커밋이 될 수 있다.
        var baseSha = (string?)null;

        if (isRepo)
        {
            var dirty = await GitAsync(path, ct, "status", "--porcelain");

            if (!string.IsNullOrWhiteSpace(dirty.Output))
            {
                // 더러운 정본은 원본 직접일 때만 결과에 섞인다. worktree·복사본은 origin/<ref> 에서 갈라지므로, 여기서 막으면 앞 실행의 찌꺼기가 뒤의 모든 실행을 죽인다.
                if (isolation == "inplace")
                {
                    // 막지 않고 치운다 — 더러운 정본을 만드는 것은 대개 앞 실행(게이트가 금지 경로에 걸렸거나 CLI 가 도는 중 죽음)이라, 막으면 사람이 손으로 치울 때까지 큐가 통째로 막힌다.
                    // 치우기는 끝날 때와 같이 stash 로 옮긴다(살릴 것이면 git stash pop).
                    var label = $"ai-run-{claim.RunKey} (작업 {claim.TaskKey}) 시작 전 정본에 남아 있던 것";

                    await say("[준비] 정본에 정리되지 않은 변경이 있습니다. "
                              + "stash 로 옮기고 시작합니다: " + Head(dirty.Output));

                    var moved = await StashAsync(path, label, ct);

                    if (moved is { } fail)
                    {
                        // 치우지도 못하면 정직하게 막는다 — 더러운 자리에서 고치면 게이트의 git add -A 가 남의 변경까지 커밋한다.
                        throw new InvalidOperationException(
                            "정본이 깨끗하지 않은데 치우지도 못했습니다. 사람이 정리해야 합니다 "
                            + "(살릴 것이면 커밋, 버릴 것이면 git checkout -- . 와 git clean -fd):\n"
                            + fail);
                    }

                    await say($"[준비] 「{label}」 로 넣어 두었습니다 — 되살리려면 git stash pop.");
                }
                else
                {

                    await say("[준비] 정본에 정리되지 않은 변경이 있습니다 "
                              + "(이 작업은 origin 에서 갈라지므로 섞이지 않습니다): " + Head(dirty.Output));
                }
            }

            await say("[준비] git fetch origin");

            var fetch = await GitAsync(path, ct, "fetch", "origin");

            if (fetch.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"origin 에서 가져오지 못했습니다. 최신에서 시작할 수 없습니다:\n{fetch.Output}");
            }

            // 가지를 짚어 맞춘다 — 인자 없는 git pull 은 upstream 이 없으면 「no tracking information」으로 죽어 원인이 안 읽힌다.
            // pull --ff-only origin <가지> 로 짚으면 FETCH_HEAD 파일을 여러 실행이 겹쳐 써서 두 글이 섞인다. 위에서 받은 뒤라 원격 추적 가지를 짚어 얹기만 한다.
            var head = await GitAsync(path, ct, "rev-parse", "--abbrev-ref", "HEAD");
            var current = head.Output.Trim();

            if (current is { Length: > 0 } && current != "HEAD")
            {
                await say($"[준비] git merge --ff-only origin/{current}");

                var pull = await GitAsync(path, ct, "merge", "--ff-only", $"origin/{current}");

                if (pull.ExitCode != 0)
                {
                    // 원본 직접은 여기서 일한다 — 뒤처진 자리에서 고쳐 올리면 남의 변경을 되돌리므로 막는다.
                    if (isolation == "inplace")
                    {
                        // 여기까지 왔으면 정본은 깨끗하다. 그래도 ff 가 안 되면 정본 가지가 origin 과 갈라진 것이라 사람이 봐야 한다 — 자동 reset 은 그 장비에만 있던 커밋을 지운다.
                        throw new InvalidOperationException(
                            $"정본({current})이 origin 과 갈라져 있어 최신으로 맞추지 못했습니다. "
                            + "사람이 풀어야 합니다 (git log --oneline origin/{current}..HEAD 로 "
                            + $"그 장비에만 있는 커밋을 먼저 확인하십시오):\n{pull.Output}");
                    }

                    // worktree·복사본은 origin/<ref> 에서 갈라지므로 작업은 최신에서 시작한다. 멈출 일은 아니지만 조용히 넘기지도 않는다.
                    await say($"[준비] 정본 {current} 를 당기지 못했습니다 "
                              + "(작업은 origin 에서 갈라집니다): " + Head(pull.Output));
                }
            }
            else
            {
                await say("[준비] 정본이 어느 가지도 보고 있지 않아 당기지 않았습니다.");
            }

            baseSha = (await GitAsync(path, ct, "rev-parse", "HEAD")).Output.Trim();

            // 당긴 결과를 적는다 — 「pull 했다」만 남으면 어디서 시작했는지 기록에 없다.
            await say($"[준비] 시작 기준 {current} {Short(baseSha)}");
        }

        Directory.CreateDirectory(options.WorkspaceRoot);

        // ── 원본 직접 ───────────────────────────────────────
        if (isolation == "inplace")
        {
            await say("[준비] 원본에서 직접 돕니다. 되돌리려면 사람이 해야 합니다.");

            // 저장소인데 원본 직접이면 그 사실을 적는다 — 등록 실수로 inplace 가 잡히면 빠른지시가 운영 정본을 직접 고친다.
            // worktree 로 두면 실행마다 제 가지에서 돌아 정본이 더러워지지 않는다.
            if (isRepo)
            {
                await say("[준비] 이 대상은 git 저장소인데 격리가 「원본 직접」입니다. "
                          + "worktree 로 바꾸면 실행마다 제 가지에서 돌아 정본이 더러워지지 않습니다 "
                          + "— push 는 그대로 됩니다.");

                logger.LogWarning(
                    "대상 {Path} 는 git 저장소인데 격리가 inplace 입니다. worktree 를 권합니다.", path);
            }

            return new Prepared
            {
                Path = path,
                BaseSha = baseSha,
                Branch = null,
                Disposable = false,
                SourcePath = path,
                SourceIsRepo = isRepo,
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

            // 기준은 정본의 HEAD 가 아니라 이 worktree 가 선 자리다. 정본이 뒤처지면 그 차이가 「이 차수가 건드린 파일」로 둔갑하고,
            // 게이트의 금지 경로 검사도 같은 목록으로 막힌다. worktree 의 HEAD 를 읽으면 차수마다 제 몫만 센다.
            var at = await GitAsync(dir, ct, "rev-parse", "HEAD");

            if (at.ExitCode == 0 && at.Output.Trim() is { Length: > 0 } sha)
            {
                if (!string.Equals(sha, baseSha, StringComparison.Ordinal))
                {
                    // 정본이 뒤처져 있다는 신호라 조용히 바꾸지 않는다.
                    await say($"[준비] 정본({Short(baseSha)})과 worktree({Short(sha)})가 "
                              + "다른 자리입니다. 처리 파일·게이트는 worktree 를 기준으로 셉니다.");
                }

                baseSha = sha;
            }

            return new Prepared
            {
                Path = dir,
                BaseSha = baseSha,
                Branch = branch,
                RepoPath = path,
                Disposable = true,
                SourcePath = path,
                SourceIsRepo = true,
            };
        }

        // ── 복사본 ──────────────────────────────────────────
        //
        // 상한을 넘으면 미리 거절한다 — 복사에 몇 분을 쓰다 타임아웃으로 죽는 것보다 낫다.
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

        return new Prepared
        {
            Path = dir,
            BaseSha = baseSha,
            Disposable = true,
            SourcePath = path,
            SourceIsRepo = isRepo,
        };
    }

    /// <summary>바뀐 것 요약. git 인 자리면 격리 방식과 무관하게 낸다 — 원본 직접도 저장소이므로 무엇이 바뀌었는지 말할 수 있다.</summary>
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

    /// <summary>한 줄에 파일 하나. diff --stat 은 경로를 줄여 목록 구실을 못 하므로 파일 목록은 따로 둔다. 상한을 넘으면 마지막 줄에 … 과 남은 수만 적는다 — 상한이 없으면 대량 치환 한 번이 수천 줄을 DB 와 회로로 민다.</summary>
    private const int ChangedFileLines = 200;

    /// <summary>이 실행이 실제로 건드린 파일 목록. 한 줄에 상태\t경로. DiffStatAsync 가 얼마나 바뀌었는지라면 여기는 무엇이 바뀌었는지다.
    /// 기준은 BaseSha 다(AI 가 커밋한 뒤면 HEAD 와의 차이가 비어 목록이 빈다). 추적 안 하는 새 파일은 ls-files --others 로 더하고, core.quotePath=false 로 한국어 경로를 살린다.</summary>
    public async Task<string?> ChangedFilesAsync(Prepared prepared, CancellationToken ct)
    {
        var dotGit = Path.Combine(prepared.Path, ".git");

        if (!Directory.Exists(dotGit) && !File.Exists(dotGit))
        {
            // 복사본에는 .git 이 없어 물을 데가 없다 — 비워 두고 화면이 그 칸을 뺀다.
            return null;
        }

        var rows = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var tracked = await NameStatusAsync(prepared.Path, prepared.BaseSha, ct);

        foreach (var line in Lines(tracked))
        {
            // `R100\told\tnew` · `M\tpath` — **마지막 칸이 지금 경로**다.
            var cols = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);

            if (cols.Length < 2)
            {
                continue;
            }

            var path = cols[^1].Trim();

            if (path.Length > 0 && seen.Add(path))
            {
                rows.Add($"{cols[0][..1]}\t{path}");
            }
        }

        var others = await GitAsync(
            prepared.Path, ct, "-c", "core.quotePath=false",
            "ls-files", "--others", "--exclude-standard");

        if (others.ExitCode == 0)
        {
            foreach (var path in Lines(others.Output))
            {
                if (seen.Add(path))
                {
                    rows.Add($"A\t{path}");
                }
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        if (rows.Count > ChangedFileLines)
        {
            var over = rows.Count - ChangedFileLines;

            rows = [.. rows.Take(ChangedFileLines), $"…\t외 {over}개"];
        }

        return string.Join('\n', rows);
    }

    /// <summary>git diff --name-status 한 번. 기준을 못 짚으면 HEAD 로 물러선다.</summary>
    private static async Task<string> NameStatusAsync(
        string path, string? baseSha, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(baseSha))
        {
            var r = await GitAsync(
                path, ct, "-c", "core.quotePath=false",
                "diff", "--name-status", "--find-renames", baseSha);

            if (r.ExitCode == 0)
            {
                return r.Output;
            }
        }

        var head = await GitAsync(
            path, ct, "-c", "core.quotePath=false",
            "diff", "--name-status", "--find-renames", "HEAD");

        return head.ExitCode == 0 ? head.Output : string.Empty;
    }

    /// <summary>빈 줄을 걷고 앞뒤를 다듬은 줄들.</summary>
    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>복사본에서 돈 결과를 정본으로 되돌린다. 사본에는 .git 이 없어 거기서 커밋할 수 없으니, 되돌린 뒤 정본에서 커밋·push 한다.
    /// 복사할 때 규칙(.git 제외·--delete)을 뒤집되 .git 은 건드리지 않는다 — 덮으면 이력이 날아간다.</summary>
    public async Task<string?> SyncBackAsync(
        Prepared prepared, Func<string, Task> say, CancellationToken ct)
    {
        if (!prepared.Disposable || prepared.Branch is not null)
        {
            return null;   // 원본 직접·worktree 는 되돌릴 것이 없다.
        }

        await say($"[게이트] 복사본의 결과를 정본({prepared.SourcePath})으로 되돌립니다.");

        var r = await RunAsync("rsync", ct, workDir: null,
            "-a", "--delete", "--exclude", ".git",
            prepared.Path.TrimEnd('/') + "/", prepared.SourcePath.TrimEnd('/') + "/");

        return r.ExitCode == 0 ? null : $"정본으로 되돌리지 못했습니다:\n{r.Output}";
    }

    /// <summary>원본 직접으로 돈 실행이 정본에 남긴 것을 stash 로 치운다. 정본이 깨끗하지 않으면 뒤의 실행이 모두 죽으므로 필수다.
    /// 버리지 않고 stash 에 넣으며, 어디 들어갔는지 로그에 남긴다.</summary>
    /// <summary>정본에 남은 것을 버리지 않고 stash 로 옮긴다. 옮겼으면 null, 못 옮겼으면 이유를 돌려준다.
    /// 끝날 때(ParkAsync)와 시작할 때(PrepareAsync) 둘 다 부른다 — 시작 쪽은 끝 자리가 안 돈 실행의 그물이다. stash 도 커밋 객체라 이름·메일을 여기서 준다.</summary>
    private async Task<string?> StashAsync(string path, string label, CancellationToken ct)
    {
        var stash = await GitAsync(path, ct,
            "-c", "user.name=AI Task Runner", "-c", "user.email=ai-task@jsini.local",
            "stash", "push", "--include-untracked", "-m", label);

        return stash.ExitCode == 0 ? null : Head(stash.Output);
    }

    public async Task<string?> ParkAsync(
        Prepared prepared, string label, Func<string, Task> say, CancellationToken ct)
    {
        // worktree · 복사본은 자기 자리에 그대로 두고 치우면 그만이다.
        if (prepared.Disposable || !prepared.SourceIsRepo)
        {
            return null;
        }

        var path = prepared.SourcePath;

        // git status --porcelain 은 파싱하지 않는다 — 앞 두 글자 상태 때문에 경로 첫 글자가 잘린다. 있나 없나만 본다.
        var tracked = await GitAsync(path, ct, "diff", "--name-only", "HEAD");
        var untracked = await GitAsync(path, ct, "ls-files", "--others", "--exclude-standard");

        if (string.IsNullOrWhiteSpace(tracked.Output) && string.IsNullOrWhiteSpace(untracked.Output))
        {
            return null;   // 게이트가 커밋했거나 애초에 바뀐 것이 없다.
        }

        if (await StashAsync(path, label, ct) is { } fail)
        {
            // 못 치운 것을 성공으로 끝내지 않는다. 다음 실행은 PrepareAsync 에서 스스로 치우고 지나가지만, 치우지 못하는 이유는 대개 반복되므로 여기서 적어 둔다.
            await say($"[정리] 정본을 치우지 못했습니다:\n{fail}");
            logger.LogWarning("정본({Path})을 치우지 못했습니다: {Message}", path, fail);

            return null;
        }

        await say($"[정리] 이번 실행이 정본에 남긴 변경을 stash 에 넣었습니다 — 「{label}」. "
                  + "되살리려면 git stash pop, 버리려면 git stash drop.");

        logger.LogInformation("정본({Path})의 잔여 변경을 stash 에 넣었습니다: {Label}", path, label);

        return label;
    }

    /// <summary>치운다. 바뀐 것이 있으면 남긴다 — 사람이 이어받을 자리다.</summary>
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

    /// <summary>짧은 명령 하나. 셸을 거치지 않고 인자를 하나씩 넣는다.</summary>
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

        /// <summary>대상으로 등록된 원래 경로. 격리 방식과 무관하게 늘 채운다. 복사본의 결과는 정본으로 되돌린 뒤 거기서 커밋해야 하므로, 「어디로 되돌리나」가 이 값이다.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>정본이 git 저장소인가. 사본이 아니라 <b>원래 경로</b> 기준이다.</summary>
        public bool SourceIsRepo { get; set; }

        /// <summary>끝나고 치워도 되는 자리인가. 원본 직접이면 거짓이다.</summary>
        public bool Disposable { get; set; }
    }
}
