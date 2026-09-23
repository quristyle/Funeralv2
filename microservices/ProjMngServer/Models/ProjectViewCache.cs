namespace ProjMngServer.Models;

// ProjectView 캐시 — `projmng.wbs_pv` · `wbs_pv_task` · `wbs_pv_node`.
//
// **셋 다 캐시다.** 통째로 지워도 대시보드는 그대로 돌고, 예전처럼 매번 다시
// 물어보는 느린 길로 돌아갈 뿐이다. 원장(`wbs_work`)은 수집 쪽에서 절대 고치지
// 않는다 — 고치는 것은 [동기화] 화면의 「반영」 하나다.

/// <summary>캐시 현황 한 벌.</summary>
public sealed class PvCacheStatus
{
    public string? Scope { get; set; }
    public PvWorkStat? Works { get; set; }
    public PvTaskStat? Tasks { get; set; }

    /// <summary>
    /// 원장에 없는데 캐시에만 있는 건수. 프로젝트에서 빠졌거나 코드가 바뀐 것이다.
    /// <b>지우지 않고 세기만 한다.</b>
    /// </summary>
    public int Orphan { get; set; }
}

/// <inheritdoc cref="PvCacheStatus"/>
public sealed class PvWorkStat
{
    /// <summary>원장에서 수집 대상인 건수.</summary>
    public int Targets { get; set; }

    /// <summary>그중 ProjectView 쪽 번호를 알아낸 건수.</summary>
    public int Identified { get; set; }

    /// <summary>현재값을 한 번이라도 담아 둔 건수.</summary>
    public int Snapshot { get; set; }

    public string? LastSeen { get; set; }
    public string? LastSnapshot { get; set; }
    public string? ProjectId { get; set; }
}

/// <inheritdoc cref="PvCacheStatus"/>
public sealed class PvTaskStat
{
    public int Tasks { get; set; }
    public int Nodes { get; set; }

    /// <summary>날짜나 담당자가 비어 있는 단계 수. [워크플로 채우기] 화면이 이것을 줄인다.</summary>
    public int NodesEmpty { get; set; }

    public int Charged { get; set; }
    public int Staged { get; set; }
    public string? LastSeen { get; set; }

    /// <summary>실제로 담아 둔 단계 줄 수. 위 <see cref="Nodes"/> 는 일감이 신고한 개수다.</summary>
    public int NodesCached { get; set; }
}

/// <summary>액티비티별 캐시 한 줄. <b>원장 값과 나란히</b> 준다.</summary>
/// <remarks>
/// 화면이 「보낼 필요가 있는 건」을 스스로 셀 수 있어야 해서 두 쪽을 같이 내려보낸다.
/// </remarks>
public sealed class PvRow
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
public sealed class PvTask
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
public sealed class PvNode
{
    public string? PvTaskId { get; set; }
    public int NodeNo { get; set; }
    public string? StageNm { get; set; }
    public string? NodeDt { get; set; }
    public string? WorkerId { get; set; }
    public string? WorkerNm { get; set; }
}

/// <summary>일감과 그 단계를 함께 담은 응답.</summary>
public sealed class PvTaskBundle
{
    public List<PvTask> Tasks { get; set; } = [];
    public List<PvNode> Nodes { get; set; } = [];
}

/// <summary>수집 결과를 담은 뒤 돌려주는 값.</summary>
public sealed class PvIngestResult
{
    public int Works { get; set; }
    public int Tasks { get; set; }
    public int Nodes { get; set; }

    /// <summary>액티비티 번호가 없어 버린 건수.</summary>
    public int SkippedNoCode { get; set; }

    /// <summary>같은 번호가 두 번 온 건수. <b>첫 번째만 담는다.</b></summary>
    public int Duplicated { get; set; }

    public List<string> DuplicatedCodes { get; set; } = [];

    /// <summary>원장에 없는 번호. 알려만 주고 지우지 않는다.</summary>
    public int UnknownCount { get; set; }

    public List<string> Unknown { get; set; } = [];

    public string? ProjectId { get; set; }
}

/// <summary>캐시 비우기 결과.</summary>
public sealed class PvClearResult
{
    public int Works { get; set; }
    public int Tasks { get; set; }
    public int Nodes { get; set; }
}

// ──────────────────────────────────────────────── 원장 반영

/// <summary>동기화가 보내온 항목 하나.</summary>
public sealed class PvSyncItem
{
    public string? Code { get; set; }
    public string? PlanStartDate { get; set; }
    public string? PlanEndDate { get; set; }
    public string? ActualStartDate { get; set; }
    public string? ActualEndDate { get; set; }
}

/// <summary>한 칸의 바뀜.</summary>
public sealed class PvSyncChange
{
    public string? Col { get; set; }
    public string? Label { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>한 액티비티의 바뀜.</summary>
public sealed class PvSyncDiffRow
{
    public string? ActivityId { get; set; }
    public string? Systemcode { get; set; }
    public string? MenuNm { get; set; }
    public List<PvSyncChange> Changes { get; set; } = [];
}

/// <summary>미리보기 — <b>DB 를 건드리지 않는다.</b></summary>
public sealed class PvSyncPreview
{
    /// <summary>읽어들인 항목 수.</summary>
    public int Read { get; set; }

    /// <summary>원장에서 찾은 건수.</summary>
    public int Matched { get; set; }

    public int Changed { get; set; }
    public int Same { get; set; }

    /// <summary>원장에 없는 번호.</summary>
    public int UnknownCount { get; set; }

    public List<string> Unknown { get; set; } = [];

    /// <summary>원장에는 있는데 이번에 안 온 건수. 「빠뜨린 것 아닌가」를 알려 준다.</summary>
    public int MissingCount { get; set; }

    public List<PvSyncDiffRow> Missing { get; set; } = [];

    public List<string> Fields { get; set; } = [];
    public bool ClearEmpty { get; set; }

    public List<PvSyncDiffRow> Rows { get; set; } = [];
}

/// <summary>반영 결과.</summary>
public sealed class PvSyncApplied
{
    public int Matched { get; set; }

    /// <summary>실제로 고친 액티비티 수.</summary>
    public int Applied { get; set; }

    public int UnknownCount { get; set; }
    public List<PvSyncDiffRow> Done { get; set; } = [];
}

/// <summary>동기화 요청 한 벌.</summary>
public sealed class PvSyncRequest
{
    public List<PvSyncItem> Items { get; set; } = [];

    /// <summary>
    /// 고칠 칸. 비면 넷 전부
    /// (<c>plan_sdt</c>·<c>plan_edt</c>·<c>plan_sdt_c</c>·<c>plan_edt_c</c>).
    /// </summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>
    /// ProjectView 가 빈 값이면 원장도 비운다. <b>기본이 참</b>이다 —
    /// 날짜의 기준이 ProjectView 쪽이라서.
    /// </summary>
    public bool ClearEmpty { get; set; } = true;
}
