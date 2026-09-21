using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 — <c>/api/ai-tasks</c>.</summary>
/// <remarks>
/// <para>
/// 게이트웨이가 <c>/api/projmng</c> 를 떼고 <c>/api</c> 를 다시 붙인다
/// (<c>/api/projmng/ai-tasks</c> → 여기 <c>/api/ai-tasks</c>).
/// </para>
/// <para>
/// <b>이 경로는 인증이 걸려 있다.</b> <c>projmng-route</c> 는 정책을 따로 적지
/// 않고 게이트웨이의 <c>FallbackPolicy</c>(= 인증 필요)를 따른다 — AI 를
/// <c>ai-route</c>(익명) 밑에 두지 않은 이유가 이것이다.
/// </para>
/// <para>
/// 등록·수정자는 <b>게이트웨이가 붙여 주는 신원</b>(<c>X-User-Id</c>)으로 채운다.
/// 본문에 실어 보내면 위조해도 서버가 알 수 없다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai-tasks")]
public sealed class AiTasksController(AiTaskService service, AiRunService runs) : ControllerBase
{
    /// <summary>요청을 보낸 사람. 없으면 <c>system</c>.</summary>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status, [FromQuery] string? flag,
        [FromQuery] long? targetKey, [FromQuery] string? keyword,
        [FromQuery] bool? userConfirmed)
    {
        var rows = await service.ListAsync(status, flag, targetKey, keyword, userConfirmed: userConfirmed);
        return Ok(ApiResponse<List<AiTask>>.Ok(rows));
    }

    [HttpGet("{taskKey:long}")]
    public async Task<IActionResult> GetAsync(long taskKey)
    {
        var item = await service.GetAsync(taskKey);

        return item is null
            ? NotFound(ApiResponse<AiTask>.Fail(message: "그런 작업이 없습니다.", code: "NOT_FOUND"))
            : Ok(ApiResponse<AiTask>.Ok(item));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        var created = await service.CreateAsync(item, UserId);
        return Ok(ApiResponse<AiTask>.Ok(created));
    }

    [HttpPut("{taskKey:long}")]
    public async Task<IActionResult> UpdateAsync(long taskKey, [FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        return Respond(await service.UpdateAsync(taskKey, item, UserId));
    }

    /// <summary>실행 이력. 최근 것이 앞이다.</summary>
    [HttpGet("{taskKey:long}/runs")]
    public async Task<IActionResult> RunsAsync(long taskKey)
        => Ok(ApiResponse<List<AiTaskRun>>.Ok(await runs.RunsAsync(taskKey)));

    /// <summary>
    /// <b>작업을 요청한다.</b> 요청여부가 <c>requested</c>, 상태가 <c>queued</c> 가 된다.
    /// </summary>
    [HttpPost("{taskKey:long}/request")]
    public async Task<IActionResult> RequestAsync(long taskKey)
        => Respond(await service.RequestAsync(taskKey, UserId));

    /// <summary>
    /// <b>끝난 작업에 이어서 지시한다.</b> 본문이 「지난 진행 + 이번에 할 일」로 바뀐다.
    /// <b>이 회차를 맡을 AI 도 갈아탈 수 있다</b>(<c>runnerKind</c>).
    /// </summary>
    [HttpPost("{taskKey:long}/continue")]
    public async Task<IActionResult> ContinueAsync(long taskKey, [FromBody] ContinueRequest req)
        => Respond(await service.ContinueAsync(taskKey, req.Addition, req.RunnerKind, UserId));

    /// <summary>
    /// <b>실패한 작업을 수동으로 다시 요청한다.</b> 추가 지시사항을 얹을 수 있다.
    /// </summary>
    [HttpPost("{taskKey:long}/retry")]
    public async Task<IActionResult> RetryAsync(long taskKey, [FromBody] RetryRequest req)
        => Respond(await service.RetryAsync(taskKey, req?.Addition, UserId));

    /// <summary>
    /// <b>사용자 확인 완료</b> 처리한다.
    /// </summary>
    [HttpPost("{taskKey:long}/confirm")]
    public async Task<IActionResult> ConfirmAsync(long taskKey)
        => Respond(await service.ConfirmAsync(taskKey, UserId));

    /// <summary>취소를 요청한다. 도는 중이면 표시만 남고 실행기가 멈춘다.</summary>
    [HttpPost("{taskKey:long}/cancel")]
    public async Task<IActionResult> CancelAsync(long taskKey)
        => Respond(await service.CancelAsync(taskKey, UserId));

    [HttpDelete("{taskKey:long}")]
    public async Task<IActionResult> DeleteAsync(long taskKey)
        => Respond(await service.DeleteAsync(taskKey, UserId));

    /// <summary>
    /// 결과를 HTTP 로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <b>「없다」와 「지금은 안 된다」를 가른다.</b> 둘을 같은 코드로 내려보내면
    /// 화면이 「없는 작업입니다」라고 말하는데 실제로는 돌고 있는 중인 경우가 생긴다.
    /// </remarks>
    public sealed class ContinueRequest
    {
        /// <summary>이번에 더 시킬 일.</summary>
        public string? Addition { get; set; }

        /// <summary>
        /// 이 회차를 맡을 AI. <b>비우면 지난 회차의 실행기를 그대로 쓴다.</b>
        /// </summary>
        public string? RunnerKind { get; set; }
    }

    public sealed class RetryRequest
    {
        /// <summary>재시도 시 더 시킬 일.</summary>
        public string? Addition { get; set; }
    }

    private IActionResult Respond(AiTaskEditResult result)
    {
        if (!result.Found)
        {
            return NotFound(ApiResponse<AiTask>.Fail(message: "그런 작업이 없습니다.", code: "NOT_FOUND"));
        }

        if (result.ConflictMessage is { } message)
        {
            return Conflict(ApiResponse<AiTask>.Fail(message: message, code: "CONFLICT"));
        }

        return Ok(ApiResponse<AiTask>.Ok(result.Item));
    }
}
