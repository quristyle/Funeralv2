using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 프로젝트관리 공통코드 — <c>/api/dev-common-codes</c>.
/// </summary>
/// <remarks>
/// 이름에 <c>dev-</c> 를 붙인 것은 <b>포털의 공통코드와 다른 표</b>라서다
/// (`scom` 쪽에 같은 이름의 것이 따로 있다). 경로만 보고 헷갈리지 않게 한다.
/// </remarks>
[ApiController]
[Route("api/dev-common-codes")]
public sealed class DevCommonCodesController(DevCommonCodeService service) : ControllerBase
{
    /// <summary>목록. <c>?groupsOnly=true</c> 면 묶음만, <c>?parentCode=</c> 면 그 묶음의 코드만.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] bool groupsOnly, [FromQuery] string? parentCode, [FromQuery] int? cmRid)
    {
        var rows = await service.ListAsync(groupsOnly, parentCode, cmRid);
        return Ok(ApiResponse<List<DevCommonCode>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] DevCommonCode item)
    {
        if (string.IsNullOrWhiteSpace(item.CmCd))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "코드값을 입력하세요."));
        }

        var created = await service.CreateAsync(item);
        return Ok(ApiResponse<DevCommonCode>.Ok(created));
    }

    [HttpPut("{cmRid:int}")]
    public async Task<IActionResult> UpdateAsync(int cmRid, [FromBody] DevCommonCode item)
    {
        if (string.IsNullOrWhiteSpace(item.CmCd))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "코드값을 입력하세요."));
        }

        var updated = await service.UpdateAsync(cmRid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 코드를 찾을 수 없습니다."))
            : Ok(ApiResponse<DevCommonCode>.Ok(updated));
    }

    /// <summary>
    /// 지운다. <b>딸린 코드가 있는 묶음은 막는다</b> — 표에 외래키가 없어
    /// DB 가 막아 주지 않고, 지우면 갈 곳 없는 코드만 남는다.
    /// </summary>
    [HttpDelete("{cmRid:int}")]
    public async Task<IActionResult> DeleteAsync(int cmRid, [FromQuery] string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var children = await service.CountChildrenAsync(code);

            if (children > 0)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "HAS_CHILDREN", $"이 묶음에 코드가 {children}건 남아 있습니다. 먼저 지우세요."));
            }
        }

        var removed = await service.DeleteAsync(cmRid);

        return removed
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 코드를 찾을 수 없습니다."));
    }
}
