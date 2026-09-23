using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>실행 이력과 로그 — <c>/api/ai-runs</c>. 화면이 읽는다.</summary>
[ApiController]
[Route("api/ai-runs")]
public sealed class AiRunsController(
    AiRunService runs, AiRunSummaryWriter summaries) : ControllerBase
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

    /// <summary>
    /// <b>「처리 요약」을 지금 만든다.</b> 화면의 「요약 다시 만들기」가 부른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 요약은 실행이 끝날 때 서버가 적어 둔다. 그 한 번에 AI 가 붐비면 비는데,
    /// 감시자가 몇 분 뒤 주워 가기는 해도(<c>AiSummaryCatchUp</c>) <b>지금 그 판을
    /// 보고 있는 사람</b>은 그때까지 빈 자리를 볼 뿐이고 무엇을 기다리는지도 모른다.
    /// 그래서 사람이 직접 한 번 더 시킬 수 있게 둔다.
    /// </para>
    /// <para>
    /// <b>만드는 일은 서버 한 곳(<see cref="AiRunSummaryWriter"/>)이 그대로 한다.</b>
    /// 화면이 모델을 직접 부르면 메일에 적힌 요약과 화면의 요약이 다른 말을 하게
    /// 되고, 그때 어느 쪽이 그 실행의 요약인지 가릴 방법이 없다. 이미 적혀 있으면
    /// 되읽어 돌려주므로 여러 번 눌러도 한 번만 만든다.
    /// </para>
    /// <para>
    /// <b>못 만들어도 오류가 아니다</b> — 빈 값으로 답한다. 화면은 「아직 못
    /// 만들었다」를 그대로 보여 주면 되고, 그 사이 감시자가 계속 다시 본다.
    /// </para>
    /// </remarks>
    [HttpPost("{runKey:long}/summary")]
    public async Task<IActionResult> SummaryAsync(long runKey, CancellationToken ct)
    {
        var summary = await summaries.EnsureAsync(runKey, ct: ct);

        return Ok(ApiResponse<string>.Ok(summary?.ToText() ?? string.Empty));
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
    /// <remarks>
    /// <para>
    /// 집어가기와 같은 토큰이다 — run 토큰이 아니다. 이 보고는 <b>실행과
    /// 무관하게</b> 주기적으로 올라오므로 묶일 run 이 없다.
    /// </para>
    /// <para>
    /// <b>한도를 하나도 못 읽었어도 200 이다.</b> 이 요청에는 「장비가 살아
    /// 있다」는 뜻이 함께 실려 있고, 그쪽이 더 자주 쓸모가 있다.
    /// </para>
    /// </remarks>
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
    //
    // **서버는 대상 경로를 볼 수 없다.** 컨테이너 안이고 그 경로는 호스트의
    // 것이다. 그래서 「지금 저 저장소가 어떤 상태인가」에 답할 수 있는 것은
    // 호스트에 상주하는 실행기뿐이고, 아래 둘이 그 통로다.
    //
    // 집어가기와 같은 **장비 토큰**을 쓴다. run 토큰은 실행 한 번에 묶인
    // 것이라 실행이 없는 이 일에는 쓸 수 없다.

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

    /// <summary>
    /// 장비 토큰을 본다. 틀리면 그 응답을, 맞으면 <c>null</c> 을 돌려준다.
    /// </summary>
    /// <remarks>
    /// 설정에 토큰이 없으면 <b>막는다</b>. 빈 값을 통과시키면 누구나 대상
    /// 경로 목록을 받아 갈 수 있다 — 집어가기가 같은 이유로 그렇게 한다.
    /// </remarks>
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
}
