using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>GitLab 빌드 상태와 종합 모니터링 — <c>/api/gitlab</c>.</summary>
/// <remarks>
/// <para>
/// <b>설정이 없으면 빈 채로 뜬다.</b> 500 이 아니라 <c>configured: false</c> 다 —
/// GitLab 을 안 쓰는 프로젝트가 정상이고, 그때 화면은 「설정이 없습니다」를
/// 보여 주면 된다.
/// </para>
///
/// <para>
/// <c>refresh=true</c> 만 캐시를 건너뛴다. 화면이 들어올 때마다 288 번씩
/// 부르면 GitLab 이 먼저 지친다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/gitlab")]
public sealed class GitlabController(
    GitlabService gitlab, GitlabMonitorService monitor) : ControllerBase
{
    /// <summary>저장소마다 가장 최근 Job 하나.</summary>
    [HttpGet("ci")]
    public async Task<IActionResult> CiAsync([FromQuery] int prjRid, [FromQuery] bool refresh = false)
        => Ok(ApiResponse<GitlabResult<GitlabJobRow>>.Ok(await gitlab.StatusAsync(prjRid, refresh)));

    /// <summary>저장소마다 통계·가지·병합요청·커밋·빌드 품질·레지스트리.</summary>
    [HttpGet("monitor")]
    public async Task<IActionResult> MonitorAsync([FromQuery] int prjRid, [FromQuery] bool refresh = false)
        => Ok(ApiResponse<GitlabResult<GitlabMonitorRow>>.Ok(await monitor.LoadAsync(prjRid, refresh)));
}
