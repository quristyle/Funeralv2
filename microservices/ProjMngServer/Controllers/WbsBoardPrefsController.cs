using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>사용자별 화면 설정(<c>/api/wbs-board/prefs/{key}</c>). 주인은 게이트웨이가 붙여 주는 로그인 계정으로 가린다.</summary>
[ApiController]
[Route("api/wbs-board/prefs")]
public sealed class WbsBoardPrefsController(WbsBoardUserService service) : ControllerBase
{
    private string LoginId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    [HttpGet("{key}")]
    public async Task<IActionResult> GetAsync([FromQuery] int prjRid, string key)
    {
        if (!WbsBoardUserService.KeyOk(key))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "설정 이름이 올바르지 않습니다."));
        }

        var owner = WbsBoardUserService.ResolveOwner(LoginId);
        return Ok(ApiResponse<WbsBoardPref>.Ok(await service.GetPrefAsync(prjRid, owner, key)));
    }

    /// <summary>본문을 통째로 담는다. null 을 보내면 지운다. 값의 모양은 서버가 들여다보지 않는다 — 화면 설정이라 칸이 자주 바뀌므로 크기만 막는다.</summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> PutAsync(
        [FromQuery] int prjRid, string key, [FromBody] JsonElement body)
    {
        if (!WbsBoardUserService.KeyOk(key))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "설정 이름이 올바르지 않습니다."));
        }

        var value = body.ValueKind == JsonValueKind.Null ? null : body.GetRawText();

        if (value is { Length: > 20000 })
        {
            return BadRequest(ApiResponse<object>.Fail("TOO_LARGE", "설정 값이 너무 큽니다."));
        }

        var owner = WbsBoardUserService.ResolveOwner(LoginId);
        await service.SetPrefAsync(prjRid, owner, key, value);

        return Ok(ApiResponse<string>.Ok(owner));
    }
}
