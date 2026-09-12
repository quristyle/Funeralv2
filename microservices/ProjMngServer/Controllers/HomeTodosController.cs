using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>할 일 — <c>/api/home-todos</c>.</summary>
/// <remarks>
/// 등록·수정자는 <b>게이트웨이가 붙여 주는 신원</b>(<c>X-User-Id</c>)으로
/// 채운다. 옛 화면은 그 값을 본문에 실어 보냈는데, 위조해도 서버가 알 수
/// 없었다.
/// </remarks>
[ApiController]
[Route("api/home-todos")]
public sealed class HomeTodosController(HomeTodoService service) : ControllerBase
{
    /// <summary>요청을 보낸 사람. 없으면 <c>system</c>.</summary>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? targetUser, [FromQuery] string? todoState,
        [FromQuery] bool? isComplete, [FromQuery] DateOnly? targetDay)
    {
        var rows = await service.ListAsync(targetUser, todoState, isComplete, targetDay);
        return Ok(ApiResponse<List<HomeTodo>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] HomeTodo item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "제목을 입력하세요."));
        }

        var created = await service.CreateAsync(item, UserId);
        return Ok(ApiResponse<HomeTodo>.Ok(created!));
    }

    [HttpPut("{todoKey:long}")]
    public async Task<IActionResult> UpdateAsync(long todoKey, [FromBody] HomeTodo item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "제목을 입력하세요."));
        }

        var updated = await service.UpdateAsync(todoKey, item, UserId);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 할 일을 찾을 수 없습니다."))
            : Ok(ApiResponse<HomeTodo>.Ok(updated));
    }

    [HttpDelete("{todoKey:long}")]
    public async Task<IActionResult> DeleteAsync(long todoKey)
    {
        var removed = await service.DeleteAsync(todoKey);

        return removed
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 할 일을 찾을 수 없습니다."));
    }

    /// <summary>
    /// 그 날짜의 되풀이 할 일을 만든다. <b>대상을 받아서</b> 만든다 —
    /// 옛 프로시저는 사람 둘을 코드에 박아 두었다.
    /// </summary>
    [HttpPost("make")]
    public async Task<IActionResult> MakeAsync([FromBody] MakeTodoRequest request)
    {
        if (request.Users.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "대상을 고르세요."));
        }

        var made = await service.MakeAsync(request.TargetDay, request.Users);
        return Ok(ApiResponse<int>.Ok(made));
    }

    /// <summary>사람별 적립 금액.</summary>
    [HttpGet("pay")]
    public async Task<IActionResult> PayAsync([FromQuery] string? targetUser)
    {
        var rows = await service.PayAsync(targetUser);
        return Ok(ApiResponse<List<HomeTodoPay>>.Ok(rows));
    }
}

/// <summary>되풀이 할 일 만들기 요청.</summary>
public sealed class MakeTodoRequest
{
    public DateOnly TargetDay { get; set; }

    /// <summary>누구 몫을 만들지. <b>비어 있으면 아무것도 안 만든다.</b></summary>
    public List<string> Users { get; set; } = [];
}
