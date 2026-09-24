using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// AI 작업에 함께 보내는 파일 — <c>/api/ai-tasks/…/files</c>.
/// </summary>
/// <remarks>
/// <para>
/// 화면은 <c>AiAskPanel</c>(빠른 지시)과 <c>AiTaskView</c>(보낸 한 건의 속)다.
/// 설계는 <c>docs/ai-task-runner.md</c> 의 「함께 보내는 파일」.
/// </para>
/// <para>
/// <b>올리기와 묶기가 갈려 있다.</b> 고르는 순간 <c>POST files</c> 로 담아 두고
/// (그때는 작업 번호가 없다), 지시를 보낼 때 그 번호들을 <c>AiTask.FileKeys</c>
/// 에 실어 보내면 등록이 묶는다. 왜 그렇게 했는지는
/// <c>deploy/sql/projmng-ai-task-file-2026-09-25.sql</c> 머리말에 있다.
/// </para>
/// <para>
/// <b>실행기는 이 경로를 쓰지 않는다.</b> 게이트웨이를 지나지 못하므로
/// <c>/api/ai-runner/runs/{runKey}/files/{fileKey}</c> 로 따로 받는다
/// (<c>AiRunnerController</c>).
/// </para>
/// </remarks>
[ApiController]
[Route("api/ai-tasks")]
public sealed class AiTaskFilesController(AiTaskFileService files) : ControllerBase
{
    /// <summary>요청을 보낸 사람. 없으면 <c>system</c>.</summary>
    /// <remarks>
    /// <c>AiTasksController</c> 와 같은 규칙이다 — <b>본문에 실어 보내지
    /// 않는다.</b> 위조해도 서버가 알 수 없다.
    /// </remarks>
    private string UserId =>
        Request.Headers.TryGetValue("X-User-Id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id.ToString()
            : "system";

    /// <summary>
    /// 담아 두기 한 번에 받아 줄 본문 크기. <b>한 장 상한 × 장수 + 여유</b>다.
    /// </summary>
    private const long StageBodyLimit =
        (AiTaskFileService.MaxBytes * AiTaskFileService.MaxCount) + (16L * 1024 * 1024);

    /// <summary>작업 하나에 붙은 첨부 목록. <b>바이트는 오지 않는다.</b></summary>
    [HttpGet("{taskKey:long}/files")]
    public async Task<IActionResult> ListAsync(long taskKey)
        => Ok(ApiResponse<List<AiTaskFile>>.Ok(await files.ListAsync(taskKey)));

    /// <summary>
    /// <b>내가 붙여 두고 아직 안 보낸 것.</b> 화면이 다시 열릴 때 읽는다.
    /// </summary>
    [HttpGet("files/mine")]
    public async Task<IActionResult> MineAsync()
        => Ok(ApiResponse<List<AiTaskFile>>.Ok(await files.ListLooseAsync(UserId)));

    /// <summary>
    /// <b>고른 파일을 미리 담아 둔다.</b> 아직 어느 작업에도 안 묶인다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>본문 상한을 두 군데에 건다.</b> <c>RequestSizeLimit</c> 은 Kestrel 의
    /// 기본값(30MB)을 이 창구에서만 밀어내고, <c>RequestFormLimits</c> 는
    /// <c>ReadFormAsync</c> 가 따로 들고 있는 multipart 상한(기본 128MB)을
    /// 밀어낸다. <b>둘 중 하나만 올리면 나머지가 막는다</b> — 증상은 같은
    /// 413/400 이라 어느 쪽이 막았는지 밖에서는 안 보인다.
    /// </para>
    /// <para>
    /// 값은 <c>MaxBytes × MaxCount</c> 에 여유를 더한 것이다. 여유는 multipart
    /// 의 경계선·헤더 몫이라 상한을 딱 맞추면 <b>마지막 한 장이 규격 안인데도
    /// 거절된다.</b>
    /// </para>
    /// </remarks>
    [HttpPost("files")]
    [RequestSizeLimit(StageBodyLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = StageBodyLimit)]
    public async Task<IActionResult> StageAsync(CancellationToken ct)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "파일을 multipart/form-data 로 보내세요."));
        }

        var form = await Request.ReadFormAsync(ct);

        if (form.Files.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID", "붙일 파일이 없습니다."));
        }

        if (form.Files.Count > AiTaskFileService.MaxCount)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "TOO_MANY", $"한 번에 {AiTaskFileService.MaxCount}개까지 붙일 수 있습니다."));
        }

        string? tooBig = null;
        var saved = await files.StageAsync(form.Files, UserId, name => tooBig = name, ct);

        if (tooBig is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "TOO_LARGE", $"{tooBig} 이(가) 너무 큽니다. 한 개 {AiTaskFileService.MaxBytes / 1024 / 1024}MB 까지."));
        }

        return saved.Count == 0
            ? BadRequest(ApiResponse<object>.Fail("INVALID", "빈 파일만 있습니다."))
            : Ok(ApiResponse<List<AiTaskFile>>.Ok(saved));
    }

    /// <summary>
    /// 내려받기. <b>그림만 그대로 내보내고 나머지는 전부 첨부다.</b>
    /// </summary>
    /// <remarks>
    /// 그림은 화면이 <c>&lt;img&gt;</c> 로 걸어야 해서 원래 형식으로 내보낸다.
    /// 그 밖에는 무엇이든 <c>application/octet-stream</c> 에 첨부로 내려 준다 —
    /// 올라온 HTML 이 우리 출처에서 실행되면 안 된다
    /// (<c>InterfaceFilesController</c> 가 먼저 정한 규칙이다). <c>image/svg+xml</c>
    /// 이 그림으로 안 쳐지는 것도 같은 이유다(<c>AiTaskFileService.LooksLikeImage</c>).
    /// </remarks>
    [HttpGet("files/{fileKey:long}/content")]
    public async Task<IActionResult> ContentAsync(long fileKey)
    {
        var found = await files.OpenAsync(fileKey);

        if (found is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "그 파일이 없습니다."));
        }

        var (meta, bytes) = found.Value;

        return meta.IsImage
            ? File(bytes, meta.ContentType)
            : File(bytes, "application/octet-stream", meta.FileNm);
    }

    /// <summary>
    /// 붙여 둔 것을 뗀다. <b>보내기 전까지만</b> — 이미 묶인 것은 안 지워진다.
    /// </summary>
    [HttpDelete("files/{fileKey:long}")]
    public async Task<IActionResult> DeleteAsync(long fileKey)
        => await files.DeleteLooseAsync(fileKey, UserId)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail(
                "NOT_FOUND", "뗄 수 없는 첨부입니다. 이미 보낸 지시에 붙어 있거나 남의 것입니다."));
}
