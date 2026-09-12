using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 프로젝트 DB 접속 — <c>/api/project-dbs</c>.
/// </summary>
/// <remarks>
/// <b>이 표가 망가지면 개발 도구 화면 전부가 접속을 못 찾는다.</b> 그래서
/// 삭제에 확인 절차가 화면 쪽에 있고, 비밀번호는 목록에 실어 보내지 않는다
/// (<see cref="ProjectDb"/> 머리말).
/// </remarks>
[ApiController]
[Route("api/project-dbs")]
public sealed class ProjectDbsController(ProjectDbService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int? prjRid)
    {
        var rows = await service.ListAsync(prjRid);
        return Ok(ApiResponse<List<ProjectDb>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectDb item)
    {
        if (string.IsNullOrWhiteSpace(item.DbNick))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "접속 이름을 입력하세요."));
        }

        var created = await service.CreateAsync(item);
        return Ok(ApiResponse<ProjectDb>.Ok(created!));
    }

    [HttpPut("{dbRid:int}")]
    public async Task<IActionResult> UpdateAsync(int dbRid, [FromBody] ProjectDb item)
    {
        if (string.IsNullOrWhiteSpace(item.DbNick))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "접속 이름을 입력하세요."));
        }

        var updated = await service.UpdateAsync(dbRid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 접속을 찾을 수 없습니다."))
            : Ok(ApiResponse<ProjectDb>.Ok(updated));
    }

    [HttpDelete("{dbRid:int}")]
    public async Task<IActionResult> DeleteAsync(int dbRid)
    {
        var removed = await service.DeleteAsync(dbRid);

        return removed
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 접속을 찾을 수 없습니다."));
    }

    // ── 접속에 딸린 속성 ──────────────────────────────────

    [HttpGet("{dbRid:int}/props")]
    public async Task<IActionResult> PropsAsync(int dbRid, [FromQuery] string? key)
    {
        var rows = await service.PropsAsync(dbRid, key);
        return Ok(ApiResponse<List<ProjectDbProp>>.Ok(rows));
    }

    [HttpPost("{dbRid:int}/props")]
    public async Task<IActionResult> CreatePropAsync(int dbRid, [FromBody] ProjectDbProp item)
    {
        // 임자는 경로가 정한다. 본문의 값은 믿지 않는다.
        item.DbRid = dbRid;

        if (string.IsNullOrWhiteSpace(item.DbPkey))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "속성 이름을 입력하세요."));
        }

        var created = await service.CreatePropAsync(item);
        return Ok(ApiResponse<ProjectDbProp>.Ok(created));
    }

    [HttpPut("{dbRid:int}/props/{dbPrid:int}")]
    public async Task<IActionResult> UpdatePropAsync(int dbRid, int dbPrid, [FromBody] ProjectDbProp item)
    {
        if (string.IsNullOrWhiteSpace(item.DbPkey))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "속성 이름을 입력하세요."));
        }

        var updated = await service.UpdatePropAsync(dbPrid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 속성을 찾을 수 없습니다."))
            : Ok(ApiResponse<ProjectDbProp>.Ok(updated));
    }

    [HttpDelete("{dbRid:int}/props/{dbPrid:int}")]
    public async Task<IActionResult> DeletePropAsync(int dbRid, int dbPrid)
    {
        var removed = await service.DeletePropAsync(dbPrid);

        return removed
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 속성을 찾을 수 없습니다."));
    }
}
