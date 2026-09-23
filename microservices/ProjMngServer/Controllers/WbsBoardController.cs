using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>WBS 대시보드 — <c>/api/wbs-board</c>.</summary>
/// <remarks>
/// <para>
/// 이름이 <c>wbs-board</c> 인 까닭 — <c>/api/wbs</c> 는 이미 있다
/// (<see cref="WbsController"/>, 프로젝트별 공정표). <b>이름만 같고 다른
/// 물건</b>이라 한 경로에 얹으면 안 된다.
/// </para>
///
/// <para>
/// [모든 조회가 <c>prjRid</c> 를 받는다]
/// </para>
///
/// <para>
/// 원본 대시보드는 프로젝트 하나 전용이었다. 여기서는 <b>필수</b>다 —
/// 기본값을 두면 프로젝트를 안 고른 화면이 조용히 남의 숫자를 보여 준다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/wbs-board")]
public sealed class WbsBoardController(
    WbsBoardService board,
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
