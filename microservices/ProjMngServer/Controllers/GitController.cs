using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>Git 빌드 상태와 종합 모니터링 — <c>/api/git</c>.</summary>
/// <remarks>
/// <para>
/// 2026-09-23 에 GitLab 에서 GitHub 로 갈아탔다. 옛 구현(<c>/api/gitlab</c>)은
/// git 이력에 있다.
/// </para>
///
/// <para>
/// <b>저장소 목록이 비면 빈 채로 뜬다.</b> 500 이 아니라
/// <c>configured: false</c> 다 — 저장소를 안 건 프로젝트가 정상이다.
/// </para>
///
/// <para>
/// <b>토큰은 없어도 된다.</b> 공개 저장소는 그대로 읽히고, 토큰을 넣으면
/// 시간당 한도가 60회에서 5,000회로 바뀐다(<c>authenticated</c> 로 알린다).
/// </para>
/// </remarks>
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
