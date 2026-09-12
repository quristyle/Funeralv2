using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 소스 정보 — <c>/api/source-infos</c> 와 그 상세.
/// </summary>
/// <remarks>
/// 상세를 <c>/{srcRid}/details</c> 아래에 둔 것은 <b>상세가 홀로 서지
/// 않기 때문</b>이다 — 늘 어느 소스의 상세다. 경로가 그 관계를 말한다.
/// </remarks>
[ApiController]
[Route("api/source-infos")]
public sealed class SourceInfosController(SourceInfoService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int? prjRid, [FromQuery] int? srcRid)
    {
        var rows = await service.ListAsync(prjRid, srcRid);
        return Ok(ApiResponse<List<SourceInfo>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] SourceInfo item)
    {
        if (string.IsNullOrWhiteSpace(item.SrcPath))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "소스 경로를 입력하세요."));
        }

        var created = await service.CreateAsync(item);
        return Ok(ApiResponse<SourceInfo>.Ok(created!));
    }

    [HttpPut("{srcRid:int}")]
    public async Task<IActionResult> UpdateAsync(int srcRid, [FromBody] SourceInfo item)
    {
        if (string.IsNullOrWhiteSpace(item.SrcPath))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "소스 경로를 입력하세요."));
        }

        var updated = await service.UpdateAsync(srcRid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 소스를 찾을 수 없습니다."))
            : Ok(ApiResponse<SourceInfo>.Ok(updated));
    }

    // ── 상세 ──────────────────────────────────────────────

    [HttpGet("{srcRid:int}/details")]
    public async Task<IActionResult> DetailsAsync(int srcRid)
    {
        var rows = await service.DetailsAsync(srcRid);
        return Ok(ApiResponse<List<SourceInfoDetail>>.Ok(rows));
    }

    [HttpPost("{srcRid:int}/details")]
    public async Task<IActionResult> CreateDetailAsync(int srcRid, [FromBody] SourceInfoDetail item)
    {
        // 경로가 임자를 정한다. 본문의 값은 믿지 않는다 —
        // 다른 소스의 번호가 실려 오면 남의 소스에 줄이 생긴다.
        item.SrcRid = srcRid;

        var created = await service.CreateDetailAsync(item);
        return Ok(ApiResponse<SourceInfoDetail>.Ok(created));
    }

    [HttpPut("{srcRid:int}/details/{srcDtlRid:int}")]
    public async Task<IActionResult> UpdateDetailAsync(int srcRid, int srcDtlRid, [FromBody] SourceInfoDetail item)
    {
        var updated = await service.UpdateDetailAsync(srcDtlRid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 상세를 찾을 수 없습니다."))
            : Ok(ApiResponse<SourceInfoDetail>.Ok(updated));
    }

    [HttpDelete("{srcRid:int}/details/{srcDtlRid:int}")]
    public async Task<IActionResult> DeleteDetailAsync(int srcRid, int srcDtlRid)
    {
        var removed = await service.DeleteDetailAsync(srcDtlRid);

        return removed
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 상세를 찾을 수 없습니다."));
    }
}
