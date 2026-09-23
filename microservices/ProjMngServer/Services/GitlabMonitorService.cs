using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// GitLab 종합 모니터링 — 저장소마다 통계 · 가지 · 태그 · 병합요청 · Job ·
/// 커밋 · 기여자 · 레지스트리 여덟 가지를 모은다.
/// </summary>
/// <remarks>
/// <para>
/// 저장소 하나에 왕복이 여덟이고 저장소가 서른여섯이니 한 번 훑으면 <b>288 번</b>
/// 부른다. 그래서 캐시가 빌드 상태보다 길고(기본 120초) 동시 실행도 열둘로 막는다.
/// </para>
///
/// <para>
/// [못 읽은 항목은 조용히 넘어간다]
/// </para>
///
/// <para>
/// 여덟 중 몇은 <b>쓰지 않는 기능</b>이라 404 나 403 이 정상이다(레지스트리를
/// 안 켠 저장소, 기여자 조회 권한이 없는 토큰). 그것을 오류로 올리면 멀쩡한
/// 저장소가 전부 빨갛게 보인다. 줄 전체가 실패한 것만 <c>Error</c> 에 담는다.
/// </para>
/// </remarks>
public sealed class GitlabMonitorService(IConfiguration configuration, IHttpClientFactory http)
{
    private sealed record Entry(List<GitlabMonitorRow> Rows, DateTimeOffset At);

    private static readonly ConcurrentDictionary<int, Entry> Cache = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public async Task<GitlabResult<GitlabMonitorRow>> LoadAsync(int prjRid, bool force)
    {
        var opt = GitlabOptions.For(configuration, prjRid);

        var result = new GitlabResult<GitlabMonitorRow>
        {
            Configured = opt.Configured,
            BaseUrl = opt.BaseUrl,
            Group = opt.Group,
        };

        if (!force && Cache.TryGetValue(prjRid, out var hit)
            && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.MonitorCacheSeconds))
        {
            result.Rows = hit.Rows;
            result.LoadedAt = hit.At.ToLocalTime().ToString("HH:mm:ss");
            return result;
        }

        var gate = Locks.GetOrAdd(prjRid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            if (!force && Cache.TryGetValue(prjRid, out hit)
                && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.MonitorCacheSeconds))
            {
                result.Rows = hit.Rows;
                result.LoadedAt = hit.At.ToLocalTime().ToString("HH:mm:ss");
                return result;
            }

            var targets = opt.Targets();
            var rows = new GitlabMonitorRow[targets.Count];

            using var slots = new SemaphoreSlim(12, 12);

            await Task.WhenAll(targets.Select(async (t, i) =>
            {
                await slots.WaitAsync();
                try { rows[i] = await OneAsync(opt, t.Module, t.Kind, t.Path); }
                finally { slots.Release(); }
            }));

            var entry = new Entry([.. rows], DateTimeOffset.UtcNow);
            Cache[prjRid] = entry;

            result.Rows = entry.Rows;
            result.LoadedAt = entry.At.ToLocalTime().ToString("HH:mm:ss");
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<GitlabMonitorRow> OneAsync(
        GitlabOptions opt, string module, string kind, string path)
    {
        var row = new GitlabMonitorRow
        {
            Module = module,
            Kind = kind,
            Path = path,
            WebUrl = $"{opt.BaseUrl}/{path}",
            JobsUrl = opt.JobsUrl(path),
        };

        if (!opt.Configured)
        {
            row.Error = "GitLab 주소나 토큰이 설정되지 않았습니다.";
            return row;
        }

        var api = $"{opt.BaseUrl}/api/v4/projects/{Uri.EscapeDataString(path)}";
        var since = DateTimeOffset.UtcNow.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ssZ");

        try
        {
            var info = GetAsync(opt, $"{api}?statistics=true");
            var branches = GetAsync(opt, $"{api}/repository/branches?per_page=100");
            var tags = CountAsync(opt, $"{api}/repository/tags?per_page=1");
            var mrs = GetAsync(opt, $"{api}/merge_requests?state=opened&per_page=20");
            var jobs = GetAsync(opt, $"{api}/jobs?per_page={opt.JobSample}");
            var commits = GetAsync(opt, $"{api}/repository/commits?since={since}&per_page=100");
            var contribs = GetAsync(opt, $"{api}/repository/contributors");
            var registry = GetAsync(opt, $"{api}/registry/repositories?tags_count=true&size=true");

            await Task.WhenAll(info, branches, tags, mrs, jobs, commits, contribs, registry);

            using var d1 = info.Result;
            using var d2 = branches.Result;
            using var d3 = mrs.Result;
            using var d4 = jobs.Result;
            using var d5 = commits.Result;
            using var d6 = contribs.Result;
            using var d7 = registry.Result;

            FillInfo(row, d1);
            FillBranches(row, d2, opt.StaleBranchDays);
            row.Tags = tags.Result;
            FillMrs(row, d3);
            FillJobs(row, d4);
            FillCommits(row, d5);
            FillContribs(row, d6);
            FillRegistry(row, d7);
        }
        catch (Exception e)
        {
            row.Error = e.Message.Length > 120 ? e.Message[..120] : e.Message;
        }

        return row;
    }

    // ──────────────────────────────────────────── 부르기

    private HttpRequestMessage Req(GitlabOptions opt, string url)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, url);
        r.Headers.Add("PRIVATE-TOKEN", opt.Token);
        r.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return r;
    }

    /// <summary>실패하면 <c>null</c>. 안 쓰는 기능은 404 가 정상이라 조용히 넘긴다.</summary>
    private async Task<JsonDocument?> GetAsync(GitlabOptions opt, string url)
    {
        try
        {
            using var req = Req(opt, url);
            using var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            var body = await res.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 개수만 필요할 때. <c>X-Total</c> 머리글만 읽으므로 <b>목록을 통째로 받지 않는다</b>.
    /// </summary>
    private async Task<int?> CountAsync(GitlabOptions opt, string url)
    {
        try
        {
            using var req = Req(opt, url);
            using var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            return res.Headers.TryGetValues("X-Total", out var v)
                   && int.TryParse(v.FirstOrDefault(), out var n)
                ? n
                : null;
        }
        catch
        {
            return null;
        }
    }

    // ──────────────────────────────────────────── 담기

    private static void FillInfo(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null) return;

        var e = d.RootElement;

        row.ProjectId = GitlabJson.Long(e, "id");
        row.DefaultBranch = GitlabJson.Str(e, "default_branch");
        row.LastActivityAt = GitlabJson.Str(e, "last_activity_at");
        row.CreatedAt = GitlabJson.Str(e, "created_at");
        row.OpenIssues = GitlabJson.Long(e, "open_issues_count");

        if (!e.TryGetProperty("statistics", out var st) || st.ValueKind != JsonValueKind.Object) return;

        row.CommitCount = GitlabJson.Long(st, "commit_count");
        row.RepoSize = GitlabJson.Long(st, "repository_size");
        row.ArtifactsSize = GitlabJson.Long(st, "job_artifacts_size");
        row.StorageSize = GitlabJson.Long(st, "storage_size");
        row.RegistrySize = GitlabJson.Long(st, "container_registry_size");
    }

    private static void FillBranches(GitlabMonitorRow row, JsonDocument? d, int staleDays)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        var list = new List<GitlabBranch>();
        var cut = DateTimeOffset.UtcNow.AddDays(-staleDays);
        var stale = 0;

        foreach (var b in d.RootElement.EnumerateArray())
        {
            string? at = null;
            if (b.TryGetProperty("commit", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                at = GitlabJson.Str(c, "committed_date");
            }

            var old = at is not null && DateTimeOffset.TryParse(at, out var t) && t < cut;
            if (old) stale++;

            list.Add(new GitlabBranch
            {
                Name = GitlabJson.Str(b, "name"),
                Default = GitlabJson.Flag(b, "default"),
                Merged = GitlabJson.Flag(b, "merged"),
                At = at,
                Stale = old,
            });
        }

        row.Branches = list.Count;
        row.StaleBranches = stale;

        // 화면에는 최근 것 여덟만 보인다. 개수는 위에서 이미 다 셌다.
        row.BranchList = [.. list.OrderByDescending(x => x.At).Take(8)];
    }

    private static void FillMrs(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var m in d.RootElement.EnumerateArray())
        {
            string? author = null;
            if (m.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object)
            {
                author = GitlabJson.Str(a, "name");
            }

            row.MrList.Add(new GitlabMr
            {
                Iid = GitlabJson.Long(m, "iid"),
                Title = GitlabJson.Str(m, "title"),
                Author = author,
                Source = GitlabJson.Str(m, "source_branch"),
                Target = GitlabJson.Str(m, "target_branch"),
                CreatedAt = GitlabJson.Str(m, "created_at"),
                UpdatedAt = GitlabJson.Str(m, "updated_at"),
                Draft = GitlabJson.Flag(m, "draft"),
                WebUrl = GitlabJson.Str(m, "web_url"),
            });
        }

        row.OpenMrs = row.MrList.Count;
    }

    private static void FillJobs(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        var stats = new GitlabJobStats();
        double durSum = 0;
        var durCnt = 0;

        foreach (var j in d.RootElement.EnumerateArray())
        {
            var status = GitlabJson.Str(j, "status");
            stats.Recent.Add(status);

            if (status == "success") stats.Success++;
            else if (status == "failed") stats.Failed++;
            else stats.Other++;

            var dur = GitlabJson.Num(j, "duration");

            // 아직 도는 Job 의 시간은 평균에 넣지 않는다 — 끝나야 나오는 값이다.
            if (dur is not null && status is "success" or "failed")
            {
                durSum += dur.Value;
                durCnt++;
            }

            var one = new GitlabJobBrief
            {
                Status = status,
                Name = GitlabJson.Str(j, "name"),
                Ref = GitlabJson.Str(j, "ref"),
                At = GitlabJson.Str(j, "finished_at")
                     ?? GitlabJson.Str(j, "started_at")
                     ?? GitlabJson.Str(j, "created_at"),
                Duration = dur,
                Url = GitlabJson.Str(j, "web_url"),
            };

            stats.Last ??= one;
            if (status == "success") stats.LastSuccess ??= one;
            if (status == "failed") stats.LastFailed ??= one;
        }

        stats.Sampled = stats.Recent.Count;

        var done = stats.Success + stats.Failed;
        stats.Rate = done > 0 ? Math.Round(stats.Success * 100.0 / done, 1) : null;
        stats.AvgDuration = durCnt > 0 ? Math.Round(durSum / durCnt, 1) : null;

        row.Jobs = stats;
    }

    private static void FillCommits(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        var arr = d.RootElement;
        row.Commits7d = arr.GetArrayLength();

        var byAuthor = new Dictionary<string, long>();

        foreach (var c in arr.EnumerateArray())
        {
            var a = GitlabJson.Str(c, "author_name") ?? "-";
            byAuthor[a] = byAuthor.GetValueOrDefault(a) + 1;
        }

        row.CommitAuthors7d =
        [
            .. byAuthor.OrderByDescending(k => k.Value)
                       .Take(5)
                       .Select(k => new GitlabNameCount { Name = k.Key, Count = k.Value })
        ];

        if (arr.GetArrayLength() == 0) return;

        var top = arr[0];
        row.LastCommit = new GitlabCommit
        {
            Id = GitlabJson.Str(top, "short_id"),
            Title = GitlabJson.Str(top, "title"),
            Author = GitlabJson.Str(top, "author_name"),
            At = GitlabJson.Str(top, "committed_date") ?? GitlabJson.Str(top, "created_at"),
            Url = GitlabJson.Str(top, "web_url"),
        };
    }

    private static void FillContribs(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        var list = d.RootElement.EnumerateArray()
            .Select(c => new GitlabNameCount
            {
                Name = GitlabJson.Str(c, "name"),
                Count = GitlabJson.Long(c, "commits") ?? 0,
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        row.Contributors = list.Count;
        row.ContributorList = [.. list.Take(5)];
    }

    private static void FillRegistry(GitlabMonitorRow row, JsonDocument? d)
    {
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) return;

        var reg = new GitlabRegistry();

        foreach (var r in d.RootElement.EnumerateArray())
        {
            var tags = GitlabJson.Long(r, "tags_count") ?? 0;
            var size = GitlabJson.Long(r, "size") ?? 0;

            reg.Tags += tags;
            reg.Size += size;

            reg.List.Add(new GitlabRegistryRepo
            {
                Name = GitlabJson.Str(r, "name") ?? GitlabJson.Str(r, "path"),
                Location = GitlabJson.Str(r, "location"),
                Tags = tags,
                Size = size,
                CreatedAt = GitlabJson.Str(r, "created_at"),
            });
        }

        reg.Repos = reg.List.Count;
        row.Registry = reg;
    }
}
