using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// AI 결과문에서 뽑은 경로를 <b>대상에 실제로 대 본다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>서버가 할 수 없는 일이라 여기 있다</b> — ProjMngServer 는 컨테이너 안에서
/// 돌고 대상 경로는 호스트의 것이다(<see cref="GitProbe"/> 와 같은 까닭).
/// </para>
/// <para>
/// <b>무엇이 문제였나 (2026-10-17).</b> 「처리 파일」 탭의 추정 줄은 모델이
/// 결과문을 읽고 적어 준 것이라 세 가지가 섞여 있었다.
/// </para>
/// <list type="number">
///   <item><description><b>파일이 아닌 것</b> — <c>AiChangedFiles.Merge</c>(메서드
///   이름) · <c>projmng.ai_task_run.files_merged_at</c>(DB 칸 이름).</description></item>
///   <item><description><b>마디가 빠진 경로</b> — <c>web/AiAskPanel.razor</c>
///   (참값은 <c>web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor</c>) ·
///   <c>Pages/AiDashboard.razor</c>.</description></item>
///   <item><description><b>이름 하나만</b> — <c>appsettings.json</c> 처럼 저장소에
///   열넷씩 있는 것.</description></item>
/// </list>
/// <para>
/// 그래서 답을 셋으로 나눈다. 찾았으면 <b>온 경로로 펴서</b> 돌려주고
/// (<see cref="State.Found"/>), 이름만 맞는 것이 여럿이면 고르지 않으며
/// (<see cref="State.Ambiguous"/>), 아무 데도 없으면 없다고 말한다
/// (<see cref="State.Missing"/>). <b>고르지 못한 것을 지어내지 않는 것</b>이
/// 요점이다 — 틀린 경로 한 줄은 없는 줄보다 나쁘다.
/// </para>
/// <para>
/// <b>읽기만 한다.</b> <c>ls-files</c> 와 <c>log</c> 뿐이고 네트워크를 타지
/// 않는다. 작업이 도는 대상도 건너뛰지 않는다(<c>--no-optional-locks</c>).
/// </para>
/// </remarks>
public sealed class FileProbe(ILogger<FileProbe> logger)
{
    /// <summary>한 대상에 쓰는 시간의 상한.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// git 이 아닌 대상(폴더 복사본)을 걸을 때의 상한. <b>끝없이 걷지 않는다</b> —
    /// 대상이 <c>node_modules</c> 를 품은 폴더면 수십만 개가 나온다.
    /// </summary>
    private const int WalkLimit = 80_000;

    /// <summary>
    /// 목록에서 못 찾은 줄만 <b>이력</b>에 물어본다. 지워진 파일이 거기 있다.
    /// 호출 하나가 저장소 이력을 훑으므로 <b>수를 묶어 둔다.</b>
    /// </summary>
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
            // **이 장비의 대상이 아니다.** 아무 말도 하지 않는다 — 없다고
            // 적으면 다른 장비에서 멀쩡히 고친 파일이 「없는 경로」가 된다.
            return answers;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(Budget);

        // **상한에 걸린 것은 고장이 아니다.** 여기서 올려 보내면 부르는 쪽의
        // 고리가 「멈추라는 신호」로 읽고 통째로 빠져나간다 — 저장소 하나가
        // 느린 날에 확인이 영영 안 돌게 된다. 재 본 데까지만 돌려준다.
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

                // 목록에 없더라도 **이력에 있으면 있었던 파일**이다 — 지운 파일이
                // 그렇다. 거기까지 봐야 `D?` 줄이 거짓으로 「없는 경로」가 되지 않는다.
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

    /// <summary>
    /// 한 줄을 가린다. <b>확실한 것부터 본다</b> — 그대로 있나 → 작업공간에 있나
    /// → 꼬리가 맞는 것이 하나인가 → 이름이 맞는 것이 하나인가.
    /// </summary>
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

        // ③ 꼬리가 맞는 것. `Pages/AiDashboard.razor` 가 여기서 펴진다.
        //    **마디 경계로만 센다** — `/` 를 앞에 붙이지 않으면
        //    `b.cs` 가 `ab.cs` 에 걸린다.
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

        // ④ 이름만 맞는 것. `web/AiAskPanel.razor` 처럼 **중간이 틀린** 경로가
        //    여기서 산다. 하나뿐일 때만 — 여럿이면 고르지 않는다.
        return sameName.Count == 1
            ? new Answer(raw, sameName[0], State.Found)
            : new Answer(raw, string.Empty, State.Ambiguous);
    }

    /// <summary>
    /// 대상의 파일 목록 한 벌. <b>저장소면 git 에게 묻는다</b> — 걷는 것보다
    /// 빠르고 <c>.gitignore</c> 를 공짜로 지킨다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>작업본만 보면 안 된다</b>(2026-10-17, 실제로 밟음). 대상 저장소
    /// (<c>/home/lee/Funeralv2</c>)는 AI 가 올린 것을 당겨 오지 않아
    /// <b>늘 뒤처져 있다</b> — 재어 보니 <c>origin/main</c> 보다 수십 커밋
    /// 뒤였다. 그 상태로 작업본만 대 보면 <b>방금 만든 파일이 「없는
    /// 경로」로 적힌다.</b> 실제로 운영 자료에서
    /// <c>wwwroot/js/tab-scroll.js</c> 가 그렇게 나왔다 — 그 파일은
    /// <c>origin/main</c> 에 멀쩡히 있다.
    /// </para>
    /// <para>
    /// 그래서 <b>참조 몇을 합친다</b> — 지금 작업본 · 지금 가지 · 따라가는
    /// 원격 · 그 실행이 쓰던 가지. 같은 커밋을 가리키는 것은 한 번만 훑는다.
    /// <b>네트워크는 안 탄다</b>(<see cref="GitProbe"/> 와 같은 규칙) — 이미
    /// 받아 둔 원격 추적 참조만 본다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 훑어 볼 참조들. <b>없는 것은 <c>rev-parse</c> 가 걸러 준다.</b>
    /// </summary>
    /// <remarks>
    /// <c>@{upstream}</c> 은 지금 가지가 따라가는 원격이고, <c>origin/HEAD</c>
    /// 는 그것이 없을 때의 기본 가지다. 뒤의 둘은 <b>그 실행이 쓰던 가지</b> —
    /// 올린 뒤 지워졌으면 원격 쪽에 남아 있다.
    /// </remarks>
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

    /// <summary>
    /// 폴더를 걷는다. <b>안 볼 자리를 미리 뺀다</b> — 그 안에 든 것은 사람이
    /// 고친 파일이 아니다.
    /// </summary>
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

    /// <summary>
    /// 이력에 이 경로가 있나. <b>지운 파일을 「없는 경로」로 적지 않으려는 것</b>이다.
    /// </summary>
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

    /// <summary>
    /// git 한 번. <b>남의 작업을 막지 않는다</b>(<c>--no-optional-locks</c>) ·
    /// <b>경로를 사람 글자로 받는다</b>(<c>core.quotePath=false</c>).
    /// </summary>
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
