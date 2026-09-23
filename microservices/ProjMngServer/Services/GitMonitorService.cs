using System.Collections.Concurrent;
using System.Text.Json;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// Git 종합 모니터링 — 저장소마다 통계 · 가지 · 태그 · 풀 리퀘스트 ·
/// Actions 실행 · 커밋 · 기여자를 모은다.
/// </summary>
/// <remarks>
/// <para>
/// 저장소 하나에 왕복이 <b>일곱 + 가지 수</b>다. GitHub 가 가지 목록에 커밋
/// 날짜를 안 주기 때문에 묵은 가지를 가리려면 가지마다 한 번씩 더 물어야 한다
/// — 그래서 <b>화면에 보이는 만큼만</b> 묻는다(<c>BranchDetail</c>, 기본 8).
/// </para>
///
/// <para>
/// 토큰이 없으면 시간당 60회다. 저장소 하나에 열다섯 번쯤 쓰므로 <b>네 번쯤
/// 새로 고치면 바닥난다</b> — 캐시가 2분이고 화면이 남은 한도를 보여 주는
/// 까닭이 그것이다.
/// </para>
/// </remarks>
public sealed class GitMonitorService(IConfiguration configuration, GitHubClient github)
{
    /// <summary>
    /// 담아 둔 결과 한 벌.
    /// </summary>
    /// <remarks>
    /// <b>남은 한도를 여기 같이 담는다.</b> <see cref="GitHubClient"/> 는 요청
    /// 마다 새로 생기므로(scoped) 캐시로 답하는 요청은 호출을 한 번도 안 해
    /// 한도를 모른다 — 그러면 화면의 「남은 한도」가 <b>캐시가 듣는 동안 내내
    /// 비어 있고</b>, 하필 그 칸은 「왜 갑자기 안 보이나」를 보려고 만든 자리다.
    /// </remarks>
    private sealed record Entry(
        List<GitMonitorRow> Rows, DateTimeOffset At, int? RateRemaining, int? RateLimit,
        List<GitBlocked> Blocked);

    private static readonly ConcurrentDictionary<int, Entry> Cache = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public async Task<GitResult<GitMonitorRow>> LoadAsync(int prjRid, bool refresh)
    {
        var opt = GitOptions.For(configuration, prjRid);

        var result = new GitResult<GitMonitorRow>
        {
            Configured = opt.Configured,
            Authenticated = opt.Authenticated,
            WebBaseUrl = opt.WebBaseUrl,
        };

        if (!opt.Configured) return result;

        if (!refresh && Cache.TryGetValue(prjRid, out var hit)
            && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.MonitorCacheSeconds))
        {
            return Fill(result, hit);
        }

        var gate = Locks.GetOrAdd(prjRid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            if (!refresh && Cache.TryGetValue(prjRid, out hit)
                && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.MonitorCacheSeconds))
            {
                return Fill(result, hit);
            }

            var rows = new GitMonitorRow[opt.Repos.Length];
            var blocked = new System.Collections.Concurrent.ConcurrentBag<GitBlocked>();

            using var slots = new SemaphoreSlim(4, 4);

            await Task.WhenAll(opt.Repos.Select(async (repo, i) =>
            {
                await slots.WaitAsync();
                try { rows[i] = await OneAsync(opt, repo, blocked); }
                finally { slots.Release(); }
            }));

            // 이미지는 저장소가 아니라 **소유자**에 매달린다. 소유자마다 한 번만
            // 묻고 각 줄에 나눠 준다 — 저장소마다 물으면 같은 목록을 여러 번 받는다.
            await FillPackagesAsync(opt, rows, blocked);

            var entry = new Entry(
                [.. rows], DateTimeOffset.UtcNow, github.RateRemaining, github.RateLimit,
                [.. blocked.DistinctBy(b => b.What)]);

            Cache[prjRid] = entry;

            return Fill(result, entry);
        }
        finally
        {
            gate.Release();
        }
    }

    private GitResult<GitMonitorRow> Fill(GitResult<GitMonitorRow> result, Entry entry)
    {
        result.Rows = entry.Rows;
        result.LoadedAt = entry.At.ToLocalTime().ToString("HH:mm:ss");
        result.RateRemaining = entry.RateRemaining;
        result.RateLimit = entry.RateLimit;
        result.Blocked = entry.Blocked;

        return result;
    }

    private async Task<GitMonitorRow> OneAsync(
        GitOptions opt, string repo, System.Collections.Concurrent.ConcurrentBag<GitBlocked> blocked)
    {
        var row = new GitMonitorRow
        {
            Repo = repo,
            WebUrl = opt.WebUrl(repo),
            RunsUrl = opt.RunsUrl(repo),
        };

        var since = DateTimeOffset.UtcNow.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ssZ");

        string? error = null;

        // 저장소 자체를 못 읽으면 나머지는 볼 것도 없다 — 그 까닭만 담는다.
        var info = github.GetAsync(opt, $"/repos/{repo}", e => error = e);
        var branches = github.GetAsync(opt, $"/repos/{repo}/branches?per_page=100");
        var tags = github.CountAsync(opt, $"/repos/{repo}/tags?per_page=1");
        var prs = github.GetAsync(opt, $"/repos/{repo}/pulls?state=open&per_page=20");
        var runs = github.GetAsync(opt, $"/repos/{repo}/actions/runs?per_page={opt.RunSample}");
        var commits = github.GetAsync(opt, $"/repos/{repo}/commits?since={since}&per_page=100");
        var contribs = github.GetAsync(opt, $"/repos/{repo}/contributors?per_page=100");
        var releases = github.GetAsync(opt, $"/repos/{repo}/releases?per_page=10");

        await Task.WhenAll(info, branches, tags, prs, runs, commits, contribs, releases);

        using var infoDoc = info.Result;
        using var branchDoc = branches.Result;
        using var prDoc = prs.Result;
        using var runDoc = runs.Result;
        using var commitDoc = commits.Result;
        using var contribDoc = contribs.Result;
        using var releaseDoc = releases.Result;

        if (infoDoc is null)
        {
            row.Error = error ?? "저장소를 읽지 못했습니다.";
            return row;
        }

        FillInfo(row, infoDoc);
        FillBranches(row, branchDoc);
        row.Tags = tags.Result;
        FillPrs(row, prDoc);
        FillRuns(row, runDoc);
        FillCommits(row, commitDoc);
        FillContribs(row, contribDoc);
        FillReleases(row, releaseDoc);

        // 날짜가 필요한 가지만 하나씩 더 묻는다(머리말).
        await FillBranchDatesAsync(opt, repo, row);

        // 통계는 **쓰기 권한이 있는 토큰**에만 열린다. 토큰이 없으면 아예
        // 부르지 않는다 — 401 을 받아 봐야 한도만 축낸다.
        await FillTrafficAsync(opt, repo, row, blocked);

        // 깨진 자리는 최근 표본에 실패가 있을 때만 더 묻는다.
        await FillFailureAsync(opt, repo, row);

        return row;
    }

    // ──────────────────────────────────────────── 담기

    private static void FillInfo(GitMonitorRow row, JsonDocument doc)
    {
        var e = doc.RootElement;

        row.Description = GitJson.Str(e, "description");
        row.DefaultBranch = GitJson.Str(e, "default_branch");
        row.Language = GitJson.Str(e, "language");
        row.Visibility = GitJson.Str(e, "visibility");
        row.PushedAt = GitJson.Str(e, "pushed_at");
        row.CreatedAt = GitJson.Str(e, "created_at");
        row.OpenIssues = GitJson.Long(e, "open_issues_count");
        row.Stars = GitJson.Long(e, "stargazers_count");
        row.Forks = GitJson.Long(e, "forks_count");
        row.SizeKb = GitJson.Long(e, "size");
    }

    private static void FillBranches(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var b in doc.RootElement.EnumerateArray())
        {
            row.BranchList.Add(new GitBranch
            {
                Name = GitJson.Str(b, "name"),
                Protected = GitJson.Flag(b, "protected"),
                Default = GitJson.Str(b, "name") == row.DefaultBranch,
            });
        }

        row.Branches = row.BranchList.Count;
    }

    /// <summary>
    /// 가지의 마지막 커밋 날짜를 채운다.
    /// </summary>
    /// <remarks>
    /// <b>일부만 채운다.</b> 가지가 백 개면 왕복도 백 번이라, 화면이 보여 주는
    /// 만큼만 묻고 「몇 개를 봤는지」를 함께 내보낸다 — 안 밝히면 묵은 가지
    /// 수를 전체 기준으로 읽는다.
    /// </remarks>
    private async Task FillBranchDatesAsync(GitOptions opt, string repo, GitMonitorRow row)
    {
        if (opt.BranchDetail <= 0 || row.BranchList.Count == 0) return;

        // 기본 가지를 먼저 본다 — 그것이 묵었다면 나머지는 볼 것도 없다.
        var targets = row.BranchList
            .OrderByDescending(b => b.Default)
            .ThenBy(b => b.Name, StringComparer.Ordinal)
            .Take(opt.BranchDetail)
            .ToList();

        var cut = DateTimeOffset.UtcNow.AddDays(-opt.StaleBranchDays);

        using var slots = new SemaphoreSlim(4, 4);

        await Task.WhenAll(targets.Select(async branch =>
        {
            if (branch.Name is null) return;

            await slots.WaitAsync();

            try
            {
                using var doc = await github.GetAsync(
                    opt, $"/repos/{repo}/branches/{Uri.EscapeDataString(branch.Name)}");

                if (doc is null) return;

                if (GitJson.Obj(doc.RootElement, "commit") is not { } commit) return;
                if (GitJson.Obj(commit, "commit") is not { } inner) return;
                if (GitJson.Obj(inner, "committer") is not { } committer) return;

                branch.At = GitJson.Str(committer, "date");
                branch.Stale = DateTimeOffset.TryParse(branch.At, out var at) && at < cut;
            }
            finally
            {
                slots.Release();
            }
        }));

        row.BranchesChecked = targets.Count(b => b.At is not null);
        row.StaleBranches = targets.Count(b => b.Stale);

        // 목록은 최근 순으로 보여 준다. 날짜를 모르는 가지는 뒤로.
        row.BranchList = [.. targets.OrderByDescending(b => b.At ?? "")];
    }

    private static void FillPrs(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var p in doc.RootElement.EnumerateArray())
        {
            string? Ref(string side) =>
                GitJson.Obj(p, side) is { } r ? GitJson.Str(r, "ref") : null;

            row.PrList.Add(new GitPr
            {
                Number = GitJson.Long(p, "number"),
                Title = GitJson.Str(p, "title"),
                Author = GitJson.Obj(p, "user") is { } u ? GitJson.Str(u, "login") : null,
                Source = Ref("head"),
                Target = Ref("base"),
                CreatedAt = GitJson.Str(p, "created_at"),
                UpdatedAt = GitJson.Str(p, "updated_at"),
                Draft = GitJson.Flag(p, "draft"),
                WebUrl = GitJson.Str(p, "html_url"),
            });
        }

        row.OpenPrs = row.PrList.Count;
    }

    private static void FillRuns(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null) return;

        if (!doc.RootElement.TryGetProperty("workflow_runs", out var runs)
            || runs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var stats = new GitRunStats
        {
            Total = (int)(GitJson.Long(doc.RootElement, "total_count") ?? 0),
        };

        double durSum = 0;
        var durCnt = 0;

        foreach (var r in runs.EnumerateArray())
        {
            var state = GitJson.RunState(r);
            stats.Recent.Add(state);

            if (state == "success") stats.Success++;
            else if (state == "failure") stats.Failure++;
            else stats.Other++;

            var at = GitJson.Str(r, "run_started_at") ?? GitJson.Str(r, "created_at");
            var done = GitJson.Str(r, "updated_at");
            var dur = GitJson.Seconds(at, done);

            // 아직 도는 실행의 시간은 평균에 넣지 않는다 — 끝나야 뜻이 있는 값이다.
            if (dur is not null && state is "success" or "failure")
            {
                durSum += dur.Value;
                durCnt++;
            }

            var one = new GitRunBrief
            {
                Status = state,
                WorkflowName = GitJson.Str(r, "name"),
                Branch = GitJson.Str(r, "head_branch"),
                At = done ?? at,
                Duration = dur,
                Url = GitJson.Str(r, "html_url"),
            };

            stats.Last ??= one;
            if (state == "success") stats.LastSuccess ??= one;
            if (state == "failure") stats.LastFailure ??= one;
        }

        stats.Sampled = stats.Recent.Count;

        var finished = stats.Success + stats.Failure;
        stats.Rate = finished > 0 ? Math.Round(stats.Success * 100.0 / finished, 1) : null;
        stats.AvgDuration = durCnt > 0 ? Math.Round(durSum / durCnt, 1) : null;

        row.Runs = stats;
    }

    private static void FillCommits(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return;

        var arr = doc.RootElement;
        row.Commits7d = arr.GetArrayLength();

        // 한 쪽에 100건까지다. 꽉 찼으면 더 있다는 뜻이고, 그것을 안 밝히면
        // 「7일에 딱 100건」으로 읽힌다.
        row.Commits7dCapped = row.Commits7d >= 100;

        var byAuthor = new Dictionary<string, long>();

        foreach (var c in arr.EnumerateArray())
        {
            // 사람 이름은 커밋 안쪽에 있다. 바깥의 `author` 는 GitHub 계정이라
            // **커밋한 사람과 다를 수 있고 없을 수도 있다**(계정이 안 이어진 메일).
            var name = GitJson.Obj(c, "commit") is { } inner
                       && GitJson.Obj(inner, "author") is { } a
                ? GitJson.Str(a, "name") ?? "-"
                : "-";

            byAuthor[name] = byAuthor.GetValueOrDefault(name) + 1;
        }

        row.CommitAuthors7d =
        [
            .. byAuthor.OrderByDescending(k => k.Value)
                       .Take(5)
                       .Select(k => new GitNameCount { Name = k.Key, Count = k.Value })
        ];

        if (arr.GetArrayLength() == 0) return;

        var top = arr[0];

        row.LastCommit = new GitCommit
        {
            Id = GitJson.Str(top, "sha") is { } sha ? sha[..Math.Min(7, sha.Length)] : null,
            Url = GitJson.Str(top, "html_url"),
        };

        if (GitJson.Obj(top, "commit") is { } commit)
        {
            row.LastCommit.Title = GitJson.Str(commit, "message")?.Split('\n')[0];

            if (GitJson.Obj(commit, "author") is { } author)
            {
                row.LastCommit.Author = GitJson.Str(author, "name");
                row.LastCommit.At = GitJson.Str(author, "date");
            }
        }
    }

    private static void FillContribs(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return;

        var list = doc.RootElement.EnumerateArray()
            .Select(c => new GitNameCount
            {
                Name = GitJson.Str(c, "login"),
                Count = GitJson.Long(c, "contributions") ?? 0,
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        row.Contributors = list.Count;
        row.ContributorList = [.. list.Take(5)];
    }

    // ──────────────────────────────────────────── 토큰이 있어야 보이는 것

    /// <summary>
    /// 방문·클론 통계(최근 14일).
    /// </summary>
    /// <remarks>
    /// <b>쓰기 권한이 있는 토큰</b>에만 열린다(fine-grained 는
    /// <c>Administration: Read</c>). 토큰이 없으면 <b>부르지도 않는다</b> —
    /// 401 을 받아 봐야 남은 한도만 축낸다. 대신 「못 봤다」를 남긴다.
    /// </remarks>
    private async Task FillTrafficAsync(
        GitOptions opt, string repo, GitMonitorRow row,
        System.Collections.Concurrent.ConcurrentBag<GitBlocked> blocked)
    {
        if (!opt.Authenticated)
        {
            blocked.Add(new GitBlocked
            {
                What = "방문·클론 통계",
                Why = "토큰이 없습니다.",
                Needs = "Administration: Read (또는 저장소 쓰기 권한)",
            });

            return;
        }

        string? why = null;

        var views = github.GetAsync(opt, $"/repos/{repo}/traffic/views", e => why = e);
        var clones = github.GetAsync(opt, $"/repos/{repo}/traffic/clones");
        var paths = github.GetAsync(opt, $"/repos/{repo}/traffic/popular/paths");

        await Task.WhenAll(views, clones, paths);

        using var viewDoc = views.Result;
        using var cloneDoc = clones.Result;
        using var pathDoc = paths.Result;

        if (viewDoc is null)
        {
            blocked.Add(new GitBlocked
            {
                What = "방문·클론 통계",
                Why = why ?? "읽지 못했습니다.",
                Needs = "Administration: Read (또는 저장소 쓰기 권한)",
            });

            return;
        }

        var traffic = new GitTraffic
        {
            Views = (int)(GitJson.Long(viewDoc.RootElement, "count") ?? 0),
            UniqueViews = (int)(GitJson.Long(viewDoc.RootElement, "uniques") ?? 0),
        };

        if (cloneDoc is not null)
        {
            traffic.Clones = (int)(GitJson.Long(cloneDoc.RootElement, "count") ?? 0);
            traffic.UniqueClones = (int)(GitJson.Long(cloneDoc.RootElement, "uniques") ?? 0);
        }

        if (pathDoc is not null && pathDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            traffic.TopPaths =
            [
                .. pathDoc.RootElement.EnumerateArray()
                    .Take(5)
                    .Select(x => new GitNameCount
                    {
                        Name = GitJson.Str(x, "path"),
                        Count = GitJson.Long(x, "count") ?? 0,
                    })
            ];
        }

        row.Traffic = traffic;
    }

    /// <summary>
    /// 소유자의 GHCR 이미지. 저장소가 아니라 <b>소유자</b>에 매달려 있어
    /// 소유자마다 한 번만 묻고 줄에 나눠 준다.
    /// </summary>
    /// <remarks>
    /// 이미지에 <c>repository</c> 가 딸려 오므로 어느 저장소 것인지 알 수 있다.
    /// 배포가 올리는 이미지 열둘이 여기 보이고, <b>운영에 떠 있는 태그와
    /// 대조하는 자리</b>다. <c>read:packages</c> 가 있는 classic 토큰이 필요하다.
    /// </remarks>
    private async Task FillPackagesAsync(
        GitOptions opt, GitMonitorRow[] rows,
        System.Collections.Concurrent.ConcurrentBag<GitBlocked> blocked)
    {
        if (!opt.Authenticated)
        {
            blocked.Add(new GitBlocked
            {
                What = "GHCR 이미지",
                Why = "토큰이 없습니다.",
                Needs = "classic 토큰 + read:packages",
            });

            return;
        }

        var owners = opt.Repos
            .Select(r => r.Split('/')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var owner in owners)
        {
            string? why = null;

            // 개인 계정과 조직은 경로가 다르다. 개인 쪽을 먼저 보고 안 되면 조직으로.
            using var doc =
                await github.GetAsync(opt, $"/users/{owner}/packages?package_type=container&per_page=100", e => why = e)
                ?? await github.GetAsync(opt, $"/orgs/{owner}/packages?package_type=container&per_page=100");

            if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                blocked.Add(new GitBlocked
                {
                    What = "GHCR 이미지",
                    Why = why ?? "읽지 못했습니다.",
                    Needs = "classic 토큰 + read:packages",
                });

                continue;
            }

            foreach (var p in doc.RootElement.EnumerateArray())
            {
                var package = new GitPackage
                {
                    Name = GitJson.Str(p, "name"),
                    Versions = GitJson.Long(p, "version_count") ?? 0,
                    UpdatedAt = GitJson.Str(p, "updated_at"),
                    Url = GitJson.Str(p, "html_url"),
                };

                // 이미지가 어느 저장소에서 왔는지 알려 준다. 못 알려 주면
                // (연결이 끊긴 이미지) 소유자의 모든 줄에 붙이지 않고 버린다 —
                // 엉뚱한 저장소 것으로 보이는 편이 안 보이는 것보다 나쁘다.
                var full = GitJson.Obj(p, "repository") is { } r ? GitJson.Str(r, "full_name") : null;
                if (full is null) continue;

                foreach (var row in rows.Where(x =>
                             string.Equals(x.Repo, full, StringComparison.OrdinalIgnoreCase)))
                {
                    row.Packages.Add(package);
                }
            }
        }
    }

    /// <summary>
    /// 가장 최근에 깨진 실행의 <b>어느 단계에서</b> 깨졌나.
    /// </summary>
    /// <remarks>
    /// 로그를 통째로 받지 않는다 — 실행 로그는 zip 이고 크다. 잡과 단계 이름만
    /// 봐도 「어디서 깨졌나」는 답이 나오고, 그 이상은 GitHub 에서 보는 편이 낫다.
    /// <b>표본에 실패가 있을 때만</b> 한 번 더 부른다.
    /// </remarks>
    private async Task FillFailureAsync(GitOptions opt, string repo, GitMonitorRow row)
    {
        var failed = row.Runs?.LastFailure;
        if (failed?.Url is null) return;

        // 실행 번호는 주소 끝에 있다. 따로 담아 두지 않은 값이라 여기서 꺼낸다.
        var id = failed.Url.Split('/').LastOrDefault();
        if (!long.TryParse(id, out var runId)) return;

        using var doc = await github.GetAsync(opt, $"/repos/{repo}/actions/runs/{runId}/jobs");
        if (doc is null) return;

        if (!doc.RootElement.TryGetProperty("jobs", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var failure = new GitFailure
        {
            RunId = runId,
            WorkflowName = failed.WorkflowName,
            Branch = failed.Branch,
            At = failed.At,
            Url = failed.Url,
        };

        foreach (var job in jobs.EnumerateArray())
        {
            if (GitJson.Str(job, "conclusion") != "failure") continue;

            var jobName = GitJson.Str(job, "name") ?? "(이름 없음)";

            if (!job.TryGetProperty("steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array)
            {
                failure.Steps.Add(jobName);
                continue;
            }

            foreach (var step in steps.EnumerateArray())
            {
                if (GitJson.Str(step, "conclusion") != "failure") continue;

                failure.Steps.Add($"{jobName} / {GitJson.Str(step, "name")}");
            }
        }

        row.LastFailure = failure;
    }

    private static void FillReleases(GitMonitorRow row, JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var r in doc.RootElement.EnumerateArray())
        {
            row.Releases.Add(new GitRelease
            {
                TagName = GitJson.Str(r, "tag_name"),
                Name = GitJson.Str(r, "name"),
                PublishedAt = GitJson.Str(r, "published_at"),
                Draft = GitJson.Flag(r, "draft"),
                Prerelease = GitJson.Flag(r, "prerelease"),
                Author = GitJson.Obj(r, "author") is { } a ? GitJson.Str(a, "login") : null,
                Url = GitJson.Str(r, "html_url"),
            });
        }
    }

}
