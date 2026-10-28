using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>WBS 대시보드 — <c>/api/wbs-board</c>.</summary>
/// /api/wbs 는 이미 프로젝트별 공정표가 쓰므로 이름만 같은 이 대시보드는 다른 경로다. 모든 조회는 prjRid 를 필수로 받는다 — 기본값을 두면 프로젝트를 안 고른 화면이 남의 숫자를 보여 준다.
[ApiController]
[Route("api/wbs-board")]
public sealed class WbsBoardController(
    WbsBoardService board,
    WbsBoardRowService rowsSvc,
    WbsProgressService progress,
    WbsDelayService delay) : ControllerBase
{
    // ──────────────────────────────────────────────────────── 집계

    /// <summary>요약 카드.</summary>
    [HttpGet("stats/summary")]
    public async Task<IActionResult> SummaryAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis, [FromQuery] string? scope)
        => Ok(ApiResponse<WbsBoardSummary>.Ok(await board.SummaryAsync(prjRid, basis, scope)));

    /// <summary>월 단위 건수.</summary>
    [HttpGet("stats/monthly")]
    public async Task<IActionResult> MonthlyAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis, [FromQuery] string? scope)
        => Ok(ApiResponse<List<WbsBoardBucket>>.Ok(await board.MonthlyAsync(prjRid, basis, scope)));

    /// <summary>주 단위 건수(ISO 주).</summary>
    [HttpGet("stats/weekly")]
    public async Task<IActionResult> WeeklyAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis, [FromQuery] string? scope)
        => Ok(ApiResponse<List<WbsBoardBucket>>.Ok(await board.WeeklyAsync(prjRid, basis, scope)));

    /// <summary>월 × 사람.</summary>
    [HttpGet("stats/monthly-by-user")]
    public async Task<IActionResult> MonthlyByUserAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis,
        [FromQuery] string? scope, [FromQuery] string? who)
        => Ok(ApiResponse<List<WbsBoardUserBucket>>.Ok(
            await board.MonthlyByUserAsync(prjRid, basis, scope, who)));

    /// <summary>주 × 사람.</summary>
    [HttpGet("stats/weekly-by-user")]
    public async Task<IActionResult> WeeklyByUserAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis,
        [FromQuery] string? scope, [FromQuery] string? who)
        => Ok(ApiResponse<List<WbsBoardUserBucket>>.Ok(
            await board.WeeklyByUserAsync(prjRid, basis, scope, who)));

    /// <summary>모듈별 건수.</summary>
    [HttpGet("stats/by-module")]
    public async Task<IActionResult> ByModuleAsync(
        [FromQuery] int prjRid, [FromQuery] string? basis, [FromQuery] string? scope)
        => Ok(ApiResponse<List<WbsBoardModule>>.Ok(await board.ByModuleAsync(prjRid, basis, scope)));

    /// <summary>담당자 고르개.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> UsersAsync(
        [FromQuery] int prjRid, [FromQuery] string? scope, [FromQuery] string? basis,
        [FromQuery] string? month, [FromQuery] string? week)
        => Ok(ApiResponse<List<WbsBoardUserOption>>.Ok(
            await board.UsersAsync(prjRid, scope, basis, month, week)));

    // ──────────────────────────────────────────────────────── 상세 목록

    [HttpGet("rows")]
    public async Task<IActionResult> RowsAsync([FromQuery] int prjRid, [FromQuery] WbsBoardQuery query)
        => Ok(ApiResponse<List<WbsBoardRow>>.Ok(await board.RowsAsync(prjRid, query)));

    /// <summary>
    /// 단건 수정. <b>화이트리스트 밖의 칸은 조용히 버린다</b> — 일정·실적은
    /// 엑셀 WBS 가 원본이라 여기서 고칠 수 없다.
    /// </summary>
    [HttpPatch("rows/{activityId}")]
    public async Task<IActionResult> PatchRowAsync(
        [FromQuery] int prjRid, string activityId, [FromBody] Dictionary<string, JsonElement> patch)
    {
        var affected = await board.PatchRowAsync(prjRid, activityId, WbsBoardSql.JsonValues(patch));

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "수정 가능한 칸이 없습니다.")),
            0 => NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"그 화면을 찾을 수 없습니다: {activityId}")),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    // ──────────────────────────────────────────────────────── 대상 추가·수정·삭제

    private string? LoginId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id) ? id.ToString() : null;

    /// <summary>한 줄의 모든 칸. 수정 창이 연다.</summary>
    [HttpGet("rows/{activityId}/full")]
    public async Task<IActionResult> RowOneAsync([FromQuery] int prjRid, string activityId)
        => await rowsSvc.OneAsync(prjRid, activityId) is { } row
            ? Ok(ApiResponse<List<WbsBoardField>>.Ok(row))
            : NotFound(ApiResponse<object>.Fail($"그 화면을 찾을 수 없습니다: {activityId}", "NOT_FOUND"));

    /// <summary>새 대상. 본문의 <c>activity_id</c> 를 비우면 임시 번호(NOID-)가 붙는다.</summary>
    [HttpPost("rows")]
    public Task<IActionResult> CreateRowAsync([FromQuery] int prjRid, [FromBody] Dictionary<string, JsonElement> body)
        => GuardAsync(async () =>
        {
            var id = body.TryGetValue("activity_id", out var el) ? WbsBoardSql.JsonValue(el) as string : null;
            return Ok(ApiResponse<string>.Ok(await rowsSvc.CreateAsync(prjRid, id, body)));
        });

    /// <summary>모든 칸 수정. 일정·실적 날짜도 고칠 수 있다 — 엑셀 WBS 에 없는 대상을 여기서 세우기 때문이다.</summary>
    [HttpPut("rows/{activityId}")]
    public Task<IActionResult> UpdateRowAsync(
        [FromQuery] int prjRid, string activityId, [FromBody] Dictionary<string, JsonElement> body)
        => GuardAsync(async () => await rowsSvc.UpdateAsync(prjRid, activityId, body) > 0
            ? Ok(ApiResponse<int>.Ok(1))
            : NotFound(ApiResponse<object>.Fail($"그 화면을 찾을 수 없습니다: {activityId}", "NOT_FOUND")));

    /// <summary>임시 번호로 세운 줄에 확정 Activity ID 를 채운다.</summary>
    [HttpPut("rows/{activityId}/activity-id")]
    public Task<IActionResult> SetRowIdAsync(
        [FromQuery] int prjRid, string activityId, [FromBody] Dictionary<string, JsonElement> body)
        => GuardAsync(async () =>
        {
            var id = body.TryGetValue("activity_id", out var el) ? WbsBoardSql.JsonValue(el) as string : null;
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(ApiResponse<object>.Fail("Activity ID 를 적으세요.", "INVALID"));

            return await rowsSvc.SetIdAsync(prjRid, activityId, id) > 0
                ? Ok(ApiResponse<string>.Ok(id.Trim()))
                : NotFound(ApiResponse<object>.Fail("그 줄을 찾지 못했습니다(이미 채워졌을 수 있습니다).", "NOT_FOUND"));
        });

    [HttpGet("rows/{activityId}/deps")]
    public async Task<IActionResult> RowDepsAsync([FromQuery] int prjRid, string activityId)
        => await rowsSvc.DepsAsync(prjRid, activityId) is { } deps
            ? Ok(ApiResponse<WbsBoardRowDeps>.Ok(deps))
            : NotFound(ApiResponse<object>.Fail($"그 화면을 찾을 수 없습니다: {activityId}", "NOT_FOUND"));

    [HttpDelete("rows/{activityId}")]
    public async Task<IActionResult> DeleteRowAsync([FromQuery] int prjRid, string activityId)
        => await rowsSvc.DeleteAsync(prjRid, activityId, LoginId) is { } gone
            ? Ok(ApiResponse<WbsBoardRowDeleted>.Ok(gone))
            : NotFound(ApiResponse<object>.Fail($"그 화면을 찾을 수 없습니다: {activityId}", "NOT_FOUND"));

    /// <summary>첨부를 올리거나 지운 뒤 그룹과 건수를 적는다.</summary>
    [HttpPut("rows/{activityId}/files")]
    public async Task<IActionResult> SetRowFilesAsync(
        [FromQuery] int prjRid, string activityId, [FromBody] WbsBoardRowFiles body)
        => await rowsSvc.SetFilesAsync(prjRid, activityId, body.GroupId, body.Count) > 0
            ? Ok(ApiResponse<int>.Ok(body.Count))
            : NotFound(ApiResponse<object>.Fail($"그 화면을 찾을 수 없습니다: {activityId}", "NOT_FOUND"));

    private async Task<IActionResult> GuardAsync(Func<Task<IActionResult>> run)
    {
        try
        {
            return await run();
        }
        catch (WbsRowConflict e)
        {
            return Conflict(ApiResponse<object>.Fail(e.Message, "CONFLICT"));
        }
    }

    // ──────────────────────────────────────────────────────── 진척률

    [HttpGet("progress/summary")]
    public async Task<IActionResult> ProgressSummaryAsync([FromQuery] int prjRid, [FromQuery] string? scope)
        => Ok(ApiResponse<WbsBoardProgress>.Ok(await progress.SummaryAsync(prjRid, scope)));

    [HttpGet("progress/by-user")]
    public async Task<IActionResult> ProgressByUserAsync(
        [FromQuery] int prjRid, [FromQuery] string? scope, [FromQuery] string? who)
        => Ok(ApiResponse<List<WbsBoardProgressUser>>.Ok(await progress.ByUserAsync(prjRid, scope, who)));

    [HttpGet("progress/by-module")]
    public async Task<IActionResult> ProgressByModuleAsync([FromQuery] int prjRid, [FromQuery] string? scope)
        => Ok(ApiResponse<List<WbsBoardProgressModule>>.Ok(await progress.ByModuleAsync(prjRid, scope)));

    [HttpGet("progress/rows")]
    public async Task<IActionResult> ProgressRowsAsync(
        [FromQuery] int prjRid, [FromQuery] string? scope,
        [FromQuery] string? user, [FromQuery] string? realUser, [FromQuery] string? module)
        => Ok(ApiResponse<List<WbsBoardProgressRow>>.Ok(
            await progress.RowsAsync(prjRid, scope, user, realUser, module)));

    // ──────────────────────────────────────────────────────── 지연

    [HttpGet("delay/summary")]
    public async Task<IActionResult> DelaySummaryAsync([FromQuery] int prjRid, [FromQuery] string? scope)
        => Ok(ApiResponse<WbsBoardDelay>.Ok(await delay.SummaryAsync(prjRid, scope)));

    [HttpGet("delay/by-user")]
    public async Task<IActionResult> DelayByUserAsync(
        [FromQuery] int prjRid, [FromQuery] string? scope, [FromQuery] string? who)
        => Ok(ApiResponse<List<WbsBoardDelayUser>>.Ok(await delay.ByUserAsync(prjRid, scope, who)));

    [HttpGet("delay/rows")]
    public async Task<IActionResult> DelayRowsAsync(
        [FromQuery] int prjRid, [FromQuery] string? scope, [FromQuery] string? kind,
        [FromQuery] string? user, [FromQuery] string? realUser)
        => Ok(ApiResponse<List<WbsBoardDelayRow>>.Ok(
            await delay.RowsAsync(prjRid, scope, kind, user, realUser)));
}
