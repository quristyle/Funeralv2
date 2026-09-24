using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// AI 작업 — <c>projmng/ai-tasks</c>.
/// </summary>
/// <remarks>
/// 설계는 <c>docs/ai-task-runner.md</c>.
///
/// <para>
/// 등록·수정자는 <b>서버가 게이트웨이 신원으로 채운다</b> — 보내지 않는다.
/// </para>
/// </remarks>
public sealed class AiTaskClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-tasks";

    public Task<IReadOnlyList<AiTaskDto>> ListAsync(
        string? status = null, string? flag = null, long? targetKey = null,
        string? keyword = null, bool? userConfirmed = null, bool? userRequest = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(flag)) query.Add($"flag={Uri.EscapeDataString(flag)}");
        if (targetKey is not null) query.Add($"targetKey={targetKey}");
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (userConfirmed is not null) query.Add($"userConfirmed={userConfirmed}");
        if (userRequest is not null) query.Add($"userRequest={userRequest}");

        return gateway.GetListAsync<AiTaskDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    // ── 「AI 작업 요청」 — 일반 사용자의 입구 ──────────────────
    //
    // 화면: Components/Pages/AiRequestList.razor
    //
    // **위의 것들과 갈라 둔 넷이다.** 서버가 여기서만 <c>cre_id = 보낸 사람</c>
    // 을 조건에 넣는다 — 위의 경로를 그대로 쓰면 로그인한 누구든 번호만 바꿔
    // 남의 작업을 고치고 지울 수 있다(그 컨트롤러 머리말).

    /// <summary><b>내가 올린 요청만.</b> 남의 것은 한 건도 오지 않는다.</summary>
    public Task<IReadOnlyList<AiTaskDto>> MineAsync(
        string? keyword = null, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskDto>(
            string.IsNullOrWhiteSpace(keyword)
                ? $"{Url}/mine"
                : $"{Url}/mine?keyword={Uri.EscapeDataString(keyword)}", ct);

    /// <summary>
    /// <b>지시를 적어 둔다.</b> 저장만 되고 아무 데서도 안 돈다 —
    /// 작업 대상과 AI 는 관리자가 나중에 채운다.
    /// </summary>
    public Task<AiTaskDto?> CreateMineAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/mine", item, ct);

    /// <summary><b>내가 올린 요청을 고친다.</b> 관리자가 손대기 전까지만.</summary>
    public Task<AiTaskDto?> UpdateMineAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PutAsync<AiTaskDto>($"{Url}/mine/{item.TaskKey}", item, ct);

    /// <summary><b>내가 올린 요청을 거둬들인다.</b> 관리자가 손대기 전까지만.</summary>
    public Task DeleteMineAsync(long taskKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/mine/{taskKey}", ct);

    public Task<AiTaskDto?> GetAsync(long taskKey, CancellationToken ct = default)
        => gateway.GetOneAsync<AiTaskDto>($"{Url}/{taskKey}", ct);

    public Task<AiTaskDto?> CreateAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>(Url, item, ct);

    public Task<AiTaskDto?> UpdateAsync(AiTaskDto item, CancellationToken ct = default)
        => gateway.PutAsync<AiTaskDto>($"{Url}/{item.TaskKey}", item, ct);

    /// <summary><b>이 한 줄이 AI 에게 일을 시킨다.</b> 상태가 「대기」로 간다.</summary>
    public Task<AiTaskDto?> RequestAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/request", new { }, ct);

    /// <summary>
    /// <b>끝난 작업에 이어서 지시한다.</b> 서버가 「지난 진행 + 이번에 할 일」로
    /// 본문을 새로 짠다 — 화면이 그 조립을 하지 않는다.
    /// </summary>
    /// <remarks>
    /// <c>runnerKind</c> 는 <b>이 회차를 맡을 AI</b> 다. <b>비워 보내면 지난
    /// 회차의 실행기가 그대로 간다</b> — 화면이 고른 값을 늘 싣지만, 그 값으로
    /// 덮어써도 되는지는 서버가 대상의 허용 목록을 보고 다시 판단한다.
    /// </remarks>
    public Task<AiTaskDto?> ContinueAsync(
        long taskKey, string addition, string? runnerKind = null, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>(
            $"{Url}/{taskKey}/continue", new { addition, runnerKind }, ct);

    /// <summary>
    /// <b>실패한 작업을 수동으로 다시 요청한다.</b> 추가 지시사항이 있으면 본문 뒤에 덧붙인다.
    /// </summary>
    public Task<AiTaskDto?> RetryAsync(
        long taskKey, string? addition = null, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/retry", new { addition }, ct);

    /// <summary>
    /// <b>사용자 확인 완료</b> 처리한다.
    /// </summary>
    public Task<AiTaskDto?> ConfirmAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/confirm", new { }, ct);

    public Task<AiTaskDto?> CancelAsync(long taskKey, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/cancel", new { }, ct);

    public Task DeleteAsync(long taskKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{taskKey}", ct);

    // ──────────────────────────────────────────── 함께 보내는 파일
    //
    // 설계는 `docs/ai-task-runner.md` 의 「함께 보내는 파일」.
    //
    // **올리기와 묶기가 갈려 있다.** 「빠른 지시」는 고르는 순간 올리는데
    // (`StageFilesAsync`) 그때는 작업 번호가 없다. 번호는 보낼 때 생기므로,
    // 받아 둔 번호들을 `AiTaskDto.FileKeys` 에 실어 보내면 서버가 묶는다.
    // 왜 「단추를 누를 때 한꺼번에」가 아닌지는 `AiAskPanel` 머리말에 있다.

    /// <summary>
    /// <b>고른 파일을 미리 올려 둔다.</b> 아직 어느 작업에도 안 묶인다.
    /// </summary>
    /// <remarks>
    /// 고른 파일은 이미 셸이 임시 자리에 받아 두었다(<see cref="PickedFile"/>).
    /// 여기서는 그 바이트를 게이트웨이로 흘려 보내기만 한다 — 메모리에 통째로
    /// 올리지 않는다(<c>InterfaceClient.UploadFilesAsync</c> 와 같은 길이다).
    /// </remarks>
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

    /// <summary>
    /// <b>내가 붙여 두고 아직 안 보낸 것.</b> 화면이 다시 열릴 때 읽는다.
    /// </summary>
    /// <remarks>
    /// 번호를 브라우저에 적어 두지 않는다 — 서버가 하루 지난 것을 치우므로
    /// 적어 두면 <b>없는 첨부가 붙어 보인다.</b>
    /// </remarks>
    public Task<IReadOnlyList<AiTaskFileDto>> MyStagedFilesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskFileDto>($"{Url}/files/mine", ct);

    /// <summary>작업 하나에 붙은 첨부. <b>바이트는 오지 않는다.</b></summary>
    public Task<IReadOnlyList<AiTaskFileDto>> FilesAsync(
        long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskFileDto>($"{Url}/{taskKey}/files", ct);

    /// <summary>
    /// 붙여 둔 것을 뗀다. <b>보내기 전까지만</b> — 이미 보낸 지시에 붙은 것은
    /// 서버가 거절한다(돌고 난 실행이 무엇을 보고 일했는지가 사라지면 안 된다).
    /// </summary>
    public Task DeleteFileAsync(long fileKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/files/{fileKey}", ct);

    /// <summary>실행 이력. 최근 것이 앞이다.</summary>
    public Task<IReadOnlyList<AiTaskRunDto>> RunsAsync(long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskRunDto>($"{Url}/{taskKey}/runs", ct);

    /// <summary>
    /// <b>「처리 요약」을 지금 만들어 달라고 한다.</b> 적힌 것이 없을 때만 뜻이 있다.
    /// </summary>
    /// <remarks>
    /// <b>만드는 것은 서버다.</b> 화면이 모델을 직접 부르면 메일에 적힌 요약과
    /// 화면의 요약이 다른 말을 하게 된다. 이미 적혀 있으면 서버가 그것을
    /// 되읽어 주므로 여러 번 눌러도 한 번만 만든다. 만들지 못하면 <b>빈 글자</b>로
    /// 온다 — 오류가 아니다.
    /// </remarks>
    public Task<string?> SummarizeAsync(long runKey, CancellationToken ct = default)
        => gateway.PostAsync<string>($"projmng/ai-runs/{runKey}/summary", new { }, ct);

    /// <summary>
    /// 로그 꼬리. <b>증분으로 읽는다</b> — <paramref name="fromSeq"/> 보다 큰 줄만 온다.
    /// </summary>
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
    public int LogLineCount { get; set; }

    /// <summary>AI 의 마지막 답. 고친 내역일 수도, 물은 것의 답일 수도 있다.</summary>
    public string? ResultText { get; set; }

    /// <summary>
    /// <b>「처리 요약」 — AI 가 결과문을 한 번 더 정리한 평문.</b>
    /// </summary>
    /// <remarks>
    /// 서버가 실행이 끝날 때마다 채운다(<c>AiResultSummarizer</c>) — 결과 메일의
    /// 「무엇을 했다나」 칸이 쓰던 그 값이고, 화면도 같은 것을 읽어 지시와 결과
    /// 사이에 놓는다. 읽는 법은 <c>AiRunSummary.Parse</c>. <b>없을 수 있다</b> —
    /// 이 칸이 생기기 전의 건과 정리에 실패한 건이 비어 있다.
    /// </remarks>
    public string? SummaryText { get; set; }

    /// <summary>그때 실제로 준 지시문.</summary>
    public string? Instruction { get; set; }

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

/// <summary>지시에 함께 올린 파일 한 개.</summary>
/// <remarks>
/// <b>바이트는 없다.</b> 화면이 쓰는 것은 이름·크기·그림이냐 셋이고, 실제
/// 바이트는 <see cref="FileDownload.AiTaskUrlFor"/> 가 가리키는 셸 중계로 받는다.
/// </remarks>
public sealed class AiTaskFileDto
{
    public long FileKey { get; set; }

    /// <summary>묶인 작업. <b>널이면 아직 안 보낸 것</b>이다.</summary>
    public long? TaskKey { get; set; }

    public string? FileNm { get; set; }
    public string? ContentType { get; set; }
    public long ByteSize { get; set; }

    /// <summary>
    /// 미리보기를 그릴 수 있는 그림인가. <b>서버가 판정한 값이다</b> —
    /// 브라우저가 준 형식을 그대로 믿지 않는다(SVG 는 그림으로 안 친다).
    /// </summary>
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

/// <summary>AI 작업 한 건.</summary>
/// <remarks>
/// <b>요청여부와 상태가 따로 있다.</b> 앞엣것은 사람의 의사이고 뒤엣것은
/// 기계의 현재 위치다 — 합치면 「취소를 눌렀는데 아직 돌고 있다」를 말할 수 없다.
/// </remarks>
public sealed class AiTaskDto
{
    public long TaskKey { get; set; }

    /// <summary>비우고 저장하면 서버가 본문에서 만들어 준다.</summary>
    public string? Title { get; set; }

    /// <summary>저장할 때 제목 칸이 비어 있었나. 이 값이 거짓이면 기계가 손대지 않는다.</summary>
    public bool TitleAuto { get; set; }

    /// <summary>어느 실행을 보고 지은 제목인가. 화면이 「아직 안 왔다」를 아는 근거.</summary>
    public long? TitleRunKey { get; set; }

    public string? Contents { get; set; }
    public string? ContentFormat { get; set; } = "markdown";

    // ── 함께 보낸 파일 ──────────────────────────────────────

    /// <summary>
    /// 붙은 첨부 개수. 읽기 전용 — 서버가 세어 준다.
    /// </summary>
    /// <remarks>
    /// 「빠른 지시」의 카드가 이 값 하나로 클립 배지를 세운다. 건마다 첨부
    /// 목록을 따로 물으면 카드 열 장에 왕복이 열 번 더 난다.
    /// </remarks>
    public int FileCount { get; set; }

    /// <summary>
    /// <b>등록할 때만 싣는 값</b> — 미리 올려 둔 첨부의 번호들.
    /// </summary>
    /// <remarks>
    /// 고르는 순간 올려 두고(<see cref="AiTaskClient.StageFilesAsync"/>) 보낼
    /// 때 그 번호를 여기 실으면 서버가 작업에 묶는다. <b>조회로는 오지 않는다.</b>
    /// </remarks>
    public long[]? FileKeys { get; set; }

    public long? TargetKey { get; set; }

    /// <summary>읽기 전용 — 서버가 조인해 준다.</summary>
    public string? TargetNm { get; set; }

    /// <summary>읽기 전용.</summary>
    public string? TargetPath { get; set; }

    /// <summary>읽기 전용. 이 값이 거짓이면 「올리기」를 켤 수 없다.</summary>
    public bool TargetAllowPush { get; set; }

    /// <summary>
    /// 읽기 전용 — 대상이 허용한 AI 목록(쉼표로 이은 코드값).
    /// </summary>
    /// <remarks>
    /// 「이어서 지시」 창이 <b>고를 수 있는 AI 를 이 값으로 좁힌다</b>
    /// (<c>AiTaskActions</c>). 그 창은 건 하나만 들고 있어 대상 목록을 따로
    /// 읽지 않으므로, 이 값이 없으면 허용하지 않는 AI 가 칸에 뜨고
    /// <b>요청 단계에서야 거절된다.</b>
    /// </remarks>
    public string? TargetRunnerKinds { get; set; }

    public string? TargetRef { get; set; }
    public string? RunnerKind { get; set; } = "claude";

    public string? RequestFlag { get; set; } = "none";
    public string? TaskStatus { get; set; } = "idle";

    public int Priority { get; set; }
    public int TimeoutMinutes { get; set; } = 30;
    public int AttemptCount { get; set; }
    public int AttemptMax { get; set; } = 3;

    public bool AutoPush { get; set; }

    public bool NotifyEmail { get; set; }
    public bool NotifyPwa { get; set; }
    public string? NotifyTo { get; set; }
    public string? NotifyWhen { get; set; } = "always";
    public string? NotifyError { get; set; }
    public bool UserConfirmed { get; set; }

    /// <summary>
    /// <b>일반 사용자가 「AI 작업 요청」 화면에서 올린 건인가.</b>
    /// </summary>
    /// <remarks>
    /// 관리자 화면(「AI 작업」)이 이 값으로 <b>「시켜 달라고 올라온 것」을
    /// 가려낸다.</b> 그 건은 요청여부 <c>none</c> · 상태 <c>idle</c> · 대상 없음이라
    /// 관리자가 쓰다 만 제 글과 값이 하나도 다르지 않다 — 이 칸이 없으면 둘이
    /// 섞여 영영 안 돌아간다.
    /// <para>
    /// <b>돌고 난 뒤에도 남는다.</b> 「누가 부탁한 일이었나」는 사라지면 안 되는
    /// 사실이라, 관리자가 대상·AI 를 채워 실제로 시켜도 값이 바뀌지 않는다.
    /// </para>
    /// </remarks>
    public bool IsUserRequest { get; set; }

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

    /// <summary>
    /// <b>실패해 놓고 「대기」로 앉아 있는 것.</b> 지난 실행이 실패해서 서버가
    /// 다시 넣어 둔 상태다(<c>AiRunService.CompleteAsync</c> 의 재시도).
    /// </summary>
    /// <remarks>
    /// 상태값만 보면 <c>queued</c> 라 방금 보낸 건과 구별되지 않는데,
    /// <b>끝난 시각이 찍혀 있으면 한 번은 돌았다는 뜻</b>이다. 보낼 때
    /// (<c>RequestAsync</c>) 서버가 <c>finished_at</c> 을 지우므로, 이 값이
    /// 남아 있는 대기는 재시도뿐이다.
    /// </remarks>
    public bool IsRetrying => TaskStatus == "queued" && FinishedAt is not null;

    /// <summary>
    /// <b>아무도 집어 가지 않았다.</b> 실행기가 안 떠 있다는 뜻이고,
    /// 이 기능에서 가장 흔한 고장이다(설계 6.8).
    /// </summary>
    /// <remarks>
    /// 판정은 서버의 감시자가 한다 — 제한 시간(<c>AiTasks:PickupTimeoutSeconds</c>,
    /// 기본 60초)이 지나도록 안 집혀 가면 <c>last_error</c> 에 사유를 적는다
    /// (<c>AiStaleSweeper</c>). <b>상태는 <c>queued</c> 인 채로 둔다</b> —
    /// 늦게 뜬 실행기가 집어 갈 수 있어 여기서 「실패」로 못 박으면 그 뒤의
    /// 보고가 갈 곳을 잃기 때문이다. 그래서 <b>화면이 대신 말한다.</b>
    /// </remarks>
    public bool IsUnclaimed => TaskStatus == "queued"
        && FinishedAt is null
        && !string.IsNullOrWhiteSpace(LastError);

    /// <summary>사람이 읽는 상태 이름.</summary>
    /// <remarks>
    /// <b>「대기」 셋을 갈라 적는다.</b> 상태값은 셋 다 <c>queued</c> 지만
    /// 사람이 할 일이 다르다 — 방금 보낸 것은 기다리면 되고, 재시도는 이미
    /// 한 번 실패한 것이며, 안 집혀 간 것은 <b>실행기를 봐야 한다.</b>
    /// 셋을 같은 「대기」로 적으면 <b>실패한 건이 실패로 안 보인다.</b>
    /// </remarks>
    public string StatusText => TaskStatus switch
    {
        "idle" => "작성중",
        "queued" => IsRetrying ? "실패·재시도" : IsUnclaimed ? "계속대기중" : "대기",
        "preparing" => "준비중",
        "running" => "실행중",
        "succeeded" => "완료",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        _ => TaskStatus ?? string.Empty,
    };

    /// <summary>
    /// 배지 수식어. <b>이 저장소에 이미 있는 것(<c>jsini-badge--*</c>)을 쓴다.</b>
    /// </summary>
    /// <remarks>
    /// 색을 새로 만들지 않는다. 공통 배지는 테두리와 글자색만 쓰는 윤곽선
    /// 모양이라 어두운 테마에서도 그대로 읽히는데, 바탕색을 따로 주면
    /// <b>밝은 쪽 값이 굳어 어두운 테마에서 흰 칩이 된다.</b>
    /// </remarks>
    public string StatusTone => TaskStatus switch
    {
        "succeeded" => "on",
        "failed" or "timeout" or "interrupted" => "err",

        // 실패해서 다시 넣은 것과 안 집혀 간 것은 **빨강**이다. 기다리라는
        // 뜻의 주황으로 칠하면 훑을 때 그냥 지나간다 — 둘 다 사람이 봐야 한다.
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

    /// <summary>
    /// <b>적어만 두고 한 번도 요청하지 않은 건</b>(「작성중」). 지워도 잃는 것이
    /// 글뿐이라 상세 화면이 삭제 단추를 준다(<c>AiTaskViewPage</c>).
    /// </summary>
    /// <remarks>
    /// 서버가 사용자의 「내 요청 거둬들이기」에 거는 조건과 같다
    /// (<c>AiTaskService.DeleteUserRequestAsync</c> — <c>request_flag = 'none'</c>
    /// · <c>task_status = 'idle'</c>).
    /// </remarks>
    public bool IsDraft => TaskStatus == "idle" && RequestFlag == "none";

    /// <summary>더 움직이지 않는 상태인가.</summary>
    public bool IsFinal => TaskStatus is "succeeded" or "failed" or "timeout" or "canceled" or "interrupted";

    /// <summary>
    /// <b>수동 재시도가 가능한가.</b> 실패로 끝났고(failed/timeout), 자동 재시도 횟수를 모두 소진한 상태다.
    /// </summary>
    public bool CanManualRetry => (TaskStatus is "failed" or "timeout")
        && AttemptCount >= Math.Max(AttemptMax, 1)
        && !IsBusy;

    /// <summary>
    /// 「끝났는데 제목이 아직」. 서버가 실행을 보고 제목을 다시 짓는 동안
    /// 화면이 몇 초 더 따라가게 하는 값이다(<c>AiTaskTitler</c>).
    /// </summary>
    /// <remarks>
    /// <b>스스로 끝난다.</b> 도장(<c>title_run_key</c>)이 찍히거나 끝난 지 3분이
    /// 지나면 거짓이 된다 — 서버가 중간에 내려가도 조회가 영원히 돌지 않는다.
    /// 그 3분을 <see cref="DateTime.Now"/> 로 재는 것은 <b>서버가 적는 시각이
    /// 현지시각(<c>now()</c> · <c>Asia/Seoul</c>)이기 때문</b>이다. UTC 로 재면
    /// 아홉 시간 어긋나 이 값이 온종일 참으로 남고, 화면은 2초마다 서버를
    /// 두들기며 멈추지 않는다.
    /// </remarks>
    public bool TitlePending =>
        TitleAuto
        && LastRunKey is not null
        && TitleRunKey != LastRunKey
        && !IsBusy
        && FinishedAt is not null
        && FinishedAt.Value.AddMinutes(3) > DateTime.Now;

    /// <summary>걸린 시간. 아직 안 끝났으면 비어 있다.</summary>
    public string DurationText => DurationMs is null or 0
        ? string.Empty
        : TimeSpan.FromMilliseconds(DurationMs.Value).ToString(@"h\:mm\:ss");
}
