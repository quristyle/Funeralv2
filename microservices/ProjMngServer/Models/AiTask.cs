namespace ProjMngServer.Models;

/// <summary>
/// AI 에게 시킬 일 한 건 — <c>projmng.ai_task</c>.
/// </summary>
/// <remarks>
/// 설계는 <c>docs/ai-task-runner.md</c> 에 있다.
///
/// <para>
/// <b>요청여부(<see cref="RequestFlag"/>)와 상태(<see cref="TaskStatus"/>)를
/// 나눠 둔 것이 이 자료의 핵심이다.</b> 앞엣것은 사람의 의사이고 뒤엣것은
/// 기계의 현재 위치다. 합치면 <i>사람이 취소를 눌렀는데 이미 실행 중</i> 을
/// 표현할 수 없고, 그때 상태를 '취소' 로 덮으면 <b>실제로는 아직 돌고 있는
/// CLI 가 화면에서 사라진다.</b>
/// </para>
/// </remarks>
public sealed class AiTask
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public long TaskKey { get; set; }

    /// <summary>제목. 비우고 저장하면 서버가 본문에서 만들어 준다.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// 저장할 때 제목 칸이 비어 있었나. <b>이 값이 거짓이면 기계가 손대지 않는다.</b>
    /// </summary>
    public bool TitleAuto { get; set; }

    /// <summary>
    /// 어느 실행을 보고 지은 제목인가. 화면이 「아직 안 왔다」를 아는 근거.
    /// </summary>
    public long? TitleRunKey { get; set; }

    /// <summary>AI 에게 줄 지시문. 편집기에 쓰는 그 글이다.</summary>
    public string? Contents { get; set; }

    /// <summary>본문 형식. 지금은 <c>markdown</c> 하나다.</summary>
    public string? ContentFormat { get; set; } = "markdown";

    // ── 함께 보낸 파일 ──────────────────────────────────────

    /// <summary>
    /// 붙은 첨부 개수. 조인해 온다 — <b>읽기 전용</b>.
    /// </summary>
    /// <remarks>
    /// 화면이 이 값 하나로 클립 배지를 세운다. 건마다 첨부 목록을 따로 묻게
    /// 두면 카드 열 장을 그리는 「빠른 지시」가 게이트웨이를 열 번 더 두드린다.
    /// </remarks>
    public int FileCount { get; set; }

    /// <summary>
    /// <b>등록할 때만 쓰는 값</b> — 미리 올려 둔 첨부의 번호들.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「빠른 지시」는 <b>고르는 순간 올린다.</b> 그때는 작업 번호가 아직
    /// 없으므로 첨부가 주인 없이 담기고(<c>ai_task_file.task_key IS NULL</c>),
    /// 보낼 때 이 칸에 그 번호들을 실어 보내 묶는다
    /// (<c>AiTaskFileService.BindAsync</c>).
    /// </para>
    /// <para>
    /// <b>조회로는 오지 않는다.</b> 붙은 것을 읽는 길은 첨부 목록 조회
    /// 하나뿐이고, 여기는 언제나 비어 있다.
    /// </para>
    /// </remarks>
    public long[]? FileKeys { get; set; }

    // ── 대상 ────────────────────────────────────────────────

    /// <summary>어디에서 일할 것인가(<c>ai_target</c>). 화면에서 고른다.</summary>
    public long? TargetKey { get; set; }

    /// <summary>대상 이름. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetNm { get; set; }

    /// <summary>대상 경로. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetPath { get; set; }

    /// <summary>대상이 push 를 허용하는가. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public bool TargetAllowPush { get; set; }

    /// <summary>
    /// 대상이 허용한 실행기 목록(쉼표로 이은 값). 조인해 온다 — <b>읽기 전용</b>.
    /// </summary>
    /// <remarks>
    /// 화면이 <b>고를 수 있는 AI 를 이 값으로 좁힌다.</b> 「이어서 지시」 창에서
    /// AI 를 바꿀 수 있게 되면서 필요해졌다 — 그 창은 작업 한 건만 들고 있고
    /// 대상 목록을 따로 읽지 않으므로, 여기 실어 보내지 않으면 허용하지 않는
    /// AI 가 칸에 뜨고 <b>요청 단계에서야 거절된다.</b>
    /// </remarks>
    public string? TargetRunnerKinds { get; set; }

    /// <summary>기준 브랜치. 비우면 대상의 기본값을 쓴다.</summary>
    public string? TargetRef { get; set; }

    /// <summary>어느 CLI 로 돌릴 것인가 — <c>claude</c> · <c>antigravity</c> · <c>copilot</c>.</summary>
    public string? RunnerKind { get; set; } = "claude";

    // ── 상태 ────────────────────────────────────────────────

    /// <summary><b>작업요청여부</b> — <c>none</c> · <c>requested</c> · <c>cancel_requested</c>.</summary>
    public string? RequestFlag { get; set; } = AiTaskFlag.None;

    /// <summary><b>작업상태</b> — <see cref="AiTaskStatus"/>.</summary>
    public string? TaskStatus { get; set; } = AiTaskStatus.Idle;

    public int Priority { get; set; }

    public int TimeoutMinutes { get; set; } = 30;

    public int AttemptCount { get; set; }

    /// <summary>
    /// 재실행 상한. <b>기본 3 이다</b>(1~5).
    ///
    /// <para>
    /// 한동안 1 이었다 — 파일을 고치는 작업이라 자동 재시도가 안전하지 않아서다.
    /// 설계 6.11 이 그 조건을 적어 두었는데, <b>작업공간이 실행마다 완전히
    /// 갈리는 것</b>이 전제였다. worktree · 복사본 대상은 지금 그 조건을
    /// 만족한다(7.3).
    /// </para>
    ///
    /// <para>
    /// 그래서 자동 재시도는 <b>그 대상에서만</b> 돈다. 원본 직접(inplace)과
    /// 연락 끊김(interrupted)은 여전히 사람이 보고 다시 누른다 —
    /// 판정은 <c>AiRunService.CompleteAsync</c> 에 있다.
    /// </para>
    /// </summary>
    public int AttemptMax { get; set; } = 3;

    // ── 끝난 뒤 ─────────────────────────────────────────────

    /// <summary>
    /// push 까지 할 것인가. <b>대상이 허용할 때만 켤 수 있다.</b>
    /// 켜면 이 작업 한 건이 곧 운영 배포 한 번이 된다.
    /// </summary>
    public bool AutoPush { get; set; }

    /// <summary>끝나면 메일로 받을 것인가.</summary>
    public bool NotifyEmail { get; set; }

    /// <summary>끝나면 PWA 알림을 받을 것인가.</summary>
    public bool NotifyPwa { get; set; }

    /// <summary>받는 사람. 비우면 요청한 사람에게 간다.</summary>
    public string? NotifyTo { get; set; }

    /// <summary><c>always</c> · <c>on_success</c> · <c>on_failure</c>.</summary>
    public string? NotifyWhen { get; set; } = "always";

    /// <summary>메일을 못 보낸 이유. <b>이 값이 있어도 작업은 완료다.</b></summary>
    public string? NotifyError { get; set; }

    // ── 시각 ────────────────────────────────────────────────

    public DateTime? RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    // ── 최근 실행 요약 (목록 조회용) ────────────────────────

    public long? LastRunKey { get; set; }
    public int? LastExitCode { get; set; }
    public string? LastError { get; set; }

    /// <summary>
    /// push 한 커밋. <b>비어 있지 않으면 운영 배포가 일어났다는 뜻이다.</b>
    /// </summary>
    public string? PushedCommit { get; set; }

    /// <summary>
    /// push 직전의 운영 <c>TAG</c>. <b>되돌릴 때 필요한 값</b>이라 미리 적어 둔다 —
    /// 되돌리기는 <c>/srv/jsini/.env</c> 의 그 값을 이전 것으로 돌리는 일인데,
    /// 아무도 안 적어 두면 되돌릴 수가 없다.
    /// </summary>
    public string? PreviousTag { get; set; }

    /// <summary>동시 편집 방지. 저장할 때 읽어 온 값을 그대로 보낸다.</summary>
    public int RowVersion { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }

    /// <summary>사용자가 확인 완료했는지 여부</summary>
    public bool UserConfirmed { get; set; }

    /// <summary>
    /// <b>일반 사용자가 「AI 작업 요청」 화면에서 올린 건인가.</b>
    /// </summary>
    /// <remarks>
    /// 이 건은 저장만 되어 있고 <b>아무 데서도 안 돈다</b> — 요청여부 <c>none</c>,
    /// 상태 <c>idle</c>, 작업 대상 없음이다. 그런데 그 셋은 관리자가 「AI 작업」
    /// 화면에서 <b>쓰다 만 건</b>과 글자 하나 다르지 않다. 가르지 못하면
    /// 「시켜 달라고 올라온 것」이 쓰다 만 제 글 사이에 섞여 영영 안 돌아간다.
    /// <para>
    /// 값은 <b>등록할 때 한 번만 정해진다.</b> 관리자가 내용을 고치고 대상·AI 를
    /// 채워 실제로 시켜도 이 값은 그대로 남는다 — 「누가 부탁한 일이었나」는
    /// 돌고 난 뒤에도 사라지면 안 되는 사실이다.
    /// </para>
    /// </remarks>
    public bool IsUserRequest { get; set; }
}

/// <summary>작업요청여부.</summary>
public static class AiTaskFlag
{
    /// <summary>작성 중. 아직 시키지 않았다.</summary>
    public const string None = "none";

    /// <summary>시켜 달라. 이 값이면 감시자가 집어 간다.</summary>
    public const string Requested = "requested";

    /// <summary>취소해 달라. 실행 중이어도 이 값은 남는다.</summary>
    public const string CancelRequested = "cancel_requested";

    public static bool IsValid(string? v) =>
        v is None or Requested or CancelRequested;
}

/// <summary>작업상태.</summary>
public static class AiTaskStatus
{
    public const string Idle = "idle";
    public const string Queued = "queued";
    public const string Preparing = "preparing";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Timeout = "timeout";
    public const string Canceled = "canceled";

    /// <summary>
    /// 실행기와 연락이 끊겼다. <b>「실패」와 다른 값이다</b> —
    /// 실패는 CLI 가 답을 준 것이고 이것은 <b>아무것도 모르는 것</b>이다.
    /// 파일이 반쯤 고쳐져 있을 수도 있어서 같은 칸에 넣으면 안 된다.
    /// </summary>
    public const string Interrupted = "interrupted";

    /// <summary>더 움직이지 않는 상태인가.</summary>
    public static bool IsFinal(string? v) =>
        v is Succeeded or Failed or Timeout or Canceled or Interrupted;

    /// <summary>지금 도는 중인가.</summary>
    public static bool IsBusy(string? v) =>
        v is Queued or Preparing or Running;
}
