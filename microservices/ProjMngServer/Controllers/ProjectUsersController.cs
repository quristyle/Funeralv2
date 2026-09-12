using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 프로젝트 참여자 — <c>/api/project-users</c>.
/// </summary>
/// <remarks>
/// 갈래가 둘인 것은 <b>보는 각도가 둘</b>이기 때문이다 —
/// 「이 사람이 어느 프로젝트에」(<see cref="AssignmentsAsync"/>)와
/// 「걸려 있는 짝 전부」(<see cref="ListAsync"/>). 옛 프로시저도 둘이었다.
/// </remarks>
[ApiController]
[Route("api/project-users")]
public sealed class ProjectUsersController(ProjectUserService service) : ControllerBase
{
    /// <summary>프로젝트 전부에 이 사람의 참여 여부를 붙여 돌려준다.</summary>
    [HttpGet("assignments")]
    public async Task<IActionResult> AssignmentsAsync([FromQuery] string? userId, [FromQuery] int? prjRid)
    {
        var rows = await service.AssignmentsAsync(userId, prjRid);
        return Ok(ApiResponse<List<ProjectAssignment>>.Ok(rows));
    }

    /// <summary>
    /// 참여를 켜고 끈다. <b>한 건이든 여러 건이든 이 길 하나다.</b> 한 트랜잭션이다.
    /// </summary>
    /// <remarks>
    /// 한 줄짜리 <c>POST assignments</c> 가 따로 있었는데 없앴다. 체크 하나가
    /// 원소 하나짜리 묶음이라 여기서 똑같이 처리되고, 갈래를 둘로 두면
    /// <b>한쪽에만 걸리는 버그</b>가 생긴다 — 실제로 넣기가 한쪽은
    /// <c>WHERE NOT EXISTS</c>, 다른 쪽은 <c>ON CONFLICT</c> 로 갈릴 뻔했다.
    /// </remarks>
    [HttpPost("assignments/bulk")]
    public async Task<IActionResult> SetAssignmentsAsync([FromBody] ProjectAssignmentBulkRequest request)
    {
        var byProject = request.PrjRid is not null;
        var byPerson = !string.IsNullOrWhiteSpace(request.UserId);

        // **둘 다이거나 둘 다 아닌 것을 거절한다.** 둘 다 주면 `Add` 에 담긴
        // 것이 아이디인지 프로젝트 번호인지 알 수 없고, 둘 다 비우면 어디에
        // 넣으라는 것인지 알 수 없다. 조용히 한쪽을 고르면 **엉뚱한 짝이
        // 들어가고도 200 이 나간다.**
        if (byProject == byPerson)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID", "기준은 프로젝트나 사람 중 하나여야 합니다."));
        }

        var (added, removed) = await service.SetAssignmentsAsync(
            request.PrjRid, request.UserId, request.Add, request.Remove);

        return Ok(ApiResponse<ProjectAssignmentBulkResult>.Ok(
            new ProjectAssignmentBulkResult { Added = added, Removed = removed }));
    }

    /// <summary>걸려 있는 사람-프로젝트 짝.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int? prjRid, [FromQuery] string? userId)
    {
        var rows = await service.ListAsync(prjRid, userId);
        return Ok(ApiResponse<List<ProjectUserRow>>.Ok(rows));
    }
}
