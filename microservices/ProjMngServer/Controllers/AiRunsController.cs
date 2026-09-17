using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>실행 이력과 로그 — <c>/api/ai-runs</c>. 화면이 읽는다.</summary>
[ApiController]
[Route("api/ai-runs")]
public sealed class AiRunsController(AiRunService runs) : ControllerBase
{
    /// <summary>
    /// 로그 꼬리. <b>증분으로 읽는다</b> — 화면이 2~3초마다 이것을 부른다.
    /// </summary>
    /// <remarks>
    /// SSE 로 흘리지 않는 이유는 <c>GatewayClient</c> 가 봉투를 통째로 받아
    /// 벗기는 클라이언트라 <c>text/event-stream</c> 을 못 읽기 때문이다.
    /// 그 길로 가면 공유 자리에 스트리밍 클라이언트가 하나 더 생기는데
    /// 얻는 것은 지연 2초뿐이다.
    /// </remarks>
    [HttpGet("{runKey:long}/logs")]
    public async Task<IActionResult> LogsAsync(long runKey, [FromQuery] int fromSeq = 0)
    {
        var lines = await runs.LogsAsync(runKey, fromSeq);
        return Ok(ApiResponse<List<AiLogLine>>.Ok(lines));
    }
}

/// <summary>
/// <b>실행기가 부르는 경로</b> — <c>/api/ai-runner</c>.
/// </summary>
/// <remarks>
/// <para>
/// 사람의 로그인이 아니라 <b>run 별 1회용 토큰</b>으로 인증한다
/// (<c>X-AiTask-Token</c>). 장비에 계정 정보를 두지 않으려는 것이고,
/// 배포 도구가 먼저 쓰던 방식이다(설계 6.9).
/// </para>
/// <para>
/// <b>집어가기(<c>claim</c>)만 장비 토큰을 쓴다</b> — 그 한 번으로 run 토큰을
/// 받고, 그 뒤로는 run 토큰이다.
/// </para>
/// <para>
/// 이 경로는 <b>게이트웨이에 열지 않는다.</b> 실행기가 같은 장비 안에서
/// :5450 을 직접 부른다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai-runner")]
public sealed class AiRunnerController(
    AiRunService runs, IConfiguration configuration, ILogger<AiRunnerController> logger)
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
        if (string.IsNullOrWhiteSpace(RunnerToken))
        {
            logger.LogWarning("AiTasks:RunnerToken 이 없어 집어가기를 거절했습니다.");
            return Unauthorized(ApiResponse<object>.Fail(
                message: "실행기 토큰이 설정돼 있지 않습니다.", code: "NO_RUNNER_TOKEN"));
        }

        if (RunToken != RunnerToken)
        {
            return Unauthorized(ApiResponse<object>.Fail(message: "토큰이 맞지 않습니다.", code: "BAD_TOKEN"));
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
}
