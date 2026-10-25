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

    /// <summary>
    /// <b>내가 참여한</b> 프로젝트만. 누구인지는 게이트웨이가 실은 신원(X-User-Id)으로 안다 —
    /// 요청에서 아이디를 받으면 남의 프로젝트를 달라고 할 수 있다.
    /// </summary>
    /// 업무 비서 화면이 「고를 수 있는 프로젝트」를 채울 때 쓴다. 줄마다 매인 RAG
    /// 카테고리가 함께 오므로 화면이 두 번 묻지 않는다.
    [HttpGet("mine")]
    public async Task<IActionResult> MineAsync()
    {
        var userId = Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", "로그인이 필요합니다."));
        }

        var rows = await service.ListAsync(userId: userId);
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
