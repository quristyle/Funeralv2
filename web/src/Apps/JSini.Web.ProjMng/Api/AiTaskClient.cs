using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>AI 작업 API(<c>projmng/ai-tasks</c>). 등록·수정자는 서버가 게이트웨이 신원으로 채운다.</summary>
public sealed class AiTaskClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-tasks";

    /// <summary>작업 목록. <c>excludeDone</c> 이면 끝난 건만 빼고, 실패·시간초과·중단은 남긴다.</summary>
    public Task<IReadOnlyList<AiTaskDto>> ListAsync(
        string? status = null, string? flag = null, long? targetKey = null,
        string? keyword = null, bool? userConfirmed = null, bool? userRequest = null,
        bool excludeDone = false, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(flag)) query.Add($"flag={Uri.EscapeDataString(flag)}");
        if (targetKey is not null) query.Add($"targetKey={targetKey}");
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (userConfirmed is not null) query.Add($"userConfirmed={userConfirmed}");
        if (userRequest is not null) query.Add($"userRequest={userRequest}");
        if (excludeDone) query.Add("excludeDone=true");

        return gateway.GetListAsync<AiTaskDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    // ── 「AI 작업 요청」 — 일반 사용자의 입구 ──────────────────
    //
    // 화면: Components/Pages/AiRequestList.razor. 서버가 cre_id = 보낸 사람을 조건에 넣는다.
    // 위 경로를 그대로 쓰면 번호만 바꿔 남의 작업을 고칠 수 있다.

    /// <summary><b>내가 올린 요청만.</b> 남의 것은 한 건도 오지 않는다.</summary>
    public Task<IReadOnlyList<AiTaskDto>> MineAsync(
        string? keyword = null, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskDto>(
            string.IsNullOrWhiteSpace(keyword)
                ? $"{Url}/mine"
                : $"{Url}/mine?keyword={Uri.EscapeDataString(keyword)}", ct);

    /// <summary>지시를 적어 둔다. 저장만 되고 실행되지 않는다(대상·AI 는 관리자가 채운다).</summary>
    public Task<AiTaskDto?> CreateMineAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/mine", item, ct);

    /// <summary><b>내가 올린 요청을 고친다.</b> 관리자가 손대기 전까지만.</summary>
    public Task<AiTaskDto?> UpdateMineAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PutAsync<AiTaskDto>($"{Url}/mine/{item.TaskKey}", item, ct);

    /// <summary><b>내가 올린 요청을 거둬들인다.</b> 관리자가 손대기 전까지만.</summary>
    public Task DeleteMineAsync(long taskKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/mine/{taskKey}", ct);

    // ── 남길말 ──────────────────────────────────────────────
    //
    // 화면: Components/Shared/AiTaskNotes.razor. 여기 mine/… 경로는 올린 사람이 아니면 「없다」로 답한다.

    /// <summary><b>내가 올린 요청에 붙은 말.</b> 남의 건은 한 줄도 오지 않는다.</summary>
    public Task<IReadOnlyList<AiTaskNoteDto>> MineNotesAsync(
        long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskNoteDto>($"{Url}/mine/{taskKey}/notes", ct);

    /// <summary><b>내가 올린 요청에 말을 남긴다.</b> 관리자에게 앱푸시가 간다.</summary>
    public Task<AiTaskNoteDto?> AddMineNoteAsync(
        long taskKey, string contents, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskNoteDto>($"{Url}/mine/{taskKey}/notes", new { contents }, ct);

    /// <summary>붙은 말을 전부 읽음으로 찍는다. 펼 때 한 번 부른다(안 읽은 수가 0 이면 부르지 않는다).</summary>
    public Task ReadMineNotesAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/mine/{taskKey}/notes/read", new { }, ct);

    /// <summary>한 건에 붙은 말 전부 — <b>관리자 쪽 입구</b>.</summary>
    public Task<IReadOnlyList<AiTaskNoteDto>> NotesAsync(
        long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskNoteDto>($"{Url}/{taskKey}/notes", ct);

    /// <summary><b>말을 남긴다 — 관리자 쪽 입구.</b> 올린 사람에게 앱푸시가 간다.</summary>
    public Task<AiTaskNoteDto?> AddNoteAsync(
        long taskKey, string contents, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskNoteDto>($"{Url}/{taskKey}/notes", new { contents }, ct);

    public Task<AiTaskDto?> GetAsync(long taskKey, CancellationToken ct = default)
        => gateway.GetOneAsync<AiTaskDto>($"{Url}/{taskKey}", ct);

    public Task<AiTaskDto?> CreateAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>(Url, item, ct);

    public Task<AiTaskDto?> UpdateAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PutAsync<AiTaskDto>($"{Url}/{item.TaskKey}", item, ct);

    /// <summary><b>이 한 줄이 AI 에게 일을 시킨다.</b> 상태가 「대기」로 간다.</summary>
    public Task<AiTaskDto?> RequestAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/request", new { }, ct);

    /// <summary>끝난 작업에 이어서 지시한다. 서버가 지난 진행과 이번 할 일로 본문을 짠다.
    /// runnerKind 를 비우면 지난 회차의 실행기가 그대로 간다(서버가 허용 목록으로 다시 본다).</summary>
    public Task<AiTaskDto?> ContinueAsync(
        long taskKey, string addition, string? runnerKind = null, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>(
            $"{Url}/{taskKey}/continue", new { addition, runnerKind }, ct);

    /// <summary>진행 중인 작업에 완료 시 이어서 지시할 내용을 예약 저장한다.</summary>
    public Task<AiTaskDto?> SetPendingContinueAsync(
        long taskKey, string addition, string? runnerKind = null, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>(
            $"{Url}/{taskKey}/pending-continue", new { addition, runnerKind }, ct);

    /// <summary>예약된 이어서 지시를 취소한다.</summary>
    public Task ClearPendingContinueAsync(
        long taskKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{taskKey}/pending-continue", ct);

    /// <summary>실패한 작업을 수동으로 다시 요청한다. 추가 지시사항이 있으면 본문 뒤에 덧붙인다.</summary>
    public Task<AiTaskDto?> RetryAsync(
        long taskKey, string? addition = null, string? kind = null, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/retry", new { addition, kind }, ct);

    /// <summary>사용자 확인 완료 처리한다.</summary>
    public Task<AiTaskDto?> ConfirmAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/confirm", new { }, ct);

    public Task<AiTaskDto?> CancelAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/cancel", new { }, ct);

    public Task DeleteAsync(long taskKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{taskKey}", ct);

    // ──────────────────────────────────────────── 함께 보내는 파일
    //
    // 올리기와 묶기가 갈려 있다: 고르는 순간 올리고(StageFilesAsync), 번호는 보낼 때 FileKeys 로 싣는다.

    /// <summary>고른 파일을 미리 올려 둔다. 아직 어느 작업에도 묶이지 않는다.</summary>
    public async Task<IReadOnlyList<AiTaskFileDto>> StageFilesAsync(
        IReadOnlyList<PickedFile> files, CancellationToken ct = default)
    {
        if (files.Count == 0)
        {
            return [];
        }

        using var form = new MultipartFormDataContent();
        var streams = new List<Stream>(files.Count);

        try
        {
            foreach (var file in files)
            {
                var stream = file.OpenRead();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(
                        string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType);

                form.Add(part, "files", file.Name);
            }

            return await gateway.PostFormListAsync<AiTaskFileDto>($"{Url}/files", form, ct);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>내가 붙여 두고 아직 안 보낸 첨부. 번호를 브라우저에 적어 두면 서버가 지난 것을 치워 없는 첨부가 붙어 보인다.</summary>
    public Task<IReadOnlyList<AiTaskFileDto>> MyStagedFilesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskFileDto>($"{Url}/files/mine", ct);

    /// <summary>작업 하나에 붙은 첨부. <b>바이트는 오지 않는다.</b></summary>
    public Task<IReadOnlyList<AiTaskFileDto>> FilesAsync(
        long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskFileDto>($"{Url}/{taskKey}/files", ct);

    /// <summary>붙여 둔 것을 뗀다. 보내기 전까지만 가능하다(보낸 지시의 첨부는 서버가 거절한다).</summary>
    public Task DeleteFileAsync(long fileKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/files/{fileKey}", ct);

    /// <summary>실행 이력. 최근 것이 앞이다.</summary>
    public Task<IReadOnlyList<AiTaskRunDto>> RunsAsync(long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskRunDto>($"{Url}/{taskKey}/runs", ct);

    /// <summary>처리 요약을 지금 만들게 한다. 만드는 것은 서버다 — 화면이 모델을 직접 부르면 메일과 화면이 다른 말을 한다. 못 만들면 빈 글자가 온다(오류 아님).</summary>
    public Task<string?> SummarizeAsync(long runKey, CancellationToken ct = default)
        => gateway.PostAsync<string>($"projmng/ai-runs/{runKey}/summary", new { }, ct);

    /// <summary>로그 꼬리. fromSeq 보다 큰 줄만 증분으로 받는다.</summary>
    public Task<IReadOnlyList<AiLogLineDto>> LogsAsync(
        long runKey, int fromSeq, CancellationToken ct = default)
        => gateway.GetListAsync<AiLogLineDto>(
            $"projmng/ai-runs/{runKey}/logs?fromSeq={fromSeq}", ct);
}

/// <summary>실행 한 번.</summary>
public sealed class AiTaskRunDto
{
    public long RunKey { get; set; }
    public int Seq { get; set; }
    public string? RunStatus { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string? GitBranch { get; set; }
    public string? BaseSha { get; set; }
    public string? DiffStat { get; set; }

    /// <summary>처리 파일 목록. 한 줄에 상태\t경로. 실행기가 git diff --name-status 로 적는다(DiffStat 으로는 긴 경로가 잘린다). 없을 수 있다.</summary>
    public string? ChangedFiles { get; set; }

    public int LogLineCount { get; set; }

    /// <summary>AI 의 마지막 답. 고친 내역일 수도, 물은 것의 답일 수도 있다.</summary>
    public string? ResultText { get; set; }

    /// <summary>처리 요약. AI 가 결과문을 한 번 더 정리한 평문이며 실행이 끝날 때 서버가 채운다. 없을 수 있다.</summary>
    public string? SummaryText { get; set; }

    /// <summary>그때 실제로 준 지시문.</summary>
    public string? Instruction { get; set; }

    /// <summary>이 차수의 제목(접힌 머리줄에 다는 한 줄). 시킬 때 지시문으로 한 번 짓고, 끝나면 결과로 한 번 더 지어 뒤엣것이 덮는다. 없을 수 있다.</summary>
    public string? TurnTitle { get; set; }

    public bool IsRunning => RunStatus is "preparing" or "running";

    public string StatusText => RunStatus switch
    {
        "preparing" => "준비중",
        "running" => "실행중",
        "succeeded" => "완료",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        _ => RunStatus ?? string.Empty,
    };

    /// <summary>배지 수식어. 공통 배지(<c>jsini-badge--*</c>)를 그대로 쓴다.</summary>
    public string StatusTone => RunStatus switch
    {
        "succeeded" => "on",
        "failed" or "timeout" or "interrupted" => "err",
        "preparing" or "running" => "warn",
        _ => "off",
    };
}

/// <summary>로그 한 줄.</summary>
public sealed class AiLogLineDto
{
    public int Seq { get; set; }
    public string? Stream { get; set; }
    public string? Text { get; set; }
    public DateTime? LogAt { get; set; }
}

/// <summary>지시에 함께 올린 파일 한 개. 바이트는 없고, 실제 바이트는 FileDownload.AiTaskUrlFor 의 셸 중계로 받는다.</summary>
public sealed class AiTaskFileDto
{
    public long FileKey { get; set; }

    /// <summary>묶인 작업. <b>널이면 아직 안 보낸 것</b>이다.</summary>
    public long? TaskKey { get; set; }

    public string? FileNm { get; set; }
    public string? ContentType { get; set; }
    public long ByteSize { get; set; }

    /// <summary>미리보기 가능한 그림인가. 서버가 판정한 값이며 브라우저가 준 형식은 믿지 않는다(SVG 는 그림으로 안 친다).</summary>
    public bool IsImage { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }

    /// <summary>사람이 읽는 크기.</summary>
    public string SizeText => ByteSize switch
    {
        >= 1024 * 1024 => $"{ByteSize / 1024.0 / 1024:0.#} MB",
        >= 1024 => $"{ByteSize / 1024.0:0.#} KB",
        _ => $"{ByteSize} B",
    };
}

/// <summary>요청에 남긴 말 한 줄. 지시문(Contents)과 다른 자리다 — 관리자가 처리 계획을 거기 적으면 대화가 실행기까지 따라간다.</summary>
public sealed class AiTaskNoteDto
{
    public long NoteKey { get; set; }

    public long TaskKey { get; set; }

    public string? Contents { get; set; }

    /// <summary>올린 사람이 확인한 시각. <b>널이면 아직 안 읽은 말</b>이다.</summary>
    public DateTime? ReadDt { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }

    /// <summary>요청을 올린 사람이 적은 줄인가(서버가 견주어 준다, 읽기 전용). 말풍선 좌우를 가르는 데만 쓴다 — 관리자 화면에서도 같은 부품이 서므로 로그인 신원으로 가르지 않는다.</summary>
    public bool IsOwner { get; set; }

    /// <summary>적은 시각. UTC 로 온 값을 보여 줄 때 한국 시각으로 옮긴다(docs/utc-time.md).</summary>
    public string WhenText => CreDt.Kst("yyyy-MM-dd HH:mm", string.Empty);
}

/// <summary>AI 작업 한 건. 요청여부와 상태는 따로 둔다 — 앞엣것은 사람의 의사, 뒤엣것은 기계의 현재 위치다.</summary>
public sealed class AiTaskDto
{
    public long TaskKey { get; set; }

    /// <summary>비우고 저장하면 서버가 본문에서 만들어 준다.</summary>
    public string? Title { get; set; }

    /// <summary>저장할 때 제목 칸이 비어 있었나. 이 값이 거짓이면 기계가 손대지 않는다.</summary>
    public bool TitleAuto { get; set; }

    /// <summary>어느 실행을 보고 지은 제목인가. 화면이 「아직 안 왔다」를 아는 근거.</summary>
    public long? TitleRunKey { get; set; }

    /// <summary>아직 실행 줄이 없는 차수의 제목(보낸 뒤 아무도 집어 가지 않은 동안). 실행기가 집어 가면 실행 줄로 옮기고 비운다. 차수 머리줄은 실행 줄의 제목을 먼저 본다.</summary>
    public string? NextTurnTitle { get; set; }

    /// <summary>차수별 제목 목록(1차부터 오름차순). 차수가 2회 이상일 때 쓴다.</summary>
    public List<AiTaskTurnTitleDto> TurnTitles { get; set; } = [];

    public string? Contents { get; set; }
    public string? ContentFormat { get; set; } = "markdown";

    private int _turnCount = 1;

    /// <summary>처리차수(읽기 전용, 서버가 센다).</summary>
    public int TurnCount
    {
        get => TurnTitles.Count > 0 ? Math.Max(_turnCount, TurnTitles.Max(t => t.No)) : _turnCount;
        set => _turnCount = value;
    }

    // ── 함께 보낸 파일 ──────────────────────────────────────

    /// <summary>붙은 첨부 개수(읽기 전용). 목록 카드의 클립 배지가 이 값 하나로 선다 — 건마다 목록을 따로 물으면 왕복이 늘어난다.</summary>
    public int FileCount { get; set; }

    /// <summary>등록할 때만 싣는 미리 올린 첨부 번호들. 보내면 서버가 작업에 묶는다. 조회로는 오지 않는다.</summary>
    public long[]? FileKeys { get; set; }

    // ── 오간 남길말 ─────────────────────────────────────────

    /// <summary>이 건에 오간 남길말 수. 읽기 전용 — 서버가 세어 준다.</summary>
    public int NoteCount { get; set; }

    /// <summary>올린 사람이 아직 안 읽은 남길말 수(읽기 전용). 상태 배지로는 답할 수 없는 물음이라 따로 센다.</summary>
    public int UnreadNoteCount { get; set; }

    public long? TargetKey { get; set; }

    /// <summary>읽기 전용 — 서버가 조인해 준다.</summary>
    public string? TargetNm { get; set; }

    public string? TargetKind { get; set; }
    public string? TargetRepoUrl { get; set; }

    /// <summary>읽기 전용.</summary>
    public string? TargetPath { get; set; }

    /// <summary>읽기 전용. 이 값이 거짓이면 「올리기」를 켤 수 없다.</summary>
    public bool TargetAllowPush { get; set; }

    /// <summary>대상이 허용한 AI 목록(쉼표로 이은 코드값, 읽기 전용). 「이어서 지시」 창이 고를 수 있는 AI 를 이 값으로 좁힌다 — 없으면 허용 안 된 AI 가 떠서 요청 단계에서야 거절된다.</summary>
    public string? TargetRunnerKinds { get; set; }

    public string? TargetRef { get; set; }
    public string? RunnerKind { get; set; } = "claude";

    public string? RequestFlag { get; set; } = "none";
    public string? TaskStatus { get; set; } = "idle";

    public int Priority { get; set; }
    /// <summary>제한 시간(분). 서버(<c>AiTask</c>)와 같은 <b>기본 120</b> 이다.</summary>
    public int TimeoutMinutes { get; set; } = 120;
    public int AttemptCount { get; set; }
    public int AttemptMax { get; set; } = 3;

    public bool AutoPush { get; set; }

    public bool NotifyEmail { get; set; }
    public bool NotifyPwa { get; set; }
    public string? NotifyTo { get; set; }
    public string? NotifyWhen { get; set; } = "always";
    public string? NotifyError { get; set; }
    public bool UserConfirmed { get; set; }

    /// <summary>일반 사용자가 「AI 작업 요청」 화면에서 올린 건인가. 관리자 화면이 「시켜 달라고 올라온 것」을 가려내는 값이며, 이 칸이 없으면 관리자가 쓰다 만 제 글과 섞인다. 돌고 난 뒤에도 남는다.</summary>
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

    public DateTime? RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }

    public long? LastRunKey { get; set; }
    public int? LastExitCode { get; set; }
    public string? LastError { get; set; }

    /// <summary>비어 있지 않으면 <b>운영에 배포된 작업</b>이다.</summary>
    public string? PushedCommit { get; set; }

    public string? PreviousTag { get; set; }

    /// <summary>동시 편집 방지. 읽어 온 값을 그대로 돌려보낸다.</summary>
    public int RowVersion { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }

    // ── 화면이 쓰는 파생값 ──────────────────────────────────

    /// <summary>실패해 놓고 「대기」로 앉아 있는 것(서버가 재시도로 다시 넣은 상태). 끝난 시각이 찍혀 있으면 한 번은 돌았다는 뜻이다.</summary>
    public bool IsRetrying => TaskStatus == "queued" && FinishedAt is not null;

    /// <summary>아무도 집어 가지 않았다. 실행기가 안 떠 있다는 뜻이며 가장 흔한 고장이다. 상태는 queued 로 둬야 늦게 뜬 실행기가 집어 갈 수 있어, 화면이 대신 말한다.</summary>
    public bool IsUnclaimed => TaskStatus == "queued"
        && FinishedAt is null
        && !string.IsNullOrWhiteSpace(LastError);

    /// <summary>이어서 지시한 뒤 아직 시작되지 않은 건(이전 실행이 있고 queued 상태).</summary>
    public bool IsContinuing => LastRunKey is not null
        && TaskStatus == "queued"
        && !IsRetrying
        && !IsUnclaimed;

    /// <summary>사람이 읽는 상태 이름. 대기는 넷으로 갈라 적는다 — 상태값은 queued 로 같아도 사람이 할 일이 다르다.</summary>
    public string StatusText => TaskStatus switch
    {
        "idle" => "작성중",
        "queued" => IsRetrying ? "실패·재시도" : IsUnclaimed ? "계속대기중" : IsContinuing ? "이어서 대기" : "대기",
        "preparing" => "준비중",
        "running" => "실행중",
        "succeeded" => "완료",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        _ => TaskStatus ?? string.Empty,
    };

    /// <summary>배지 수식어. 이 저장소의 공통 배지(jsini-badge--*)를 쓴다 — 바탕색을 따로 주면 어두운 테마에서 흰 칩이 된다.</summary>
    public string StatusTone => TaskStatus switch
    {
        "succeeded" => "on",
        "failed" or "timeout" or "interrupted" => "err",

        // 실패로 다시 넣은 것과 안 집혀 간 것은 빨강이다. 둘 다 사람이 봐야 한다.
        "queued" => IsRetrying || IsUnclaimed ? "err" : "warn",
        "preparing" or "running" => "warn",
        _ => "off",
    };

    public string FlagText => RequestFlag switch
    {
        "requested" => "요청",
        "cancel_requested" => "취소요청",
        _ => "-",
    };

    /// <summary>지금 도는 중인가. 편집과 삭제를 막는 기준이다.</summary>
    public bool IsBusy => TaskStatus is "queued" or "preparing" or "running";

    /// <summary>적어만 두고 요청하지 않은 건(작성중). 잃는 것이 글뿐이라 상세 화면이 삭제 단추를 준다. 서버의 「내 요청 거둬들이기」 조건과 같다.</summary>
    public bool IsDraft => TaskStatus == "idle" && RequestFlag == "none" && LastRunKey is null;

    /// <summary>더 움직이지 않는 상태인가.</summary>
    public bool IsFinal => TaskStatus is "succeeded" or "failed" or "timeout" or "canceled" or "interrupted";

    /// <summary>수동 재시도 가능. 실패(failed/timeout)로 끝났고 자동 재시도 횟수를 모두 쓴 상태다.</summary>
    public bool CanManualRetry => (TaskStatus is "failed" or "timeout")
        && AttemptCount >= Math.Max(AttemptMax, 1)
        && !IsBusy;

    /// <summary>끝났는데 제목이 아직인 상태. 서버가 제목을 다시 짓는 동안 몇 초 더 따라가게 한다. 3분 뒤 스스로 거짓이 되며, 그 시간은 UTC 끼리 잰다(한쪽만 한국 시각이면 아홉 시간 어긋난다).</summary>
    public bool TitlePending =>
        TitleAuto
        && LastRunKey is not null
        && TitleRunKey != LastRunKey
        && !IsBusy
        && FinishedAt is not null
        && FinishedAt.Value.AddMinutes(3) > AppTime.UtcNow;

    /// <summary>걸린 시간. 아직 안 끝났으면 비어 있다.</summary>
    public string DurationText => DurationMs is null or 0
        ? string.Empty
        : TimeSpan.FromMilliseconds(DurationMs.Value).ToString(@"h\:mm\:ss");
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

