using System.Net.Sockets;
using System.Text.Json;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

/// <summary>
/// 배포 현황 엔드포인트 — 상태관리 &gt; 배포 현황 화면이 쓴다.
/// </summary>
/// <remarks>
/// GitHub Actions(빌드·배포 이력, 러너)와 운영서버 Docker(떠 있는 컨테이너와
/// 이미지 태그)를 한 번에 모아 준다. 화면이 GitHub 를 직접 부르지 않는 이유:
/// 토큰이 브라우저에 노출되기 때문이다. 토큰은 appsettings.Local.json 의
/// <c>DeployStatus:GithubToken</c> 에만 있고 여기서만 쓴다.
///
/// Docker 는 유닉스 소켓(/var/run/docker.sock, 컨테이너에 읽기 전용 마운트)으로
/// 엔진 API 를 부른다. 소켓이 없으면(개발 장비 등) docker.available=false 로
/// 조용히 빠진다 — 화면은 GitHub 부분만 보여 주면 된다.
///
/// 이미지 정리(POST /cleanup)는 배포가 거듭될수록 옛 태그가 쌓여 디스크를
/// 차지하는 것을 걷어낸다 — funeralv2-* 저장소마다 사용 중 + 최근 2개만 남긴다.
/// 관리자 계열(ADMINISTRATOR·SYSTEM_ADMINISTRATOR)만 부를 수 있다.
///
/// 게이트웨이의 /api/auth/** 는 Anonymous 라 UserContext 가 없으면 401 을 준다.
/// </remarks>
public static class DeployStatusEndpoints
{
    /// <summary>저장소별로 남겨 둘 최근 태그 수 (사용 중 태그는 별도로 항상 남는다). 롤백 여지다.</summary>
    private const int KeepRecentTags = 2;

    public static void MapDeployStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/deploy-status").WithTags("DeployStatus");

        group.MapGet("", async (UserContext? user,
            IConfiguration config,
            IHttpClientFactory httpFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var repo = config["DeployStatus:GithubRepo"] ?? "quristyle/Funeralv2";
            var token = config["DeployStatus:GithubToken"];

            var (runs, runners, githubError) = await FetchGithubAsync(httpFactory, repo, token, ct);
            var docker = await FetchDockerAsync(SocketPath(config), ct);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                repo,
                generatedAt = DateTimeOffset.UtcNow,
                github = new { error = githubError, runs, runners },
                docker,
            }));
        });

        // ── 오래된 이미지 정리 ──────────────────────────────
        group.MapPost("/cleanup", async (UserContext? user,
            HttpContext http,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // X-User-Role 은 첫 역할 하나뿐이라 전체 목록(X-User-Roles)으로 판정한다.
            var roles = http.Request.Headers["X-User-Roles"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var allowed = roles.Contains("ADMINISTRATOR") || roles.Contains("SYSTEM_ADMINISTRATOR")
                          || user.Role is "ADMINISTRATOR" or "SYSTEM_ADMINISTRATOR";
            if (!allowed)
                return Results.Json(ApiResponse<object>.Fail("관리자만 정리할 수 있습니다.", "403"), statusCode: 403);

            var socketPath = SocketPath(config);
            if (!File.Exists(socketPath))
                return Results.Json(ApiResponse<object>.Fail("이 환경에는 Docker 소켓이 없습니다.", "400"), statusCode: 400);

            try
            {
                var result = await CleanupImagesAsync(socketPath, ct);
                return Results.Ok(ApiResponse<object>.Ok(result));
            }
            catch (Exception ex)
            {
                return Results.Json(ApiResponse<object>.Fail(ex.Message, "500"), statusCode: 500);
            }
        });
    }

    private static string SocketPath(IConfiguration config) =>
        config["DeployStatus:DockerSocket"] ?? "/var/run/docker.sock";

    // ── GitHub ──────────────────────────────────────────────

    private static async Task<(List<object> Runs, List<object> Runners, string? Error)> FetchGithubAsync(
        IHttpClientFactory httpFactory, string repo, string? token, CancellationToken ct)
    {
        var runs = new List<object>();
        var runners = new List<object>();
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("jsini-portal-deploy-status");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            // 자리표시자("__SET_IN_...")면 토큰 없이 부른다 — public 저장소라 조회는 되고 한도만 낮다.
            if (!string.IsNullOrWhiteSpace(token) && !token.StartsWith("__"))
                client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            using var runsDoc = JsonDocument.Parse(
                await client.GetStringAsync($"https://api.github.com/repos/{repo}/actions/runs?per_page=20", ct));
            foreach (var r in runsDoc.RootElement.GetProperty("workflow_runs").EnumerateArray())
            {
                var started = r.TryGetProperty("run_started_at", out var s) && s.ValueKind == JsonValueKind.String
                    ? s.GetDateTimeOffset() : (DateTimeOffset?)null;
                var updated = r.GetProperty("updated_at").GetDateTimeOffset();
                runs.Add(new
                {
                    id = r.GetProperty("id").GetInt64(),
                    name = r.GetProperty("name").GetString(),
                    status = r.GetProperty("status").GetString(),
                    conclusion = r.GetProperty("conclusion").GetString(),
                    branch = r.GetProperty("head_branch").GetString(),
                    sha = r.GetProperty("head_sha").GetString(),
                    @event = r.GetProperty("event").GetString(),
                    actor = r.TryGetProperty("actor", out var a) && a.ValueKind == JsonValueKind.Object
                        ? a.GetProperty("login").GetString() : null,
                    title = r.TryGetProperty("display_title", out var t) ? t.GetString() : null,
                    startedAt = started,
                    updatedAt = updated,
                    durationSec = started is null ? (double?)null : Math.Max(0, (updated - started.Value).TotalSeconds),
                    htmlUrl = r.GetProperty("html_url").GetString(),
                });
            }

            using var runnersDoc = JsonDocument.Parse(
                await client.GetStringAsync($"https://api.github.com/repos/{repo}/actions/runners", ct));
            foreach (var r in runnersDoc.RootElement.GetProperty("runners").EnumerateArray())
            {
                runners.Add(new
                {
                    name = r.GetProperty("name").GetString(),
                    status = r.GetProperty("status").GetString(),
                    busy = r.GetProperty("busy").GetBoolean(),
                    labels = r.GetProperty("labels").EnumerateArray()
                        .Select(l => l.GetProperty("name").GetString()).ToArray(),
                });
            }
            return (runs, runners, null);
        }
        catch (Exception ex)
        {
            return (runs, runners, ex.Message);
        }
    }

    // ── Docker (유닉스 소켓) ─────────────────────────────────

    private static HttpClient CreateDockerClient(string socketPath)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    private static async Task<object> FetchDockerAsync(string socketPath, CancellationToken ct)
    {
        if (!File.Exists(socketPath))
            return new { available = false, error = (string?)null, containers = Array.Empty<object>(), images = Array.Empty<object>(), imagesTotalMb = 0L };

        try
        {
            using var client = CreateDockerClient(socketPath);

            // 버전 접두사(/v1.xx)를 붙이지 않는다 — Docker 29 는 낡은 버전 명시를
            // 400 으로 거부한다. 비버전 경로는 엔진이 현재 버전으로 처리한다.
            using var doc = JsonDocument.Parse(
                await client.GetStringAsync("/containers/json?all=true", ct));

            var containers = new List<object>();
            var imagesInUse = new HashSet<string>();
            foreach (var c in doc.RootElement.EnumerateArray())
            {
                var image = c.GetProperty("Image").GetString() ?? "";
                imagesInUse.Add(image);

                var labels = c.GetProperty("Labels");
                var service = labels.TryGetProperty("com.docker.compose.service", out var svc)
                    ? svc.GetString() : null;
                if (service is null) continue; // compose 가 아닌 컨테이너는 제외

                var tagIdx = image.LastIndexOf(':');
                containers.Add(new
                {
                    service,
                    project = labels.TryGetProperty("com.docker.compose.project", out var prj) ? prj.GetString() : null,
                    image,
                    tag = tagIdx > 0 ? image[(tagIdx + 1)..] : "",
                    state = c.GetProperty("State").GetString(),
                    status = c.GetProperty("Status").GetString(),
                    createdAt = DateTimeOffset.FromUnixTimeSeconds(c.GetProperty("Created").GetInt64()),
                });
            }

            // 이미지 사용량 — 배포 태그가 얼마나 쌓였는지 화면에서 보여 준다.
            var (images, totalMb) = await ListImagesAsync(client, imagesInUse, ct);

            return new { available = true, error = (string?)null, containers, images, imagesTotalMb = totalMb };
        }
        catch (Exception ex)
        {
            return new { available = false, error = ex.Message, containers = Array.Empty<object>(), images = Array.Empty<object>(), imagesTotalMb = 0L };
        }
    }

    private static async Task<(List<object> Images, long TotalMb)> ListImagesAsync(
        HttpClient client, HashSet<string> imagesInUse, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/images/json", ct));
        var images = new List<object>();
        long totalBytes = 0;
        foreach (var i in doc.RootElement.EnumerateArray())
        {
            if (i.GetProperty("RepoTags").ValueKind != JsonValueKind.Array) continue;
            foreach (var t in i.GetProperty("RepoTags").EnumerateArray())
            {
                var name = t.GetString() ?? "";
                if (!name.Contains("funeralv2-")) continue;
                var size = i.GetProperty("Size").GetInt64();
                totalBytes += size;
                images.Add(new
                {
                    name,
                    sizeMb = size / (1024 * 1024),
                    createdAt = DateTimeOffset.FromUnixTimeSeconds(i.GetProperty("Created").GetInt64()),
                    inUse = imagesInUse.Contains(name),
                });
            }
        }
        return (images, totalBytes / (1024 * 1024));
    }

    /// <summary>
    /// funeralv2-* 저장소마다 사용 중 태그 + 최근 <see cref="KeepRecentTags"/>개만 남기고
    /// 지운 뒤, 이름 없는(dangling) 레이어를 정리한다.
    /// </summary>
    private static async Task<object> CleanupImagesAsync(string socketPath, CancellationToken ct)
    {
        using var client = CreateDockerClient(socketPath);

        var imagesInUse = new HashSet<string>();
        using (var cdoc = JsonDocument.Parse(await client.GetStringAsync("/containers/json?all=true", ct)))
        {
            foreach (var c in cdoc.RootElement.EnumerateArray())
                imagesInUse.Add(c.GetProperty("Image").GetString() ?? "");
        }

        // 저장소별 태그 목록 (생성 시각 내림차순)
        var byRepo = new Dictionary<string, List<(string Name, long Created)>>();
        using (var idoc = JsonDocument.Parse(await client.GetStringAsync("/images/json", ct)))
        {
            foreach (var i in idoc.RootElement.EnumerateArray())
            {
                if (i.GetProperty("RepoTags").ValueKind != JsonValueKind.Array) continue;
                var created = i.GetProperty("Created").GetInt64();
                foreach (var t in i.GetProperty("RepoTags").EnumerateArray())
                {
                    var name = t.GetString() ?? "";
                    var repoIdx = name.LastIndexOf(':');
                    if (repoIdx < 0 || !name.Contains("funeralv2-")) continue;
                    var repoName = name[..repoIdx];
                    (byRepo.TryGetValue(repoName, out var list) ? list : byRepo[repoName] = []).Add((name, created));
                }
            }
        }

        var removed = new List<string>();
        var errors = new List<string>();
        foreach (var (_, tags) in byRepo)
        {
            var candidates = tags.OrderByDescending(t => t.Created)
                .Where(t => !imagesInUse.Contains(t.Name))
                .Skip(KeepRecentTags);
            foreach (var (name, _) in candidates)
            {
                var res = await client.DeleteAsync($"/images/{Uri.EscapeDataString(name)}", ct);
                if (res.IsSuccessStatusCode) removed.Add(name);
                else errors.Add($"{name}: {(int)res.StatusCode}");
            }
        }

        // 이름 잃은 레이어 정리 — 실제 디스크 회수는 대부분 여기서 일어난다.
        long reclaimed = 0;
        var pruneRes = await client.PostAsync(
            "/images/prune?filters=" + Uri.EscapeDataString("""{"dangling":["true"]}"""), null, ct);
        if (pruneRes.IsSuccessStatusCode)
        {
            using var pdoc = JsonDocument.Parse(await pruneRes.Content.ReadAsStringAsync(ct));
            if (pdoc.RootElement.TryGetProperty("SpaceReclaimed", out var sr))
                reclaimed = sr.GetInt64();
        }

        return new
        {
            removed,
            errors,
            keptRecent = KeepRecentTags,
            spaceReclaimedMb = reclaimed / (1024 * 1024),
        };
    }
}
