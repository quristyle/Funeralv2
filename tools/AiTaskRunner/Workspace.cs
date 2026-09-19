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
    /// <summary>
    /// 이 대상이 <b>어떤 격리로 돌 것인가.</b>
    ///
    /// <para>
    /// <see cref="PrepareAsync"/> 안에서만 정하던 값을 밖으로 냈다.
    /// 실행기가 <b>어디까지 잠글지</b>를 이 값으로 정하기 때문이다
    /// (<c>RunnerWorker</c> 의 대상 잠금). 두 곳이 서로 다른 답을 내면
    /// 원본 직접인데 잠그지 않는 경우가 생기고, 그것이 곧 두 실행이 같은
    /// 파일을 동시에 고치는 상황이다.
    /// </para>
    ///
    /// <para>
    /// worktree 의 <c>.git</c> 은 폴더가 아니라 <b>파일</b>이다. 폴더만 보면
    /// worktree 안에서 돌 때 저장소가 아닌 것으로 읽힌다.
    /// </para>
    /// </summary>
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

        var isolation = IsolationOf(target);

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
                // **더러운 정본이 실제로 문제가 되는 것은 거기서 직접 돌 때뿐이다.**
                // worktree · 복사본은 방금 받은 origin/<ref> 에서 갈라지므로 정본에
                // 남은 것이 결과에 섞이지 않는다. 그런데도 예전에는 여기서 전부
                // 막았고, 원본 직접으로 돈 실행 하나가 남긴 찌꺼기가 **그 뒤의
                // 모든 실행을 죽였다** — 실제로 그렇게 멈췄다.
                if (isolation == "inplace")
                {
                    // **막지 않고 치운다 (2026-09-19).**
                    //
                    // 예전에는 여기서 던졌다. 그런데 이 자리에서 더러운 정본을
                    // 만드는 것은 대개 사람이 아니라 **앞 실행**이다 — 게이트가
                    // 금지 경로에 걸려 커밋을 못 했거나, 실행기가 CLI 가 도는
                    // 중에 죽어 뒷정리(<see cref="ParkAsync"/>)가 안 돌았거나.
                    // 그러면 그 뒤로 집혀 가는 건이 **하나도 빠짐없이** 같은
                    // 말로 죽고, 사람이 그 장비에 들어가 손으로 치울 때까지
                    // 큐가 통째로 막힌다. 실제로 그렇게 여덟 건이 연달아
                    // 실패했다(대상 5번, 2026-09-19).
                    //
                    // 치우는 방법은 끝날 때 하는 것과 **똑같다** — 버리지 않고
                    // stash 로 옮긴다. 살릴 것이면 `git stash pop` 이다.
                    var label = $"ai-run-{claim.RunKey} (작업 {claim.TaskKey}) 시작 전 정본에 남아 있던 것";

                    await say("[준비] 정본에 정리되지 않은 변경이 있습니다. "
                              + "stash 로 옮기고 시작합니다: " + Head(dirty.Output));

                    var moved = await StashAsync(path, label, ct);

                    if (moved is { } fail)
                    {
                        // 치우지도 못하면 그때는 정직하게 막는다. 더러운 자리에서
                        // 고치면 게이트의 `git add -A` 가 **남의 변경까지** 집어
                        // 커밋하고, 이 대상은 그것이 곧 운영 배포다.
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

            // **가지를 짚어서 맞춘다.** 인자 없는 `git pull` 은 그 가지에
            // upstream 설정이 있어야 돌고, 없으면 「no tracking information」
            // 으로 죽는다 — 최신이 아니라 설정이 없다는 뜻인데 그 문구로는
            // 읽히지 않는다.
            //
            // 그렇다고 `git pull --ff-only origin <가지>` 로 짚지도 않는다.
            // pull 은 받은 것을 `FETCH_HEAD` **파일**로 넘기는데 그 파일은
            // 자물쇠 없이 덮어써서, **같은 정본에서 다른 실행의 git 이 겹치면
            // 두 글이 한 파일에 섞인다**(PushGate ⑤ 에 같은 설명이 있다).
            // 바로 위에서 이미 다 받았으니 **원격 추적 가지를 짚어 얹기만**
            // 하면 된다 — 그 길은 `FETCH_HEAD` 를 읽지 않는다.
            var head = await GitAsync(path, ct, "rev-parse", "--abbrev-ref", "HEAD");
            var current = head.Output.Trim();

            if (current is { Length: > 0 } && current != "HEAD")
            {
                await say($"[준비] git merge --ff-only origin/{current}");

                var pull = await GitAsync(path, ct, "merge", "--ff-only", $"origin/{current}");

                if (pull.ExitCode != 0)
                {
                    // **원본 직접은 여기서 일한다.** 뒤처진 자리에서 고치고
                    // 올리면 그 사이 남이 올린 것을 되돌린다 — 막는다.
                    if (isolation == "inplace")
                    {
                        // **여기까지 왔으면 정본은 이미 깨끗하다**(위에서 치웠다).
                        // 그런데도 ff 가 안 된다는 것은 정본의 가지가 origin 과
                        // **갈라져 있다**는 뜻이고, 그것은 사람이 봐야 한다 —
                        // 자동으로 reset 해 버리면 그 장비에만 있던 커밋이
                        // 말없이 사라진다.
                        throw new InvalidOperationException(
                            $"정본({current})이 origin 과 갈라져 있어 최신으로 맞추지 못했습니다. "
                            + "사람이 풀어야 합니다 (git log --oneline origin/{current}..HEAD 로 "
                            + $"그 장비에만 있는 커밋을 먼저 확인하십시오):\n{pull.Output}");
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

            // **저장소인데 원본 직접이면 그 사실을 적어 둔다.**
            //
            // 대상 5번(`portal-jsini`)이 그랬다 — `/home/lee/Funeralv2` 를
            // 「그냥 폴더」로 등록하는 바람에 격리가 inplace 로 잡혔고, 빠른지시가
            // 전부 운영 정본을 직접 고쳤다. 같은 경로가 1번에는 저장소로 제대로
            // 등록돼 있었는데 **push 를 켜려고 5번을 따로 만든 것**이 원인이었다.
            //
            // worktree 로 두면 실행마다 제 가지에서 돌아 정본이 더러워질 일이
            // 없고 push 도 그대로 된다. 그래서 굳이 이 길을 갈 이유가 거의 없다.
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

        return new Prepared
        {
            Path = dir,
            BaseSha = baseSha,
            Disposable = true,
            SourcePath = path,
            SourceIsRepo = isRepo,
        };
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
    /// 복사본에서 돈 결과를 <b>정본으로 되돌린다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사본에는 <c>.git</c> 이 없어 그 자리에서는 커밋할 수 없다. 그렇다고
    /// 「올릴 수 없는 대상」으로 끝내면 <b>올리기를 켜 둔 사람의 결과가
    /// 아무 데도 안 간다</b> — 그래서 되돌린 뒤 정본에서 커밋·push 한다.
    /// </para>
    /// <para>
    /// <b>복사할 때 쓴 규칙을 그대로 뒤집는다</b>(<c>.git</c> 제외 · <c>--delete</c>).
    /// 사본은 정본을 그대로 뜬 것에 AI 의 손이 얹힌 것이라, AI 가 지운 파일은
    /// 정본에서도 지워지는 것이 맞다. <c>.git</c> 만은 건드리지 않는다 —
    /// 그것까지 덮으면 이력이 통째로 날아간다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 원본 직접으로 돈 실행이 정본에 남긴 것을 <b>stash 로 치워 둔다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「다음 실행은 깨끗한 정본에서 시작한다」(설계 7.3)는 <b>앞의 실행이 자기
    /// 뒤를 치워야</b> 성립한다. worktree · 복사본은 치울 자리가 따로 있지만
    /// 원본 직접은 그 자리가 정본이라, CLI 가 실패하거나 게이트가 커밋 전에
    /// 막으면(금지 경로) 고쳐 놓은 파일이 정본에 그대로 남는다. 그러면
    /// <b>그 뒤의 모든 실행이 「정본이 깨끗하지 않습니다」로 죽는다</b> —
    /// 사람이 손으로 치울 때까지. 실제로 그렇게 멈췄다.
    /// </para>
    /// <para>
    /// <b>버리지 않고 stash 에 넣는다.</b> 시작할 때 깨끗한 것을 확인했으니
    /// 지금 더러운 것은 전부 이번 실행이 만든 것이다. 그래도 사람이 볼 값어치가
    /// 있는 결과물이라 지우지 않고, 어디 들어갔는지 로그에 남긴다 —
    /// 조용히 치우면 「분명 고쳤는데 없어졌다」가 된다.
    /// </para>
    /// </remarks>
    /// <summary>
    /// 정본에 남은 것을 <b>버리지 않고 stash 로 옮긴다.</b>
    /// 옮겼으면 <c>null</c>, 못 옮겼으면 그 이유를 돌려준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 부르는 자리가 둘이다 — 실행이 <b>끝날 때</b>(<see cref="ParkAsync"/>)와
    /// <b>시작할 때</b>(<see cref="PrepareAsync"/>). 끝에서 치우는 것이
    /// 정상이고, 시작에서 치우는 것은 <b>그 정상이 한 번 걸렀을 때를 받는
    /// 그물</b>이다(실행기가 중간에 죽으면 끝나는 자리가 아예 안 돈다).
    /// </para>
    /// <para>
    /// <b>stash 도 커밋 객체를 만든다</b> — 이름·메일이 없으면 「unable to
    /// auto-detect email address」로 죽는다. systemd 로 도는 프로세스에는 그
    /// 설정이 없을 수 있어 여기서 준다(<c>PushGate</c> 의 커밋과 같은 값).
    /// </para>
    /// </remarks>
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

        // **`git status --porcelain` 을 파싱하지 않는다**(PushGate 와 같은 이유 —
        // 앞 두 글자 상태 때문에 경로 첫 글자가 잘린다). 있나 없나만 본다.
        var tracked = await GitAsync(path, ct, "diff", "--name-only", "HEAD");
        var untracked = await GitAsync(path, ct, "ls-files", "--others", "--exclude-standard");

        if (string.IsNullOrWhiteSpace(tracked.Output) && string.IsNullOrWhiteSpace(untracked.Output))
        {
            return null;   // 게이트가 커밋했거나 애초에 바뀐 것이 없다.
        }

        if (await StashAsync(path, label, ct) is { } fail)
        {
            // 치우지 못한 것을 성공으로 끝내지 않는다. 다음 실행이 여기에서
            // 막힐 것이므로 **왜 막히는지를 지금 적어 둔다.**
            //
            // 다만 이제는 다음 실행이 그 자리에서 죽지 않고 **스스로 치우고
            // 지나간다**(`PrepareAsync`). 여기 적는 것은 그래도 사람이 알고
            // 있어야 하기 때문이다 — 치우지 못하는 이유는 대개 반복된다.
            await say($"[정리] 정본을 치우지 못했습니다:\n{fail}");
            logger.LogWarning("정본({Path})을 치우지 못했습니다: {Message}", path, fail);

            return null;
        }

        await say($"[정리] 이번 실행이 정본에 남긴 변경을 stash 에 넣었습니다 — 「{label}」. "
                  + "되살리려면 git stash pop, 버리려면 git stash drop.");

        logger.LogInformation("정본({Path})의 잔여 변경을 stash 에 넣었습니다: {Label}", path, label);

        return label;
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

        /// <summary>
        /// 대상으로 등록된 원래 경로. <b>격리 방식과 무관하게 늘 채운다.</b>
        /// </summary>
        /// <remarks>
        /// 복사본에서 돈 결과를 올리려면 <b>정본으로 되돌린 뒤 거기서
        /// 커밋해야 한다</b> — 사본에는 <c>.git</c> 이 없다. 그때 「어디로
        /// 되돌리나」가 이 값이다.
        /// </remarks>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>정본이 git 저장소인가. 사본이 아니라 <b>원래 경로</b> 기준이다.</summary>
        public bool SourceIsRepo { get; set; }

        /// <summary>끝나고 치워도 되는 자리인가. 원본 직접이면 거짓이다.</summary>
        public bool Disposable { get; set; }
    }
}
