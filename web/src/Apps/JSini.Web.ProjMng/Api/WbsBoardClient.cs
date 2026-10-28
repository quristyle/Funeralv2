using System.Net.Http.Json;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>WBS 대시보드(projmng/wbs-board). 사내망에서 옮겨 온 것으로, 프로젝트관리의 WBS(<see cref="WbsClient"/>)와 이름만 같고 다른 물건이다 — 모든 조회는 프로젝트 번호를 받는다.
/// 사람 이름은 서버가 아니라 <see cref="WbsBoardNames"/> 가 응답마다 채운다 — 원장과 계정이 다른 DB 라 조인할 수 없다.</summary>
public sealed class WbsBoardClient(GatewayClient gateway, WbsBoardNames names)
{
    private const string Url = "projmng/wbs-board";

    /// <summary>조회 조건을 주소로 엮는다. 빈 값은 뺀다 — 서버는 빈 글자도 조건으로 읽는다.</summary>
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

    public async Task<IReadOnlyList<WbsBoardUserBucketDto>> MonthlyByUserAsync(
        int prjRid, string? basis = null, string? scope = null, string? who = null,
        CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardUserBucketDto>(
                $"{Url}/stats/monthly-by-user?{Query(prjRid, ("basis", basis), ("scope", scope), ("who", who))}", ct),
            (r, name) => r.UserNm = name(r.UserBpId), ct);

    public async Task<IReadOnlyList<WbsBoardUserBucketDto>> WeeklyByUserAsync(
        int prjRid, string? basis = null, string? scope = null, string? who = null,
        CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardUserBucketDto>(
                $"{Url}/stats/weekly-by-user?{Query(prjRid, ("basis", basis), ("scope", scope), ("who", who))}", ct),
            (r, name) => r.UserNm = name(r.UserBpId), ct);

    public Task<IReadOnlyList<WbsBoardModuleDto>> ByModuleAsync(
        int prjRid, string? basis = null, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardModuleDto>(
            $"{Url}/stats/by-module?{Query(prjRid, ("basis", basis), ("scope", scope))}", ct);

    /// <summary>담당자 고르개. 기간을 주면 <b>그 기간에 배정된 사람만</b> 온다.</summary>
    public async Task<IReadOnlyList<WbsBoardUserOptionDto>> UsersAsync(
        int prjRid, string? scope = null, string? basis = null,
        string? month = null, string? week = null, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardUserOptionDto>(
                $"{Url}/users?{Query(prjRid, ("scope", scope), ("basis", basis), ("month", month), ("week", week))}", ct),
            (r, name) => r.UserNm = name(r.UserBpId), ct);

    // ──────────────────────────────────────────── 상세 목록

    public async Task<IReadOnlyList<WbsBoardRowDto>> RowsAsync(
        int prjRid, WbsBoardFilter filter, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardRowDto>($"{Url}/rows?{filter.ToQuery(prjRid)}", ct),
            (r, name) =>
            {
                r.UserNm = name(r.UserBpId);
                r.UserRealNm = name(r.UserRealId);
            }, ct);

    /// <summary>한 줄의 칸 몇 개를 고친다. 보낸 칸만 바뀐다 — 일정·실적은 엑셀 WBS 가 원본이라 서버가 받지 않는다.</summary>
    public async Task PatchRowAsync(
        int prjRid, string activityId, IReadOnlyDictionary<string, object?> patch,
        CancellationToken ct = default)
    {
        // 응답은 반드시 버린다 — SendRawAsync 는 헤더만 읽고 돌아와 연결이 열린 채 남는다.
        using var body = JsonContent.Create(patch);

        using var response = await gateway.SendRawAsync(
            HttpMethod.Patch,
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}?prjRid={prjRid}",
            body, cancellationToken: ct);

        response.EnsureSuccessStatusCode();
    }

    // ──────────────────────────────────────────── 대상 추가·수정·삭제

    /// <summary>한 줄의 모든 칸(칸 이름 → 값). 목록에 안 실리는 긴 글(화면 설명·메모)까지 온다.</summary>
    public async Task<Dictionary<string, string?>> RowFullAsync(
        int prjRid, string activityId, CancellationToken ct = default)
    {
        var fields = await gateway.GetListAsync<WbsBoardFieldDto>(
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}/full?prjRid={prjRid}", ct);

        return fields
            .Where(f => f.Key is not null)
            .ToDictionary(f => f.Key!, f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>새 대상. <c>activity_id</c> 를 비우면 서버가 임시 번호(NOID-)를 붙인다.</summary>
    /// <returns>만든 줄의 Activity ID.</returns>
    public Task<string?> CreateRowAsync(
        int prjRid, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
        => gateway.PostAsync<string>($"{Url}/rows?prjRid={prjRid}", values, ct);

    public Task UpdateRowAsync(
        int prjRid, string activityId, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/rows/{Uri.EscapeDataString(activityId)}?prjRid={prjRid}", values, ct);

    /// <summary>임시 번호로 세운 줄에 확정 Activity ID 를 채운다. 일감이 함께 따라간다.</summary>
    public Task SetRowIdAsync(int prjRid, string activityId, string newId, CancellationToken ct = default)
        => gateway.PutAsync(
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}/activity-id?prjRid={prjRid}",
            new Dictionary<string, string> { ["activity_id"] = newId }, ct);

    public Task<WbsBoardRowDepsDto?> RowDepsAsync(int prjRid, string activityId, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardRowDepsDto>(
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}/deps?prjRid={prjRid}", ct);

    /// <summary>지운다. 줄과 일감은 서버가 보관 표(wbs_work_deleted)에 먼저 담는다.</summary>
    public async Task<WbsBoardRowDeletedDto?> DeleteRowAsync(int prjRid, string activityId, CancellationToken ct = default)
        => (await gateway.DeleteListAsync<WbsBoardRowDeletedDto>(
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}?prjRid={prjRid}", ct)).FirstOrDefault();

    /// <summary>첨부를 고친 뒤 그룹과 건수를 원장에 적는다.</summary>
    public Task SetRowFilesAsync(
        int prjRid, string activityId, string? groupId, int count, CancellationToken ct = default)
        => gateway.PutAsync(
            $"{Url}/rows/{Uri.EscapeDataString(activityId)}/files?prjRid={prjRid}",
            new { groupId, count }, ct);

    // ──────────────────────────────────────────── 진척률

    public Task<WbsBoardProgressDto?> ProgressAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardProgressDto>(
            $"{Url}/progress/summary?{Query(prjRid, ("scope", scope))}", ct);

    public async Task<IReadOnlyList<WbsBoardProgressUserDto>> ProgressByUserAsync(
        int prjRid, string? scope = null, string? who = null, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardProgressUserDto>(
                $"{Url}/progress/by-user?{Query(prjRid, ("scope", scope), ("who", who))}", ct),
            (r, name) => r.UserNm = name(r.UserBpId), ct);

    public Task<IReadOnlyList<WbsBoardProgressModuleDto>> ProgressByModuleAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardProgressModuleDto>(
            $"{Url}/progress/by-module?{Query(prjRid, ("scope", scope))}", ct);

    public async Task<IReadOnlyList<WbsBoardProgressRowDto>> ProgressRowsAsync(
        int prjRid, string? scope = null, string? user = null,
        string? realUser = null, string? module = null, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardProgressRowDto>(
                $"{Url}/progress/rows?{Query(prjRid, ("scope", scope), ("user", user), ("realUser", realUser), ("module", module))}", ct),
            (r, name) =>
            {
                r.UserNm = name(r.UserBpId);
                r.UserRealNm = name(r.UserRealId);
            }, ct);

    // ──────────────────────────────────────────── 지연

    public Task<WbsBoardDelayDto?> DelayAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsBoardDelayDto>(
            $"{Url}/delay/summary?{Query(prjRid, ("scope", scope))}", ct);

    public async Task<IReadOnlyList<WbsBoardDelayUserDto>> DelayByUserAsync(
        int prjRid, string? scope = null, string? who = null, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardDelayUserDto>(
                $"{Url}/delay/by-user?{Query(prjRid, ("scope", scope), ("who", who))}", ct),
            (r, name) => r.UserNm = name(r.UserBpId), ct);

    public async Task<IReadOnlyList<WbsBoardDelayRowDto>> DelayRowsAsync(
        int prjRid, string? scope = null, string? kind = null,
        string? user = null, string? realUser = null, CancellationToken ct = default)
        => await names.FillAsync(
            await gateway.GetListAsync<WbsBoardDelayRowDto>(
                $"{Url}/delay/rows?{Query(prjRid, ("scope", scope), ("kind", kind), ("user", user), ("realUser", realUser))}", ct),
            (r, name) =>
            {
                r.UserNm = name(r.UserBpId);
                r.UserRealNm = name(r.UserRealId);
            }, ct);
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

    /// <summary>성명. 서버가 아니라 <see cref="WbsBoardNames"/> 가 채운다. 계정과 안 이어진 사람은 적힌 값 그대로 남는다.</summary>
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

    // 원장의 표시 칸은 'o' 아니면 빈 값이다. 편집 폼은 @bind- 로만 묶이므로 여기서 참·거짓으로 잇는다.

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

    public string? DwgViewUse { get; set; }
    public string? ReportUse { get; set; }
    public string? PvTestCaseId { get; set; }
    public string? FileGroupId { get; set; }
    public int FileCnt { get; set; }

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

    /// <summary>대형 재확인 요청.</summary>
    public bool RecheckBig
    {
        get => RecheckBigYn == "o";
        set => RecheckBigYn = value ? "o" : null;
    }

    /// <summary>도면 조회 사용.</summary>
    public bool DwgView
    {
        get => DwgViewUse == "o";
        set => DwgViewUse = value ? "o" : null;
    }

    /// <summary>리포트 사용.</summary>
    public bool Report
    {
        get => ReportUse == "o";
        set => ReportUse = value ? "o" : null;
    }

    /// <summary>모듈 칸에 보이는 글자(코드 + 이름). 칸별 검색·정렬·엑셀이 이것을 본다.</summary>
    public string ModuleText => string.Join(' ', new[] { Systemcode, SystemNm }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>일감 칸의 상태 글자. 칸별 검색이 「미완료」·「완료」·「없음」으로 거른다.</summary>
    public string TaskState => TaskCnt == 0 ? "없음" : TaskDone < TaskCnt ? $"미완료 {TaskDone}/{TaskCnt}" : $"완료 {TaskDone}/{TaskCnt}";

    /// <summary>PV 현재단계(「(90%) PI검토」).</summary>
    public string PvStageText => WbsBoardLinks.StageText(this);

    /// <summary>원장 칸 이름으로 표시 값을 읽는다.</summary>
    public string? ValueOf(string col) => col switch
    {
        "complate_yn" => ComplateYn,
        "complate_real_yn" => ComplateRealYn,
        "recheck_yn" => RecheckYn,
        "complate_big_yn" => ComplateBigYn,
        "complate_real_big_yn" => ComplateRealBigYn,
        "recheck_big_yn" => RecheckBigYn,
        "db_ready_big_yn" => DbReadyBigYn,
        "dwg_view_use" => DwgViewUse,
        "report_use" => ReportUse,
        "user_bp_id" => UserBpId,
        "user_real_id" => UserRealId,
        "priority_order" => PriorityOrder,
        _ => null,
    };

    /// <summary>원장 칸 이름으로 값을 고쳐 적는다. 서버에 보낸 뒤 화면의 줄을 맞추는 데 쓴다.</summary>
    public void SetValue(string col, string? value)
    {
        switch (col)
        {
            case "complate_yn": ComplateYn = value; break;
            case "complate_real_yn": ComplateRealYn = value; break;
            case "recheck_yn": RecheckYn = value; break;
            case "complate_big_yn": ComplateBigYn = value; break;
            case "complate_real_big_yn": ComplateRealBigYn = value; break;
            case "recheck_big_yn": RecheckBigYn = value; break;
            case "db_ready_big_yn": DbReadyBigYn = value; break;
            case "dwg_view_use": DwgViewUse = value; break;
            case "report_use": ReportUse = value; break;
            case "user_bp_id": UserBpId = value; break;
            case "user_real_id": UserRealId = value; break;
            case "priority_order": PriorityOrder = value; break;
        }
    }

    /// <summary>표시 칸이 켜져 있나('o').</summary>
    public bool Flag(string col) => ValueOf(col) == "o";

    /// <summary>Activity ID 가 아직 없어 임시 번호(NOID-)로 선 줄.</summary>
    public bool NoId => ActivityId?.StartsWith(WbsBoardLinks.NoIdPrefix, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>일감 건수. 서버가 주는 값이 아니다 — 화면이 WbsBoardTaskClient.CountsAsync 로 받아 채운다.</summary>
    public int TaskCnt { get; set; }

    /// <inheritdoc cref="TaskCnt"/>
    public int TaskDone { get; set; }
}

/// <summary>원장 한 칸(이름 · 값).</summary>
public sealed class WbsBoardFieldDto
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

/// <summary>지우기 전에 함께 사라질 것.</summary>
public sealed class WbsBoardRowDepsDto
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? MenuNm { get; set; }
    public int Tasks { get; set; }
    public int Files { get; set; }
}

/// <summary>지운 결과. 되돌릴 자료는 projmng.wbs_work_deleted 의 DelId 줄이다.</summary>
public sealed class WbsBoardRowDeletedDto
{
    public long DelId { get; set; }
    public string? ActivityId { get; set; }
    public int Tasks { get; set; }
}
