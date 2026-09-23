namespace ProjMngServer.Models;

/// <summary>
/// 대상 하나의 git 상태 스냅샷 — <c>projmng.ai_target_status</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>서버가 만든 값이 아니다.</b> ProjMngServer 는 컨테이너 안에서 돌고
/// 대상 경로는 호스트의 것이라 여기서 <c>git</c> 을 부를 수 없다 — 그 경로를
/// 실제로 볼 수 있는 것은 호스트에 상주하는 실행기뿐이다. 이 표는 실행기가
/// 주기적으로 적어 두고 가는 자리고, 화면은 그것을 읽는다.
/// </para>
/// <para>
/// 그래서 값에는 언제나 <see cref="ProbedAt"/> 이 붙는다. <b>화면은 그 시각을
/// 반드시 같이 보여 준다</b> — 안 보여 주면 사람이 옛 값을 지금 값으로 읽는다.
/// </para>
/// </remarks>
public class AiTargetStatus
{
    public long TargetKey { get; set; }

    /// <summary>어느 장비에서 본 것인가. 「경로가 없다」의 뜻이 여기에 달려 있다.</summary>
    public string? RunnerNm { get; set; }

    public bool PathExists { get; set; }

    /// <summary>
    /// <c>.git</c> 이 있나. <b>등록된 종류와 다를 수 있다</b> — repo 로 등록해
    /// 놓고 <c>.git</c> 이 없으면 그 대상의 작업은 준비 단계에서 죽는다.
    /// </summary>
    public bool IsRepo { get; set; }

    public string? Branch { get; set; }
    public string? Upstream { get; set; }

    /// <summary>원격보다 앞선 커밋 수. <b>fetch 하지 않고 센 값</b>이다.</summary>
    public int Ahead { get; set; }

    public int Behind { get; set; }

    public int Staged { get; set; }
    public int Unstaged { get; set; }
    public int Untracked { get; set; }
    public int Conflicted { get; set; }

    /// <summary>실행기가 정본에서 치운 변경이 쌓이는 곳(<c>ParkAsync</c>).</summary>
    public int StashCount { get; set; }

    public string? HeadSha { get; set; }
    public string? HeadSubject { get; set; }
    public string? HeadAuthor { get; set; }
    public DateTime? HeadDt { get; set; }

    public string? RemoteUrl { get; set; }

    /// <summary><c>git status --porcelain</c> 앞 몇 줄. 숫자가 못 하는 말을 한다.</summary>
    public string? DirtyFiles { get; set; }

    /// <summary>들여다보다 실패한 이유. <b>비어 있어야 정상이다.</b></summary>
    public string? ProbeError { get; set; }

    public DateTime? ProbedAt { get; set; }

    /// <summary>「지금 확인」이 찍어 둔 시각. 실행기가 보고하면서 지운다.</summary>
    public DateTime? ProbeReqDt { get; set; }
}

/// <summary>
/// 화면이 받는 한 줄 — 대상 + 그 대상의 git 상태.
/// </summary>
/// <remarks>
/// 둘을 나눠 내려 화면에서 짝짓게 하지 않는다. 대상은 있는데 상태가 없는
/// 경우(아직 한 번도 안 봤다)가 정상이라, 나눠 주면 <b>화면마다 바깥 조인을
/// 다시 짜게 된다.</b>
/// </remarks>
public sealed class AiTargetStatusRow : AiTargetStatus
{
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

    /// <summary>지금 이 대상에서 도는 실행. 있으면 상태가 흔들리는 중이다.</summary>
    public long? RunningRunKey { get; set; }
}

/// <summary>실행기가 「볼 대상」을 물었을 때 받아 가는 한 줄.</summary>
/// <remarks>
/// <b>경로만 준다.</b> 실행기가 알 필요가 없는 값(자격 이름 · push 설정)은
/// 싣지 않는다 — 이 응답은 장비 토큰 하나로 받아 가는 것이다.
/// </remarks>
public sealed class AiTargetProbe
{
    public long TargetKey { get; set; }
    public string? TargetNm { get; set; }
    public string? TargetPath { get; set; }
    public string? TargetKind { get; set; }

    /// <summary>
    /// 주기를 기다리지 말고 <b>지금</b> 보라는 표시. 화면의 「지금 확인」이 찍는다.
    /// </summary>
    public bool ProbeRequested { get; set; }

    /// <summary>마지막으로 본 시각. 실행기가 주기를 이것으로 잰다.</summary>
    public DateTime? ProbedAt { get; set; }
}
