using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// AI 결과문에서 뽑은 경로를 대상에 실제로 대 본다. 서버가 호스트 저장소를 볼 수 없어 여기 있다.
/// 찾았으면 온 경로로 펴서 돌려주고, 이름만 맞는 것이 여럿이면 고르지 않으며, 없으면 없다고 말한다 — 틀린 경로 한 줄은 없는 줄보다 나쁘다.
/// </summary>
public sealed class FileProbe(ILogger<FileProbe> logger)
{
    /// <summary>한 대상에 쓰는 시간의 상한.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    /// <summary>git 이 아닌 대상(폴더 복사본)을 걸을 때의 상한. node_modules 같은 것을 끝없이 걷지 않게 한다.</summary>
    private const int WalkLimit = 80_000;

    /// <summary>목록에서 못 찾은 줄만 이력에 물어본다. 지운 파일이 거기 있으며, 호출 하나가 이력을 훑으므로 수를 묶어 둔다.</summary>
    private const int HistoryLookups = 10;

    /// <summary>대 본 결과.</summary>
    public enum State
    {
        /// <summary>실제로 있는(또는 있었던) 파일이다.</summary>
        Found,

        /// <summary>대상 어디에도 없다.</summary>
        Missing,

        /// <summary>이름만 맞는 것이 여럿이라 고르지 못했다.</summary>
        Ambiguous,
    }

    /// <summary>한 줄의 답.</summary>
    /// <param name="Raw">물어본 그대로. 서버가 이것으로 줄을 찾는다.</param>
    /// <param name="Path">찾았으면 대상 위치에서 본 온 경로. 아니면 빈 글자.</param>
    /// <param name="Result">셋 중 하나.</param>
    public sealed record Answer(string Raw, string Path, State Result);

    /// <summary>
    /// 경로 여럿을 한 대상에 대 본다. <b>목록은 한 번만 만든다.</b>
    /// </summary>
    /// <param name="targetPath">대상 위치. 경로를 재는 뿌리다.</param>
    /// <param name="workspacePath">
    /// 그때 일하던 자리. <b>아직 남아 있으면 거기부터 본다</b> — 올리지 못한
    /// 새 파일은 대상 저장소에 아직 없다.
    /// </param>
    /// <param name="gitBranch">
    /// 그 실행이 쓰던 가지. <b>대상 저장소의 작업본이 뒤처져 있어도</b> 그
    /// 가지에서는 보인다 — 아래 <see cref="IndexAsync"/> 머리말.
    /// </param>
    /// <param name="paths">대 볼 경로들.</param>
    public async Task<List<Answer>> CheckAsync(
        string? targetPath,
        string? workspacePath,
        string? gitBranch,
        IReadOnlyList<string> paths,
        CancellationToken ct)
    {
        var answers = new List<Answer>(paths.Count);

        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            // 이 장비의 대상이 아니다. 아무 말도 하지 않는다 — 없다고 적으면 다른 장비의 멀쩡한 파일이 없는 경로가 된다.
            return answers;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(Budget);

        // 상한에 걸린 것은 고장이 아니다. 여기서 올려 보내면 부르는 쪽이 멈추라는 신호로 읽는다 — 재 본 데까지만 돌려준다.
        try
        {
            var index = await IndexAsync(targetPath, gitBranch, budget.Token);
            var workspace = workspacePath is { Length: > 0 } w && Directory.Exists(w) ? w : null;

            // 마지막 마디로 미리 묶어 둔다 — 줄마다 전체를 훑으면 이백 줄 × 수만 개다.
            var byName = index
                .GroupBy(p => p[(p.LastIndexOf('/') + 1)..], StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var history = HistoryLookups;

            foreach (var raw in paths)
            {
                if (budget.IsCancellationRequested)
                {
                    break;
                }

                var answer = Resolve(raw, targetPath, workspace, index, byName);

                // 목록에 없어도 이력에 있으면 있었던 파일이다(지운 파일). 거기까지 봐야 D? 줄이 「없는 경로」가 되지 않는다.
                if (answer.Result == State.Missing && history > 0 && raw.Contains('/'))
                {
                    history--;

                    if (await KnownToGitAsync(targetPath, raw, budget.Token))
                    {
                        answer = answer with { Path = raw, Result = State.Found };
                    }
                }

                answers.Add(answer);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogInformation(
                "{Target} 을 대 보다 {Sec}초 상한에 걸렸습니다. 재 본 {Done}줄만 올립니다.",
                targetPath, Budget.TotalSeconds, answers.Count);
        }

        logger.LogDebug(
            "{Target} 에 {Asked}개를 대 봤습니다 (찾음 {Found} · 없음 {Missing} · 여럿 {Many}).",
            targetPath, paths.Count,
            answers.Count(a => a.Result == State.Found),
            answers.Count(a => a.Result == State.Missing),
            answers.Count(a => a.Result == State.Ambiguous));

        return answers;
    }

    /// <summary>한 줄을 가린다. 확실한 것부터 본다 — 그대로 있나 → 작업공간 → 꼬리가 맞는 것이 하나 → 이름이 맞는 것이 하나.</summary>
    private static Answer Resolve(
        string raw,
        string targetPath,
        string? workspacePath,
        HashSet<string> index,
        Dictionary<string, List<string>> byName)
    {
        // ① 적힌 그대로 있다. 가장 흔하고 가장 미덥다.
        if (index.Contains(raw) || Exists(targetPath, raw))
        {
            return new Answer(raw, raw, State.Found);
        }

        // ② 작업공간에만 있다 — 올리지 않은 새 파일이 여기 걸린다.
        if (workspacePath is not null && Exists(workspacePath, raw))
        {
            return new Answer(raw, raw, State.Found);
        }

        var name = raw[(raw.LastIndexOf('/') + 1)..];

        if (name.Length == 0)
        {
            return new Answer(raw, string.Empty, State.Missing);
        }

        if (!byName.TryGetValue(name, out var sameName) || sameName.Count == 0)
        {
            return new Answer(raw, string.Empty, State.Missing);
        }

        // ③ 꼬리가 맞는 것. 마디 경계로만 센다 — / 를 붙이지 않으면 b.cs 가 ab.cs 에 걸린다.
        var tail = sameName
            .Where(p => p.EndsWith('/' + raw, StringComparison.Ordinal))
            .ToList();

        if (tail.Count == 1)
        {
            return new Answer(raw, tail[0], State.Found);
        }

        if (tail.Count > 1)
        {
            return new Answer(raw, string.Empty, State.Ambiguous);
        }

        // ④ 이름만 맞는 것. 중간이 틀린 경로가 여기서 산다. 하나뿐일 때만 — 여럿이면 고르지 않는다.
        return sameName.Count == 1
            ? new Answer(raw, sameName[0], State.Found)
            : new Answer(raw, string.Empty, State.Ambiguous);
    }

    /// <summary>대상의 파일 목록 한 벌. 저장소면 git 에게 묻는다(빠르고 .gitignore 를 지킨다).
    /// 작업본만 보면 안 된다 — 대상 저장소는 늘 뒤처져 있어 방금 만든 파일이 「없는 경로」로 적히므로, 작업본·지금 가지·따라가는 원격·그 실행의 가지를 합쳐 본다(네트워크는 타지 않는다).</summary>
    private async Task<HashSet<string>> IndexAsync(
        string targetPath, string? gitBranch, CancellationToken ct)
    {
        var index = new HashSet<string>(StringComparer.Ordinal);
        var dotGit = Path.Combine(targetPath, ".git");

        if (Directory.Exists(dotGit) || File.Exists(dotGit))
        {
            // ① 지금 작업본 — 따라가는 것 + 아직 안 담은 것.
            foreach (var args in new[]
                     {
                         new[] { "ls-files" },
                         ["ls-files", "--others", "--exclude-standard"],
                     })
            {
                Collect(index, await GitAsync(targetPath, ct, args));
            }

            // ② 커밋 쪽 — **같은 커밋은 한 번만** 훑는다.
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var reference in Refs(gitBranch))
            {
                var sha = await GitAsync(targetPath, ct, "rev-parse", "--verify", "-q", reference + "^{commit}");

                if (sha.ExitCode != 0 || sha.Output.Trim() is not { Length: > 0 } id || !seen.Add(id))
                {
                    continue;
                }

                Collect(index, await GitAsync(targetPath, ct, "ls-tree", "-r", "--name-only", id));
            }

            if (index.Count > 0)
            {
                return index;
            }
        }

        // 저장소가 아니거나 git 이 빈손이다. 걸어서 센다 — **상한을 건다.**
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));

            foreach (var file in Walk(root))
            {
                index.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));

                if (index.Count >= WalkLimit)
                {
                    logger.LogDebug("{Target} 의 파일이 {Limit}개를 넘어 거기서 멈췄습니다.", targetPath, WalkLimit);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("{Target} 을 걷지 못했습니다: {Message}", targetPath, ex.Message);
        }

        return index;
    }

    /// <summary>훑어 볼 참조들. 없는 것은 rev-parse 가 걸러 준다. 뒤의 둘은 그 실행이 쓰던 가지다(올린 뒤 지워졌으면 원격 쪽에 남아 있다).</summary>
    private static IEnumerable<string> Refs(string? gitBranch)
    {
        yield return "HEAD";
        yield return "@{upstream}";
        yield return "origin/HEAD";

        if (gitBranch is { Length: > 0 } branch)
        {
            yield return branch;
            yield return "origin/" + branch;
        }
    }

    /// <summary>git 이 내놓은 경로 줄들을 목록에 담는다. 실패한 호출은 지나친다.</summary>
    private static void Collect(HashSet<string> index, (int ExitCode, string Output) r)
    {
        if (r.ExitCode != 0)
        {
            return;
        }

        foreach (var line in r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = line.Trim();

            if (path.Length > 0)
            {
                index.Add(path);
            }
        }
    }

    /// <summary>폴더를 걷는다. 사람이 고친 파일이 아닌 자리(안 볼 곳)는 미리 뺀다.</summary>
    private static IEnumerable<string> Walk(string root)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "bin", "obj", ".vs", ".idea", "dist", "packages",
        };

        var stack = new Stack<string>([root]);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            string[] dirs;

            try
            {
                files = Directory.GetFiles(dir);
                dirs = Directory.GetDirectories(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var f in files)
            {
                yield return f;
            }

            foreach (var d in dirs)
            {
                if (!skip.Contains(Path.GetFileName(d)))
                {
                    stack.Push(d);
                }
            }
        }
    }

    /// <summary>이력에 이 경로가 있나 — 지운 파일을 「없는 경로」로 적지 않으려는 것이다.</summary>
    private static async Task<bool> KnownToGitAsync(string targetPath, string path, CancellationToken ct)
    {
        var r = await GitAsync(
            targetPath, ct, "log", "-1", "--format=%H", "--all", "--", path);

        return r.ExitCode == 0 && r.Output.Trim().Length > 0;
    }

    /// <summary>뿌리 밖으로 나가지 않는 <c>File.Exists</c>.</summary>
    private static bool Exists(string root, string relative)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var head = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

            return full.StartsWith(head + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   && File.Exists(full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>git 한 번. 남의 작업을 막지 않고(--no-optional-locks), 경로를 사람 글자로 받는다(core.quotePath=false).</summary>
    private static async Task<(int ExitCode, string Output)> GitAsync(
        string workDir, CancellationToken ct, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = workDir,
        };

        psi.ArgumentList.Add("--no-optional-locks");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("core.quotePath=false");

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            using var p = Process.Start(psi)!;

            var stdout = await p.StandardOutput.ReadToEndAsync(ct);

            await p.WaitForExitAsync(ct);

            return (p.ExitCode, stdout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (-1, string.Empty);
        }
    }
}
