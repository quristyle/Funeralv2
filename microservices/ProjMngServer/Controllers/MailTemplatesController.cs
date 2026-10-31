using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>메일 HTML 템플릿 — <c>/api/mail-templates</c>.</summary>
/// 읽기는 로그인한 누구나(일정표 메일발송이 고른다), 쓰기는 <c>MailTemplate:ManageRoles</c> 역할만 —
/// 템플릿 하나가 여러 사람이 보내는 메일의 생김새를 통째로 바꾸기 때문이다.
[ApiController]
[Route("api/mail-templates")]
public sealed class MailTemplatesController(MailTemplateService service, IConfiguration configuration) : ControllerBase
{
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    private bool CanManage()
    {
        var allowed = configuration.GetSection("MailTemplate:ManageRoles").Get<string[]>()
                      ?? ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];

        // 게이트웨이는 역할 전부를 X-User-Roles 에, 첫 번째만 X-User-Role 에 싣는다.
        var roles = Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(Request.Headers["X-User-Role"].ToString());

        return roles.Any(r => allowed.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("메일 템플릿을 고칠 권한이 없습니다.", code: "E403"));

    private static string? Invalid(MailTemplate item) =>
        string.IsNullOrWhiteSpace(item.TplNm) ? "템플릿 이름을 입력하세요."
        : string.IsNullOrWhiteSpace(item.BodyHtml) ? "본문 HTML 을 입력하세요."
        : null;

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? kind, [FromQuery] bool usableOnly = false)
    {
        var rows = await service.ListAsync(kind, usableOnly);
        return Ok(ApiResponse<List<MailTemplate>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] MailTemplate item)
    {
        if (!CanManage()) return Forbidden();
        if (Invalid(item) is { } why) return BadRequest(ApiResponse<object>.Fail(why, "INVALID"));

        var created = await service.CreateAsync(item, UserId);
        return Ok(ApiResponse<MailTemplate>.Ok(created!));
    }

    [HttpPut("{tplId:long}")]
    public async Task<IActionResult> UpdateAsync(long tplId, [FromBody] MailTemplate item)
    {
        if (!CanManage()) return Forbidden();
        if (Invalid(item) is { } why) return BadRequest(ApiResponse<object>.Fail(why, "INVALID"));

        var updated = await service.UpdateAsync(tplId, item, UserId);

        return updated is null
            ? NotFound(ApiResponse<object>.Fail("그 템플릿을 찾을 수 없습니다.", "NOT_FOUND"))
            : Ok(ApiResponse<MailTemplate>.Ok(updated));
    }

    [HttpDelete("{tplId:long}")]
    public async Task<IActionResult> DeleteAsync(long tplId)
    {
        if (!CanManage()) return Forbidden();

        return await service.DeleteAsync(tplId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("그 템플릿을 찾을 수 없습니다.", "NOT_FOUND"));
    }
}
