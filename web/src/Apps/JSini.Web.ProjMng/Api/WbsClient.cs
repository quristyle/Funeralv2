using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>WBS · 일정표 REST 클라이언트. 옛 프로시저 둘(sp_proj_wbs_exec · sp_proj_wbs_moniter)을 대신한다. 등록·수정자는 서버가 채운다.</summary>
public sealed class WbsClient(GatewayClient gateway)
{
    private const string Url = "projmng/wbs";

    /// <summary>목록.</summary>
    /// <param name="prjRid">프로젝트</param>
    /// <param name="compStat"><c>READY</c> · <c>RUNNING</c> · <c>COMP</c> · <c>DISCOMP</c></param>
    /// <param name="scheduleTypes">구분(WBS · Public · Private · Event). 여럿이면 값마다 scheduleType= 을 따로 싣는다 — 쉼표로 잇면 구분값에 쉼표가 들어가는 날 갈라진다. 비면 전체.</param>
    /// <param name="from">계획 기간이 이 날 뒤에 걸치는 것만</param>
    /// <param name="to">계획 기간이 이 날 앞에 걸치는 것만</param>
    /// <param name="ct">취소 토큰</param>
    public Task<IReadOnlyList<WbsItemDto>> ListAsync(
        int? prjRid = null, string? compStat = null, IEnumerable<string>? scheduleTypes = null,
        DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (prjRid is not null) query.Add($"prjRid={prjRid}");
        if (!string.IsNullOrWhiteSpace(compStat)) query.Add($"compStat={Uri.EscapeDataString(compStat)}");
        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");

        foreach (var type in scheduleTypes ?? [])
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                query.Add($"scheduleType={Uri.EscapeDataString(type)}");
            }
        }

        return gateway.GetListAsync<WbsItemDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    public Task<WbsItemDto?> CreateAsync(WbsItemDto item, CancellationToken ct = default)
        => gateway.PostAsync<WbsItemDto>(Url, item, ct);

    public Task<WbsItemDto?> UpdateAsync(WbsItemDto item, CancellationToken ct = default)
        => gateway.PutAsync<WbsItemDto>($"{Url}/{item.WbsId}", item, ct);

    public Task DeleteAsync(int wbsId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{wbsId}", ct);

    /// <summary>진척 집계. 프로젝트 하나를 본다.</summary>
    public Task<WbsSummaryDto?> SummaryAsync(int prjRid, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsSummaryDto>($"{Url}/summary?prjRid={prjRid}", ct);
}

/// <summary>WBS 한 줄. 일정표와 <b>같은 표</b>다 — <see cref="ScheduleType"/> 로 갈린다.</summary>
public sealed class WbsItemDto
{
    public int WbsId { get; set; }
    public int? PrjRid { get; set; }

    /// <summary>사람이 붙인 공정 번호. 목록 정렬의 첫 열쇠다.</summary>
    public string? ProcId { get; set; }

    public string? Gb1 { get; set; }
    public string? Gb2 { get; set; }
    public string? ProcNm { get; set; }
    public string? ProcTp { get; set; }
    public string? ProcLvl { get; set; }

    public string? BuildUser { get; set; }
    public string? BuildStatus { get; set; }
    public string? DevUser { get; set; }

    public DateOnly? PlanSdt { get; set; }
    public DateOnly? PlanEdt { get; set; }
    public DateOnly? DevSdt { get; set; }
    public DateOnly? DevEdt { get; set; }

    public string? DevChk { get; set; }
    public string? BuildChk { get; set; }
    public DateOnly? BuildChkDt { get; set; }

    public string? QcUser { get; set; }
    public string? QcChk { get; set; }
    public DateOnly? QcChkDt { get; set; }

    public string? CreUser { get; set; }
    public DateOnly? CreDt { get; set; }
    public string? ModUser { get; set; }
    public DateOnly? ModDt { get; set; }

    public string? Comm { get; set; }

    /// <summary>일정 요약. 일정 메모 화면이 채운다. null 로 보내면 서버가 저장된 값을 그대로 둔다.</summary>
    public string? Summary { get; set; }

    /// <summary>일정 메모 화면에서 적은 본문 원문. null 이면 그대로 둔다.</summary>
    public string? BodyText { get; set; }

    /// <summary><c>WBS</c> 또는 <c>SCHEDULE</c>.</summary>
    public string? ScheduleType { get; set; }

    /// <summary>계획 기간(일). <b>읽기 전용</b> — 서버가 센다.</summary>
    public int? PlanGap { get; set; }

    /// <summary>
    /// 진행 상태(<c>READY</c> · <c>RUNNING</c> · <c>COMP</c>). <b>읽기 전용</b> —
    /// 개발 시작·종료일로 정해진다.
    /// </summary>
    public string? WbsState { get; set; }

    /// <summary>화면에 보여 줄 상태 이름.</summary>
    public string StateName => WbsState switch
    {
        "COMP" => "완료",
        "RUNNING" => "진행",
        "READY" => "대기",
        _ => string.Empty,
    };

    /// <summary>상태 딱지의 CSS 클래스. 화면이 아니라 여기 둔다 — WBS 와 일정표가 같은 표를 보므로 같은 색이어야 한다.</summary>
    public string StateClass => WbsState switch
    {
        "COMP" => "jsini-badge--on",
        "RUNNING" => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };
}

/// <summary>프로젝트 하나의 진척 집계.</summary>
public sealed class WbsSummaryDto
{
    public int TotalTaskCount { get; set; }

    public int CompletedTaskCount { get; set; }
    public decimal CompletedTaskPct { get; set; }

    /// <summary>완료 + 진행 중.</summary>
    public int CompAndIngCnt { get; set; }

    public int CompletedWithinPlanCount { get; set; }
    public decimal CompletedWithinPlanPct { get; set; }

    /// <summary>계획 종료일이 지났는데 아직 안 끝난 건수.</summary>
    public int DelayedTaskCount { get; set; }
    public decimal DelayedTaskPct { get; set; }

    public int InProgressTaskCount { get; set; }
    public decimal InProgressTaskPct { get; set; }

    public int NotStartedYetTaskCount { get; set; }
    public decimal NotStartedYetPct { get; set; }

    public int PlannedsUntilNowCount { get; set; }
    public decimal PlannedsUntilNowPct { get; set; }

    public int PlannedUntilNowCount { get; set; }
    public decimal PlannedUntilNowPct { get; set; }
}
