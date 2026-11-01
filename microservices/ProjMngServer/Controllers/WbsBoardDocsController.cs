using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>팀 공유 문서 — <c>/api/wbs-board/docs</c>. 지우기는 휴지통으로 보내고, 영영 지우기는 휴지통에서만 한다.</summary>
[ApiController]
[Route("api/wbs-board/docs")]
public sealed class WbsBoardDocsController(WbsDocsService service) : ControllerBase
{
    /// <summary>고친 사람. 게이트웨이가 붙여 주는 신원을 쓴다 — 본문에서 받지 않는다.</summary>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    private static IActionResult Missing() =>
        new NotFoundObjectResult(ApiResponse<object>.Fail("NOT_FOUND", "그 문서를 찾을 수 없습니다."));

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardDoc>>.Ok(await service.ListAsync(prjRid)));

    [HttpGet("trash")]
    public async Task<IActionResult> TrashListAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardDoc>>.Ok(await service.TrashAsync(prjRid)));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromQuery] int prjRid, [FromBody] WbsBoardDoc doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Title))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "제목을 입력하세요."));
        }

        return Ok(ApiResponse<int>.Ok(await service.CreateAsync(prjRid, doc, UserId)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(
        [FromQuery] int prjRid, int id, [FromBody] WbsBoardDoc doc)
        => await service.UpdateAsync(prjRid, id, doc, UserId) > 0
            ? Ok(ApiResponse<bool>.Ok(true))
            : Missing();

    /// <summary>목차 차례만 바꾼다 — 고친 때를 올리지 않는다. "order" 는 int 제약을 못 지나 {id:int} 와 안 겹친다.</summary>
    [HttpPut("order")]
    public async Task<IActionResult> ReorderAsync([FromQuery] int prjRid, [FromBody] List<int> ids)
    {
        if (ids is not { Count: > 0 })
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "차례가 비어 있습니다."));
        }

        return Ok(ApiResponse<int>.Ok(await service.ReorderAsync(prjRid, ids)));
    }

    /// <summary>purge 가 아니면 휴지통으로 보낸다. purge 는 휴지통에 있는 쪽만 받는다.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromQuery] int prjRid, int id, [FromQuery] bool purge = false)
        => (purge ? await service.PurgeAsync(prjRid, id) : await service.TrashAsync(prjRid, id, UserId))
            ? Ok(ApiResponse<bool>.Ok(true))
            : Missing();

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> RestoreAsync([FromQuery] int prjRid, int id)
        => await service.RestoreAsync(prjRid, id)
            ? Ok(ApiResponse<bool>.Ok(true))
            : Missing();

    // ── 붙인 문서 ──────────────────────────────────────────────

    [HttpGet("files")]
    public async Task<IActionResult> FilesAsync([FromQuery] int prjRid)
        => Ok(ApiResponse<List<WbsBoardDocFile>>.Ok(await service.FilesAsync(prjRid)));

    /// <summary>HTML 한 장을 올린다. 이름이 겹쳐도 덮지 않고 따로 선다 — 탭은 번호로 가른다.</summary>
    [HttpPost("{id:int}/files")]
    [RequestSizeLimit(WbsDocsService.MaxHtmlBytes * 2)]
    public async Task<IActionResult> UploadAsync([FromQuery] int prjRid, int id, [FromBody] WbsBoardDocUpload body)
    {
        var name = Path.GetFileName(body.Name ?? "").Trim();

        if (name.Length == 0 || !(name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "HTML 파일만 올릴 수 있습니다 (.html · .htm)."));
        }

        if (string.IsNullOrEmpty(body.Html))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "빈 파일입니다."));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(body.Html) > WbsDocsService.MaxHtmlBytes)
        {
            return BadRequest(ApiResponse<object>.Fail("TOO_LARGE", "8MB 를 넘습니다."));
        }

        if (name.Length > 200) name = name[^200..];

        var (file, error) = await service.AddFileAsync(prjRid, id, name, null, body.Html, UserId);

        return file is null
            ? BadRequest(ApiResponse<object>.Fail("INVALID", error ?? "올리지 못했습니다."))
            : Ok(ApiResponse<WbsBoardDocFile>.Ok(file));
    }

    /// <summary>주소를 건다. 받아다 중계하지 않는다 — 중계하면 그쪽 로그인이 끊기고 남의 화면을 우리 주소로 내보내게 된다.</summary>
    [HttpPost("{id:int}/links")]
    public async Task<IActionResult> LinkAsync([FromQuery] int prjRid, int id, [FromBody] WbsBoardDocLink body)
    {
        var raw = body.Url?.Trim() ?? "";

        if (raw.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "주소를 적어 주세요."));
        }

        // 스킴을 안 적는 일이 잦다 — 되묻지 않고 https 로 메운다.
        if (!raw.Contains("://")) raw = "https://" + raw;

        if (raw.Length > 2000
            || !Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "http · https 주소만 걸 수 있습니다."));
        }

        // 이름을 안 주면 주소에서 딴다. 조각(#/…)까지 넣는다 — 해시로 길을 나누는 사내 화면이 많아 빼면 같은 서버의 두 화면이 이름이 같아진다.
        var name = string.IsNullOrWhiteSpace(body.Name)
            ? uri.Host + (uri.AbsolutePath.Length > 1 ? uri.AbsolutePath : "") + uri.Fragment
            : body.Name.Trim();

        if (name.Length > 200) name = name[..200];

        var (file, error) = await service.AddFileAsync(prjRid, id, name, raw, null, UserId);

        return file is null
            ? BadRequest(ApiResponse<object>.Fail("INVALID", error ?? "걸지 못했습니다."))
            : Ok(ApiResponse<WbsBoardDocFile>.Ok(file));
    }

    /// <summary>올린 HTML 을 그대로 내준다. 셸이 중계하며 iframe 이 연다 — 막는 머리(sandbox)는 셸이 붙인다.</summary>
    [HttpGet("{id:int}/files/{fileId:int}/content")]
    public async Task<IActionResult> ContentAsync([FromQuery] int prjRid, int id, int fileId)
    {
        var body = await service.ContentAsync(prjRid, id, fileId);

        if (body is null) return NotFound();
        if (!string.IsNullOrEmpty(body.Url)) return Redirect(body.Url);

        return Content(body.Html ?? "", "text/html; charset=utf-8");
    }

    [HttpDelete("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> RemoveFileAsync([FromQuery] int prjRid, int id, int fileId)
        => await service.RemoveFileAsync(prjRid, id, fileId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : Missing();

    [HttpPut("{id:int}/files/order")]
    public async Task<IActionResult> ReorderFilesAsync([FromQuery] int prjRid, int id, [FromBody] List<int> ids)
        => Ok(ApiResponse<int>.Ok(await service.ReorderFilesAsync(prjRid, id, ids ?? [])));
}
