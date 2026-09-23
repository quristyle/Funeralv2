using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>EAI 인터페이스 카탈로그 — <c>/api/interfaces</c>.</summary>
/// <remarks>
/// <para>
/// 딸린 자료(단계·메모·항목값)에는 <c>prj_rid</c> 가 없다. 그래서 그것들을
/// 만지기 전에 <b>그 인터페이스가 이 프로젝트 것인지 먼저 묻는다</b> —
/// 빠뜨리면 번호만 알면 남의 프로젝트를 읽고 고칠 수 있다.
/// </para>
/// </remarks>
[ApiController]
[Route("api/interfaces")]
public sealed class InterfacesController(InterfaceService service) : ControllerBase
{
    private string Who =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    /// <summary>
    /// 본문 한 벌을 DB 에 넣을 수 있는 값으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WBS 쪽의 <see cref="WbsBoardSql.JsonValues"/> 를 쓰면 안 된다 —
    /// 그쪽은 객체·배열을 <b>버린다</b>(그 표에는 담을 자리가 없어서). 여기에는
    /// <c>ext</c>·<c>params</c> 두 <c>jsonb</c> 칸이 있어서 <b>글자로 굳혀
    /// 그대로 넘겨야 한다.</b>
    /// </para>
    ///
    /// <para>
    /// 그것을 모르고 한 번 밟았다 — 화면이 <c>{"ext":{"a":1}}</c> 를 보내면
    /// 값이 <c>null</c> 이 되고, 그 칸은 <c>NOT NULL</c> 이라 등록이 통째로
    /// <c>23502</c> 로 끊긴다.
    /// </para>
    /// </remarks>
    private static Dictionary<string, object?> Body(Dictionary<string, JsonElement> body)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, el) in body)
        {
            map[key] = el.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,

                // DB 의 참·거짓 칸은 'Y'/'N' 글자다.
                JsonValueKind.True => "Y",
                JsonValueKind.False => "N",

                // 숫자도 글자로 넘긴다 — 받는 쪽에 `::int` 가 붙어 있다.
                JsonValueKind.Number => el.GetRawText(),

                // jsonb 칸. 굳힌 글자에 `::jsonb` 가 붙는다.
                JsonValueKind.Object or JsonValueKind.Array => el.GetRawText(),

                _ => el.GetString(),
            };
        }

        return map;
    }

    private IActionResult NotOurs(int ifId)
        => NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"그 인터페이스를 찾을 수 없습니다: {ifId}"));

    // ──────────────────────────────────────────── 코드 · 시스템

    /// <summary>선택목록. <b>이것이 비면 화면의 드롭다운이 전부 빈다.</b></summary>
    [HttpGet("codes")]
    public async Task<IActionResult> CodesAsync([FromQuery] string? grp)
        => Ok(ApiResponse<List<IfCodeRow>>.Ok(await service.CodesAsync(grp)));

    [HttpGet("systems")]
    public async Task<IActionResult> SystemsAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<IfSystemRow>>.Ok(await service.SystemsAsync(prjRid)));

    // ──────────────────────────────────────────── 목록 · 상세

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int prjRid, [FromQuery] string? status, [FromQuery] string? domain,
        [FromQuery] string? q, [FromQuery] string? useYn)
        => Ok(ApiResponse<List<IfMasterRow>>.Ok(
            await service.ListAsync(prjRid, status, domain, q, useYn)));

    [HttpGet("{ifId:int}")]
    public async Task<IActionResult> DetailAsync([FromQuery] int prjRid, int ifId)
    {
        var detail = await service.DetailAsync(prjRid, ifId);
        return detail is null ? NotOurs(ifId) : Ok(ApiResponse<IfDetail>.Ok(detail));
    }

    // ──────────────────────────────────────────── 기본정보

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] int prjRid, [FromBody] Dictionary<string, JsonElement> body)
    {
        var values = Body(body);

        if (values.GetValueOrDefault("if_cd") is not string cd || string.IsNullOrWhiteSpace(cd)
            || values.GetValueOrDefault("if_nm") is not string nm || string.IsNullOrWhiteSpace(nm))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "코드와 이름은 필수입니다."));
        }

        var ifId = await service.CreateAsync(prjRid, values, Who);

        return ifId < 0
            ? BadRequest(ApiResponse<object>.Fail("DUPLICATE", $"이미 있는 인터페이스 코드입니다: {cd}"))
            : Ok(ApiResponse<int>.Ok(ifId));
    }

    [HttpPut("{ifId:int}")]
    public async Task<IActionResult> UpdateAsync(
        [FromQuery] int prjRid, int ifId, [FromBody] Dictionary<string, JsonElement> body)
    {
        var affected = await service.UpdateAsync(prjRid, ifId, Body(body), Who);

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "고칠 칸이 없습니다.")),
            0 => NotOurs(ifId),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    [HttpDelete("{ifId:int}")]
    public async Task<IActionResult> DeleteAsync([FromQuery] int prjRid, int ifId)
        => await service.DeleteAsync(prjRid, ifId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotOurs(ifId);

    // ──────────────────────────────────────────── 처리 단계

    [HttpGet("{ifId:int}/steps")]
    public async Task<IActionResult> StepsAsync([FromQuery] int prjRid, int ifId)
        => await service.OwnsAsync(prjRid, ifId)
            ? Ok(ApiResponse<List<IfStepRow>>.Ok(await service.StepsAsync(ifId)))
            : NotOurs(ifId);

    [HttpPost("{ifId:int}/steps")]
    public async Task<IActionResult> CreateStepAsync(
        [FromQuery] int prjRid, int ifId, [FromBody] Dictionary<string, JsonElement> body)
        => await service.OwnsAsync(prjRid, ifId)
            ? Ok(ApiResponse<int>.Ok(await service.CreateStepAsync(ifId, Body(body), Who)))
            : NotOurs(ifId);

    [HttpPut("{ifId:int}/steps/{stepId:int}")]
    public async Task<IActionResult> UpdateStepAsync(
        [FromQuery] int prjRid, int ifId, int stepId,
        [FromBody] Dictionary<string, JsonElement> body)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        var affected = await service.UpdateStepAsync(stepId, Body(body), Who);

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "고칠 칸이 없습니다.")),
            0 => NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 단계를 찾을 수 없습니다.")),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    [HttpDelete("{ifId:int}/steps/{stepId:int}")]
    public async Task<IActionResult> DeleteStepAsync([FromQuery] int prjRid, int ifId, int stepId)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        return await service.DeleteStepAsync(stepId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 단계를 찾을 수 없습니다."));
    }

    /// <summary>차례를 통째로 다시 매긴다. 본문은 단계 번호를 <b>바뀐 차례대로</b> 담은 배열이다.</summary>
    [HttpPut("{ifId:int}/steps/reorder")]
    public async Task<IActionResult> ReorderStepsAsync(
        [FromQuery] int prjRid, int ifId, [FromBody] List<int> order)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        if (order.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "차례가 비어 있습니다."));
        }

        return Ok(ApiResponse<int>.Ok(await service.ReorderStepsAsync(ifId, order)));
    }

    // ──────────────────────────────────────────── 메모 · 이슈

    [HttpGet("{ifId:int}/notes")]
    public async Task<IActionResult> NotesAsync([FromQuery] int prjRid, int ifId)
        => await service.OwnsAsync(prjRid, ifId)
            ? Ok(ApiResponse<List<IfNoteRow>>.Ok(await service.NotesAsync(ifId)))
            : NotOurs(ifId);

    [HttpPost("{ifId:int}/notes")]
    public async Task<IActionResult> CreateNoteAsync(
        [FromQuery] int prjRid, int ifId, [FromBody] Dictionary<string, JsonElement> body)
        => await service.OwnsAsync(prjRid, ifId)
            ? Ok(ApiResponse<int>.Ok(await service.CreateNoteAsync(ifId, Body(body), Who)))
            : NotOurs(ifId);

    [HttpPut("{ifId:int}/notes/{noteId:int}")]
    public async Task<IActionResult> UpdateNoteAsync(
        [FromQuery] int prjRid, int ifId, int noteId,
        [FromBody] Dictionary<string, JsonElement> body)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        var affected = await service.UpdateNoteAsync(noteId, Body(body), Who);

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "고칠 칸이 없습니다.")),
            0 => NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 메모를 찾을 수 없습니다.")),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    [HttpDelete("{ifId:int}/notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNoteAsync([FromQuery] int prjRid, int ifId, int noteId)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        return await service.DeleteNoteAsync(noteId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 메모를 찾을 수 없습니다."));
    }

    // ──────────────────────────────────────────── 추가 관리항목

    [HttpGet("{ifId:int}/attrs")]
    public async Task<IActionResult> AttrsAsync([FromQuery] int prjRid, int ifId)
        => await service.OwnsAsync(prjRid, ifId)
            ? Ok(ApiResponse<List<IfAttrRow>>.Ok(await service.AttrsAsync(ifId)))
            : NotOurs(ifId);

    /// <summary>
    /// 항목값 한꺼번에 담기. 본문은 <c>{ "12": "값", "13": null }</c> 꼴이고
    /// 열쇠가 항목 정의 번호다.
    /// </summary>
    [HttpPut("{ifId:int}/attrs")]
    public async Task<IActionResult> SaveAttrsAsync(
        [FromQuery] int prjRid, int ifId, [FromBody] Dictionary<string, JsonElement> body)
    {
        if (!await service.OwnsAsync(prjRid, ifId)) return NotOurs(ifId);

        var values = new Dictionary<int, string?>();

        foreach (var (key, el) in body)
        {
            // 열쇠가 번호가 아닌 것은 조용히 버린다. 화면이 잘못 보낸 것이지
            // 사람이 고칠 수 있는 일이 아니다.
            if (!int.TryParse(key, out var defId)) continue;

            values[defId] = el.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.True => "Y",
                JsonValueKind.False => "N",
                JsonValueKind.String => el.GetString(),
                _ => el.GetRawText(),
            };
        }

        return Ok(ApiResponse<int>.Ok(await service.SaveAttrsAsync(ifId, values, Who)));
    }
}
