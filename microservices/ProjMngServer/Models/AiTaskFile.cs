namespace ProjMngServer.Models;

/// <summary>
/// AI 작업 지시에 함께 올린 파일 한 개 — <c>projmng.ai_task_file</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>바이트는 여기 없다.</b> 목록·상세가 쓰는 것은 이름과 크기뿐인데,
/// 담아 두면 카드 넉 장을 그리는 조회가 사진 몇 장을 함께 끌고 온다.
/// 바이트는 내려받을 때만 따로 읽는다(<c>AiTaskFileService.OpenAsync</c>).
/// </para>
/// <para>
/// <see cref="TaskKey"/> 가 널이면 <b>아직 안 묶인 것</b>이다 — 「빠른 지시」는
/// 고르는 순간 올리는데 그때는 작업 번호가 없다. 왜 그렇게 했는지는
/// <c>deploy/sql/projmng-ai-task-file-2026-09-25.sql</c> 머리말에 있다.
/// </para>
/// </remarks>
public sealed class AiTaskFile
{
    public long FileKey { get; set; }

    /// <summary>묶인 작업. 널이면 아직 안 보낸 「떠 있는 첨부」다.</summary>
    public long? TaskKey { get; set; }

    /// <summary>올릴 때의 이름. 경로와 제어문자를 걷어낸 값이다.</summary>
    public string FileNm { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long ByteSize { get; set; }

    /// <summary>
    /// 미리보기를 그릴 수 있는 그림인가.
    /// </summary>
    /// <remarks>
    /// <b>올라온 <c>Content-Type</c> 을 그대로 믿지 않는다.</b> 브라우저가 주는
    /// 값이라 비어 오거나 엉뚱한 것이 실려 오는데, 화면은 이 값 하나로
    /// <c>&lt;img&gt;</c> 를 세울지 아이콘을 세울지 가른다 — 틀리면 깨진 그림이
    /// 목록에 남는다. 판정은 서버가 한다(<c>AiTaskFileService.LooksLikeImage</c>).
    /// </remarks>
    public bool IsImage { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
}
