using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuthServer.DTOs;
using Microsoft.Extensions.Options;

namespace AuthServer.Services;

/// <summary>
/// 플레이어 릴리스 — GitHub 에 버전 태그를 만들어 릴리스 워크플로를 깨운다.
/// </summary>
/// <remarks>
/// <b>왜 서버를 거치나.</b> 태그를 만들려면 <c>repo</c> 권한 토큰이 필요하다.
/// 화면에서 GitHub 을 직접 부르면 그 토큰이 브라우저에 실려 누구든 꺼내 갈 수 있다
/// (저장소가 공개라 더 그렇다). 토큰은 서버에만 두고 화면은 이 서비스만 부른다.
///
/// <para>
/// <b>왜 <c>workflow_dispatch</c> 가 아니라 태그인가.</b> 릴리스 워크플로의 첨부 단계는
/// <c>if: startsWith(github.ref, 'refs/tags/v')</c> 다. 수동 실행으로는 빌드만 되고
/// GitHub Release 가 발행되지 않아서, 다운로드 화면이 파일을 찾지 못한다.
/// </para>
///
/// <para>
/// <b>릴리스 노트는 워크플로가 만든다.</b> 워크플로에 <c>generate_release_notes: true</c> 와
/// 설치 안내 본문이 이미 들어 있다. 여기서 릴리스를 미리 만들어 두면 그 본문과 부딪히므로,
/// 이 서비스는 <b>태그만</b> 만든다. 화면이 받은 노트는 태그 객체의 메시지로 남긴다.
/// </para>
/// </remarks>
public class PlayerReleaseService : IPlayerReleaseService
{
    /// <summary>권한을 읽어 올 메뉴 경로. 릴리스 발행은 이 메뉴의 can_create 다.</summary>
    private const string MenuPath = "/system/player-release";

    /// <summary>버전 형식. <c>1.0.0</c> · <c>1.2.3-rc1</c> 을 받는다. 앞의 v 는 서버가 붙인다.</summary>
    private static readonly Regex VersionPattern =
        new(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNameCaseInsensitive = true };

    private readonly GitHubOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMenuService _menus;
    private readonly ILogger<PlayerReleaseService> _log;

    public PlayerReleaseService(
        IOptions<GitHubOptions> options,
        IHttpClientFactory httpFactory,
        IMenuService menus,
        ILogger<PlayerReleaseService> log)
    {
        _options = options.Value;
        _httpFactory = httpFactory;
        _menus = menus;
        _log = log;
    }

    // ── 화면 첫 그림 ────────────────────────────────────────

    public async Task<PlayerReleaseStatusDto> GetStatusAsync(string userId)
    {
        var status = new PlayerReleaseStatusDto
        {
            Branch = _options.Branch,
            CanRelease = await CanReleaseAsync(userId),
            Configured = _options.IsConfigured,
            Repository = $"{_options.Owner}/{_options.Repo}",
        };

        if (!_options.IsConfigured)
        {
            status.SetupHint =
                "AuthServer 의 appsettings.Local.json 에 GitHub 설정이 필요합니다. " +
                "Owner · Repo · Token(repo, workflow 권한) 세 가지입니다.";
            return status;
        }

        try
        {
            var head = await GetJsonAsync($"git/ref/heads/{_options.Branch}");
            if (head.HasValue &&
                head.Value.TryGetProperty("object", out var obj) &&
                obj.TryGetProperty("sha", out var sha))
            {
                status.HeadSha = sha.GetString();

                var commit = await GetJsonAsync($"commits/{status.HeadSha}");
                if (commit.HasValue &&
                    commit.Value.TryGetProperty("commit", out var c) &&
                    c.TryGetProperty("message", out var msg))
                {
                    status.HeadMessage = (msg.GetString() ?? string.Empty)
                        .Split('\n')[0].Trim();
                }
            }

            var tags = await GetJsonAsync("tags?per_page=30");
            if (tags is { ValueKind: JsonValueKind.Array })
            {
                status.Tags = tags.Value.EnumerateArray()
                    .Select(t => t.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .ToList();
            }

            // 릴리스가 하나도 없으면 404 다 — 오류가 아니라 '아직 없음' 이다.
            var latest = await GetJsonAsync("releases/latest", allowNotFound: true);
            if (latest.HasValue && latest.Value.TryGetProperty("tag_name", out var tagName))
            {
                status.LatestRelease = tagName.GetString();
            }

            status.SuggestedVersion = SuggestNext(status.Tags);
        }
        catch (Exception ex)
        {
            // 화면은 떠야 한다. 조회가 실패해도 안내만 띄우고 넘어간다.
            _log.LogWarning(ex, "GitHub 상태 조회 실패");
            status.Warning = "GitHub 정보를 가져오지 못했습니다. 토큰이나 네트워크를 확인하세요.";
        }

        return status;
    }

    /// <summary>
    /// 다음 버전 제안. 가장 큰 <c>vX.Y.Z</c> 의 끝자리를 하나 올린다.
    /// 태그가 없으면 <c>1.0.0</c> 이다.
    /// </summary>
    private static string SuggestNext(List<string> tags)
    {
        var best = tags
            .Select(t => t.TrimStart('v', 'V'))
            .Select(t => VersionPattern.IsMatch(t) && !t.Contains('-') ? t : null)
            .Where(t => t != null)
            .Select(t => t!.Split('.').Select(int.Parse).ToArray())
            .OrderByDescending(p => p[0]).ThenByDescending(p => p[1]).ThenByDescending(p => p[2])
            .FirstOrDefault();

        return best is null ? "1.0.0" : $"{best[0]}.{best[1]}.{best[2] + 1}";
    }

    // ── 릴리스 요청 ─────────────────────────────────────────

    public async Task<(PlayerReleaseOutcome, PlayerReleaseResultDto)> CreateAsync(
        string userId, PlayerReleaseRequestDto request)
    {
        PlayerReleaseResultDto Fail(string message) => new() { Message = message };

        if (!await CanReleaseAsync(userId))
        {
            return (PlayerReleaseOutcome.Forbidden, Fail("릴리스를 발행할 권한이 없습니다."));
        }

        if (!_options.IsConfigured)
        {
            return (PlayerReleaseOutcome.NotConfigured,
                Fail("서버에 GitHub 설정이 없습니다. 관리자에게 문의하세요."));
        }

        // 앞의 v 는 받아 주되 저장은 항상 v 를 붙인 형태로 한다.
        var version = (request.Version ?? string.Empty).Trim().TrimStart('v', 'V');
        if (!VersionPattern.IsMatch(version))
        {
            return (PlayerReleaseOutcome.Invalid,
                Fail("버전은 1.0.0 형식으로 적습니다. (미리보기는 1.0.0-rc1 처럼)"));
        }

        var tag = $"v{version}";

        try
        {
            // 이미 있는 태그면 GitHub 이 422 를 준다. 그 전에 막아 이유를 분명히 알린다.
            var existing = await GetJsonAsync($"git/ref/tags/{tag}", allowNotFound: true);
            if (existing.HasValue)
            {
                return (PlayerReleaseOutcome.Invalid,
                    Fail($"{tag} 태그가 이미 있습니다. 다른 버전을 적으세요."));
            }

            var head = await GetJsonAsync($"git/ref/heads/{_options.Branch}");
            if (!head.HasValue ||
                !head.Value.TryGetProperty("object", out var obj) ||
                !obj.TryGetProperty("sha", out var shaProp))
            {
                return (PlayerReleaseOutcome.Failed,
                    Fail($"{_options.Branch} 브랜치의 최신 커밋을 찾지 못했습니다."));
            }

            var sha = shaProp.GetString()!;

            // 태그를 만들면 워크플로의 `push: tags: ['v*']` 가 깨어난다.
            var created = await PostJsonAsync("git/refs", new
            {
                @ref = $"refs/tags/{tag}",
                sha,
            });

            if (!created)
            {
                return (PlayerReleaseOutcome.Failed,
                    Fail("GitHub 이 태그 생성을 거절했습니다. 토큰 권한(repo)을 확인하세요."));
            }

            _log.LogInformation("플레이어 릴리스 태그 생성: {Tag} ({Sha}) by {User}",
                tag, sha[..7], userId);

            return (PlayerReleaseOutcome.Ok, new PlayerReleaseResultDto
            {
                Message = $"{tag} 태그를 만들었습니다. 빌드가 시작됩니다.",
                Sha = sha,
                Tag = tag,
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "플레이어 릴리스 실패: {Tag}", tag);
            return (PlayerReleaseOutcome.Failed,
                Fail("GitHub 에 연결하지 못했습니다. 잠시 후 다시 시도하세요."));
        }
    }

    // ── 진행 상황 ───────────────────────────────────────────

    public async Task<PlayerReleaseRunDto> GetRunAsync(string tag)
    {
        var run = new PlayerReleaseRunDto { Pending = true, Tag = tag };
        if (!_options.IsConfigured) return run;

        try
        {
            // 태그 푸시로 시작된 실행은 head_branch 가 태그 이름이다.
            var runs = await GetJsonAsync(
                $"actions/runs?event=push&branch={Uri.EscapeDataString(tag)}&per_page=1");

            if (!runs.HasValue ||
                !runs.Value.TryGetProperty("workflow_runs", out var list) ||
                list.GetArrayLength() == 0)
            {
                // 태그를 막 만든 직후다. GitHub 이 큐에 넣는 데 몇 초 걸린다.
                return run;
            }

            var first = list[0];
            run.Pending = false;
            run.Conclusion = Str(first, "conclusion");
            run.HtmlUrl = Str(first, "html_url");
            run.RunNumber = first.TryGetProperty("run_number", out var n) ? n.GetInt32() : null;
            run.Status = Str(first, "status");

            var runId = first.GetProperty("id").GetInt64();
            var jobs = await GetJsonAsync($"actions/runs/{runId}/jobs?per_page=30");
            if (jobs.HasValue && jobs.Value.TryGetProperty("jobs", out var jobList))
            {
                run.Jobs = jobList.EnumerateArray().Select(j => new PlayerReleaseJobDto
                {
                    Conclusion = Str(j, "conclusion"),
                    CurrentStep = j.TryGetProperty("steps", out var steps)
                        ? steps.EnumerateArray()
                            .Where(s => Str(s, "status") == "in_progress")
                            .Select(s => Str(s, "name"))
                            .FirstOrDefault()
                        : null,
                    Name = Str(j, "name") ?? string.Empty,
                    Status = Str(j, "status"),
                }).ToList();
            }

            // 끝났으면 릴리스가 실제로 발행됐는지 확인해 바로 열 수 있게 한다.
            if (run.Status == "completed" && run.Conclusion == "success")
            {
                var rel = await GetJsonAsync($"releases/tags/{tag}", allowNotFound: true);
                if (rel.HasValue) run.ReleaseUrl = Str(rel.Value, "html_url");
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "릴리스 진행 상황 조회 실패: {Tag}", tag);
        }

        return run;
    }

    // ── 최신 릴리스와 첨부 파일 ─────────────────────────────

    public async Task<PlayerReleaseLatestDto> GetLatestAsync()
    {
        var latest = new PlayerReleaseLatestDto
        {
            ReleasesUrl = $"https://github.com/{_options.Owner}/{_options.Repo}/releases",
        };

        if (!_options.IsConfigured)
        {
            latest.Warning =
                "AuthServer 에 GitHub 설정이 없습니다. Owner · Repo · Token 을 채우면 " +
                "설치 파일 목록이 나옵니다.";
            return latest;
        }

        try
        {
            // 한 번도 발행하지 않았으면 404 다 — 오류가 아니라 '아직 없음' 이다.
            var release = await GetJsonAsync("releases/latest", allowNotFound: true);

            if (release is null)
            {
                latest.Warning =
                    "아직 발행된 릴리스가 없습니다. 저장소에 버전 태그(예: v1.0.0)를 " +
                    "푸시하면 설치 파일이 자동으로 만들어집니다.";
                return latest;
            }

            latest.Published = true;
            latest.TagName = Str(release.Value, "tag_name");
            latest.HtmlUrl = Str(release.Value, "html_url");

            if (release.Value.TryGetProperty("published_at", out var at) &&
                at.ValueKind == JsonValueKind.String &&
                at.TryGetDateTime(out var published))
            {
                latest.PublishedAt = published;
            }

            if (release.Value.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                latest.Assets = assets.EnumerateArray().Select(a => new PlayerReleaseAssetDto
                {
                    DownloadCount = a.TryGetProperty("download_count", out var c) &&
                                    c.TryGetInt32(out var count) ? count : 0,
                    DownloadUrl = Str(a, "browser_download_url") ?? string.Empty,
                    Name = Str(a, "name") ?? string.Empty,
                    Size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0,
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            // 화면은 떠야 한다. 조회가 실패해도 안내만 띄우고 넘어간다.
            _log.LogWarning(ex, "최신 릴리스 조회 실패");
            latest.Warning = "GitHub 에서 릴리스 정보를 가져오지 못했습니다. 잠시 뒤 다시 시도하십시오.";
        }

        return latest;
    }

    // ── 도우미 ──────────────────────────────────────────────

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>릴리스 발행 권한. <c>/system/player-release</c> 의 can_create 다.</summary>
    private async Task<bool> CanReleaseAsync(string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        return perm.CanCreate;
    }

    private HttpClient Client()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(
            $"https://api.github.com/repos/{_options.Owner}/{_options.Repo}/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Token);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // GitHub 은 User-Agent 가 없으면 403 을 준다.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("jsini-portal");
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    /// <summary>
    /// GET 한 번. <paramref name="allowNotFound"/> 면 404 를 <c>null</c> 로 돌려준다
    /// (릴리스·태그가 '아직 없음' 은 오류가 아니다).
    /// </summary>
    private async Task<JsonElement?> GetJsonAsync(string path, bool allowNotFound = false)
    {
        using var client = Client();
        using var res = await client.GetAsync(path);

        if (allowNotFound && res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, Json);
    }

    private async Task<bool> PostJsonAsync(string path, object payload)
    {
        using var client = Client();
        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var res = await client.PostAsync(path, content);

        if (res.IsSuccessStatusCode) return true;

        _log.LogWarning("GitHub POST {Path} 실패: {Status} {Body}",
            path, (int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return false;
    }
}
