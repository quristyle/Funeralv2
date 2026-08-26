namespace AuthServer.DTOs;

/// <summary>
/// 배포 대상 한 건.
/// </summary>
/// <remarks>
/// 예전에는 헬프데스크 화면에 'jin114 배포' / 'goldb 배포' 두 버튼이 박혀 있었다.
/// JSini 포털은 여러 시스템의 배포를 관장하므로, 대상을 설정으로 뺐다.
/// 대상을 늘리려면 appsettings 의 Release:Targets 에 항목을 더하면 된다.
/// </remarks>
public class ReleaseTargetDto
{
    /// <summary>호출에 쓰는 식별자 (URL 에 들어간다)</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>화면에 보일 이름</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>무엇을 배포하는지에 대한 설명</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 이 대상이 진행 상황을 되돌려 보고하나.
    /// </summary>
    /// <remarks>
    /// 꺼져 있으면 화면은 "요청을 보냈다" 까지만 말한다. 성공/실패를 아는 척하지 않는다.
    /// 배포 장비에 래퍼(<c>deploy/release-consumer/</c>)를 붙인 뒤에 켠다.
    /// </remarks>
    public bool ReportsProgress { get; set; }

    /// <summary>이 시간을 넘겨도 소식이 없으면 timeout 으로 본다 (초).</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// 대략의 소요 시간(초). 화면이 "보통 N초쯤 걸립니다" 로 안내한다.
    /// </summary>
    /// <remarks>
    /// 진행률을 만들어 내는 데 쓰지 않는다. 예전 화면이 이 값으로 가짜 단계를
    /// 초록색으로 찍고 있었다 — 그 계산이 이번에 사라진 것이다.
    /// </remarks>
    public int EstimatedSeconds { get; set; }

    /// <summary>지금 돌고 있는 실행. 없으면 null. 화면이 새로 들어와도 이어 볼 수 있게 한다.</summary>
    public string? ActiveRunId { get; set; }

    /// <summary>가장 최근 실행 요약. 화면이 "언제 누가 무엇을" 을 바로 보여 준다.</summary>
    public ReleaseRunDto? LastRun { get; set; }
}

/// <summary>
/// 배포 대상 목록 응답.
/// </summary>
/// <remarks>
/// <see cref="CanRelease"/> 를 함께 담는다. 화면의 <c>v-perm</c> 은 버튼을 숨기는
/// 장치일 뿐이고 실제 판정은 서버가 한다 — 화면이 그 판정을 그대로 쓰게 해서
/// "버튼은 보이는데 누르면 403" 이 생기지 않도록 한다.
/// </remarks>
public class ReleaseTargetListDto
{
    public List<ReleaseTargetDto> Items { get; set; } = new();

    /// <summary>배포를 실행할 수 있나 (<c>/portal/release</c> 의 can_cust1)</summary>
    public bool CanRelease { get; set; }

    /// <summary>
    /// 보고를 켠 대상이 있는데 콜백 주소가 비어 있으면 채운다.
    /// 설정이 반쪽만 된 것을 화면에서 알 수 있어야 한다.
    /// </summary>
    public string? ConfigWarning { get; set; }
}

/// <summary>
/// 배포 실행 한 건의 상태.
/// </summary>
public class ReleaseRunDto
{
    public string Id { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;

    /// <summary>queued / running / succeeded / failed / timeout / dispatched</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>이 대상이 보고를 하기로 되어 있었나. 'dispatched' 의 뜻을 화면이 정확히 쓰게 한다.</summary>
    public bool ReportsProgress { get; set; }

    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? CurrentStep { get; set; }
    public string? Message { get; set; }

    /// <summary>배포 후 대상이 스스로 알려 준 버전. VersionUrl 을 둔 대상만 찬다.</summary>
    public string? DeployedVersion { get; set; }

    public string? ScriptPath { get; set; }

    /// <summary>지금까지 받은 마지막 이벤트 순번. 화면이 이어 받을 자리다.</summary>
    public int LastSeq { get; set; }

    /// <summary>더 폴링할 필요가 있나</summary>
    public bool IsFinal { get; set; }

    /// <summary>요청한 이벤트 구간 (<c>sinceSeq</c> 이후). 상태만 물었으면 빈 목록이다.</summary>
    public List<ReleaseRunEventDto> Events { get; set; } = new();
}

/// <summary>
/// 진행 로그 한 줄.
/// </summary>
public class ReleaseRunEventDto
{
    public int Seq { get; set; }

    /// <summary>info / stdout / step / warn / error / result</summary>
    public string Level { get; set; } = string.Empty;

    public string? Step { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>서버가 받은 시각. 배포 장비의 시계를 믿지 않는다.</summary>
    public DateTime At { get; set; }
}

/// <summary>
/// 배포 실행 요청 결과.
/// </summary>
public class ReleaseResultDto
{
    /// <summary>큐에 넣었나</summary>
    public bool Queued { get; set; }

    public string TargetKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 만들어진 실행 아이디. 화면이 이 값으로 진행 상황을 폴링한다.
    /// 요청이 거절되면 null 이다.
    /// </summary>
    public string? RunId { get; set; }
}

/// <summary>
/// 배포 장비가 보내 오는 보고 (콜백 본문).
/// </summary>
/// <remarks>
/// 한 줄에 한 번씩 부르지 않는다 — 래퍼가 여러 줄을 모아 한 번에 보낸다.
/// 줄마다 HTTP 요청을 하면 로그가 긴 배포에서 요청 수천 건이 된다.
/// </remarks>
public class ReportReleaseEventsDto
{
    public List<ReportReleaseEventLineDto> Events { get; set; } = new();

    /// <summary>이번이 마지막 보고인가. 참이면 <see cref="ExitCode"/> 로 성공/실패를 정한다.</summary>
    public bool Final { get; set; }

    /// <summary>스크립트 종료 코드. <see cref="Final"/> 일 때만 본다.</summary>
    public int? ExitCode { get; set; }
}

/// <summary>보고된 로그 한 줄.</summary>
public class ReportReleaseEventLineDto
{
    /// <summary>info / stdout / step / warn / error. 비우면 stdout 으로 본다.</summary>
    public string? Level { get; set; }

    public string? Step { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// 콜백 응답.
/// </summary>
public class ReportReleaseEventsResultDto
{
    public bool Ok { get; set; }

    /// <summary>실제로 저장한 줄 수</summary>
    public int Accepted { get; set; }

    /// <summary>
    /// 참이면 래퍼는 더 보내지 않는다. 이미 끝난 run 이거나 로그 한도를 넘었다.
    /// </summary>
    public bool Stop { get; set; }

    public string? Message { get; set; }
}

/// <summary>
/// 배포 설정. appsettings 의 Release 섹션과 대응한다.
/// </summary>
public class ReleaseOptions
{
    /// <summary>메시지 큐 호스트. 큐 소비자가 도는 장비다.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>스크립트 실행 요청을 넣을 큐 이름</summary>
    /// <remarks>
    /// 기본값을 <c>run_script</c> 로 둔다. 배포 장비의 소비자가 지금 그 큐를 보고 있고,
    /// 이름을 바꾸면 래퍼를 붙이기 전에 배포가 멈춘다.
    ///
    /// <para>
    /// 이 큐는 헬프데스크의 메일 발송과 공유한다. 전용 큐로 옮기는 것은
    /// 배포 장비 쪽 소비자를 함께 고쳐야 하는 일이라 판단 대기로 남겼다
    /// (28-release-tool.md 참고).
    /// </para>
    /// </remarks>
    public string QueueName { get; set; } = "run_script";

    /// <summary>
    /// 큐를 durable 로 선언할지.
    /// </summary>
    /// <remarks>
    /// <b>기본값 false 를 함부로 바꾸지 않는다.</b> <c>run_script</c> 는 이미 non-durable 로
    /// 존재하고, durable 로 다시 선언하면 RabbitMQ 가 PRECONDITION_FAILED 를 낸다.
    /// 전용 큐로 옮길 때 함께 켠다.
    /// </remarks>
    public bool Durable { get; set; }

    /// <summary>
    /// 배포 장비가 진행 상황을 보고할 주소의 앞부분. 게이트웨이 주소다.
    /// </summary>
    /// <remarks>
    /// 예: <c>http://10.0.0.5:5265/api/auth</c>
    ///
    /// <para>
    /// 게이트웨이의 <c>/api/auth/**</c> 는 Anonymous 라 별도 라우트가 필요 없다.
    /// 인증은 run 별로 발급한 1회용 토큰으로 한다 — 계정 인증이 아니라 실행 인증이다.
    /// </para>
    ///
    /// <para>
    /// 비어 있으면 보고를 켠 대상은 실행을 거절한다. 조용히 못 받는 것보다
    /// 왜 안 되는지 말하는 편이 낫다.
    /// </para>
    /// </remarks>
    public string? CallbackBaseUrl { get; set; }

    /// <summary>
    /// 큐에 넣고 이 시간 안에 아무도 집어가지 않으면 timeout 으로 본다 (초).
    /// </summary>
    /// <remarks>
    /// 스크립트가 오래 도는 것과 <b>아무도 집어가지 않는 것</b>은 다른 문제다.
    /// 후자는 소비자가 안 떠 있다는 뜻이라 빨리 알려 주는 편이 낫다.
    /// 보고를 켠 대상에만 적용된다.
    /// </remarks>
    public int PickupTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// run 하나에 저장할 로그 줄의 상한.
    /// </summary>
    /// <remarks>
    /// 스크립트가 폭주해 수십만 줄을 찍으면 표가 부풀고 화면도 못 견딘다.
    /// 넘으면 한 줄로 알려 주고 그 뒤는 버린다 — 조용히 자르지 않는다.
    /// </remarks>
    public int MaxEventsPerRun { get; set; } = 5000;

    /// <summary>로그 한 줄의 길이 상한 (글자). 넘으면 잘라서 표시한다.</summary>
    public int MaxEventLength { get; set; } = 4000;

    public List<ReleaseTargetOption> Targets { get; set; } = new();
}

/// <summary>
/// 배포 대상 하나의 설정.
/// </summary>
public class ReleaseTargetOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>실제로 배포를 하는 셸 스크립트의 절대 경로</summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>스크립트에 넘길 인자</summary>
    public List<string> Args { get; set; } = new();

    /// <summary>
    /// 배포 장비에 둔 래퍼 스크립트의 절대 경로 (<c>release-run.sh</c>).
    /// </summary>
    /// <remarks>
    /// <b>이 값이 있으면 배포 장비의 큐 소비자를 고치지 않아도 진행 보고를 받을 수 있다.</b>
    ///
    /// <para>
    /// 지금 소비자는 메시지의 <c>script</c> 를 <c>args</c> 와 함께 실행하는 일만 한다.
    /// 그래서 <c>script</c> 자리에 래퍼를 넣고 <c>args</c> 앞에 run 정보를 끼워 보내면,
    /// 소비자는 예전과 똑같이 "스크립트를 실행" 하는데 그 스크립트가 래퍼가 된다.
    /// </para>
    ///
    /// <code>
    ///   script = WrapperPath
    ///   args   = [runId, token, callbackUrl, ScriptPath, ...Args]
    /// </code>
    ///
    /// <para>
    /// 비워 두면 <c>script</c> 는 <see cref="ScriptPath"/> 그대로 가고, 진행 보고는
    /// 소비자가 직접 <c>runId</c>·<c>callbackUrl</c>·<c>token</c> 을 읽어 처리해야 한다.
    /// </para>
    ///
    /// <para>
    /// <b>토큰이 인자로 들어가므로 배포 장비에서 <c>ps</c> 로 보인다.</b> 배포 장비에
    /// 로그인할 수 있는 사람은 이미 스크립트를 직접 돌릴 수 있고, 토큰은 그 run 하나에만
    /// 쓰이며 끝나면 무효가 되므로 이 정도로 두었다.
    /// </para>
    /// </remarks>
    public string? WrapperPath { get; set; }

    /// <summary>
    /// 이 대상이 진행 상황을 되돌려 보고하나.
    /// </summary>
    /// <remarks>
    /// <b>기본 꺼짐.</b> 배포 장비의 큐 소비자는 이 저장소 밖에 있고, 래퍼를 붙이기
    /// 전에는 보고가 올 수 없다. 꺼져 있으면 개선 전과 똑같이 동작하되 화면이
    /// "요청을 보냈다" 까지만 말한다(성공했다고 하지 않는다).
    ///
    /// <para>래퍼를 붙인 뒤 대상별로 켠다. 켜는 순서를 대상마다 따로 갈 수 있다.</para>
    /// </remarks>
    public bool ReportsProgress { get; set; }

    /// <summary>이 시간을 넘겨도 소식이 없으면 timeout 으로 본다 (초).</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// 배포가 끝난 뒤 버전을 확인할 주소.
    /// </summary>
    /// <remarks>
    /// 예: <c>https://jin114.co.kr/version.json</c>
    ///
    /// <para>
    /// JSON 이면 <c>version</c> 키를, 아니면 본문 앞부분을 그대로 읽는다.
    /// 종료 코드가 0 이어도 이 값이 안 바뀌면 "돌기는 했는데 반영이 안 됐다" 를 알 수 있다.
    /// </para>
    ///
    /// <para>
    /// 예전 화면은 <b>포털 자신의</b> <c>/version.json</c> 을 브라우저에서 읽었다.
    /// 다른 시스템을 배포할 때는 아무 의미가 없는 숫자였다.
    /// </para>
    /// </remarks>
    public string? VersionUrl { get; set; }

    /// <summary>대략의 소요 시간(초). 화면이 안내에 쓴다.</summary>
    public int EstimatedSeconds { get; set; } = 20;
}
