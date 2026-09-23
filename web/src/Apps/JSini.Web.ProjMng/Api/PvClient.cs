using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// ProjectView 캐시와 원장 반영 — <c>projmng/pv</c>.
/// </summary>
/// <remarks>
/// <para>
/// ProjectView 는 사내망 안에 있고 HTTPS 라 서버가 직접 부를 수 없다. 사람이
/// ProjectView 화면의 콘솔에 <b>수집 스크립트를 붙여넣어</b> 걷고, 그 결과를
/// <see cref="IngestAsync"/> 로 담는다.
/// </para>
///
/// <para>
/// [원본의 엑셀 경로는 없다]
/// </para>
///
/// <para>
/// 사내 대시보드는 ProjectView 의 Excel Export 파일을 읽는 길도 가지고 있었다.
/// 그 엑셀이 <b>DRM 으로 잠겨 있어</b> 프로그램이 직접 못 열고, 원본은 Windows 의
/// Excel COM 으로 CSV 를 만든 뒤 브라우저 내려받기 폴더를 뒤졌다. 포털은 리눅스
/// 컨테이너에서 돌아 그 길이 성립하지 않는다.
/// </para>
/// </remarks>
public sealed class PvClient(GatewayClient gateway)
{
    private const string Url = "projmng/pv";

    public Task<PvCacheStatusDto?> StatusAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetOneAsync<PvCacheStatusDto>(
            $"{Url}/cache?prjRid={prjRid}"
            + (string.IsNullOrWhiteSpace(scope) ? "" : $"&scope={Uri.EscapeDataString(scope)}"), ct);

    public Task<IReadOnlyList<PvRowDto>> RowsAsync(
        int prjRid, string? scope = null, CancellationToken ct = default)
        => gateway.GetListAsync<PvRowDto>(
            $"{Url}/rows?prjRid={prjRid}"
            + (string.IsNullOrWhiteSpace(scope) ? "" : $"&scope={Uri.EscapeDataString(scope)}"), ct);

    /// <summary>
    /// 일감 목록. <paramref name="activityId"/> 를 주면 <b>그 화면의 단계까지</b> 온다.
    /// </summary>
    public Task<PvTaskBundleDto?> TasksAsync(
        int prjRid, string? activityId = null, CancellationToken ct = default)
        => gateway.GetOneAsync<PvTaskBundleDto>(
            $"{Url}/tasks?prjRid={prjRid}"
            + (string.IsNullOrWhiteSpace(activityId) ? "" : $"&activityId={Uri.EscapeDataString(activityId)}"), ct);

    /// <summary>걷어 온 결과를 담는다. 본문은 수집 스크립트가 만든 그대로다.</summary>
    public Task<PvIngestResultDto?> IngestAsync(
        int prjRid, object body, CancellationToken ct = default)
        => gateway.PostAsync<PvIngestResultDto>($"{Url}/ingest?prjRid={prjRid}", body, ct);

    /// <summary>캐시를 비운다. <c>what</c> 은 <c>all</c> · <c>works</c> · <c>tasks</c>.</summary>
    public Task ClearAsync(int prjRid, string what = "all", CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/cache?prjRid={prjRid}&what={Uri.EscapeDataString(what)}", ct);

    /// <summary>무엇이 바뀌는지 먼저 본다. <b>DB 는 안 바뀐다.</b></summary>
    public Task<PvSyncPreviewDto?> PreviewAsync(
        int prjRid, PvSyncRequestDto request, CancellationToken ct = default)
        => gateway.PostAsync<PvSyncPreviewDto>($"{Url}/sync/preview?prjRid={prjRid}", request, ct);

    public Task<PvSyncAppliedDto?> ApplyAsync(
        int prjRid, PvSyncRequestDto request, CancellationToken ct = default)
        => gateway.PostAsync<PvSyncAppliedDto>($"{Url}/sync/apply?prjRid={prjRid}", request, ct);
}

/// <summary>캐시 현황.</summary>
public sealed class PvCacheStatusDto
{
    public string? Scope { get; set; }
    public PvWorkStatDto? Works { get; set; }
    public PvTaskStatDto? Tasks { get; set; }

    /// <summary>원장에 없는데 캐시에만 있는 건수. <b>지우지 않고 세기만 한다.</b></summary>
    public int Orphan { get; set; }
}

/// <inheritdoc cref="PvCacheStatusDto"/>
public sealed class PvWorkStatDto
{
    public int Targets { get; set; }
    public int Identified { get; set; }
    public int Snapshot { get; set; }
    public string? LastSeen { get; set; }
    public string? LastSnapshot { get; set; }
    public string? ProjectId { get; set; }
}

/// <inheritdoc cref="PvCacheStatusDto"/>
public sealed class PvTaskStatDto
{
    public int Tasks { get; set; }
    public int Nodes { get; set; }

    /// <summary>날짜나 담당자가 빈 단계 수. [워크플로 채우기]가 이것을 줄인다.</summary>
    public int NodesEmpty { get; set; }

    public int Charged { get; set; }
    public int Staged { get; set; }
    public string? LastSeen { get; set; }
    public int NodesCached { get; set; }
}

/// <summary>액티비티별 캐시 한 줄 — 원장 값과 나란히 온다.</summary>
public sealed class PvRowDto
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? MenuNm { get; set; }
    public string? UserBpId { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? PlanSdtC { get; set; }

    public string? PvWorkId { get; set; }
    public string? PvWorkTitle { get; set; }
    public string? PvProjectId { get; set; }
    public string? PvSeenAt { get; set; }

    public decimal? PvFinishRate { get; set; }
    public decimal? PvActualRate { get; set; }

    public string? PvPlanSdt { get; set; }
    public string? PvPlanEdt { get; set; }
    public string? PvActualSdt { get; set; }
    public string? PvActualEdt { get; set; }
    public string? PvSnapshotAt { get; set; }

    public int? TaskCnt { get; set; }
    public int? NodeCnt { get; set; }
    public int? NodeEmpty { get; set; }
    public string? PvTaskEdt { get; set; }
    public string? PvWorkers { get; set; }
    public string? PvStatus { get; set; }
    public string? PvStatusAt { get; set; }
    public int? PvStatusCnt { get; set; }
    public string? PvTaskCode { get; set; }
}

/// <summary>워크플로 일감 한 줄.</summary>
public sealed class PvTaskDto
{
    public string? PvTaskId { get; set; }
    public string? ActivityId { get; set; }
    public string? PvWorkId { get; set; }
    public string? PvTaskCode { get; set; }
    public string? PvTaskTitle { get; set; }
    public string? PvPlanSdt { get; set; }
    public string? PvPlanEdt { get; set; }
    public int? PvNodeCnt { get; set; }
    public int? PvNodeEmpty { get; set; }
    public string? PvChargerId { get; set; }
    public string? PvChargerNm { get; set; }
    public string? PvStatus { get; set; }
    public string? PvStatusAt { get; set; }
    public string? PvSeenAt { get; set; }
}

/// <summary>일감 하나의 워크플로 단계.</summary>
public sealed class PvNodeDto
{
    public string? PvTaskId { get; set; }
    public int NodeNo { get; set; }
    public string? StageNm { get; set; }
    public string? NodeDt { get; set; }
    public string? WorkerId { get; set; }
    public string? WorkerNm { get; set; }
}

/// <summary>일감과 단계를 함께 담은 응답.</summary>
public sealed class PvTaskBundleDto
{
    public List<PvTaskDto> Tasks { get; set; } = [];
    public List<PvNodeDto> Nodes { get; set; } = [];
}

/// <summary>수집 결과.</summary>
public sealed class PvIngestResultDto
{
    public int Works { get; set; }
    public int Tasks { get; set; }
    public int Nodes { get; set; }

    /// <summary>액티비티 번호가 없어 버린 건수.</summary>
    public int SkippedNoCode { get; set; }

    /// <summary>같은 번호가 두 번 온 건수. <b>첫 번째만 담았다.</b></summary>
    public int Duplicated { get; set; }

    public List<string> DuplicatedCodes { get; set; } = [];

    /// <summary>원장에 없는 번호. 알려만 주고 지우지 않는다.</summary>
    public int UnknownCount { get; set; }

    public List<string> Unknown { get; set; } = [];
    public string? ProjectId { get; set; }
}

/// <summary>동기화가 보내는 항목 하나.</summary>
public sealed class PvSyncItemDto
{
    public string? Code { get; set; }
    public string? PlanStartDate { get; set; }
    public string? PlanEndDate { get; set; }
    public string? ActualStartDate { get; set; }
    public string? ActualEndDate { get; set; }
}

/// <summary>한 칸의 바뀜.</summary>
public sealed class PvSyncChangeDto
{
    public string? Col { get; set; }
    public string? Label { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>한 액티비티의 바뀜.</summary>
public sealed class PvSyncDiffRowDto
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? MenuNm { get; set; }
    public List<PvSyncChangeDto> Changes { get; set; } = [];

    /// <summary>바뀌는 칸을 한 줄로 적은 글자. 표에서 쓴다.</summary>
    public string Summary =>
        string.Join(" · ", Changes.Select(c => $"{c.Label} {c.From ?? "—"} → {c.To ?? "—"}"));
}

/// <summary>미리보기 결과.</summary>
public sealed class PvSyncPreviewDto
{
    public int Read { get; set; }
    public int Matched { get; set; }
    public int Changed { get; set; }
    public int Same { get; set; }

    public int UnknownCount { get; set; }
    public List<string> Unknown { get; set; } = [];

    /// <summary>원장에는 있는데 이번에 안 온 건수. <b>오류가 아니다</b> — 범위를 좁혀 걷으면 늘 생긴다.</summary>
    public int MissingCount { get; set; }

    public List<PvSyncDiffRowDto> Missing { get; set; } = [];

    public List<string> Fields { get; set; } = [];
    public bool ClearEmpty { get; set; }

    public List<PvSyncDiffRowDto> Rows { get; set; } = [];
}

/// <summary>반영 결과.</summary>
public sealed class PvSyncAppliedDto
{
    public int Matched { get; set; }
    public int Applied { get; set; }
    public int UnknownCount { get; set; }
    public List<PvSyncDiffRowDto> Done { get; set; } = [];
}

/// <summary>동기화 요청.</summary>
public sealed class PvSyncRequestDto
{
    public List<PvSyncItemDto> Items { get; set; } = [];

    /// <summary>고칠 칸. 비면 넷 전부.</summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>
    /// ProjectView 가 빈 값이면 원장도 비운다. <b>기본이 참</b>이다 —
    /// 날짜의 기준이 그쪽이라서.
    /// </summary>
    public bool ClearEmpty { get; set; } = true;
}
