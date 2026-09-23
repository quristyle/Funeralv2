using System.Net.Http.Headers;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 하나의 Git 설정.
/// </summary>
/// <remarks>
/// <para>
/// 설정은 <c>Git</c> 아래 두 겹이다 — 바깥이 기본값이고
/// <c>Git:Projects:{프로젝트번호}</c> 가 그것을 덮는다. 저장소 목록은 프로젝트
/// 마다 다르므로 보통 아래에만 적는다.
/// </para>
///
/// <para>
/// [저장소를 만들어 내지 않는다]
/// </para>
///
/// <para>
/// 사내 GitLab 시절에는 <c>pmm001</c>…<c>pmm018</c> × <c>be</c>/<c>fe</c> 규칙으로
/// 서른여섯 곳을 <b>계산해서</b> 훑었다. GitHub 로 오면서 그 규칙이 뜻을 잃어
/// (저장소가 하나다) 설정이 목록을 준다.
/// </para>
///
/// <para>
/// [토큰이 없어도 돈다]
/// </para>
///
/// <para>
/// 공개 저장소는 토큰 없이 읽힌다 — 시간당 60회라는 한도가 붙을 뿐이다.
/// 토큰을 넣으면 5,000회가 되고, 비공개 저장소와 패키지도 보인다.
/// <b>그래서 「설정됐나」의 기준은 토큰이 아니라 저장소 목록이다.</b>
/// </para>
/// </remarks>
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
    /// <remarks>
    /// GitHub 의 가지 목록에는 <b>커밋 날짜가 없다.</b> 묵은 가지를 가리려면
    /// 가지마다 한 번씩 더 물어야 해서, 화면에 보이는 만큼만 묻는다.
    /// 0 으로 두면 아예 안 묻고 「묵음」 칸이 비어 있게 된다.
    /// </remarks>
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
/// <remarks>
/// <para>
/// [<c>User-Agent</c> 를 반드시 붙인다]
/// </para>
///
/// <para>
/// GitHub 는 그 머리글이 없으면 <b>403 으로 끊는다.</b> 토큰이 없어서라고
/// 읽기 쉬운 자리라 여기 한 곳에서만 만든다.
/// </para>
///
/// <para>
/// [실패를 예외로 올리지 않는다]
/// </para>
///
/// <para>
/// 훑는 것 중 몇은 <b>없는 것이 정상</b>이다(태그가 없는 저장소, 토큰이 없어
/// 못 보는 패키지). 그것을 오류로 올리면 멀쩡한 저장소가 전부 빨갛게 보인다.
/// 줄 전체가 실패한 것만 부르는 쪽이 <c>Error</c> 에 담는다.
/// </para>
/// </remarks>
public sealed class GitHubClient(IHttpClientFactory factory)
{
    /// <summary>마지막 응답이 알려 준 남은 한도. 화면이 보여 준다.</summary>
    public int? RateRemaining { get; private set; }

    /// <inheritdoc cref="RateRemaining"/>
    public int? RateLimit { get; private set; }

    private HttpRequestMessage Request(GitOptions opt, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

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
        try
        {
            using var request = Request(opt, $"{opt.ApiBaseUrl}{path}");

            using var http = factory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);

            using var response = await http.SendAsync(request);

            RememberRate(response);

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

            return string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
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
    /// <remarks>
    /// GitHub 는 총 개수를 안 준다. 한 쪽에 하나만 달라고 한 뒤 <c>Link</c>
    /// 머리글의 마지막 쪽 번호를 읽으면 그것이 개수다. 쪽 나눔이 없으면
    /// (한 쪽에 다 들어가면) 받은 배열의 길이가 곧 개수다.
    /// </remarks>
    public async Task<int?> CountAsync(GitOptions opt, string path)
    {
        try
        {
            using var request = Request(opt, $"{opt.ApiBaseUrl}{path}");

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
    /// <remarks>
    /// GitHub 는 둘로 나눠 준다 — 끝났는지(<c>status</c>)와 어떻게 끝났는지
    /// (<c>conclusion</c>). <b>안 끝난 실행에는 결론이 없다</b>(<c>null</c>)
    /// 이므로 그때는 진행 상태를 그대로 쓴다.
    /// </remarks>
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
