namespace ProjMngServer.Models;

/// <summary>
/// AI 작업 지시에 함께 올린 파일 한 개 — <c>projmng.ai_task_file</c>.
/// </summary>
/// 바이트는 여기 없다 — 목록은 이름과 크기만 쓰므로 바이트를 담으면 카드 조회가 사진을 끌고 온다. 바이트는 내려받을 때만 읽는다.
/// TaskKey 가 null 이면 아직 안 묶인 「떠 있는 첨부」다(빠른 지시는 고르는 순간 올리므로 작업 번호가 없다).
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
    /// 올라온 Content-Type 은 믿지 않는다 — 비어 오거나 엉뚱한 값이 올 수 있고, 틀리면 깨진 그림이 목록에 남는다. 판정은 서버가 한다(AiTaskFileService.LooksLikeImage).
    public bool IsImage { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
}
