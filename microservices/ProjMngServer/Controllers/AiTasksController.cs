using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 — <c>/api/ai-tasks</c>. 게이트웨이가 <c>/api/projmng</c> 를 떼고 <c>/api</c> 를 다시 붙인다.</summary>
[ApiController]
[Route("api/ai-tasks")]
public sealed class AiTasksController(
    AiTaskService service, AiRunService runs, AiTaskNoteService notes) : ControllerBase
{
    /// <summary>요청을 보낸 사람. 없으면 <c>system</c>.</summary>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    /// <summary>요청을 보낸 사람의 이름. 없으면 <c>null</c> — 그때는 아이디로 적는다. 이름은 한글이라 게이트웨이가 URL 인코딩해 보내므로 되돌려 읽는다.</summary>
    private string? UserName
    {
        get
        {
            var raw = Request.Headers["X-User-Name"].ToString();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try { return Uri.UnescapeDataString(raw); }
            catch (UriFormatException) { return raw; }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status, [FromQuery] string? flag,
        [FromQuery] long? targetKey, [FromQuery] string? keyword,
        [FromQuery] bool? userConfirmed, [FromQuery] bool? userRequest,
        [FromQuery] bool excludeDone = false)
    {
        var rows = await service.ListAsync(
            status, flag, targetKey, keyword,
            userConfirmed: userConfirmed, userRequest: userRequest,
            excludeDone: excludeDone);

        return Ok(ApiResponse<List<AiTask>>.Ok(rows));
    }

    // 일반 사용자 입구 — 게이트웨이는 신원만 보고 역할은 보지 않으므로 관리자용 경로와 입구를 갈라 둔다.

    /// <summary><b>내가 올린 요청 목록.</b> 남의 것은 한 건도 오지 않는다.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> MineAsync([FromQuery] string? keyword)
    {
        var rows = await service.ListAsync(keyword: keyword, creId: UserId);
        return Ok(ApiResponse<List<AiTask>>.Ok(rows));
    }

    /// <summary><b>지시를 적어 둔다.</b> 저장만 되고 아무 데서도 안 돈다 — 작업 대상과 AI 는 관리자가 나중에 채운다.</summary>
    [HttpPost("mine")]
    public async Task<IActionResult> CreateMineAsync([FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        var created = await service.CreateUserRequestAsync(item, UserId, UserName);
        return Ok(ApiResponse<AiTask>.Ok(created));
    }

    /// <summary><b>내가 올린 요청을 고친다.</b> 관리자가 손대기 전까지만.</summary>
    [HttpPut("mine/{taskKey:long}")]
    public async Task<IActionResult> UpdateMineAsync(long taskKey, [FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        return Respond(await service.UpdateUserRequestAsync(taskKey, item, UserId));
    }

    /// <summary><b>내가 올린 요청을 거둬들인다.</b> 관리자가 손대기 전까지만.</summary>
    [HttpDelete("mine/{taskKey:long}")]
    public async Task<IActionResult> DeleteMineAsync(long taskKey)
        => Respond(await service.DeleteUserRequestAsync(taskKey, UserId));

    // 남길말 입구가 둘이다 — mine/… 은 요청을 올린 사람인지를 서비스가 먼저 본 뒤 다룬다.

    /// <summary><b>내가 올린 요청에 붙은 말.</b> 남의 건은 한 줄도 오지 않는다.</summary>
    [HttpGet("mine/{taskKey:long}/notes")]
    public async Task<IActionResult> MineNotesAsync(long taskKey)
    {
        var rows = await notes.MineListAsync(taskKey, UserId);

        return rows is null
            ? NotFound(ApiResponse<List<AiTaskNote>>.Fail(message: "그런 요청이 없습니다.", code: "NOT_FOUND"))
            : Ok(ApiResponse<List<AiTaskNote>>.Ok(rows));
    }

    /// <summary><b>내가 올린 요청에 말을 남긴다.</b> 관리자에게 앱푸시가 간다.</summary>
    [HttpPost("mine/{taskKey:long}/notes")]
    public async Task<IActionResult> AddMineNoteAsync(long taskKey, [FromBody] AiTaskNote item)
        => RespondNote(await notes.MineAddAsync(taskKey, item.Contents, UserId, UserName));

    /// <summary>붙은 말을 전부 읽은 것으로 찍는다. 줄마다 따로 찍지 않는 까닭은 <c>AiTaskNoteService.MineMarkReadAsync</c> 머리말에 있다.</summary>
    [HttpPost("mine/{taskKey:long}/notes/read")]
    public async Task<IActionResult> ReadMineNotesAsync(long taskKey)
    {
        var marked = await notes.MineMarkReadAsync(taskKey, UserId);

        return marked is null
            ? NotFound(ApiResponse<int>.Fail(message: "그런 요청이 없습니다.", code: "NOT_FOUND"))
            : Ok(ApiResponse<int>.Ok(marked.Value));
    }

    /// <summary>한 건에 붙은 말 전부 — <b>관리자 쪽 입구</b>.</summary>
    [HttpGet("{taskKey:long}/notes")]
    public async Task<IActionResult> NotesAsync(long taskKey)
        => Ok(ApiResponse<List<AiTaskNote>>.Ok(await notes.ListAsync(taskKey)));

    /// <summary><b>말을 남긴다 — 관리자 쪽 입구.</b> 올린 사람에게 앱푸시가 간다.</summary>
    [HttpPost("{taskKey:long}/notes")]
    public async Task<IActionResult> AddNoteAsync(long taskKey, [FromBody] AiTaskNote item)
        => RespondNote(await notes.AddAsync(taskKey, item.Contents, UserId, UserName));

    [HttpGet("{taskKey:long}")]
    public async Task<IActionResult> GetAsync(long taskKey)
    {
        var item = await service.GetAsync(taskKey);

        return item is null
            ? NotFound(ApiResponse<AiTask>.Fail(message: "그런 작업이 없습니다.", code: "NOT_FOUND"))
            : Ok(ApiResponse<AiTask>.Ok(item));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        var created = await service.CreateAsync(item, UserId);
        return Ok(ApiResponse<AiTask>.Ok(created));
    }

    [HttpPut("{taskKey:long}")]
    public async Task<IActionResult> UpdateAsync(long taskKey, [FromBody] AiTask item)
    {
        if (string.IsNullOrWhiteSpace(item.Contents))
        {
            return BadRequest(ApiResponse<AiTask>.Fail(message: "내용이 필요합니다.", code: "INVALID"));
        }

        return Respond(await service.UpdateAsync(taskKey, item, UserId));
    }

    /// <summary>실행 이력. 최근 것이 앞이다.</summary>
    [HttpGet("{taskKey:long}/runs")]
    public async Task<IActionResult> RunsAsync(long taskKey)
        => Ok(ApiResponse<List<AiTaskRun>>.Ok(await runs.RunsAsync(taskKey)));

    /// <summary><b>작업을 요청한다.</b> 요청여부가 <c>requested</c>, 상태가 <c>queued</c> 가 된다.</summary>
    [HttpPost("{taskKey:long}/request")]
    public async Task<IActionResult> RequestAsync(long taskKey)
        => Respond(await service.RequestAsync(taskKey, UserId));

    /// <summary><b>끝난 작업에 이어서 지시한다.</b> 본문이 「지난 진행 + 이번에 할 일」로 바뀐다. <b>이 회차를 맡을 AI 도 갈아탈 수 있다</b>(<c>runnerKind</c>).</summary>
    [HttpPost("{taskKey:long}/continue")]
    public async Task<IActionResult> ContinueAsync(long taskKey, [FromBody] ContinueRequest req)
        => Respond(await service.ContinueAsync(taskKey, req.Addition, req.RunnerKind, UserId));

    /// <summary><b>진행 중인 작업에 대해 완료 후 이어서 지시할 내용을 예약/저장한다.</b></summary>
    [HttpPost("{taskKey:long}/pending-continue")]
    public async Task<IActionResult> SetPendingContinueAsync(long taskKey, [FromBody] PendingContinueRequest req)
        => Respond(await service.SetPendingContinueAsync(taskKey, req.Addition, req.RunnerKind, UserId));

    /// <summary><b>예약된 이어서 지시를 취소한다.</b></summary>
    [HttpDelete("{taskKey:long}/pending-continue")]
    public async Task<IActionResult> ClearPendingContinueAsync(long taskKey)
        => Respond(await service.SetPendingContinueAsync(taskKey, null, null, UserId));

    /// <summary><b>실패한 작업을 수동으로 다시 요청한다.</b> 추가 지시사항을 얹을 수 있다.</summary>
    [HttpPost("{taskKey:long}/retry")]
    public async Task<IActionResult> RetryAsync(long taskKey, [FromBody] RetryRequest req)
        => Respond(await service.RetryAsync(taskKey, req?.Addition, req?.Kind, UserId));

    /// <summary><b>사용자 확인 완료</b> 처리한다.</summary>
    [HttpPost("{taskKey:long}/confirm")]
    public async Task<IActionResult> ConfirmAsync(long taskKey)
        => Respond(await service.ConfirmAsync(taskKey, UserId));

    /// <summary>취소를 요청한다. 도는 중이면 표시만 남고 실행기가 멈춘다.</summary>
    [HttpPost("{taskKey:long}/cancel")]
    public async Task<IActionResult> CancelAsync(long taskKey)
        => Respond(await service.CancelAsync(taskKey, UserId));

    [HttpDelete("{taskKey:long}")]
    public async Task<IActionResult> DeleteAsync(long taskKey)
        => Respond(await service.DeleteAsync(taskKey, UserId));

    /// <summary>결과를 HTTP 로 옮긴다. 「없다」와 「지금은 안 된다」를 가른다 — 같은 코드로 내리면 도는 중인 작업이 「없는 작업」으로 보인다.</summary>
    public sealed class ContinueRequest
    {
        /// <summary>이번에 더 시킬 일.</summary>
        public string? Addition { get; set; }

        /// <summary>이 회차를 맡을 AI. <b>비우면 지난 회차의 실행기를 그대로 쓴다.</b></summary>
        public string? RunnerKind { get; set; }
    }

    public sealed class PendingContinueRequest
    {
        /// <summary>작업 완료 후 이어서 시킬 일.</summary>
        public string? Addition { get; set; }

        /// <summary>이어서 시킬 때 맡길 AI.</summary>
        public string? RunnerKind { get; set; }
    }

    public sealed class RetryRequest
    {
        /// <summary>재시도 시 더 시킬 일.</summary>
        public string? Addition { get; set; }
        
        /// <summary>재시도에 사용할 AI 모델 (선택).</summary>
        public string? Kind { get; set; }
    }

    private IActionResult Respond(AiTaskEditResult result)
    {
        if (!result.Found)
        {
            return NotFound(ApiResponse<AiTask>.Fail(message: "그런 작업이 없습니다.", code: "NOT_FOUND"));
        }

        if (result.ConflictMessage is { } message)
        {
            return Conflict(ApiResponse<AiTask>.Fail(message: message, code: "CONFLICT"));
        }

        return Ok(ApiResponse<AiTask>.Ok(result.Item));
    }

    /// <summary>남길말 저장의 답. <see cref="Respond"/> 와 <b>같은 세 갈래</b>다 — 없는 번호는 404, 적을 수 없는 사정은 409.</summary>
    private IActionResult RespondNote(AiTaskNoteResult result)
    {
        if (!result.Found)
        {
            return NotFound(ApiResponse<AiTaskNote>.Fail(message: "그런 요청이 없습니다.", code: "NOT_FOUND"));
        }

        if (result.ConflictMessage is { } message)
        {
            return Conflict(ApiResponse<AiTaskNote>.Fail(message: message, code: "CONFLICT"));
        }

        return Ok(ApiResponse<AiTaskNote>.Ok(result.Item));
    }
}
