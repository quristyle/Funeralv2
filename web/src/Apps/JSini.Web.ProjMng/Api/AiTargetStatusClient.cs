using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// AI 작업 대상의 git 상태 — <c>projmng/ai-targets/status</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>여기서 읽는 것은 언제나 스냅샷이다.</b> 서버는 대상 경로를 볼 수 없고
/// (컨테이너 안에서 돌고 경로는 호스트의 것이다) 실제로 들여다보는 것은
/// 호스트에 상주하는 실행기다. 그것이 몇 분마다 적어 두고 가는 값을 읽는다.
/// </para>
/// <para>
/// 그래서 화면은 <c>ProbedAt</c> 을 <b>반드시 같이 보여 준다</b> —
/// 안 보여 주면 옛 값을 지금 값으로 읽는다.
/// </para>
/// </remarks>
public sealed class AiTargetStatusClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-targets";

    public Task<IReadOnlyList<AiTargetStatusDto>> ListAsync(
        bool onlyEnabled = false, CancellationToken ct = default)
        => gateway.GetListAsync<AiTargetStatusDto>(
            onlyEnabled ? $"{Url}/status?onlyEnabled=true" : $"{Url}/status", ct);

    /// <summary>
    /// 「지금 확인」. <b>이 호출이 끝난다고 값이 바뀌어 있지 않다</b> —
    /// 보라고 적어 두는 것뿐이고, 보는 것은 실행기다.
    /// </summary>
    public Task ProbeAsync(long targetKey, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/{targetKey}/probe", new { }, ct);
}

/// <summary>대상 한 건과 그 git 상태.</summary>
public sealed class AiTargetStatusDto
{
    public long TargetKey { get; set; }

    public string? TargetNm { get; set; }

    /// <summary>등록된 종류 — <c>repo</c> · <c>folder</c>.</summary>
    public string? TargetKind { get; set; }

    public string? TargetPath { get; set; }
    public string? DefaultRef { get; set; }
    public string? IsolationMode { get; set; }

    /// <summary>이 대상을 집을 수 있는 장비. 비면 아무 장비나.</summary>
    public string? TargetRunnerNm { get; set; }

    public bool AllowPush { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>지금 이 대상에서 도는 실행.</summary>
    public long? RunningRunKey { get; set; }

    // ── 실행기가 보고 온 것 ─────────────────────────────────

    /// <summary>어느 장비에서 본 것인가.</summary>
    public string? RunnerNm { get; set; }

    public bool PathExists { get; set; }

    /// <summary><c>.git</c> 이 있나. <b>등록된 종류와 다를 수 있다.</b></summary>
    public bool IsRepo { get; set; }

    public string? Branch { get; set; }
    public string? Upstream { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }

    public int Staged { get; set; }
    public int Unstaged { get; set; }
    public int Untracked { get; set; }
    public int Conflicted { get; set; }
    public int StashCount { get; set; }

    public string? HeadSha { get; set; }
    public string? HeadSubject { get; set; }
    public string? HeadAuthor { get; set; }
    public DateTime? HeadDt { get; set; }

    public string? RemoteUrl { get; set; }
    public string? DirtyFiles { get; set; }
    public string? ProbeError { get; set; }

    public DateTime? ProbedAt { get; set; }

    /// <summary>「지금 확인」이 찍혀 아직 답이 안 온 상태.</summary>
    public DateTime? ProbeReqDt { get; set; }

    // ── 화면이 쓰는 파생값 ──────────────────────────────────
    //
    // 화면에 계산식을 흩뿌리지 않는다. 표의 칸과 아래 상세가 같은 값을
    // 두 군데서 다르게 세는 일을 막으려는 것이다.

    /// <summary>한 번이라도 본 적이 있나.</summary>
    public bool Probed => ProbedAt is not null;

    /// <summary>「지금 확인」을 눌러 놓고 답을 기다리는 중.</summary>
    public bool ProbePending => ProbeReqDt is not null;

    /// <summary>정리되지 않은 칸의 수. 어느 갈래든 하나로 센다.</summary>
    public int DirtyCount => Staged + Unstaged + Untracked + Conflicted;

    public bool IsClean => DirtyCount == 0;

    /// <summary>
    /// 등록된 종류와 실제가 어긋났나.
    /// </summary>
    /// <remarks>
    /// <b>이것을 보자는 것이 이 화면의 절반이다.</b> <c>repo</c> 로 등록해
    /// 놓고 <c>.git</c> 이 없으면 그 대상의 작업은 준비 단계에서
    /// 「저장소로 등록된 대상인데 .git 이 없습니다」로 죽는다 — 그때까지는
    /// 아무 데도 안 보인다.
    /// </remarks>
    public bool KindMismatch => Probed && PathExists && TargetKind == "repo" && !IsRepo;

    /// <summary>
    /// 한 줄 요약. <b>표의 「상태」 칸에 그대로 들어간다.</b>
    /// </summary>
    /// <remarks>
    /// 순서가 곧 심각도다 — 위에서 걸리는 것이 사람이 먼저 봐야 할 것이다.
    /// </remarks>
    public string StateText =>
        !Probed ? "확인된 적 없음"
        : !PathExists ? "경로 없음"
        : ProbeError is { Length: > 0 } ? "확인 실패"
        : KindMismatch ? "저장소 아님"
        : !IsRepo ? "폴더"
        : Conflicted > 0 ? "충돌"
        : !IsClean ? $"변경 {DirtyCount}건"
        : Ahead > 0 && Behind > 0 ? $"엇갈림 +{Ahead} -{Behind}"
        : Ahead > 0 ? $"앞섬 +{Ahead}"
        : Behind > 0 ? $"뒤처짐 -{Behind}"
        : "깨끗함";

    /// <summary>
    /// 그 상태의 색. 배지와 줄 강조가 같은 값을 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>「앞섬」은 경고가 아니다.</b> 작업이 만든 커밋이 아직 안 올라간
    /// 정상적인 모습일 수 있다 — 빨강으로 칠하면 늘 빨간 화면이 되고,
    /// 늘 빨간 화면은 아무도 안 본다.
    /// </remarks>
    public string Tone =>
        !Probed ? "off"
        : !PathExists || ProbeError is { Length: > 0 } || KindMismatch || Conflicted > 0 ? "err"
        : !IsRepo ? "off"
        : !IsClean || Behind > 0 ? "warn"
        : "on";

    /// <summary>앞섬·뒤처짐 한 칸. 추적하는 곳이 없으면 그렇게 적는다.</summary>
    public string SyncText =>
        !IsRepo ? string.Empty
        : string.IsNullOrWhiteSpace(Upstream) ? "추적 없음"
        : $"+{Ahead} / -{Behind}";

    /// <summary>커밋을 짧게. 목록에서 40글자짜리 sha 는 자리만 먹는다.</summary>
    public string ShortSha => HeadSha is { Length: > 7 } ? HeadSha[..7] : HeadSha ?? string.Empty;

    public string KindText => TargetKind == "folder" ? "폴더" : "저장소";
}
