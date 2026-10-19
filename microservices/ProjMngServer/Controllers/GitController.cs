using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>Git 빌드 상태와 종합 모니터링 — <c>/api/git</c>.</summary>
/// 저장소 목록이 비면 500 이 아니라 configured: false 로 빈 채로 뜬다 — 저장소를 안 건 프로젝트가 정상이다. 토큰은 없어도 되고, 있으면 시간당 한도가 60회에서 5,000회로 바뀐다(authenticated 로 알린다).
[ApiController]
[Route("api/git")]
public sealed class GitController(GitService git, GitMonitorService monitor) : ControllerBase
{
    /// <summary>저장소마다 가장 최근 Actions 실행 하나.</summary>
    [HttpGet("ci")]
    public async Task<IActionResult> CiAsync([FromQuery] int prjRid, [FromQuery] bool refresh = false)
        => Ok(ApiResponse<GitResult<GitRunRow>>.Ok(await git.StatusAsync(prjRid, refresh)));

    /// <summary>저장소마다 통계·가지·풀 리퀘스트·커밋·빌드 품질.</summary>
    [HttpGet("monitor")]
    public async Task<IActionResult> MonitorAsync([FromQuery] int prjRid, [FromQuery] bool refresh = false)
        => Ok(ApiResponse<GitResult<GitMonitorRow>>.Ok(await monitor.LoadAsync(prjRid, refresh)));
}
