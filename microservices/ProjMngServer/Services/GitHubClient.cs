using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 하나의 Git 설정.
/// </summary>
/// 설정은 Git 아래 두 겹이다 — Git:Projects:{프로젝트번호} 가 바깥 기본값을 덮는다.
/// 토큰이 없어도 공개 저장소는 읽히므로 「설정됐나」의 기준은 토큰이 아니라 저장소 목록이다.
public sealed class GitOptions
{
    public string ApiBaseUrl { get; init; } = "https://api.github.com";
    public string WebBaseUrl { get; init; } = "https://github.com";

    /// <summary>조회 전용 토큰. 비어 있어도 된다(머리말).</summary>
    public string Token { get; init; } = "";

    /// <summary>볼 저장소 — <c>owner/name</c>.</summary>
    public string[] Repos { get; init; } = [];

    public int CacheSeconds { get; init; } = 60;
    public int MonitorCacheSeconds { get; init; } = 120;
    public int StaleBranchDays { get; init; } = 30;

    /// <summary>훑을 최근 실행 개수.</summary>
    public int RunSample { get; init; } = 20;

    /// <summary>
    /// 날짜를 물어볼 가지 수.
    /// </summary>
    /// GitHub 가지 목록에는 커밋 날짜가 없어 화면에 보이는 만큼만 묻는다. 0 이면 묻지 않고 묵음 칸이 빈다.
    public int BranchDetail { get; init; } = 8;

    /// <summary>볼 저장소가 하나라도 있나.</summary>
    public bool Configured => Repos.Length > 0;

    public bool Authenticated => !string.IsNullOrWhiteSpace(Token);

    public static GitOptions For(IConfiguration cfg, int prjRid)
    {
        var root = cfg.GetSection("Git");
        var mine = root.GetSection($"Projects:{prjRid}");

        string Str(string key, string fallback = "") =>
            (mine[key] ?? root[key] ?? fallback).Trim();

        int Num(string key, int fallback)
        {
            var raw = mine[key] ?? root[key];
            return int.TryParse(raw, out var n) ? n : fallback;
        }

        // 저장소 목록은 **프로젝트 것이 있으면 그것만** 쓴다. 둘을 합치면
        // 프로젝트마다 공용 저장소가 섞여 들어간다.
        var repos = mine.GetSection("Repos").Get<string[]>()
                    ?? root.GetSection("Repos").Get<string[]>()
                    ?? [];

        return new GitOptions
        {
            ApiBaseUrl = Str("ApiBaseUrl", "https://api.github.com").TrimEnd('/'),
            WebBaseUrl = Str("WebBaseUrl", "https://github.com").TrimEnd('/'),

            // 토큰만 환경변수도 본다. 비밀값이라 설정 파일에 안 두는 자리가 있다.
            Token = Str("Token", Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? ""),

            Repos = [.. repos.Select(r => r.Trim().Trim('/')).Where(r => r.Contains('/'))],

            CacheSeconds = Num("CacheSeconds", 60),
            MonitorCacheSeconds = Num("MonitorCacheSeconds", 120),
            StaleBranchDays = Num("StaleBranchDays", 30),
            RunSample = Num("RunSample", 20),
            BranchDetail = Num("BranchDetail", 8),
        };
    }

    public string WebUrl(string repo) => $"{WebBaseUrl}/{repo}";

    public string RunsUrl(string repo) => $"{WebBaseUrl}/{repo}/actions";
}

/// <summary>
/// GitHub REST 를 부르는 얇은 창구.
/// </summary>
/// User-Agent 를 빠뜨리면 GitHub 가 403 으로 끊는다. 실패는 예외로 올리지 않는다 — 없는 것이 정상인 조회가 빨갛게 보이므로, 줄 전체가 실패한 것만 Error 에 담는다.
public sealed class GitHubClient(IHttpClientFactory factory)
{
    /// <summary>
    /// 주소마다 마지막으로 받은 표식과 본문.
    /// </summary>
    /// 받은 ETag 를 If-None-Match 로 붙이면 안 바뀐 응답(304)은 한도에서 빠지지 않는다. 본문은 JsonDocument 가 아니라 글자로 담는다 — 버려진 것을 읽으면 던진다.
    /// 프로세스 전체가 나눠 쓰도록 static 이다. 이 클래스는 요청마다 새로 생긴다.
    private static readonly ConcurrentDictionary<string, (string Tag, string Body)> Tags = new();

    /// <summary>마지막 응답이 알려 준 남은 한도. 화면이 보여 준다.</summary>
    public int? RateRemaining { get; private set; }

    /// <inheritdoc cref="RateRemaining"/>
    public int? RateLimit { get; private set; }

    private HttpRequestMessage Request(GitOptions opt, string url, bool conditional = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // 지난번 표식을 붙인다. 안 바뀌었으면 304 가 오고 한도가 안 깎인다.
        if (conditional && Tags.TryGetValue(url, out var seen))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", seen.Tag);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        // 없으면 403 이다(머리말).
        request.Headers.UserAgent.ParseAdd("jsini-projmng");

        if (opt.Authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.Token);
        }

        return request;
    }

    private void RememberRate(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
            && int.TryParse(remaining.FirstOrDefault(), out var r))
        {
            RateRemaining = r;
        }

        if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limit)
            && int.TryParse(limit.FirstOrDefault(), out var l))
        {
            RateLimit = l;
        }
    }

    /// <summary>한 번 부른다. 실패하면 <c>null</c> — 까닭은 <paramref name="error"/> 로 나간다.</summary>
    public async Task<JsonDocument?> GetAsync(GitOptions opt, string path, Action<string>? error = null)
    {
        var url = $"{opt.ApiBaseUrl}{path}";

        try
        {
            using var request = Request(opt, url);

            using var http = factory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);

            using var response = await http.SendAsync(request);

            RememberRate(response);

            // 안 바뀌었다. 지난번 본문을 그대로 쓴다 — 이 응답은 한도를 안 깎는다.
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified
                && Tags.TryGetValue(url, out var cached))
            {
                return JsonDocument.Parse(cached.Body);
            }

            if (!response.IsSuccessStatusCode)
            {
                // 상태 번호만 보여 주면 고칠 곳을 못 찾는다. 셋은 고칠 곳이 다르다.
                error?.Invoke((int)response.StatusCode switch
                {
                    401 => "인증 실패 (토큰 확인)",
                    403 when RateRemaining == 0 => "호출 한도를 다 썼습니다 (토큰을 넣으면 5,000회)",
                    403 => "접근이 막혔습니다 (토큰 권한 확인)",
                    404 => "저장소가 없거나 볼 수 없습니다 (비공개면 토큰이 필요합니다)",
                    _ => $"HTTP {(int)response.StatusCode}",
                });

                return null;
            }

            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body)) return null;

            if (response.Headers.ETag?.Tag is { } tag)
            {
                Tags[url] = (tag, body);
            }

            return JsonDocument.Parse(body);
        }
        catch (Exception e)
        {
            error?.Invoke(e.Message.Length > 120 ? e.Message[..120] : e.Message);
            return null;
        }
    }

    /// <summary>
    /// 개수만 필요할 때. <b>목록을 통째로 받지 않는다.</b>
    /// </summary>
    /// GitHub 는 총 개수를 주지 않는다. 한 쪽에 하나만 받고 Link 머리글의 마지막 쪽 번호를 읽는다. 쪽 나눔이 없으면 배열 길이가 곧 개수다.
    public async Task<int?> CountAsync(GitOptions opt, string path)
    {
        try
        {
            // **여기서는 조건부 요청을 쓰지 않는다.** 이 조회는 본문이 아니라
            // `Link` 머리글을 읽는데, 304 에는 그 머리글이 없다.
            using var request = Request(opt, $"{opt.ApiBaseUrl}{path}", conditional: false);

            using var http = factory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);

            using var response = await http.SendAsync(request);

            RememberRate(response);

            if (!response.IsSuccessStatusCode) return null;

            if (response.Headers.TryGetValues("Link", out var links))
            {
                var last = links.FirstOrDefault() ?? "";

                var match = System.Text.RegularExpressions.Regex.Match(
                    last, @"[?&]page=(\d+)>; rel=""last""");

                if (match.Success && int.TryParse(match.Groups[1].Value, out var pages))
                {
                    return pages;
                }
            }

            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return 0;

            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>GitHub 응답에서 값 하나 꺼내기. 없거나 형이 다르면 <c>null</c>.</summary>
internal static class GitJson
{
    public static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    public static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static JsonElement? Obj(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    /// <summary>
    /// 실행의 상태를 한 낱말로 줄인다.
    /// </summary>
    /// GitHub 는 진행(status)과 결론(conclusion)을 나눠 준다. 안 끝난 실행은 결론이 null 이라 진행 상태를 그대로 쓴다.
    public static string? RunState(JsonElement run)
    {
        var status = Str(run, "status");

        return status == "completed" ? Str(run, "conclusion") ?? "completed" : status;
    }

    /// <summary>두 시각의 차(초). 어느 쪽이든 없으면 <c>null</c>.</summary>
    public static double? Seconds(string? from, string? to)
    {
        if (!DateTimeOffset.TryParse(from, out var a)) return null;
        if (!DateTimeOffset.TryParse(to, out var b)) return null;

        var gap = (b - a).TotalSeconds;

        // 끝 시각이 시작보다 앞서는 응답이 드물게 온다(재시도한 실행).
        // 음수를 그대로 평균에 넣으면 숫자가 뒤집힌다.
        return gap < 0 ? null : Math.Round(gap, 1);
    }
}
