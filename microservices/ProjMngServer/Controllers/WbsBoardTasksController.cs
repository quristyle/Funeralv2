using System.Text.Json;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>화면별 일감 — <c>/api/wbs-board/tasks</c>.</summary>
[ApiController]
[Route("api/wbs-board/tasks")]
public sealed class WbsBoardTasksController(WbsBoardTaskService service) : ControllerBase
{
    /// <summary>화면별 건수. 상세 목록이 줄마다 묻지 않고 한 번에 받는다.</summary>
    [HttpGet("counts")]
    public async Task<IActionResult> CountsAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardTaskCount>>.Ok(await service.CountsAsync(prjRid)));

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int prjRid, [FromQuery] string? activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "화면을 고르세요."));
        }

        return Ok(ApiResponse<List<WbsBoardTask>>.Ok(await service.ListAsync(prjRid, activityId)));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromQuery] int prjRid, [FromBody] WbsBoardTask item)
    {
        if (string.IsNullOrWhiteSpace(item.ActivityId))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "화면을 고르세요."));
        }

        // 외래키가 막아 주기는 하지만, 그때 나오는 것은 23503 이라 화면이
        // 사람에게 보여 줄 말이 못 된다.
        if (!await service.ParentExistsAsync(prjRid, item.ActivityId))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "NOT_FOUND", $"그 화면이 원장에 없습니다: {item.ActivityId}"));
        }

        var taskId = await service.CreateAsync(prjRid, item.ActivityId, item.TaskDiv, item.Memo);
        return Ok(ApiResponse<int>.Ok(taskId));
    }

    /// <summary>
    /// 담겨 온 칸만 고친다. 본문은 <c>taskDiv</c>·<c>memo</c>·<c>doneYn</c> 중
    /// <b>고칠 것만</b> 담는다 — 안 담긴 칸은 그대로 남는다.
    /// </summary>
    [HttpPut("{taskId:int}")]
    public async Task<IActionResult> UpdateAsync(
        [FromQuery] int prjRid, int taskId, [FromBody] Dictionary<string, JsonElement> patch)
    {
        // 화면은 낱말 첫 글자를 소문자로 보내고(taskDiv) DB 칸은 밑줄이다.
        var mapped = new Dictionary<string, object?>();
        foreach (var (key, value) in patch)
        {
            var col = key.ToLowerInvariant() switch
            {
                "taskdiv" or "task_div" => "task_div",
                "memo" => "memo",
                "doneyn" or "done_yn" => "done_yn",
                _ => null,
            };
            if (col is not null) mapped[col] = WbsBoardSql.JsonValue(value);
        }

        var affected = await service.UpdateAsync(prjRid, taskId, mapped);

        return affected switch
        {
            -1 => BadRequest(ApiResponse<object>.Fail("INVALID", "고칠 칸이 없습니다.")),
            0 => NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 일감을 찾을 수 없습니다.")),
            _ => Ok(ApiResponse<int>.Ok(affected)),
        };
    }

    [HttpDelete("{taskId:int}")]
    public async Task<IActionResult> DeleteAsync([FromQuery] int prjRid, int taskId)
        => await service.DeleteAsync(prjRid, taskId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 일감을 찾을 수 없습니다."));
}
