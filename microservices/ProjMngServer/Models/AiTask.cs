namespace ProjMngServer.Models;

/// <summary>AI 에게 시킬 일 한 건(<c>projmng.ai_task</c>). 설계는 <c>docs/ai-task-runner.md</c>.
/// 요청여부(RequestFlag)와 상태(TaskStatus)를 나눠 둔 것이 핵심이다 — 합치면 「취소를 눌렀는데 이미 실행 중」을 표현할 수 없고, 상태를 취소로 덮으면 아직 도는 CLI 가 화면에서 사라진다.</summary>
public sealed class AiTask
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public long TaskKey { get; set; }

    /// <summary>제목. 비우고 저장하면 서버가 본문에서 만들어 준다.</summary>
    public string? Title { get; set; }

    /// <summary>저장할 때 제목 칸이 비어 있었나. <b>이 값이 거짓이면 기계가 손대지 않는다.</b></summary>
    public bool TitleAuto { get; set; }

    /// <summary>어느 실행을 보고 지은 제목인가. 화면이 「아직 안 왔다」를 아는 근거.</summary>
    public long? TitleRunKey { get; set; }

    /// <summary>아직 실행 줄이 없는 차수의 제목을 잠깐 얹어 두는 자리. 화면도 이것을 읽는다 — 보냈는데 아무도 집어 가지 않은 차수는 여기밖에 제목이 없다.</summary>
    public string? NextTurnTitle { get; set; }

    /// <summary>DB 서브쿼리로 가져온 차수별 정보 JSON (내부용).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? TurnTitlesJson { get; set; }

    /// <summary>이 건에 딸린 차수별 제목 목록 (1차부터 오름차순). 차수가 2회 이상일 때 화면에서 차수별 제목을 보여주는 데 쓴다.</summary>
    public List<AiTaskTurnTitleDto> TurnTitles { get; set; } = [];

    /// <summary>AI 에게 줄 지시문. 편집기에 쓰는 그 글이다.</summary>
    public string? Contents { get; set; }

    /// <summary>본문 형식. 지금은 <c>markdown</c> 하나다.</summary>
    public string? ContentFormat { get; set; } = "markdown";

    /// <summary>처리차수. 실행 이력의 최대 seq 또는 진행/대기 중인 차수. 조인해 온다 — <b>읽기 전용</b>. 2차 이상일 때 화면 카드에 배지로 표시된다.</summary>
    public int TurnCount { get; set; } = 1;

    // ── 함께 보낸 파일 ──────────────────────────────────────

    /// <summary>붙은 첨부 개수. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public int FileCount { get; set; }

    /// <summary><b>등록할 때만 쓰는 값</b> — 미리 올려 둔 첨부의 번호들. 고르는 순간 주인 없이 올라가므로 보낼 때 이 칸으로 묶는다(<c>AiTaskFileService.BindAsync</c>).
    /// 조회로는 오지 않는다 — 여기는 언제나 비어 있다.</summary>
    public long[]? FileKeys { get; set; }

    // ── 오간 남길말 ─────────────────────────────────────────

    /// <summary>이 건에 오간 남길말 수. 조인해 온다 — 읽기 전용. 본문은 여기 오지 않는다.</summary>
    public int NoteCount { get; set; }

    /// <summary><b>올린 사람이 아직 안 읽은</b> 남길말 수. 조인해 온다 — 읽기 전용. 상태 배지로는 답할 수 없다 — 대상을 아직 안 채운 건은 상태가 그대로 「접수 대기」다.</summary>
    public int UnreadNoteCount { get; set; }

    // ── 대상 ────────────────────────────────────────────────

    /// <summary>어디에서 일할 것인가(<c>ai_target</c>). 화면에서 고른다.</summary>
    public long? TargetKey { get; set; }

    /// <summary>대상 이름. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetNm { get; set; }

    /// <summary>대상 종류(<c>repo</c> · <c>folder</c>). 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetKind { get; set; }

    /// <summary>저장소 주소. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetRepoUrl { get; set; }

    /// <summary>대상 경로. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TargetPath { get; set; }

    /// <summary>대상이 push 를 허용하는가. 조인해 온다 — <b>읽기 전용</b>.</summary>
    public bool TargetAllowPush { get; set; }

    /// <summary>대상이 허용한 실행기 목록(쉼표로 이은 값). 화면이 AI 를 이 값으로 좁힌다 — 허용하지 않는 AI 를 칸에 띄우면 요청 단계에서야 거절된다.</summary>
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

    /// <summary>제한 시간(분). 기본 120(1~1440). 짧은 건은 어차피 일찍 끝나므로 길게 잡아도 손해가 없다.</summary>
    public int TimeoutMinutes { get; set; } = 120;

    public int AttemptCount { get; set; }

    /// <summary>재실행 상한. 기본 3(1~5). 자동 재시도는 작업공간이 실행마다 갈리는 대상(worktree · 복사본)에서만 돈다 — 원본 직접(inplace)과 연락 끊김(interrupted)은 사람이 다시 누른다. 판정은 <c>AiRunService.CompleteAsync</c> 에 있다.</summary>
    public int AttemptMax { get; set; } = 3;

    // ── 끝난 뒤 ─────────────────────────────────────────────

    /// <summary>push 까지 할 것인가. 대상이 허용할 때만 켤 수 있고, 켜면 이 작업 한 건이 곧 운영 배포 한 번이 된다.</summary>
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

    /// <summary>push 한 커밋. 비어 있지 않으면 운영 배포가 일어났다는 뜻이다.</summary>
    public string? PushedCommit { get; set; }

    /// <summary>push 직전의 운영 <c>TAG</c>. 되돌릴 때 필요하므로 미리 적어 둔다 — 되돌리기는 <c>/srv/jsini/.env</c> 의 그 값을 이전 것으로 돌리는 일이다.</summary>
    public string? PreviousTag { get; set; }

    /// <summary>동시 편집 방지. 저장할 때 읽어 온 값을 그대로 보낸다.</summary>
    public int RowVersion { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }

    /// <summary>사용자가 확인 완료했는지 여부</summary>
    public bool UserConfirmed { get; set; }

    /// <summary>일반 사용자가 「AI 작업 요청」 화면에서 올린 건인가. 요청여부 none · 상태 idle · 대상 없음이라 관리자의 쓰다 만 건과 글자 하나 다르지 않으므로 이 값으로 가른다. 등록할 때 한 번만 정해지고 돌고 난 뒤에도 남는다.</summary>
    public bool IsUserRequest { get; set; }

    /// <summary>현재 작업 완료 시 자동으로 이어서 지시할 내용 (예약).</summary>
    public string? PendingContinueAddition { get; set; }

    /// <summary>예약된 이어서 지시의 실행기 종류.</summary>
    public string? PendingContinueRunnerKind { get; set; }

    /// <summary>예약된 이어서 지시 등록자.</summary>
    public string? PendingContinueUserId { get; set; }

    /// <summary>예약된 이어서 지시 등록 시각.</summary>
    public DateTime? PendingContinueAt { get; set; }

    /// <summary>예약된 이어서 지시가 있는가.</summary>
    public bool HasPendingContinue => !string.IsNullOrWhiteSpace(PendingContinueAddition);

    /// <summary>지금 도는 중인가. 편집과 삭제를 막는 기준이다.</summary>
    public bool IsBusy => AiTaskStatus.IsBusy(TaskStatus);
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

    /// <summary>실행기와 연락이 끊겼다. 실패와 다른 값이다 — 실패는 CLI 가 답을 준 것이고 이것은 아무것도 모르는 것이라, 파일이 반쯤 고쳐졌을 수 있어 같은 칸에 넣지 않는다.</summary>
    public const string Interrupted = "interrupted";

    /// <summary>더 움직이지 않는 상태인가.</summary>
    public static bool IsFinal(string? v) =>
        v is Succeeded or Failed or Timeout or Canceled or Interrupted;

    /// <summary>지금 도는 중인가.</summary>
    public static bool IsBusy(string? v) =>
        v is Queued or Preparing or Running;
}

/// <summary>작업 한 건의 차수별 제목과 상태.</summary>
public sealed class AiTaskTurnTitleDto
{
    /// <summary>몇 차인가 (1부터 시작).</summary>
    public int No { get; set; }

    /// <summary>그 차수의 제목.</summary>
    public string? Title { get; set; }

    /// <summary>그 차수의 실행 상태.</summary>
    public string? Status { get; set; }
}

