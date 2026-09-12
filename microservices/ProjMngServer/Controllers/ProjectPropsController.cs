using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 프로젝트 속성. 옛 <c>sp_dev_proj_prop_exec</c> 를 대신한다.
/// </summary>
[ApiController]
[Route("api/project-props")]
public sealed class ProjectPropsController(ProjectPropService service) : ControllerBase {

  /// <summary>목록.</summary>
  [HttpGet]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectProp>>>> List(
      [FromQuery] string? prjRid, [FromQuery] string? propCd, [FromQuery] string? propType,
      CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<ProjectProp>>.Ok(
        await service.ListAsync(prjRid, propCd, propType, ct)));

  /// <summary>넣거나 고친다.</summary>
  [HttpPost]
  public async Task<ActionResult<ApiResponse<ProjectProp>>> Save(
      [FromBody] ProjectProp item, CancellationToken ct) {

    if (string.IsNullOrWhiteSpace(item.PrjRid) || string.IsNullOrWhiteSpace(item.PropCd)) {
      return BadRequest(ApiResponse<object>.Fail("프로젝트와 이름이 있어야 합니다.", "INVALID"));
    }

    return Ok(ApiResponse<ProjectProp>.Ok(await service.SaveAsync(item, ct)));
  }

  /// <summary>지운다.</summary>
  [HttpDelete]
  public async Task<ActionResult<ApiResponse<bool>>> Delete(
      [FromQuery] string prjRid, [FromQuery] string propCd, [FromQuery] string propType,
      CancellationToken ct)
    => await service.DeleteAsync(prjRid, propCd, propType, ct)
       ? Ok(ApiResponse<bool>.Ok(true))
       : NotFound(ApiResponse<object>.Fail("없는 속성입니다.", "NOT_FOUND"));
}
