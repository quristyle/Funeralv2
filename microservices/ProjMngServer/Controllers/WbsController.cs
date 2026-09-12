using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// WBS · 일정표. 옛 <c>sp_proj_wbs_exec</c> · <c>sp_proj_wbs_moniter</c> 를 대신한다.
/// </summary>
[ApiController]
[Route("api/wbs")]
public sealed class WbsController(WbsService service) : ControllerBase {

  /// <summary>등록·수정자로 남길 이름. 게이트웨이가 붙여 준다.</summary>
  private string UserId {
    get {
      var id = Request.Headers["X-User-Id"].ToString();
      return string.IsNullOrWhiteSpace(id) ? "system" : id;
    }
  }

  /// <summary>목록.</summary>
  [HttpGet]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<WbsItem>>>> List(
      [FromQuery] int? prjRid, [FromQuery] string? compStat, [FromQuery] string? scheduleType,
      [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<WbsItem>>.Ok(
        await service.ListAsync(prjRid, compStat, scheduleType, from, to, ct)));

  /// <summary>한 건.</summary>
  [HttpGet("{wbsId:int}")]
  public async Task<ActionResult<ApiResponse<WbsItem>>> Get(int wbsId, CancellationToken ct) {
    var item = await service.GetAsync(wbsId, ct);

    return item is null
      ? NotFound(ApiResponse<object>.Fail("없는 일감입니다.", "NOT_FOUND"))
      : Ok(ApiResponse<WbsItem>.Ok(item));
  }

  /// <summary>등록.</summary>
  [HttpPost]
  public async Task<ActionResult<ApiResponse<WbsItem>>> Create(
      [FromBody] WbsItem item, CancellationToken ct) {

    if (item.PrjRid is null) {
      return BadRequest(ApiResponse<object>.Fail("프로젝트를 고르세요.", "INVALID"));
    }

    return Ok(ApiResponse<WbsItem>.Ok(await service.CreateAsync(item, UserId, ct)));
  }

  /// <summary>수정.</summary>
  [HttpPut("{wbsId:int}")]
  public async Task<ActionResult<ApiResponse<WbsItem>>> Update(
      int wbsId, [FromBody] WbsItem item, CancellationToken ct) {

    item.WbsId = wbsId;

    var saved = await service.UpdateAsync(item, UserId, ct);

    return saved is null
      ? NotFound(ApiResponse<object>.Fail("없는 일감입니다.", "NOT_FOUND"))
      : Ok(ApiResponse<WbsItem>.Ok(saved));
  }

  /// <summary>삭제.</summary>
  [HttpDelete("{wbsId:int}")]
  public async Task<ActionResult<ApiResponse<bool>>> Delete(int wbsId, CancellationToken ct)
    => await service.DeleteAsync(wbsId, ct)
       ? Ok(ApiResponse<bool>.Ok(true))
       : NotFound(ApiResponse<object>.Fail("없는 일감입니다.", "NOT_FOUND"));

  /// <summary>진척 집계. 프로젝트 하나를 본다.</summary>
  [HttpGet("summary")]
  public async Task<ActionResult<ApiResponse<WbsSummary>>> Summary(
      [FromQuery] int prjRid, CancellationToken ct)
    => Ok(ApiResponse<WbsSummary>.Ok(await service.SummaryAsync(prjRid, ct)));
}
