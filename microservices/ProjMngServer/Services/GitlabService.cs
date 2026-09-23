using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 하나의 GitLab 설정.
/// </summary>
/// <remarks>
/// <para>
/// 설정은 <c>Gitlab</c> 아래 두 겹이다 — 바깥이 기본값이고
/// <c>Gitlab:Projects:{프로젝트번호}</c> 가 그것을 덮는다. 대부분의 값은 기본만
/// 있으면 되고, GitLab 이 다른 프로젝트가 생길 때만 아래를 쓴다.
/// </para>
///
/// <para>
/// <b>주소나 토큰이 비면 아무것도 부르지 않는다.</b> 원본은 사내 주소를 코드에
/// 기본값으로 박아 두었는데(<c>https://code.hd.com</c>), 밖에서는 그 주소가
/// 닿지 않아 화면마다 15초씩 기다렸다가 실패한다. 여기서는 기본값을 비워 두고
/// <see cref="Configured"/> 가 거짓이면 <b>즉시</b> 빈 결과를 돌려준다.
/// </para>
/// </remarks>
public sealed class GitlabOptions
{
    public string BaseUrl { get; init; } = "";
    public string Group { get; init; } = "";
    public string Token { get; init; } = "";

    /// <summary>훑을 모듈 번호 범위 — <c>pmm001</c> … <c>pmm018</c>.</summary>
    public int ModuleFrom { get; init; } = 1;

    /// <inheritdoc cref="ModuleFrom"/>
    public int ModuleTo { get; init; } = 18;

    public int CacheSeconds { get; init; } = 60;
    public int MonitorCacheSeconds { get; init; } = 120;
    public int StaleBranchDays { get; init; } = 30;
    public int JobSample { get; init; } = 20;

    public bool Configured => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(BaseUrl);

    public static GitlabOptions For(IConfiguration cfg, int prjRid)
    {
        var root = cfg.GetSection("Gitlab");
        var mine = root.GetSection($"Projects:{prjRid}");

        string Str(string key, string fallback = "") =>
            (mine[key] ?? root[key] ?? fallback).Trim();

        int Num(string key, int fallback)
        {
            var raw = mine[key] ?? root[key];
            return int.TryParse(raw, out var n) ? n : fallback;
        }

        return new GitlabOptions
        {
            BaseUrl = Str("BaseUrl").TrimEnd('/'),
            Group = Str("Group").Trim('/'),

            // 토큰만 환경변수도 본다. 비밀값이라 설정 파일에 안 두는 자리가 있다.
            Token = Str("Token", Environment.GetEnvironmentVariable("GITLAB_TOKEN") ?? ""),

            ModuleFrom = Num("ModuleFrom", 1),
            ModuleTo = Num("ModuleTo", 18),
            CacheSeconds = Num("CacheSeconds", 60),
            MonitorCacheSeconds = Num("MonitorCacheSeconds", 120),
            StaleBranchDays = Num("StaleBranchDays", 30),
            JobSample = Num("JobSample", 20),
        };
    }

    /// <summary>훑을 저장소들. <b>토큰이 없어도 만든다</b> — 화면이 링크는 걸 수 있다.</summary>
    public List<(string Module, string Kind, string Path)> Targets()
    {
        var list = new List<(string, string, string)>();

        for (var i = ModuleFrom; i <= ModuleTo; i++)
        {
            var m = $"pmm{i:D3}";
            foreach (var kind in new[] { "be", "fe" })
            {
                list.Add((m.ToUpperInvariant(), kind, $"{Group}/{m}/{m}-{kind}"));
            }
        }

        return list;
    }

    public string JobsUrl(string path) => $"{BaseUrl}/{path}/-/jobs";
}

/// <summary>
/// GitLab 빌드(Job) 상태 — 저장소마다 <b>가장 최근 Job 하나</b>.
/// </summary>
/// <remarks>
/// <para>
/// 저장소가 서른여섯이라 한 번 훑는 데 왕복이 일흔둘이다. 그래서 결과를
/// 잠깐 들고 있고(기본 60초), 화면의 [새로고침]만 그것을 건너뛴다.
/// </para>
///
/// <para>
/// <b>프로젝트마다 따로 담는다.</b> 원본은 프로젝트가 하나라 캐시도 하나였다.
/// </para>
/// </remarks>
public sealed class GitlabService(IConfiguration configuration, IHttpClientFactory http)
{
    private sealed record Entry(List<GitlabJobRow> Rows, DateTimeOffset At);

    private static readonly ConcurrentDictionary<int, Entry> Cache = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public async Task<GitlabResult<GitlabJobRow>> StatusAsync(int prjRid, bool force)
    {
        var opt = GitlabOptions.For(configuration, prjRid);

        var result = new GitlabResult<GitlabJobRow>
        {
            Configured = opt.Configured,
            BaseUrl = opt.BaseUrl,
            Group = opt.Group,
        };

        if (!force && Cache.TryGetValue(prjRid, out var hit)
            && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.CacheSeconds))
        {
            result.Rows = hit.Rows;
            result.LoadedAt = hit.At.ToLocalTime().ToString("HH:mm:ss");
            return result;
        }

        var gate = Locks.GetOrAdd(prjRid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            // 기다리는 사이에 다른 요청이 채워 두었을 수 있다.
            if (!force && Cache.TryGetValue(prjRid, out hit)
                && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(opt.CacheSeconds))
            {
                result.Rows = hit.Rows;
                result.LoadedAt = hit.At.ToLocalTime().ToString("HH:mm:ss");
                return result;
            }

            var targets = opt.Targets();
            var rows = new GitlabJobRow[targets.Count];

            // 서른여섯을 한꺼번에 던지면 GitLab 이 먼저 지친다.
            using var slots = new SemaphoreSlim(8, 8);

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

    private async Task<GitlabJobRow> OneAsync(GitlabOptions opt, string module, string kind, string path)
    {
        var row = new GitlabJobRow
        {
            Module = module,
            Kind = kind,
            Path = path,
            JobsUrl = opt.JobsUrl(path),
        };

        if (!opt.Configured)
        {
            row.Error = "GitLab 주소나 토큰이 설정되지 않았습니다.";
            return row;
        }

        try
        {
            var enc = Uri.EscapeDataString(path);
            var api = $"{opt.BaseUrl}/api/v4/projects/{enc}/jobs";

            // 최근 한 건과 최근 성공 한 건을 같이 묻는다. 지금 깨져 있을 때
            // **언제까지 됐는지**가 화면에서 가장 먼저 찾는 값이다.
            var latest = FetchAsync(opt, $"{api}?per_page=1");
            var success = FetchAsync(opt, $"{api}?scope%5B%5D=success&per_page=1");

            await Task.WhenAll(latest, success);

            if (latest.Result.Error is { } err)
            {
                row.Error = err;
                return row;
            }

            if (latest.Result.Job is not { } job)
            {
                // Job 이 한 번도 없었다. **못 읽은 것과 다르다.**
                row.Status = "none";
                return row;
            }

            row.Status = job.Status;
            row.JobId = job.JobId;
            row.JobName = job.Name;
            row.Stage = job.Stage;
            row.Ref = job.Ref;
            row.CreatedAt = job.CreatedAt;
            row.StartedAt = job.StartedAt;
            row.FinishedAt = job.FinishedAt;
            row.Duration = job.Duration;
            row.JobUrl = job.Url;
            row.CommitId = job.CommitId;
            row.CommitTitle = job.CommitTitle;
            row.CommitAuthor = job.CommitAuthor;
            row.PipelineId = job.PipelineId;
            row.PipelineStatus = job.PipelineStatus;
            row.UserName = job.UserName;

            row.LastSuccess = success.Result.Job is { } ok
                ? new GitlabJobBrief
                {
                    Status = ok.Status,
                    Name = ok.Name,
                    Ref = ok.Ref,
                    At = ok.FinishedAt ?? ok.StartedAt ?? ok.CreatedAt,
                    Duration = ok.Duration,
                    Url = ok.Url,
                }
                : null;
        }
        catch (Exception e)
        {
            row.Error = e.Message.Length > 120 ? e.Message[..120] : e.Message;
        }

        return row;
    }

    /// <summary>GitLab 이 돌려주는 Job 한 건. 이 안에서만 쓴다.</summary>
    private sealed class RawJob
    {
        public string? Status, Name, Stage, Ref, CreatedAt, StartedAt, FinishedAt, Url;
        public string? CommitId, CommitTitle, CommitAuthor, PipelineStatus, UserName;
        public long? JobId, PipelineId;
        public double? Duration;
    }

    private async Task<(RawJob? Job, string? Error)> FetchAsync(GitlabOptions opt, string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("PRIVATE-TOKEN", opt.Token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        using var res = await client.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            // 상태 번호 그대로 보여 주면 고칠 곳을 못 찾는다. 셋은 고칠 곳이 다르다.
            return (null, (int)res.StatusCode switch
            {
                401 => "인증 실패 (토큰 확인)",
                403 => "권한 없음 (read_api 필요)",
                404 => "저장소 없음",
                _ => $"HTTP {(int)res.StatusCode}",
            });
        }

        var body = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return (null, null);
        }

        var j = doc.RootElement[0];

        var job = new RawJob
        {
            Status = GitlabJson.Str(j, "status"),
            JobId = GitlabJson.Long(j, "id"),
            Name = GitlabJson.Str(j, "name"),
            Stage = GitlabJson.Str(j, "stage"),
            Ref = GitlabJson.Str(j, "ref"),
            CreatedAt = GitlabJson.Str(j, "created_at"),
            StartedAt = GitlabJson.Str(j, "started_at"),
            FinishedAt = GitlabJson.Str(j, "finished_at"),
            Duration = GitlabJson.Num(j, "duration"),
            Url = GitlabJson.Str(j, "web_url"),
        };

        if (j.TryGetProperty("commit", out var c) && c.ValueKind == JsonValueKind.Object)
        {
            job.CommitId = GitlabJson.Str(c, "short_id");
            job.CommitTitle = GitlabJson.Str(c, "title");
            job.CommitAuthor = GitlabJson.Str(c, "author_name");
        }

        if (j.TryGetProperty("pipeline", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            job.PipelineId = GitlabJson.Long(p, "id");
            job.PipelineStatus = GitlabJson.Str(p, "status");
        }

        if (j.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            job.UserName = GitlabJson.Str(u, "name");
        }

        return (job, null);
    }
}

/// <summary>GitLab 응답에서 값 하나 꺼내기. 없거나 형이 다르면 <c>null</c>.</summary>
internal static class GitlabJson
{
    public static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    public static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
