using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>프로젝트 목록(<c>/api/projects</c>). 옛 <c>ProjController</c> 와 달리 프로시저 이름을 받는 범용 통로가 아니라 프로젝트 하나만 아는 평범한 REST 다.</summary>
[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService service) : ControllerBase
{
    /// <summary>프로젝트 목록. <c>?prjRid=</c> 를 주면 한 건만.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int? prjRid)
    {
        var rows = await service.ListAsync(prjRid);
        return Ok(ApiResponse<List<Project>>.Ok(rows));
    }

    /// <summary>새 프로젝트.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] Project item)
    {
        // 이름 없는 프로젝트는 목록에서 고를 수가 없다. 프로시저는 이것을
        // 막지 않아서 빈 줄이 실제로 몇 개 들어가 있다.
        if (string.IsNullOrWhiteSpace(item.PrjName))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "프로젝트명을 입력하세요."));
        }

        var created = await service.CreateAsync(item);
        return Ok(ApiResponse<Project>.Ok(created));
    }

    /// <summary>프로젝트를 고친다.</summary>
    [HttpPut("{prjRid:int}")]
    public async Task<IActionResult> UpdateAsync(int prjRid, [FromBody] Project item)
    {
        if (string.IsNullOrWhiteSpace(item.PrjName))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "프로젝트명을 입력하세요."));
        }

        var updated = await service.UpdateAsync(prjRid, item);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 프로젝트를 찾을 수 없습니다."))
            : Ok(ApiResponse<Project>.Ok(updated));
    }
}
