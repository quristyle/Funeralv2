using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 드롭다운 목록. 옛 <c>sp_projCommon</c> 을 대신한다.
/// </summary>
[ApiController]
[Route("api/proj-codes")]
public sealed class ProjCodesController(ProjCodeService service) : ControllerBase {

  /// <summary>부르는 사람. 게이트웨이가 붙여 준다.</summary>
  private string UserId => Request.Headers["X-User-Id"].ToString();

  /// <summary>한 갈래를 읽는다.</summary>
  /// <param name="codeId">코드 묶음 이름 또는 이름 있는 갈래</param>
  /// <param name="etc0">갈래 안에서 다시 좁히는 값</param>
  /// <param name="ct">취소 토큰</param>
  [HttpGet("{codeId}")]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>>> List(
      string codeId, [FromQuery] string? etc0, CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<Dictionary<string, object?>>>.Ok(
        await service.ListAsync(codeId, etc0, UserId, ct)));
}
