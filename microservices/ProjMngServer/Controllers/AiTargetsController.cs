using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>AI 작업 대상 — <c>/api/ai-targets</c>.</summary>
/// 경로를 받는 유일한 자리다. 작업 저장에는 targetKey 만 오고, 검사도 전부 여기서 한다(AiTargetService.ValidatePath).
[ApiController]
[Route("api/ai-targets")]
public sealed class AiTargetsController(
    AiTargetService service, AiTargetStatusService status) : ControllerBase
{
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    /// <summary>대상 목록. onlyEnabled 를 켜면 고를 수 있는 것만 온다.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool onlyEnabled = false)
    {
        var rows = await service.ListAsync(onlyEnabled);
        return Ok(ApiResponse<List<AiTarget>>.Ok(rows));
    }

    /// <summary>경로를 어디 아래에 둘 수 있는지. 화면이 안내에 쓰므로 규칙을 화면에 박지 않는다.</summary>
    [HttpGet("allowed-roots")]
    public IActionResult AllowedRoots()
        => Ok(ApiResponse<List<string>>.Ok([.. service.AllowedRoots]));

    /// <summary>대상마다의 git 상태. 여기서 git 을 부르지 않고 실행기가 적어 둔 스냅샷을 준다.</summary>
    /// 줄마다 probedAt 이 붙으므로 화면은 그 시각을 함께 보여 줘야 한다.
    [HttpGet("status")]
    public async Task<IActionResult> StatusAsync([FromQuery] bool onlyEnabled = false)
    {
        var rows = await status.ListAsync(onlyEnabled);
        return Ok(ApiResponse<List<AiTargetStatusRow>>.Ok(rows));
    }

    /// <summary>「지금 확인」. 표시만 남긴다 — 보는 것은 실행기다.</summary>
    /// 성공은 「봤다」가 아니라 「보라고 적어 두었다」는 뜻이라 화면이 그렇게 말해야 한다.
    [HttpPost("{targetKey:long}/probe")]
    public async Task<IActionResult> ProbeAsync(long targetKey)
    {
        var done = await status.RequestProbeAsync(targetKey);

        return done
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<bool>.Fail(message: "그런 대상이 없습니다.", code: "NOT_FOUND"));
    }

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
