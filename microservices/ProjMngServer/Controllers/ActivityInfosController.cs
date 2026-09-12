using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// Glue 서비스 정의. 옛 <c>sp_dev_activityinfo_exec</c> 를 대신한다.
/// </summary>
[ApiController]
[Route("api/activity-infos")]
public sealed class ActivityInfosController(ActivityInfoService service) : ControllerBase {

  /// <summary>목록. <b>소스로만 거른다.</b></summary>
  [HttpGet]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<ActivityInfoRow>>>> List(
      [FromQuery] string? srcRid, CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<ActivityInfoRow>>.Ok(await service.ListAsync(srcRid, ct)));

  /// <summary>한 줄을 넣거나 고친다.</summary>
  [HttpPost]
  public async Task<ActionResult<ApiResponse<int>>> Save(
      [FromBody] ActivityInfoRow item, CancellationToken ct) {

    if (string.IsNullOrWhiteSpace(item.ServiceName) || string.IsNullOrWhiteSpace(item.SrcRid)) {
      return BadRequest(ApiResponse<object>.Fail("서비스 이름과 소스가 있어야 합니다.", "INVALID"));
    }

    return Ok(ApiResponse<int>.Ok(await service.SaveAsync(item, ct)));
  }

  /// <summary>한 줄을 지운다.</summary>
  [HttpDelete]
  public async Task<ActionResult<ApiResponse<bool>>> Delete(
      [FromQuery] string srcRid, [FromQuery] string serviceName,
      [FromQuery] string transitionName, CancellationToken ct)
    => await service.DeleteAsync(srcRid, serviceName, transitionName, ct)
       ? Ok(ApiResponse<bool>.Ok(true))
       : NotFound(ApiResponse<object>.Fail("없는 줄입니다.", "NOT_FOUND"));
}
