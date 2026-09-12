using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 프로젝트 목록 — <c>/api/projects</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>옛 <c>ProjController</c> 와 다른 점</b> — 그쪽은 프로시저 이름을 몸에
/// 실어 보내면 무엇이든 돌려주는 <b>범용 통로</b>다. 여기는 프로젝트 하나만
/// 아는 평범한 REST 다.
/// </para>
///
/// <para>
/// 그래서 얻는 것 셋 —
/// </para>
///
/// <list type="number">
///   <item>
///     <b>봉투가 다른 서비스와 같아진다</b>(<c>ApiResponse</c>). 포털의
///     <c>GatewayClient</c> 가 그대로 벗겨 읽고, 화면은 <c>DataTable</c> 이
///     아니라 타입 있는 목록을 받는다 — <c>CommGrd</c> 가 요구하는 모양이다.
///   </item>
///   <item>
///     <b>무엇을 부를 수 있는지가 경로에 드러난다.</b> 범용 통로는 프로시저
///     이름만 맞으면 무엇이든 열리므로, 무엇이 쓰이는지 코드로 셀 수 없다.
///   </item>
///   <item>
///     <b>실패가 업무 문구로 온다.</b> 프로시저는 실패를 음수 코드 하나로
///     알려서 화면이 「빈 표」와 구분하지 못했다.
///   </item>
/// </list>
///
/// <para>
/// 게이트웨이가 <c>/api/projmng/**</c> 를 이 서비스로 넘기므로 포털이 부르는
/// 주소는 <c>auth</c> 계열과 같은 모양이 된다 — <c>projmng/projects</c>.
/// </para>
/// </remarks>
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
