using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 대상 — <c>/api/ai-targets</c>.</summary>
/// <remarks>
/// <b>경로를 받는 유일한 자리다.</b> 작업 저장에는 <c>targetKey</c> 만 오고
/// 경로 문자열을 받는 곳이 없다 — 등록은 관리자가, 선택은 작성자가 한다.
/// 그래서 검사도 전부 여기서 한다(<see cref="AiTargetService.ValidatePath"/>).
/// </remarks>
[ApiController]
[Route("api/ai-targets")]
public sealed class AiTargetsController(AiTargetService service) : ControllerBase
{
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    /// <summary>
    /// 대상 목록. <paramref name="onlyEnabled"/> 를 켜면 고를 수 있는 것만 온다 —
    /// 작업 화면의 선택 목록이 그것을 쓴다.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool onlyEnabled = false)
    {
        var rows = await service.ListAsync(onlyEnabled);
        return Ok(ApiResponse<List<AiTarget>>.Ok(rows));
    }

    /// <summary>
    /// 경로를 어디 아래에 둘 수 있는지. <b>화면이 안내에 쓴다</b> —
    /// 규칙을 화면에 박아 두면 설정을 바꿨을 때 두 곳이 갈린다.
    /// </summary>
    [HttpGet("allowed-roots")]
    public IActionResult AllowedRoots()
        => Ok(ApiResponse<List<string>>.Ok([.. service.AllowedRoots]));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] AiTarget item)
    {
        if (Validate(item) is { } bad)
        {
            return bad;
        }

        var created = await service.CreateAsync(item, UserId);
        return Ok(ApiResponse<AiTarget>.Ok(created));
    }

    [HttpPut("{targetKey:long}")]
    public async Task<IActionResult> UpdateAsync(long targetKey, [FromBody] AiTarget item)
    {
        if (Validate(item) is { } bad)
        {
            return bad;
        }

        var updated = await service.UpdateAsync(targetKey, item, UserId);

        return updated is null
            ? NotFound(ApiResponse<AiTarget>.Fail(message: "그런 대상이 없습니다.", code: "NOT_FOUND"))
            : Ok(ApiResponse<AiTarget>.Ok(updated));
    }

    [HttpDelete("{targetKey:long}")]
    public async Task<IActionResult> DeleteAsync(long targetKey)
    {
        var done = await service.DeleteAsync(targetKey, UserId);

        return done
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<bool>.Fail(message: "그런 대상이 없습니다.", code: "NOT_FOUND"));
    }

    /// <summary>이름과 경로를 본다. 경로가 이 화면의 위험한 칸이다.</summary>
    private IActionResult? Validate(AiTarget item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetNm))
        {
            return BadRequest(ApiResponse<AiTarget>.Fail(message: "이름이 필요합니다.", code: "INVALID"));
        }

        if (service.ValidatePath(item.TargetPath) is { } reason)
        {
            return BadRequest(ApiResponse<AiTarget>.Fail(message: reason, code: "INVALID_PATH"));
        }

        return null;
    }
}
