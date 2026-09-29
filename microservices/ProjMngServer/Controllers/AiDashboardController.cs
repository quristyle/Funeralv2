using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 현황 — <c>/api/ai-dashboard</c>.</summary>
/// <remarks>
/// <para>
/// 게이트웨이가 <c>/api/projmng</c> 를 떼고 <c>/api</c> 를 다시 붙인다
/// (<c>/api/projmng/ai-dashboard</c> → 여기 <c>/api/ai-dashboard</c>).
/// </para>
/// <para>
/// <b>읽기뿐이다.</b> 「AI 작업」·「빠른 지시」가 만든 자료를 세기만 한다 —
/// 이 경로로는 아무것도 바뀌지 않는다.
/// </para>
/// <para>
/// <b>주소가 하나다.</b> 조각마다 주소를 두면 화면이 열릴 때 여덟 번 왕복하고,
/// 그중 하나가 늦으면 대시보드가 조각조각 채워진다 — 이유는
/// <see cref="AiDashboardData"/> 머리말에 있다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai-dashboard")]
public sealed class AiDashboardController(AiDashboardService dashboard) : ControllerBase
{
    /// <summary>
    /// 대시보드 한 판.
    /// </summary>
    /// <param name="from">시작일(포함). 비우면 30일 전.</param>
    /// <param name="to">종료일(포함). 비우면 오늘.</param>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(ApiResponse<AiDashboardData>.Ok(await dashboard.LoadAsync(from, to)));

    /// <summary>
    /// AI CLI 한도만.
    /// </summary>
    /// <remarks>
    /// <b>위의 「주소가 하나다」에 대한 예외다.</b> 대시보드가 조각을 나누지
    /// 않는 것은 <i>한 화면</i>이 여러 번 왕복하지 않게 하려는 것인데, 이것을
    /// 쓰는 곳은 다른 화면이다 — 「빠른 지시」의 AI 고르개가 한도를 적으려고
    /// 부른다. 그쪽에 대시보드 한 판을 주면 <b>표 하나면 될 일에 30일치 집계가
    /// 따라붙는다.</b> 자료를 만드는 자리는 그대로 하나다
    /// (<c>AiDashboardService.UsageAsync</c>).
    /// </remarks>
    [HttpGet("usage")]
    public async Task<IActionResult> UsageAsync()
        => Ok(ApiResponse<List<AiUsageSnapshot>>.Ok(await dashboard.LoadUsageAsync()));
}
