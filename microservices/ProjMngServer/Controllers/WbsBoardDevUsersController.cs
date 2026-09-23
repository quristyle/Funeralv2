using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>개발자 명부 — <c>/api/wbs-board/dev-users</c>.</summary>
/// <remarks>
/// 경로가 <c>users</c> 가 아닌 까닭 — <c>/api/wbs-board/users</c> 는 이미
/// <b>담당자 고르개</b>다(집계에 실제로 나오는 사람만 준다). 명부는 그보다
/// 넓다(아직 아무것도 배정 안 된 사람도 있다).
/// </remarks>
[ApiController]
[Route("api/wbs-board/dev-users")]
public sealed class WbsBoardDevUsersController(WbsBoardUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardUser>>.Ok(await service.ListAsync(prjRid)));

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] int prjRid, [FromBody] Dictionary<string, JsonElement> body)
    {
        var bpId = body.TryGetValue("bpId", out var a) || body.TryGetValue("bp_id", out a)
            ? a.ValueKind == JsonValueKind.String ? a.GetString()?.Trim() : null
            : null;

        if (string.IsNullOrWhiteSpace(bpId))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "사번을 입력하세요."));
        }

        var inserted = await service.CreateAsync(prjRid, bpId, Columns(body));

        return inserted > 0
            ? Ok(ApiResponse<string>.Ok(bpId))
            : BadRequest(ApiResponse<object>.Fail("DUPLICATE", $"이미 있는 사번입니다: {bpId}"));
    }

    [HttpPut("{bpId}")]
    public async Task<IActionResult> UpdateAsync(
        [FromQuery] int prjRid, string bpId, [FromBody] Dictionary<string, JsonElement> body)
    {
        var affected = await service.UpdateAsync(prjRid, bpId, Columns(body));

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "고칠 칸이 없습니다.")),
            0 => NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"그 사람을 찾을 수 없습니다: {bpId}")),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    [HttpDelete("{bpId}")]
    public async Task<IActionResult> DeleteAsync([FromQuery] int prjRid, string bpId)
        => await service.DeleteAsync(prjRid, bpId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"그 사람을 찾을 수 없습니다: {bpId}"));

    /// <summary>
    /// 본문의 이름을 DB 칸 이름으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// 화면은 <c>notebookChkNo</c> 처럼 보내고 칸은 <c>notebook_chk_no</c> 다.
    /// <b>화이트리스트는 서비스가 다시 본다</b> — 여기서 이름만 맞춘다고
    /// 아무 칸이나 열리지는 않는다.
    /// </remarks>
    private static Dictionary<string, object?> Columns(Dictionary<string, JsonElement> body)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, el) in body)
        {
            if (key.Equals("bpId", StringComparison.OrdinalIgnoreCase)) continue;
            if (key.Equals("bp_id", StringComparison.OrdinalIgnoreCase)) continue;

            map[Snake(key)] = WbsBoardSql.JsonValue(el);
        }

        return map;
    }

    /// <summary><c>notebookChkNo</c> → <c>notebook_chk_no</c>. 이미 밑줄이면 그대로.</summary>
    private static string Snake(string name)
    {
        if (name.Contains('_')) return name.ToLowerInvariant();

        var sb = new System.Text.StringBuilder(name.Length + 8);
        foreach (var c in name)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
