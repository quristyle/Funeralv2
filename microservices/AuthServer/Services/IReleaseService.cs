using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 배포 실행 요청 결과 구분.
/// </summary>
public enum ReleaseTriggerOutcome
{
    /// <summary>큐에 넣었다.</summary>
    Ok,

    /// <summary>배포 실행 권한이 없다.</summary>
    Forbidden,

    /// <summary>설정에 없는 대상이거나 설정이 반쪽이다.</summary>
    Invalid,

    /// <summary>같은 대상이 이미 돌고 있다.</summary>
    Conflict,

    /// <summary>큐에 넣지 못했다 (브로커 문제).</summary>
    Failed
}

/// <summary>
/// 배포 장비가 보내 온 보고의 처리 결과 구분.
/// </summary>
/// <remarks>
/// 래퍼는 셸 스크립트다. 상태 코드로 "다시 보내라" 와 "그만 보내라" 를
/// 구분할 수 있어야 한다 — 구분이 없으면 놓친 로그를 조용히 잃거나
/// 끝난 run 에 영원히 재시도한다.
/// </remarks>
public enum ReleaseReportOutcome
{
    /// <summary>받았다.</summary>
    Ok,

    /// <summary>그런 실행이 없다. 래퍼는 그만 보낸다.</summary>
    NotFound,

    /// <summary>토큰이 맞지 않거나 이미 끝난 실행이다. 래퍼는 그만 보낸다.</summary>
    Rejected,

    /// <summary>일시적인 충돌이다. 래퍼는 잠시 뒤 다시 보낸다.</summary>
    Retry
}

/// <summary>
/// 배포 실행 서비스.
/// </summary>
/// <remarks>
/// 배포는 여기서 하지 않는다. "이 스크립트를 돌려 달라" 를 큐에 넣고,
/// 배포 장비의 래퍼가 진행 상황을 <see cref="ReportEventsAsync"/> 로 되돌려 보고한다.
///
/// <para>
/// 예전에는 큐에 넣고 잊었다. 그래서 화면이 진행 단계를 스스로 만들어 내
/// 실패한 배포도 초록색으로 보여 주었다. 이제 화면에 보이는 줄은 전부
/// 실제로 일어난 일이다.
/// </para>
/// </remarks>
public interface IReleaseService
{
    /// <summary>
    /// 배포 대상 목록. 각 대상의 진행 중인 실행과 최근 실행 요약을 함께 담는다.
    /// </summary>
    Task<ReleaseTargetListDto> GetTargetsAsync(string userId);

    /// <summary>
    /// 배포 실행 요청.
    /// </summary>
    /// <returns>구분과 결과. 구분에 따라 엔드포인트가 상태 코드를 정한다.</returns>
    Task<(ReleaseTriggerOutcome Outcome, ReleaseResultDto Result)> TriggerAsync(
        string key, string userId);

    /// <summary>
    /// 실행 한 건의 상태와 <paramref name="sinceSeq"/> 이후의 로그.
    /// </summary>
    /// <param name="runId">실행 아이디</param>
    /// <param name="sinceSeq">이 순번보다 큰 로그만 담는다. 화면이 이어 받는 자리다.</param>
    Task<ReleaseRunDto?> GetRunAsync(string runId, int sinceSeq);

    /// <summary>최근 실행 이력.</summary>
    Task<List<ReleaseRunDto>> GetRunsAsync(int take);

    /// <summary>
    /// 배포 장비가 보내 온 보고를 저장한다.
    /// </summary>
    /// <remarks>
    /// <b>계정 인증이 아니라 실행 인증이다.</b> 게이트웨이의 <c>/api/auth/**</c> 는
    /// Anonymous 라 이 요청에는 로그인 정보가 없다. run 별로 발급한 1회용
    /// <c>callback_token</c> 이 맞는지만 본다.
    /// </remarks>
    /// <param name="runId">실행 아이디</param>
    /// <param name="token">요청 헤더로 온 1회용 토큰</param>
    /// <param name="report">보고 내용</param>
    Task<(ReleaseReportOutcome Outcome, ReportReleaseEventsResultDto Result)> ReportEventsAsync(
        string runId, string? token, ReportReleaseEventsDto report);
}
