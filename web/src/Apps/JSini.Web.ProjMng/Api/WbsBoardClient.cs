using System.Net.Http.Json;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// WBS 대시보드 — <c>projmng/wbs-board</c>.
/// </summary>
/// <remarks>
/// <para>
/// 사내망에서 따로 돌던 대시보드를 옮겨 온 것이다. <b>프로젝트관리의 WBS
/// (<see cref="WbsClient"/>)와 이름만 같고 다른 물건</b>이다 — 그쪽은 프로젝트별
/// 공정표(1줄 = 1공정)이고 이쪽은 화면 단위 원장(1줄 = 1화면)이다.
/// </para>
///
/// <para>
/// <b>모든 조회가 프로젝트 번호를 받는다.</b> 원본은 프로젝트 하나 전용이라
/// 그 조건이 없었다.
/// </para>
/// </remarks>
public sealed class WbsBoardClient(GatewayClient gateway)
{
    private const string Url = "projmng/wbs-board";

    /// <summary>
    /// 조회 조건을 주소로 엮는다. <b>빈 값은 빼고 보낸다</b> — 서버가
    /// 「안 걸린다」로 읽는 값이라 빈 글자를 보내면 조건이 걸려 버린다.
    /// </summary>
    private static string Query(int prjRid, params (string Key, string? Value)[] parts)
    {
        var query = new List<string> { $"prjRid={prjRid}" };

        foreach (var (key, value) in parts)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        return string.Join('&', query);
    }

    // ──────────────────────────────────────────── 집계

    public Task<WbsBoardSummaryDto?> SummaryAsync(
        int prjRid, string? basis = null, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardSummaryDto>(
            $"{Url}/stats/summary?{Query(prjRid, ("basis", basis), ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardBucketDto>> MonthlyAsync(
        int prjRid, string? basis = null, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardBucketDto>(
            $"{Url}/stats/monthly?{Query(prjRid, ("basis", basis), ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardBucketDto>> WeeklyAsync(
        int prjRid, string? basis = null, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardBucketDto>(
            $"{Url}/stats/weekly?{Query(prjRid, ("basis", basis), ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardUserBucketDto>> MonthlyByUserAsync(
        int prjRid, string? basis = null, string? scope = null, string? who = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardUserBucketDto>(
            $"{Url}/stats/monthly-by-user?{Query(prjRid, ("basis", basis), ("scope", scope), ("who", who))}", ct);

    public Task<IReadOnlyList<WbsBoardUserBucketDto>> WeeklyByUserAsync(
        int prjRid, string? basis = null, string? scope = null, string? who = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardUserBucketDto>(
            $"{Url}/stats/weekly-by-user?{Query(prjRid, ("basis", basis), ("scope", scope), ("who", who))}", ct);

    public Task<IReadOnlyList<WbsBoardModuleDto>> ByModuleAsync(
        int prjRid, string? basis = null, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardModuleDto>(
            $"{Url}/stats/by-module?{Query(prjRid, ("basis", basis), ("scope", scope))}", ct);

    /// <summary>담당자 고르개. 기간을 주면 <b>그 기간에 배정된 사람만</b> 온다.</summary>
    public Task<IReadOnlyList<WbsBoardUserOptionDto>> UsersAsync(
        int prjRid, string? scope = null, string? basis = null,
        string? month = null, string? week = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardUserOptionDto>(
            $"{Url}/users?{Query(prjRid, ("scope", scope), ("basis", basis), ("month", month), ("week", week))}", ct);

    // ──────────────────────────────────────────── 상세 목록

    public Task<IReadOnlyList<WbsBoardRowDto>> RowsAsync(
        int prjRid, WbsBoardFilter filter, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardRowDto>($"{Url}/rows?{filter.ToQuery(prjRid)}", ct);

    /// <summary>
    /// 한 줄의 칸 몇 개를 고친다. <b>보낸 칸만</b> 바뀐다 — 일정·실적은
    /// 엑셀 WBS 가 원본이라 서버가 받지 않는다.
    /// </summary>
    public async Task PatchRowAsync(
        int prjRid, string activityId, IReadOnlyDictionary<string, object?> patch,
        CancellationToken ct = default)
    {
        // 게이트웨이 창구에 PATCH 를 위한 짧은 길이 없다. 본문을 직접 싣는
        // 대신 응답을 **반드시 버려야 한다** — `SendRawAsync` 는 헤더만 읽고
        // 돌아오므로 연결이 열린 채로 남는다.
        using var body = JsonContent.Create(patch);

        using var response = await gateway.SendRawAsync(
            HttpMethod.Patch,
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}?prjRid={prjRid}",
            body, cancellationToken: ct);

        response.EnsureSuccessStatusCode();
    }

    // ──────────────────────────────────────────── 진척률

    public Task<WbsBoardProgressDto?> ProgressAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardProgressDto>(
            $"{Url}/progress/summary?{Query(prjRid, ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardProgressUserDto>> ProgressByUserAsync(
        int prjRid, string? scope = null, string? who = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardProgressUserDto>(
            $"{Url}/progress/by-user?{Query(prjRid, ("scope", scope), ("who", who))}", ct);

    public Task<IReadOnlyList<WbsBoardProgressModuleDto>> ProgressByModuleAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardProgressModuleDto>(
            $"{Url}/progress/by-module?{Query(prjRid, ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardProgressRowDto>> ProgressRowsAsync(
        int prjRid, string? scope = null, string? user = null,
        string? realUser = null, string? module = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardProgressRowDto>(
            $"{Url}/progress/rows?{Query(prjRid, ("scope", scope), ("user", user), ("realUser", realUser), ("module", module))}", ct);

    // ──────────────────────────────────────────── 지연

    public Task<WbsBoardDelayDto?> DelayAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardDelayDto>(
            $"{Url}/delay/summary?{Query(prjRid, ("scope", scope))}", ct);

    public Task<IReadOnlyList<WbsBoardDelayUserDto>> DelayByUserAsync(
        int prjRid, string? scope = null, string? who = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardDelayUserDto>(
            $"{Url}/delay/by-user?{Query(prjRid, ("scope", scope), ("who", who))}", ct);

    public Task<IReadOnlyList<WbsBoardDelayRowDto>> DelayRowsAsync(
        int prjRid, string? scope = null, string? kind = null,
        string? user = null, string? realUser = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardDelayRowDto>(
            $"{Url}/delay/rows?{Query(prjRid, ("scope", scope), ("kind", kind), ("user", user), ("realUser", realUser))}", ct);
}

/// <summary>상세 목록의 조회 조건. 칸이 열둘이라 인자로 늘어놓지 않는다.</summary>
public sealed class WbsBoardFilter
{
    /// <summary>집계 기준 날짜. <c>sdt</c> 면 계획시작일, 그 밖이면 계획종료일.</summary>
    public string? Basis { get; set; }

    /// <summary><c>all</c> 이면 전체, 그 밖이면 개발 대상만.</summary>
    public string? Scope { get; set; }

    /// <summary><c>2026-09</c>. 주와 <b>같이 걸 수 없다</b> — 둘 다 날짜 기준이다.</summary>
    public string? Month { get; set; }

    /// <summary><c>2026-W39</c>.</summary>
    public string? Week { get; set; }

    public string? User { get; set; }
    public string? RealUser { get; set; }
    public string? Module { get; set; }
    public string? Q { get; set; }

    /// <summary><c>y</c> 완료만 · <c>n</c> 미완료만.</summary>
    public string? Done { get; set; }

    /// <summary><c>start</c> · <c>finish</c> · <c>both</c> · <c>any</c> · <c>none</c>.</summary>
    public string? Late { get; set; }

    /// <summary><c>open</c> · <c>notyet</c> · <c>done</c>.</summary>
    public string? Status { get; set; }

    public int? Limit { get; set; }

    public string ToQuery(int prjRid)
    {
        var query = new List<string> { $"prjRid={prjRid}" };

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        Add("basis", Basis);
        Add("scope", Scope);
        Add("month", Month);
        Add("week", Week);
        Add("user", User);
        Add("realUser", RealUser);
        Add("module", Module);
        Add("q", Q);
        Add("done", Done);
        Add("late", Late);
        Add("status", Status);

        if (Limit is not null) query.Add($"limit={Limit}");

        return string.Join('&', query);
    }
}

/// <summary>요약 카드.</summary>
public sealed class WbsBoardSummaryDto
{
    public int Total { get; set; }
    public int Dated { get; set; }
    public int Modules { get; set; }
    public int Users { get; set; }
    public string? MinDt { get; set; }
    public string? MaxDt { get; set; }
    public int Done { get; set; }
    public int DoneReal { get; set; }

    /// <summary>대형 트랙. <b>위 완료 수치에 섞지 않는다</b> — 같은 줄을 다른 잣대로 센 것이다.</summary>
    public int DoneBig { get; set; }

    /// <inheritdoc cref="DoneBig"/>
    public int DoneRealBig { get; set; }
}

/// <summary>월·주 한 칸.</summary>
public sealed class WbsBoardBucketDto
{
    public string? Bucket { get; set; }
    public string? WeekStart { get; set; }
    public string? WeekEnd { get; set; }
    public int Cnt { get; set; }
    public int Done { get; set; }
    public int Modules { get; set; }
    public int Users { get; set; }
}

/// <summary>사람 × 기간 한 칸.</summary>
public sealed class WbsBoardUserBucketDto
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public string? Bucket { get; set; }
    public string? WeekStart { get; set; }
    public string? WeekEnd { get; set; }
    public int Cnt { get; set; }
    public int Done { get; set; }
    public int DonePlan { get; set; }
    public int DoneReal { get; set; }
    public int DoneBig { get; set; }
    public int DoneRealBig { get; set; }
}

/// <summary>모듈 한 줄.</summary>
public sealed class WbsBoardModuleDto
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
    public int ActSdt { get; set; }
    public int ActEdt { get; set; }
    public string? MinAct { get; set; }
    public string? MaxAct { get; set; }
}

/// <summary>담당자 고르개 한 줄.</summary>
public sealed class WbsBoardUserOptionDto
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public int Cnt { get; set; }
}

/// <summary>진척률 요약.</summary>
public sealed class WbsBoardProgressDto
{
    /// <summary>기준일. <b>DB 의 오늘</b>이다 — 브라우저 시각이 아니다.</summary>
    public string? Asof { get; set; }

    public int Total { get; set; }
    public int Dated { get; set; }
    public decimal? PlanRate { get; set; }
    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Elapsed { get; set; }
    public int DoneCnt { get; set; }
    public decimal? DoneRate { get; set; }
    public int DoneBigCnt { get; set; }
    public decimal? DoneBigRate { get; set; }
}

/// <summary>사람별 진척률.</summary>
public sealed class WbsBoardProgressUserDto
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

/// <summary>모듈별 진척률.</summary>
public sealed class WbsBoardProgressModuleDto
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

/// <summary>항목별 진척률 한 줄.</summary>
public sealed class WbsBoardProgressRowDto
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? SystemNm { get; set; }
    public string? MenuNm { get; set; }
    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? PlanSdtC { get; set; }
    public string? PlanEdtC { get; set; }
    public int? SpanDays { get; set; }
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
public sealed class WbsBoardDelayDto
{
    public string? Asof { get; set; }
    public int Total { get; set; }
    public int StartLate { get; set; }
    public int FinishLate { get; set; }

    /// <summary>둘 다인 건수. <b>착수 + 종료로 더하면 겹쳐 센다.</b></summary>
    public int BothLate { get; set; }

    public int AnyLate { get; set; }
    public int? MaxStartDays { get; set; }
    public int? MaxFinishDays { get; set; }
}

/// <summary>사람별 지연.</summary>
public sealed class WbsBoardDelayUserDto
{
    public string? UserBpId { get; set; }
    public string? UserNm { get; set; }
    public int Assigned { get; set; }
    public int StartLate { get; set; }
    public int FinishLate { get; set; }
    public int AnyLate { get; set; }
}

/// <summary>지연 상세 한 줄.</summary>
public sealed class WbsBoardDelayRowDto
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

/// <summary>상세 목록 한 줄.</summary>
/// <remarks>
/// 뒤쪽 <c>Pv*</c> 는 ProjectView 캐시라 <b>없으면 전부 비어 있다</b> — 수집을
/// 한 번도 안 돌린 프로젝트가 정상이다.
/// </remarks>
public sealed class WbsBoardRowDto
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

    /// <summary>성명. <b>명부에 있을 때만</b> 찬다 — 비어 있으면 화면이 「미할당」으로 보인다.</summary>
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

    // 원장의 표시 칸은 'o' 아니면 빈 값이다. 편집 폼은 `@bind-` 로만 묶을 수
    // 있고(`EditFormBindingTests`) 확인칸은 참·거짓을 받으므로, 그 사이를
    // 잇는 속성을 여기 낸다 — 화면마다 변환을 적으면 한 화면만 'Y' 를 쓰는
    // 날이 온다.

    /// <summary>담당자 완료.</summary>
    public bool Complate
    {
        get => ComplateYn == "o";
        set => ComplateYn = value ? "o" : null;
    }

    /// <summary>개발자 완료.</summary>
    public bool ComplateReal
    {
        get => ComplateRealYn == "o";
        set => ComplateRealYn = value ? "o" : null;
    }

    /// <summary>재확인 요청.</summary>
    public bool Recheck
    {
        get => RecheckYn == "o";
        set => RecheckYn = value ? "o" : null;
    }

    /// <summary>대형 완료. <b>위 완료와 별개로 센다.</b></summary>
    public bool ComplateBig
    {
        get => ComplateBigYn == "o";
        set => ComplateBigYn = value ? "o" : null;
    }

    /// <inheritdoc cref="ComplateBig"/>
    public bool ComplateRealBig
    {
        get => ComplateRealBigYn == "o";
        set => ComplateRealBigYn = value ? "o" : null;
    }

    /// <summary>대형 DB 준비됨.</summary>
    public bool DbReadyBig
    {
        get => DbReadyBigYn == "o";
        set => DbReadyBigYn = value ? "o" : null;
    }

    /// <summary>
    /// 일감 건수. <b>서버가 주는 값이 아니다</b> — 화면이
    /// <see cref="WbsBoardTaskClient.CountsAsync"/> 로 한 번에 받아 채운다.
    /// </summary>
    public int TaskCnt { get; set; }

    /// <inheritdoc cref="TaskCnt"/>
    public int TaskDone { get; set; }
}
