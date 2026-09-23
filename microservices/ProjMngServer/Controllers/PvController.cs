using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>ProjectView 캐시와 원장 반영 — <c>/api/pv</c>.</summary>
[ApiController]
[Route("api/pv")]
public sealed class PvController(PvCacheService cache, PvSyncService sync) : ControllerBase
{
    [HttpGet("cache")]
    public async Task<IActionResult> StatusAsync([FromQuery] int prjRid, [FromQuery] string? scope)
        => Ok(ApiResponse<PvCacheStatus>.Ok(await cache.StatusAsync(prjRid, scope)));

    [HttpGet("rows")]
    public async Task<IActionResult> RowsAsync([FromQuery] int prjRid, [FromQuery] string? scope)
        => Ok(ApiResponse<List<PvRow>>.Ok(await cache.RowsAsync(prjRid, scope)));

    [HttpGet("tasks")]
    public async Task<IActionResult> TasksAsync([FromQuery] int prjRid, [FromQuery] string? activityId)
        => Ok(ApiResponse<PvTaskBundle>.Ok(await cache.TasksAsync(prjRid, activityId)));

    /// <summary>
    /// 걷어 온 결과를 담는다. 본문은 수집 스크립트가 만든 그대로다 —
    /// <c>{ projectId, works: [{ code, id, …, tasks: [{ …, nodes: [] }] }] }</c>.
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> IngestAsync([FromQuery] int prjRid, [FromBody] JsonElement body)
    {
        try
        {
            return Ok(ApiResponse<PvIngestResult>.Ok(await cache.IngestAsync(prjRid, body)));
        }
        catch (ArgumentException e)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", e.Message));
        }
    }

    /// <param name="what"><c>all</c> · <c>works</c> · <c>tasks</c>.</param>
    [HttpDelete("cache")]
    public async Task<IActionResult> ClearAsync([FromQuery] int prjRid, [FromQuery] string? what)
        => Ok(ApiResponse<PvClearResult>.Ok(await cache.ClearAsync(prjRid, what)));

    // ──────────────────────────────────────────── 원장 반영

    /// <summary>무엇이 바뀌는지 먼저 보여 준다. <b>DB 는 건드리지 않는다.</b></summary>
    [HttpPost("sync/preview")]
    public async Task<IActionResult> PreviewAsync(
        [FromQuery] int prjRid, [FromBody] PvSyncRequest request)
        => Ok(ApiResponse<PvSyncPreview>.Ok(await sync.PreviewAsync(prjRid, request)));

    [HttpPost("sync/apply")]
    public async Task<IActionResult> ApplyAsync(
        [FromQuery] int prjRid, [FromBody] PvSyncRequest request)
        => Ok(ApiResponse<PvSyncApplied>.Ok(await sync.ApplyAsync(prjRid, request)));
}
