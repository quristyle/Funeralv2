namespace ProjMngServer.Models;

// WBS 대시보드가 쓰는 자료 타입. 원장은 `projmng.wbs_work` 다.
//
// 이름이 전부 `WbsBoard` 로 시작하는 까닭 — 이 서버에는 이미 <see cref="WbsItem"/>
// 과 <see cref="WbsSummary"/> 가 있다. **이름만 같고 다른 물건**이다.
// 그쪽은 프로젝트별 공정표(`projmng.dev_wbs`, 1행 = 1공정)이고 이쪽은 화면 단위
// 원장(1행 = 1화면)이다. 짧게 `WbsSummary` 로 지으면 그 둘이 부딪힌다.

/// <summary>요약 카드 한 벌.</summary>
public sealed class WbsBoardSummary
{
    public int Total { get; set; }

    /// <summary>기준일이 들어 있는 건수. 나머지는 집계에서 빠진다.</summary>
    public int Dated { get; set; }

    public int Modules { get; set; }
    public int Users { get; set; }

    public string? MinDt { get; set; }
    public string? MaxDt { get; set; }

    /// <summary>담당자 완료(<c>complate_yn = 'o'</c>).</summary>
    public int Done { get; set; }

    /// <summary>개발자 완료(<c>complate_real_yn = 'o'</c>).</summary>
    public int DoneReal { get; set; }

    /// <summary>
    /// 대형 트랙. <b>위 두 수치에 섞지 않는다</b> — 같은 줄을 다른 잣대로 한 번 더
    /// 세는 것이라 합치면 완료율이 100 을 넘는다.
    /// </summary>
    public int DoneBig { get; set; }

    /// <inheritdoc cref="DoneBig"/>
    public int DoneRealBig { get; set; }
}

/// <summary>월·주 단위 건수 한 칸.</summary>
public sealed class WbsBoardBucket
{
    /// <summary><c>2026-09</c> 또는 <c>2026-W39</c>.</summary>
    public string? Bucket { get; set; }

    /// <summary>주 단위일 때만 채워진다.</summary>
    public string? WeekStart { get; set; }

    /// <inheritdoc cref="WeekStart"/>
    public string? WeekEnd { get; set; }

    public int Cnt { get; set; }
    public int Done { get; set; }
    public int Modules { get; set; }
    public int Users { get; set; }
}

/// <summary>사람 × 기간 한 칸. 매트릭스 화면이 이것을 펼친다.</summary>
public sealed class WbsBoardUserBucket
{
    public string? UserBpId { get; set; }

    /// <summary>
    /// 성명. <c>wbs_user</c> 에 없는 사번은 <b>저장된 값 그대로</b> 내려간다 —
    /// 오타인지 퇴사자인지 화면에서 가려낼 수 있어야 한다.
    /// </summary>
    public string? UserNm { get; set; }

    public string? Bucket { get; set; }
    public string? WeekStart { get; set; }
    public string? WeekEnd { get; set; }

    public int Cnt { get; set; }

    /// <summary>고른 잣대(<c>who</c>)로 센 완료.</summary>
    public int Done { get; set; }

    public int DonePlan { get; set; }
    public int DoneReal { get; set; }
    public int DoneBig { get; set; }
    public int DoneRealBig { get; set; }
}

/// <summary>모듈(서브시스템) 한 줄.</summary>
public sealed class WbsBoardModule
{
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }

    public int Cnt { get; set; }
    public int Done { get; set; }
    public int DoneReal { get; set; }
    public int DoneBig { get; set; }
    public int DoneRealBig { get; set; }

    public int Dated { get; set; }
    public string? MinDt { get; set; }
    public string? MaxDt { get; set; }

    /// <summary>실적시작일이 적힌 건수.</summary>
    public int ActSdt { get; set; }

    /// <summary>실적종료일이 적힌 건수.</summary>
    public int ActEdt { get; set; }

    public string? MinAct { get; set; }
    public string? MaxAct { get; set; }
}

/// <summary>담당자 고르개 한 줄.</summary>
public sealed class WbsBoardUserOption
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public int Cnt { get; set; }
}

/// <summary>계획 진척률 요약.</summary>
public sealed class WbsBoardProgress
{
    /// <summary>기준일. <b>DB 의 <c>current_date</c></b> 다 — 브라우저 시각이 아니다.</summary>
    public string? Asof { get; set; }

    public int Total { get; set; }
    public int Dated { get; set; }

    public decimal? PlanRate { get; set; }

    public int NotStarted { get; set; }
    public int InProgress { get; set; }

    /// <summary>계획종료일이 지난 건수.</summary>
    public int Elapsed { get; set; }

    public int DoneCnt { get; set; }
    public decimal? DoneRate { get; set; }

    public int DoneBigCnt { get; set; }
    public decimal? DoneBigRate { get; set; }
}

/// <summary>사람별 계획 진척률.</summary>
public sealed class WbsBoardProgressUser
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }

    public int Cnt { get; set; }
    public decimal? PlanRate { get; set; }

    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Elapsed { get; set; }

    public int DoneCnt { get; set; }
    public decimal? DoneRate { get; set; }
    public int DoneBigCnt { get; set; }
    public decimal? DoneBigRate { get; set; }
}

/// <summary>모듈별 계획 진척률.</summary>
public sealed class WbsBoardProgressModule
{
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }

    public int Cnt { get; set; }
    public decimal? PlanRate { get; set; }

    public int DoneCnt { get; set; }
    public decimal? DoneRate { get; set; }
    public int DoneBigCnt { get; set; }
    public decimal? DoneBigRate { get; set; }

    public string? MinDt { get; set; }
    public string? MaxDt { get; set; }
}

/// <summary>항목별 계획 진척률 한 줄.</summary>
public sealed class WbsBoardProgressRow
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }
    public string? MenuNm { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? PlanSdtC { get; set; }
    public string? PlanEdtC { get; set; }

    /// <summary>계획 기간(일).</summary>
    public int? SpanDays { get; set; }

    /// <summary>경과 일수. 진척률과 분모·분자가 맞아야 해서 함께 내려보낸다.</summary>
    public int? PassedDays { get; set; }

    public decimal? PlanRate { get; set; }

    public string? ComplateYn { get; set; }
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public string? UserRealId { get; set; }
    public string? UserRealNm { get; set; }
    public string? ComplateRealYn { get; set; }
    public string? PriorityOrder { get; set; }
}

/// <summary>지연 요약.</summary>
public sealed class WbsBoardDelay
{
    public string? Asof { get; set; }
    public int Total { get; set; }

    /// <summary>계획시작일이 지났는데 실적시작일이 없다.</summary>
    public int StartLate { get; set; }

    /// <summary>계획종료일이 지났는데 실적종료일이 없다.</summary>
    public int FinishLate { get; set; }

    public int BothLate { get; set; }
    public int AnyLate { get; set; }

    public int? MaxStartDays { get; set; }
    public int? MaxFinishDays { get; set; }
}

/// <summary>사람별 지연 건수.</summary>
public sealed class WbsBoardDelayUser
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }

    public int Assigned { get; set; }
    public int StartLate { get; set; }
    public int FinishLate { get; set; }
    public int AnyLate { get; set; }
}

/// <summary>지연 상세 한 줄.</summary>
public sealed class WbsBoardDelayRow
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }
    public string? MenuNm { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? PlanSdtC { get; set; }
    public string? PlanEdtC { get; set; }

    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public string? UserRealId { get; set; }
    public string? UserRealNm { get; set; }

    public string? ComplateYn { get; set; }
    public string? ComplateRealYn { get; set; }

    public bool StartLate { get; set; }
    public bool FinishLate { get; set; }
    public int? StartDays { get; set; }
    public int? FinishDays { get; set; }

    public string? PriorityOrder { get; set; }
    public string? ProgType { get; set; }
}

/// <summary>
/// 상세 목록 한 줄. 화면이 가장 많이 읽는 자료다.
/// </summary>
/// <remarks>
/// 뒤쪽 <c>Pv*</c> 는 ProjectView 캐시라 <b>없으면 전부 <c>null</c></b> 이고,
/// 그래도 목록은 그대로 나온다 — 수집을 한 번도 안 돌린 프로젝트가 정상이다.
/// </remarks>
public sealed class WbsBoardRow
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }
    public string? MenuNm { get; set; }
    public string? ProgramId { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? PlanSdtC { get; set; }
    public string? PlanEdtC { get; set; }

    public string? UserBpId { get; set; }

    /// <summary>
    /// 성명. <b>명부에 있을 때만</b> 채운다. 안 맞는 값(자리표시자 · 오타 ·
    /// 퇴사자)은 비워 내려보내 화면이 「미할당」으로 보이게 한다.
    /// </summary>
    public string? UserNm { get; set; }

    public string? ComplateYn { get; set; }
    public string? UserRealId { get; set; }
    public string? UserRealNm { get; set; }
    public string? ComplateRealYn { get; set; }
    public string? RecheckYn { get; set; }

    public string? ComplateBigYn { get; set; }
    public string? ComplateRealBigYn { get; set; }
    public string? RecheckBigYn { get; set; }
    public string? DbReadyBigYn { get; set; }

    public string? PriorityOrder { get; set; }
    public string? ProgType { get; set; }
    public string? ProgTypeDesc { get; set; }
    public string? TrgUseChk { get; set; }

    public decimal? PlanRate { get; set; }

    public bool StartLate { get; set; }
    public bool FinishLate { get; set; }
    public int? StartDays { get; set; }
    public int? FinishDays { get; set; }

    public decimal? PvFinishRate { get; set; }
    public string? PvActualSdt { get; set; }
    public string? PvActualEdt { get; set; }
    public string? PvSnapshotAt { get; set; }

    public int? PvTaskCnt { get; set; }
    public int? PvNodeCnt { get; set; }
    public int? PvNodeEmpty { get; set; }
    public string? PvTaskEdt { get; set; }
    public string? PvWorkers { get; set; }
    public string? PvStatus { get; set; }
    public string? PvStatusAt { get; set; }
    public int? PvStatusCnt { get; set; }
    public string? PvTaskCode { get; set; }
}

/// <summary>화면별 일감 건수. 상세 목록의 「일감」 칸이 읽는다.</summary>
public sealed class WbsBoardTaskCount
{
    public string? ActivityId { get; set; }
    public int Cnt { get; set; }
    public int Done { get; set; }
}

/// <summary>일감 한 줄.</summary>
public sealed class WbsBoardTask
{
    public int TaskId { get; set; }
    public string? ActivityId { get; set; }
    public string? TaskDiv { get; set; }
    public string? Memo { get; set; }

    /// <summary><c>o</c> 면 완료. 빈 값이 미완료다.</summary>
    public string? DoneYn { get; set; }

    public int SortOrder { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// 상세 목록 조회 조건. 칸이 열둘이라 인자로 늘어놓지 않는다.
/// </summary>
public sealed class WbsBoardQuery
{
    /// <summary>집계 기준 날짜 칸. <c>sdt</c> 면 계획시작일, 그 밖이면 계획종료일.</summary>
    public string? Basis { get; set; }

    /// <summary><c>all</c> 이면 전체, 그 밖이면 개발 대상(<c>new_dev2 = 'o'</c>)만.</summary>
    public string? Scope { get; set; }

    public string? Month { get; set; }
    public string? Week { get; set; }
    public string? User { get; set; }
    public string? RealUser { get; set; }
    public string? Module { get; set; }

    /// <summary>화면명 또는 액티비티 번호에 걸리는 글자.</summary>
    public string? Q { get; set; }

    /// <summary><c>y</c> 완료만 · <c>n</c> 미완료만 · 그 밖이면 전체.</summary>
    public string? Done { get; set; }

    /// <summary><c>start</c> · <c>finish</c> · <c>both</c> · <c>any</c> · <c>none</c>.</summary>
    public string? Late { get; set; }

    /// <summary><c>open</c>(착수도래 미완료) · <c>notyet</c>(미도래) · <c>done</c>.</summary>
    public string? Status { get; set; }

    public int? Limit { get; set; }
}
