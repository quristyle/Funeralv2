using JSini.Shared.Internal;

using Microsoft.AspNetCore.Mvc;

using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 업무 비서(AI)가 쓰는 <b>내부 전용</b> 조회 — <c>/internal/*</c>. 게이트웨이를 거치지 않고
/// 서버 간 머리글(<see cref="InternalCallGuard"/>)만 본다. 길마다 첫 줄에서 그 자격을 본다.
/// </summary>
[ApiController]
[Route("internal")]
public sealed class InternalQueryController(InternalQueryService service, IConfiguration configuration)
    : ControllerBase
{
    private bool IsAllowed =>
        InternalCallGuard.IsAllowed(Request.Headers["X-Internal-Token"], configuration["Assistant:InternalToken"]);

    /// <summary>
    /// 프로젝트관리 일정(<c>projmng.dev_wbs</c>). 헬프데스크의 <c>/internal/schedules</c> 와
    /// 칸 이름을 맞춘다(<c>source, title, startDate, endDate, done, owner</c>) — 도구 하나가 둘을 합친다.
    /// 한 줄에 개발·빌드·검수 세 사람이 걸려 있어 <paramref name="userId"/> 는 셋 중 아무거나
    /// 맞으면 걸리고, 어느 역할로 걸렸는지는 <c>role</c> 칸에 따로 싣는다.
    /// </summary>
    [HttpGet("schedules")]
    public async Task<IActionResult> SchedulesAsync(
        [FromQuery] string? userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? keyword, [FromQuery] int? take, CancellationToken ct)
    {
        if (!IsAllowed) return NotFound();

        var takeCount = Math.Clamp(take ?? 30, 1, 100);

        var (total, rows) = await service.SchedulesAsync(userId, from, to, keyword, takeCount, ct);

        var items = rows.Select(r => new
        {
            source = "projmng",
            title = r.ProcNm,
            startDate = r.PlanSdt,
            endDate = r.PlanEdt,
            // 세 확인 칸(개발·빌드·검수) 중 하나라도 적혀 있으면 확인이 된 것으로 본다 — 이 표에는
            // 완료를 뜻하는 별도의 참/거짓 칸이 없다.
            done = !string.IsNullOrWhiteSpace(r.DevChk)
                   || !string.IsNullOrWhiteSpace(r.BuildChk)
                   || !string.IsNullOrWhiteSpace(r.QcChk),
            owner = r.DevUser,
            role = WbsRoleMatch.Of(
                dev: !string.IsNullOrEmpty(userId) && r.DevUser == userId,
                build: !string.IsNullOrEmpty(userId) && r.BuildUser == userId,
                qc: !string.IsNullOrEmpty(userId) && r.QcUser == userId),
        });

        return Ok(new { total, items });
    }

    /// <summary>할 일(<c>projmng.home_todo</c>). 사람은 <c>target_user</c> 하나뿐이라 역할 매칭이 없다.</summary>
    [HttpGet("todos")]
    public async Task<IActionResult> TodosAsync(
        [FromQuery] string? userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] bool? done, [FromQuery] int? take, CancellationToken ct)
    {
        if (!IsAllowed) return NotFound();

        var takeCount = Math.Clamp(take ?? 30, 1, 100);

        var (total, rows) = await service.TodosAsync(userId, from, to, done, takeCount, ct);

        var items = rows.Select(r => new
        {
            title = r.Title,
            targetDay = r.TargetDay,
            done = r.IsComplete,
            owner = r.TargetUser,
            comments = r.Comments,
        });

        return Ok(new { total, items });
    }
}
