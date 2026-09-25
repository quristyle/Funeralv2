namespace ProjMngServer.Models;

/// <summary>
/// AI 작업 요청에 남긴 말 한 줄 — <c>projmng.ai_task_note</c>.
/// </summary>
/// <remarks>
/// <para>
/// 스키마는 <c>deploy/sql/projmng-ai-task-note-2026-09-25.sql</c>.
/// </para>
/// <para>
/// <b>지시문(<see cref="AiTask.Contents"/>)과 갈라 둔 자리다.</b> 그쪽은
/// AI 에게 그대로 가는 글이라, 관리자가 「이렇게 처리하겠습니다」를 거기
/// 적으면 <b>대화가 지시에 섞여</b> 실행기까지 따라간다.
/// </para>
/// </remarks>
public sealed class AiTaskNote
{
    public long NoteKey { get; set; }

    public long TaskKey { get; set; }

    /// <summary>남긴 말. 빈 줄은 저장하지 않는다.</summary>
    public string? Contents { get; set; }

    /// <summary>
    /// 요청을 올린 사람이 확인한 시각. <b>널이면 아직 안 읽은 말</b>이다.
    /// </summary>
    /// <remarks>
    /// 올린 사람이 스스로 적은 줄에는 처음부터 값이 들어간다 — 자기 글을
    /// 자기에게 「안 읽음」으로 세울 이유가 없다.
    /// </remarks>
    public DateTime? ReadDt { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }

    /// <summary>
    /// <b>요청을 올린 사람이 적은 줄인가.</b> 조인해 온다 — <b>읽기 전용</b>.
    /// </summary>
    /// <remarks>
    /// 표에 칸으로 두지 않는다. <c>cre_id</c> 와 <c>ai_task.cre_id</c> 를 견주면
    /// 나오는 값이고, 굳혀 두면 나중에 역할이 바뀔 때 지난 줄이 거짓말을 한다.
    /// 화면은 이 값으로 말풍선을 좌우로 가른다.
    /// </remarks>
    public bool IsOwner { get; set; }
}
