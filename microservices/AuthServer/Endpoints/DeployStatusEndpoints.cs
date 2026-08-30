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
/// 게이트웨이의 /api/auth/** 는 Anonymous 라 UserContext 가 없으면 401 을 준다.
/// </remarks>
public static class DeployStatusEndpoints
{
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
            var docker = await FetchDockerAsync(config["DeployStatus:DockerSocket"] ?? "/var/run/docker.sock", ct);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                repo,
                generatedAt = DateTimeOffset.UtcNow,
                github = new { error = githubError, runs, runners },
                docker,
            }));
        });
    }

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

    private static async Task<object> FetchDockerAsync(string socketPath, CancellationToken ct)
    {
        if (!File.Exists(socketPath))
            return new { available = false, error = (string?)null, containers = Array.Empty<object>() };

        try
        {
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, ct2) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct2);
                    return new NetworkStream(socket, ownsSocket: true);
                },
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            client.Timeout = TimeSpan.FromSeconds(5);

            // 버전 접두사(/v1.xx)를 붙이지 않는다 — Docker 29 는 낡은 버전 명시를
            // 400 으로 거부한다. 비버전 경로는 엔진이 현재 버전으로 처리한다.
            using var doc = JsonDocument.Parse(
                await client.GetStringAsync("/containers/json?all=true", ct));

            var containers = new List<object>();
            foreach (var c in doc.RootElement.EnumerateArray())
            {
                var labels = c.GetProperty("Labels");
                var service = labels.TryGetProperty("com.docker.compose.service", out var svc)
                    ? svc.GetString() : null;
                if (service is null) continue; // compose 가 아닌 컨테이너는 제외

                var image = c.GetProperty("Image").GetString() ?? "";
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
            return new { available = true, error = (string?)null, containers };
        }
        catch (Exception ex)
        {
            return new { available = false, error = ex.Message, containers = Array.Empty<object>() };
        }
    }
}
