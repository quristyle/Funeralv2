using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 현황 — <c>/api/ai-dashboard</c>.</summary>
/// 읽기뿐이다. 주소가 하나라 화면이 열릴 때 한 번에 받는다 — 조각마다 주소를 두면 늦은 조각이 대시보드를 조각조각 채운다.
[ApiController]
[Route("api/ai-dashboard")]
public sealed class AiDashboardController(AiDashboardService dashboard) : ControllerBase
{
    /// <summary>대시보드 한 판.</summary>
    /// <param name="from">시작일(포함). 비우면 30일 전.</param>
    /// <param name="to">종료일(포함). 비우면 오늘.</param>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(ApiResponse<AiDashboardData>.Ok(await dashboard.LoadAsync(from, to)));

    /// <summary>AI CLI 한도만. 대시보드 한 판을 부르지 않으려고 따로 둔다 — 고르개는 한도 표 하나면 충분하다.</summary>
    [HttpGet("usage")]
    public async Task<IActionResult> UsageAsync()
        => Ok(ApiResponse<List<AiUsageSnapshot>>.Ok(await dashboard.LoadUsageAsync()));
}
