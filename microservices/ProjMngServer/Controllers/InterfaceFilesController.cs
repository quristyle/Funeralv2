using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>인터페이스 첨부 — <c>/api/interfaces/files</c>.</summary>
[ApiController]
[Route("api/interfaces")]
public sealed class InterfaceFilesController(
    InterfaceFileService files, InterfaceService service) : ControllerBase
{
    /// <summary>인터페이스별 개수. 목록의 배지가 읽는다.</summary>
    [HttpGet("files/counts")]
    public IActionResult Counts([FromQuery] int prjRid)
        => Ok(ApiResponse<List<IfFileCount>>.Ok(files.Counts(prjRid)));

    [HttpGet("{ifId:int}/files")]
    public IActionResult List([FromQuery] int prjRid, int ifId)
        => Ok(ApiResponse<List<IfFileRow>>.Ok(files.List(prjRid, ifId)));

    [HttpPost("{ifId:int}/files")]
    public async Task<IActionResult> UploadAsync([FromQuery] int prjRid, int ifId)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "파일을 multipart/form-data 로 보내세요."));
        }

        // 없는 인터페이스 아래에 파일이 쌓이면 아무도 찾지 못한다.
        if (!await service.OwnsAsync(prjRid, ifId))
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"그 인터페이스를 찾을 수 없습니다: {ifId}"));
        }

        var form = await Request.ReadFormAsync();

        if (form.Files.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "첨부할 파일이 없습니다."));
        }

        if (form.Files.Count > InterfaceFileService.MaxCount)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "TOO_MANY", $"한 번에 {InterfaceFileService.MaxCount}개까지 올릴 수 있습니다."));
        }

        string? tooBig = null;
        var saved = await files.SaveAsync(prjRid, ifId, form.Files, name => tooBig = name);

        if (tooBig is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "TOO_LARGE", $"{tooBig} 이(가) 너무 큽니다. 파일당 25MB 까지."));
        }

        return saved.Count == 0
            ? BadRequest(ApiResponse<object>.Fail("INVALID", "빈 파일만 있습니다."))
            : Ok(ApiResponse<List<IfFileRow>>.Ok(saved));
    }

    /// <summary>
    /// 내려받기. <b>무엇이든 첨부로 내려 준다</b> — 올라온 HTML 이 우리 출처에서
    /// 실행되면 안 된다.
    /// </summary>
    [HttpGet("{ifId:int}/files/{fileId}")]
    public IActionResult Download([FromQuery] int prjRid, int ifId, string fileId)
    {
        var found = files.Open(prjRid, ifId, fileId);

        return found is null
            ? NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 파일이 없습니다."))
            : PhysicalFile(found.Value.Path, "application/octet-stream", found.Value.Name);
    }

    [HttpDelete("{ifId:int}/files/{fileId}")]
    public IActionResult Delete([FromQuery] int prjRid, int ifId, string fileId)
        => files.Delete(prjRid, ifId, fileId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 파일이 없습니다."));
}
