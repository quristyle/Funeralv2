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
    /// 한 줄에 개발·빌드·검수 세 사람이 걸려 있고, 그 칸들은 로그인 아이디가 아니라 손으로 적은
    /// 이름이라(군더더기 포함) <paramref name="userId"/> 뿐 아니라 <paramref name="name"/> 으로도
    /// 찾을 수 있다 — 아이디·이름 중 아무거나, 세 칸 중 아무거나 맞으면 걸리고, 어느 역할로
    /// 걸렸는지는 <c>role</c> 칸에 따로 싣는다. <c>done</c> 은 화면(WbsService)과 같은 식 —
    /// <c>dev_edt</c>(개발 종료일) 유무 — 으로 셈한다.
    /// </summary>
    [HttpGet("schedules")]
    public async Task<IActionResult> SchedulesAsync(
        [FromQuery] string? userId, [FromQuery] string? name,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? keyword, [FromQuery] int? take, CancellationToken ct)
    {
        if (!IsAllowed) return NotFound();

        var takeCount = Math.Clamp(take ?? 30, 1, 100);

        var (total, rows) = await service.SchedulesAsync(userId, name, from, to, keyword, takeCount, ct);

        var items = rows.Select(r => new
        {
            source = "projmng",
            title = r.ProcNm,
            startDate = r.StartDate,
            endDate = r.EndDate,
            done = r.WbsState == "COMP",
            owner = r.DevUser,
            // READY · RUNNING · COMP — done 하나로는 "아직 시작 전"과 "진행 중"을 못 가른다.
            state = r.WbsState,
            role = WbsRoleMatch.Of(
                dev: WbsPersonMatch.Matches(r.DevUser, userId) || WbsPersonMatch.Matches(r.DevUser, name),
                build: WbsPersonMatch.Matches(r.BuildUser, userId) || WbsPersonMatch.Matches(r.BuildUser, name),
                qc: WbsPersonMatch.Matches(r.QcUser, userId) || WbsPersonMatch.Matches(r.QcUser, name)),
            // dev_chk · build_chk 는 참거짓이 아니라 글로 적은 메모라, comm 과 묶어 그대로 들려준다 —
            // 비서가 "왜 그런지"를 물으면 인용할 수 있는 유일한 자유 글이다.
            note = CombineNotes(r.DevChk, r.BuildChk, r.Comm),
        });

        return Ok(new { total, items });
    }

    /// <summary>개발·빌드 확인 메모와 비고를 사람이 읽을 한 덩이 글로 묶는다. 빈 칸은 건너뛴다.</summary>
    private static string? CombineNotes(string? devChk, string? buildChk, string? comm)
    {
        var parts = new[] { devChk, buildChk, comm }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" / ", parts);
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
