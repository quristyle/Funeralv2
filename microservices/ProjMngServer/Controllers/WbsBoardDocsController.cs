using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>팀 공유 문서 — <c>/api/wbs-board/docs</c>.</summary>
[ApiController]
[Route("api/wbs-board/docs")]
public sealed class WbsBoardDocsController(WbsDocsService service) : ControllerBase
{
    /// <summary>고친 사람. 게이트웨이가 붙여 주는 신원을 쓴다 — 본문에서 받지 않는다.</summary>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardDoc>>.Ok(await service.ListAsync(prjRid)));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromQuery] int prjRid, [FromBody] WbsBoardDoc doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Title))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "제목을 입력하세요."));
        }

        return Ok(ApiResponse<int>.Ok(await service.CreateAsync(prjRid, doc, UserId)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(
        [FromQuery] int prjRid, int id, [FromBody] WbsBoardDoc doc)
        => await service.UpdateAsync(prjRid, id, doc, UserId) > 0
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 문서를 찾을 수 없습니다."));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromQuery] int prjRid, int id)
        => await service.DeleteAsync(prjRid, id)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 문서를 찾을 수 없습니다."));
}
