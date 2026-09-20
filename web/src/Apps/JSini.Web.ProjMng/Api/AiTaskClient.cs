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
        string? keyword = null, bool? userConfirmed = null, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(flag)) query.Add($"flag={Uri.EscapeDataString(flag)}");
        if (targetKey is not null) query.Add($"targetKey={targetKey}");
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (userConfirmed is not null) query.Add($"userConfirmed={userConfirmed}");

        return gateway.GetListAsync<AiTaskDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

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
    public Task<AiTaskDto?> ContinueAsync(
        long taskKey, string addition, CancellationToken ct = default)
        => gateway.PostAsync<AiTaskDto>($"{Url}/{taskKey}/continue", new { addition }, ct);

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

    /// <summary>실행 이력. 최근 것이 앞이다.</summary>
    public Task<IReadOnlyList<AiTaskRunDto>> RunsAsync(long taskKey, CancellationToken ct = default)
        => gateway.GetListAsync<AiTaskRunDto>($"{Url}/{taskKey}/runs", ct);

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

    public string? Contents { get; set; }
    public string? ContentFormat { get; set; } = "markdown";

    public long? TargetKey { get; set; }

    /// <summary>읽기 전용 — 서버가 조인해 준다.</summary>
    public string? TargetNm { get; set; }

    /// <summary>읽기 전용.</summary>
    public string? TargetPath { get; set; }

    /// <summary>읽기 전용. 이 값이 거짓이면 「올리기」를 켤 수 없다.</summary>
    public bool TargetAllowPush { get; set; }

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

    /// <summary>더 움직이지 않는 상태인가.</summary>
    public bool IsFinal => TaskStatus is "succeeded" or "failed" or "timeout" or "canceled" or "interrupted";

    /// <summary>
    /// <b>수동 재시도가 가능한가.</b> 실패로 끝났고(failed/timeout), 자동 재시도 횟수를 모두 소진한 상태다.
    /// </summary>
    public bool CanManualRetry => (TaskStatus is "failed" or "timeout")
        && AttemptCount >= Math.Max(AttemptMax, 1)
        && !IsBusy;

    /// <summary>걸린 시간. 아직 안 끝났으면 비어 있다.</summary>
    public string DurationText => DurationMs is null or 0
        ? string.Empty
        : TimeSpan.FromMilliseconds(DurationMs.Value).ToString(@"h\:mm\:ss");
}
