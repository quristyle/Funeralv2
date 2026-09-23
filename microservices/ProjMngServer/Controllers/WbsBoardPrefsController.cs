using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 사용자별 화면 설정 — <c>/api/wbs-board/prefs/{key}</c>.
/// </summary>
/// <remarks>
/// 상세 목록의 「보이는 칸 · 차례」처럼 <b>사람마다 다른 것</b>을 담는다.
/// 주인은 게이트웨이가 붙여 주는 로그인 계정으로 가린다
/// (<see cref="WbsBoardUserService.ResolveOwnerAsync"/>).
/// </remarks>
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

        var owner = await service.ResolveOwnerAsync(prjRid, LoginId);
        return Ok(ApiResponse<WbsBoardPref>.Ok(await service.GetPrefAsync(prjRid, owner, key)));
    }

    /// <summary>
    /// 본문을 <b>통째로</b> 담는다. <c>null</c> 을 보내면 지운다.
    /// </summary>
    /// <remarks>
    /// 값의 모양을 서버가 들여다보지 않는다 — 화면 설정이라 칸이 자주 바뀌고,
    /// 서버가 형태를 알면 화면을 고칠 때마다 서버도 고쳐야 한다. 대신
    /// <b>크기만 막는다</b> — 몇백 바이트면 되는 자리다.
    /// </remarks>
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

        var owner = await service.ResolveOwnerAsync(prjRid, LoginId);
        await service.SetPrefAsync(prjRid, owner, key, value);

        return Ok(ApiResponse<string>.Ok(owner));
    }
}
