using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>실행 이력과 로그 — <c>/api/ai-runs</c>. 화면이 읽는다.</summary>
[ApiController]
[Route("api/ai-runs")]
public sealed class AiRunsController(
    AiRunService runs, AiRunSummaryWriter summaries, AiTaskNotifier notifier) : ControllerBase
{
    /// <summary>
    /// 로그 꼬리. <b>증분으로 읽는다</b> — 화면이 2~3초마다 이것을 부른다.
    /// </summary>
    /// SSE 로 흘리지 않는다 — GatewayClient 가 봉투를 통째로 받아 벗기는 클라이언트라 text/event-stream 을 못 읽고, 스트리밍을 더해 얻는 것은 지연 2초뿐이다.
    [HttpGet("{runKey:long}/logs")]
    public async Task<IActionResult> LogsAsync(long runKey, [FromQuery] int fromSeq = 0)
    {
        var lines = await runs.LogsAsync(runKey, fromSeq);
        return Ok(ApiResponse<List<AiLogLine>>.Ok(lines));
    }

    /// <summary>
    /// <b>「처리 요약」을 지금 만든다.</b> 화면의 「요약 다시 만들기」가 부른다.
    /// </summary>
    /// 요약은 실행이 끝날 때 서버가 적어 두는데 AI 가 붐비면 비므로, 사람이 다시 시킬 수 있게 둔다. 만드는 일은 AiRunSummaryWriter 한 곳이 하고, 못 만들면 오류가 아니라 빈 값이다.
    /// 만들어졌으면 미뤄 둔 결과 메일도 여기서 풀어 준다(이미 나간 건은 그대로 돌아간다).
    [HttpPost("{runKey:long}/summary")]
    public async Task<IActionResult> SummaryAsync(long runKey, CancellationToken ct)
    {
        var summary = await summaries.EnsureAsync(runKey, ct: ct);

        if (summary is not null)
        {
            await notifier.SendMailAsync(runKey, deferUntilSummary: false, ct);
        }

        return Ok(ApiResponse<string>.Ok(summary?.ToText() ?? string.Empty));
    }
}

/// <summary>
/// <b>실행기가 부르는 경로</b> — <c>/api/ai-runner</c>.
/// </summary>
/// 사람 로그인이 아니라 run 별 1회용 토큰(X-AiTask-Token)으로 인증한다. 집어가기만 장비 토큰을 쓰고 그 뒤로는 run 토큰이다.
/// 게이트웨이에 열지 않는다 — 실행기가 같은 장비 안에서 직접 부른다.
[ApiController]
[Route("api/ai-runner")]
public sealed class AiRunnerController(
    AiRunService runs,
    AiTargetStatusService targetStatus,
    IConfiguration configuration,
    ILogger<AiRunnerController> logger)
    : ControllerBase
{
    /// <summary>실행기가 헤더로 보내는 run 토큰.</summary>
    private const string TokenHeader = "X-AiTask-Token";

    private string? RunToken =>
        Request.Headers.TryGetValue(TokenHeader, out var v) ? v.ToString() : null;

    /// <summary>
    /// 장비 토큰. 설정에 없으면 <b>집어가기를 아예 막는다</b> —
    /// 빈 값을 통과시키면 누구나 큐의 내용(=지시문 전문)을 받아 갈 수 있다.
    /// </summary>
    private string? RunnerToken => configuration["AiTasks:RunnerToken"];

    [HttpPost("claim")]
    public async Task<IActionResult> ClaimAsync([FromBody] ClaimRequest req)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        var kinds = req.Kinds is { Length: > 0 } ? req.Kinds : ["claude"];

        var claims = await runs.ClaimAsync(
            req.RunnerName ?? "runner", kinds, Math.Clamp(req.Capacity, 0, 5));

        return Ok(ApiResponse<List<AiClaim>>.Ok(claims));
    }

    [HttpPost("runs/{runKey:long}/heartbeat")]
    public async Task<IActionResult> HeartbeatAsync(long runKey)
    {
        var beat = await runs.HeartbeatAsync(runKey, RunToken ?? string.Empty);

        // 403 은 「보고를 그만두라」는 뜻이다. 실행기는 그때도 **작업은 계속한다** —
        // 보고가 안 되는 것과 작업이 실패하는 것은 다른 일이다.
        return beat is null
            ? StatusCode(403, ApiResponse<object>.Fail(message: "끝났거나 토큰이 다릅니다.", code: "GONE"))
            : Ok(ApiResponse<AiHeartbeat>.Ok(beat));
    }

    /// <summary>
    /// <b>지시에 붙어 온 파일 하나를 실행기가 받아 간다.</b>
    /// </summary>
    /// 집어가기 목록에는 이름과 크기뿐이다 — 바이트를 거기 실으면 1초마다 도는 집어가기가 사진을 매번 끌고 온다. 여는 열쇠는 run 토큰이고 나가는 것은 그 실행의 작업에 붙은 것뿐이다.
    [HttpGet("runs/{runKey:long}/files/{fileKey:long}")]
    public async Task<IActionResult> RunFileAsync(long runKey, long fileKey)
    {
        var found = await runs.RunFileAsync(runKey, RunToken ?? string.Empty, fileKey);

        // **무엇이 틀렸는지 갈라 말하지 않는다.** 토큰이 다른 것과 그 실행의
        // 파일이 아닌 것이 같은 답을 받는다 — 가르면 토큰 하나로 남의 첨부
        // 번호를 훑을 수 있다.
        return found is null
            ? StatusCode(403, ApiResponse<object>.Fail(message: "끝났거나 토큰이 다릅니다.", code: "GONE"))
            : File(found.Value.Bytes, "application/octet-stream", found.Value.FileNm);
    }

    [HttpPost("runs/{runKey:long}/logs")]
    public async Task<IActionResult> LogsAsync(long runKey, [FromBody] LogsRequest req)
    {
        var ok = await runs.AppendLogsAsync(runKey, RunToken ?? string.Empty, req.Lines ?? []);

        return ok
            ? Ok(ApiResponse<bool>.Ok(true))
            : StatusCode(403, ApiResponse<object>.Fail(message: "끝났거나 토큰이 다릅니다.", code: "GONE"));
    }

    [HttpPost("runs/{runKey:long}/complete")]
    public async Task<IActionResult> CompleteAsync(long runKey, [FromBody] AiCompleteRequest req)
    {
        var ok = await runs.CompleteAsync(runKey, RunToken ?? string.Empty, req);

        return ok
            ? Ok(ApiResponse<bool>.Ok(true))
            : StatusCode(403, ApiResponse<object>.Fail(message: "끝났거나 토큰이 다릅니다.", code: "GONE"));
    }

    /// <summary>
    /// <b>AI CLI 의 <c>/usage</c> 보고.</b> 장비 토큰으로 인증한다.
    /// </summary>
    /// 집어가기와 같은 장비 토큰이다. 실행과 무관하게 주기적으로 올라와 묶일 run 이 없다. 한도를 하나도 못 읽어도 200 — 「장비가 살아 있다」는 뜻이 함께 실려 있다.
    [HttpPost("usage")]
    public async Task<IActionResult> UsageAsync(
        [FromBody] AiUsageReport req, [FromServices] AiUsageService usage)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        return Ok(ApiResponse<int>.Ok(await usage.SaveAsync(req)));
    }

    // ── 대상의 git 상태 ─────────────────────────────────────
    // 서버는 대상 경로를 볼 수 없어(컨테이너 안) 호스트의 실행기가 답한다. 집어가기와 같은 장비 토큰을 쓴다 — run 토큰은 실행 한 번에 묶여 있어 이 일에 쓸 수 없다.

    /// <summary>이 장비가 들여다볼 대상 목록.</summary>
    [HttpGet("targets")]
    public async Task<IActionResult> ProbeTargetsAsync([FromQuery] string? runnerName)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        var rows = await targetStatus.ProbeListAsync(runnerName ?? "runner");
        return Ok(ApiResponse<List<AiTargetProbe>>.Ok(rows));
    }

    /// <summary>들여다본 결과를 받아 둔다.</summary>
    [HttpPost("target-status")]
    public async Task<IActionResult> TargetStatusAsync([FromBody] TargetStatusRequest req)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        var saved = await targetStatus.ReportAsync(req.RunnerName ?? "runner", req.Items ?? []);
        return Ok(ApiResponse<int>.Ok(saved));
    }

    // ── 처리 파일 대 보기 ───────────────────────────────────
    // 위와 같은 까닭으로 여기 있다 — 추정 경로가 실제 파일인지는 호스트의 실행기만 안다(AiRunFileCheckService).

    /// <summary>이 장비가 대 볼 추정 경로들.</summary>
    [HttpGet("file-checks")]
    public async Task<IActionResult> FileChecksAsync(
        [FromQuery] string? runnerName, [FromServices] AiRunFileCheckService checks, CancellationToken ct)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        var jobs = await checks.PendingAsync(runnerName ?? "runner", ct);
        return Ok(ApiResponse<List<AiRunFileCheckJob>>.Ok(jobs));
    }

    /// <summary>대 본 결과를 받아 새긴다.</summary>
    [HttpPost("file-checks")]
    public async Task<IActionResult> FileChecksReportAsync(
        [FromBody] FileCheckRequest req,
        [FromServices] AiRunFileCheckService checks,
        CancellationToken ct)
    {
        if (RunnerTokenBad() is { } bad)
        {
            return bad;
        }

        var saved = await checks.ApplyAsync(req.RunnerName ?? "runner", req.Items ?? [], ct);
        return Ok(ApiResponse<int>.Ok(saved));
    }

    /// <summary>
    /// 장비 토큰을 본다. 틀리면 그 응답을, 맞으면 <c>null</c> 을 돌려준다.
    /// </summary>
    /// 설정에 토큰이 없으면 막는다 — 빈 값을 통과시키면 누구나 대상 경로 목록을 받아 갈 수 있다.
    private IActionResult? RunnerTokenBad()
    {
        if (string.IsNullOrWhiteSpace(RunnerToken))
        {
            logger.LogWarning("AiTasks:RunnerToken 이 없어 실행기 요청을 거절했습니다.");

            return Unauthorized(ApiResponse<object>.Fail(
                message: "실행기 토큰이 설정돼 있지 않습니다.", code: "NO_RUNNER_TOKEN"));
        }

        return RunToken == RunnerToken
            ? null
            : Unauthorized(ApiResponse<object>.Fail(message: "토큰이 맞지 않습니다.", code: "BAD_TOKEN"));
    }

    public sealed class ClaimRequest
    {
        public string? RunnerName { get; set; }
        public string[]? Kinds { get; set; }
        public int Capacity { get; set; } = 1;
    }

    public sealed class LogsRequest
    {
        public List<AiLogLine>? Lines { get; set; }
    }

    public sealed class TargetStatusRequest
    {
        public string? RunnerName { get; set; }

        public List<AiTargetStatus>? Items { get; set; }
    }

    public sealed class FileCheckRequest
    {
        public string? RunnerName { get; set; }

        public List<AiRunFileCheckReport>? Items { get; set; }
    }
}
