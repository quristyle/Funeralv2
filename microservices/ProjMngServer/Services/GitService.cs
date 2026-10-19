using JSini.Shared.Infrastructure.Time;
using System.Collections.Concurrent;
using System.Text.Json;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>빌드 상태 — 저장소마다 가장 최근 Actions 실행 하나. 최근 성공도 같이 묻는다 — 지금 깨져 있을 때 언제까지 됐는지가 가장 먼저 필요해서다.</summary>
public sealed class GitService(IConfiguration configuration, GitHubClient github)
{
    /// <summary>담아 둔 결과 한 벌. 남은 한도도 여기 같이 담는다 — 캐시로 답한 요청은 호출을 안 해 한도를 모르므로, 따로 두면 화면의 남은 한도가 캐시가 듣는 동안 틀린 값으로 남는다.</summary>
    private sealed record Entry(
        List<GitRunRow> Rows, DateTimeOffset At, int? RateRemaining, int? RateLimit);

    private static readonly ConcurrentDictionary<int, Entry> Cache = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public async Task<GitResult<GitRunRow>> StatusAsync(int prjRid, bool refresh)
    {
        var opt = GitOptions.For(configuration, prjRid);

        var result = new GitResult<GitRunRow>
        {
            Configured = opt.Configured,
            Authenticated = opt.Authenticated,
            WebBaseUrl = opt.WebBaseUrl,
        };

        if (!opt.Configured) return result;

        if (!refresh && Cache.TryGetValue(prjRid, out var hit)
            && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.CacheSeconds))
        {
            return Fill(result, hit);
        }

        var gate = Locks.GetOrAdd(prjRid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            // 기다리는 사이에 다른 요청이 채워 두었을 수 있다.
            if (!refresh && Cache.TryGetValue(prjRid, out hit)
                && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.CacheSeconds))
            {
                return Fill(result, hit);
            }

            var rows = new GitRunRow[opt.Repos.Length];

            using var slots = new SemaphoreSlim(6, 6);

            await Task.WhenAll(opt.Repos.Select(async (repo, i) =>
            {
                await slots.WaitAsync();
                try { rows[i] = await OneAsync(opt, repo); }
                finally { slots.Release(); }
            }));

            var entry = new Entry(
                [.. rows], DateTimeOffset.UtcNow, github.RateRemaining, github.RateLimit);
            Cache[prjRid] = entry;

            return Fill(result, entry);
        }
        finally
        {
            gate.Release();
        }
    }

    private GitResult<GitRunRow> Fill(GitResult<GitRunRow> result, Entry entry)
    {
        result.Rows = entry.Rows;
        // 사람이 읽는 글자라 한국 시각으로. 장비 시계(UTC)에 기대지 않는다.
        result.LoadedAt = AppTime.ToKorea(entry.At.UtcDateTime).ToString("HH:mm:ss");
        result.RateRemaining = entry.RateRemaining;
        result.RateLimit = entry.RateLimit;

        return result;
    }

    private async Task<GitRunRow> OneAsync(GitOptions opt, string repo)
    {
        var row = new GitRunRow
        {
            Repo = repo,
            RunsUrl = opt.RunsUrl(repo),
        };

        string? error = null;

        var latest = github.GetAsync(opt, $"/repos/{repo}/actions/runs?per_page=1", e => error = e);
        var success = github.GetAsync(opt, $"/repos/{repo}/actions/runs?status=success&per_page=1");

        await Task.WhenAll(latest, success);

        using var latestDoc = latest.Result;
        using var successDoc = success.Result;

        if (error is not null)
        {
            row.Error = error;
            return row;
        }

        var run = First(latestDoc);

        if (run is null)
        {
            // 실행이 한 번도 없었다. **못 읽은 것과 다르다.**
            row.Status = "none";
            return row;
        }

        var r = run.Value;

        row.Status = GitJson.RunState(r);
        row.RunId = GitJson.Long(r, "id");
        row.WorkflowName = GitJson.Str(r, "name");
        row.Event = GitJson.Str(r, "event");
        row.Branch = GitJson.Str(r, "head_branch");
        row.StartedAt = GitJson.Str(r, "run_started_at") ?? GitJson.Str(r, "created_at");
        row.UpdatedAt = GitJson.Str(r, "updated_at");
        row.Duration = GitJson.Seconds(row.StartedAt, row.UpdatedAt);
        row.RunUrl = GitJson.Str(r, "html_url");

        if (GitJson.Obj(r, "head_commit") is { } commit)
        {
            row.CommitId = GitJson.Str(commit, "id")?[..Math.Min(7, GitJson.Str(commit, "id")!.Length)];

            // 커밋 메시지는 여러 줄이다. 표의 한 칸에는 첫 줄만 뜻이 있다.
            row.CommitTitle = GitJson.Str(commit, "message")?.Split('\n')[0];

            if (GitJson.Obj(commit, "author") is { } author)
            {
                row.CommitAuthor = GitJson.Str(author, "name");
            }
        }

        if (GitJson.Obj(r, "actor") is { } actor)
        {
            row.Actor = GitJson.Str(actor, "login");
        }

        if (First(successDoc) is { } ok)
        {
            var at = GitJson.Str(ok, "run_started_at") ?? GitJson.Str(ok, "created_at");

            row.LastSuccess = new GitRunBrief
            {
                Status = "success",
                WorkflowName = GitJson.Str(ok, "name"),
                Branch = GitJson.Str(ok, "head_branch"),
                At = GitJson.Str(ok, "updated_at") ?? at,
                Duration = GitJson.Seconds(at, GitJson.Str(ok, "updated_at")),
                Url = GitJson.Str(ok, "html_url"),
            };
        }

        return row;
    }

    /// <summary><c>workflow_runs</c> 배열의 첫 줄. 비었으면 <c>null</c>.</summary>
    private static JsonElement? First(JsonDocument? doc)
    {
        if (doc is null) return null;

        if (!doc.RootElement.TryGetProperty("workflow_runs", out var runs)
            || runs.ValueKind != JsonValueKind.Array
            || runs.GetArrayLength() == 0)
        {
            return null;
        }

        return runs[0];
    }
}
