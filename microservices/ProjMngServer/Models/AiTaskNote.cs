namespace ProjMngServer.Models;

/// <summary>
/// AI 작업 요청에 남긴 말 한 줄 — <c>projmng.ai_task_note</c>.
/// </summary>
/// 스키마는 deploy/sql/projmng-ai-task-note-2026-09-25.sql. 지시문(AiTask.Contents)과 갈라 둔 자리다 — 관리자의 「이렇게 처리하겠습니다」가 지시문에 섞이면 대화가 지시에 섞여 실행기까지 따라간다.
public sealed class AiTaskNote
{
    public long NoteKey { get; set; }

    public long TaskKey { get; set; }

    /// <summary>남긴 말. 빈 줄은 저장하지 않는다.</summary>
    public string? Contents { get; set; }

    /// <summary>
    /// 요청을 올린 사람이 확인한 시각. <b>널이면 아직 안 읽은 말</b>이다.
    /// </summary>
    /// 올린 사람이 스스로 적은 줄에는 처음부터 값이 들어간다 — 자기 글을 자기에게 안 읽음으로 세우지 않는다.
    public DateTime? ReadDt { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }

    /// <summary>
    /// <b>요청을 올린 사람이 적은 줄인가.</b> 조인해 온다 — <b>읽기 전용</b>.
    /// </summary>
    /// cre_id 와 ai_task.cre_id 를 견주는 값이라 칸으로 두지 않는다 — 굳혀 두면 역할이 바뀐 뒤 지난 줄이 거짓이 된다. 화면은 이 값으로 말풍선을 좌우로 가른다.
    public bool IsOwner { get; set; }
}
