using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 배포 실행 상태값.
/// </summary>
/// <remarks>
/// 문자열로 두는 이유: 이력 표를 SQL 로 직접 들여다보는 일이 잦고,
/// 숫자 코드는 그때 무슨 뜻인지 알 수 없다.
/// </remarks>
public static class ReleaseRunStatus
{
    /// <summary>큐에 넣었고 아직 아무도 집어가지 않았다.</summary>
    /// <remarks>
    /// 개선 전 화면이 감추고 있던 상태다. 소비자가 안 떠 있으면 여기서 멈춘다.
    /// </remarks>
    public const string Queued = "queued";

    /// <summary>배포 장비가 집어가서 돌고 있다.</summary>
    public const string Running = "running";

    /// <summary>스크립트가 0 으로 끝났다.</summary>
    public const string Succeeded = "succeeded";

    /// <summary>스크립트가 0 이 아닌 코드로 끝났다.</summary>
    public const string Failed = "failed";

    /// <summary>제한 시간을 넘겨도 소식이 없다. 소비자가 죽었거나 없다.</summary>
    public const string Timeout = "timeout";

    /// <summary>
    /// 보고를 하지 않는 대상에 요청만 보냈다. 개선 전과 같은 동작이다.
    /// </summary>
    /// <remarks>
    /// 배포 장비의 큐 소비자는 이 저장소 밖에 있고 아직 래퍼를 붙이지 않았다.
    /// 붙이기 전에는 보고가 올 수 없으므로 성공/실패를 아는 척하지 않는다.
    /// 대상별 <c>ReportsProgress</c> 를 켜면 queued → running → succeeded 로 간다.
    /// </remarks>
    public const string Dispatched = "dispatched";

    /// <summary>더 이상 바뀌지 않는 상태인가.</summary>
    public static bool IsFinal(string status) =>
        status is Succeeded or Failed or Timeout or Dispatched;
}

/// <summary>
/// 배포 실행 한 건 (<c>/portal/release</c>)
/// </summary>
/// <remarks>
/// 예전에는 이런 기록이 없었다. 화면이 <c>setTimeout</c> 으로 단계를 초록색으로 찍고
/// 서버는 큐에 넣고 잊었다. 그래서 스크립트가 실패해도 화면은 전부 초록이었고,
/// 누가 언제 무엇을 배포했는지 남는 곳이 없었다.
///
/// <para>
/// 없던 것은 <b>run id</b> 였다. 요청 한 건을 행 하나로 만들면 배포 장비의 래퍼가
/// 그 id 로 진행 상황을 되돌려 보고할 수 있고, 화면은 그 id 를 폴링한다.
/// 화면을 새로 고쳐도 이어 볼 수 있다.
/// </para>
/// </remarks>
[Table("release_runs", Schema = "scom")]
public class ReleaseRun : BaseEntity<string>
{
    public ReleaseRun()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>어떤 대상을 배포했나. 설정(Release:Targets)의 Key 다.</summary>
    [Required]
    [Column("target_key")]
    public string TargetKey { get; set; } = string.Empty;

    /// <summary>
    /// 그때의 표시 이름. 설정이 나중에 바뀌어도 이력은 그대로 읽혀야 하므로 박아 둔다.
    /// </summary>
    [Required]
    [Column("target_name")]
    public string TargetName { get; set; } = string.Empty;

    /// <summary>그때 실행을 요청한 스크립트 경로.</summary>
    [Column("script_path")]
    public string? ScriptPath { get; set; }

    /// <summary>스크립트에 넘긴 인자 (JSON 배열 문자열)</summary>
    [Column("args")]
    public string? Args { get; set; }

    /// <summary><see cref="ReleaseRunStatus"/> 참고.</summary>
    [Required]
    [Column("status")]
    public string Status { get; set; } = ReleaseRunStatus.Queued;

    /// <summary>
    /// 이 대상이 보고를 하기로 되어 있었나.
    /// </summary>
    /// <remarks>
    /// 나중에 설정을 바꿔도 지난 이력의 해석이 흔들리지 않게 그때 값을 박아 둔다.
    /// 꺼진 대상의 'dispatched' 는 실패가 아니라 "알 수 없음" 이다.
    /// </remarks>
    [Column("reports_progress")]
    public bool ReportsProgress { get; set; }

    /// <summary>요청한 사람. 게이트웨이가 넘긴 로그인 아이디다.</summary>
    [Column("requested_by")]
    public string? RequestedBy { get; set; }

    /// <summary>배포 장비가 처음 보고를 보내 온 시각. 큐에서 집어간 시각이다.</summary>
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>스크립트 종료 코드. 0 이면 성공으로 본다.</summary>
    [Column("exit_code")]
    public int? ExitCode { get; set; }

    /// <summary>마지막으로 지나간 단계 이름. 스크립트가 <c>##STEP ...</c> 을 찍은 경우에만 찬다.</summary>
    [Column("current_step")]
    public string? CurrentStep { get; set; }

    /// <summary>사람이 읽을 최종 한 줄. 실패 사유가 여기 들어간다.</summary>
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 배포 후 대상이 스스로 알려 준 버전. 설정에 <c>VersionUrl</c> 이 있는 대상만 채워진다.
    /// </summary>
    /// <remarks>
    /// 종료 코드가 0 이어도 이 값이 안 바뀌면 "돌기는 했는데 반영이 안 됐다" 를 알 수 있다.
    /// 예전 화면은 포털 자신의 <c>/version.json</c> 을 읽었는데, 다른 시스템을 배포할 때는
    /// 아무 의미가 없는 숫자였다.
    /// </remarks>
    [Column("deployed_version")]
    public string? DeployedVersion { get; set; }

    /// <summary>이 시간을 넘기면 timeout 으로 본다 (초).</summary>
    [Column("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// 배포 장비가 보고할 때 쓰는 1회용 토큰. 계정 인증이 아니라 실행 인증이다.
    /// </summary>
    /// <remarks>
    /// 끝나면 지운다. 남겨 두면 이미 끝난 run 에 아무나 로그를 덧붙일 수 있다.
    /// </remarks>
    [Column("callback_token")]
    public string? CallbackToken { get; set; }

    /// <summary>지금까지 받은 이벤트의 마지막 순번. 화면이 <c>sinceSeq</c> 로 이어 받는다.</summary>
    [Column("last_seq")]
    public int LastSeq { get; set; }

    /// <summary>진행 로그</summary>
    public ICollection<ReleaseRunEvent>? Events { get; set; }
}

/// <summary>
/// 배포 진행 로그 한 줄
/// </summary>
/// <remarks>
/// 배포 장비가 스크립트의 stdout 을 한 줄씩 보내 온 것이다.
/// <b>이제 화면에 보이는 줄은 전부 실제로 일어난 일이다.</b>
/// </remarks>
[Table("release_run_events", Schema = "scom")]
public class ReleaseRunEvent : BaseEntity<string>
{
    public ReleaseRunEvent()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    [Column("run_id")]
    public string RunId { get; set; } = string.Empty;

    [ForeignKey(nameof(RunId))]
    public virtual ReleaseRun? Run { get; set; }

    /// <summary>
    /// run 안에서의 순번. 화면이 <c>sinceSeq</c> 로 이어 받으므로 빈틈이 없어야 한다.
    /// </summary>
    [Column("seq")]
    public int Seq { get; set; }

    /// <summary>info / stdout / step / warn / error / result</summary>
    [Required]
    [Column("level")]
    public string Level { get; set; } = "stdout";

    /// <summary><c>level</c> 이 step 인 경우의 단계 이름</summary>
    [Column("step")]
    public string? Step { get; set; }

    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;
}
