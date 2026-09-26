using Microsoft.AspNetCore.Components;
using System.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class MaintenanceMonitor
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => $"{_year} 년 {_month} 월";

    /// <summary>해결 시간 구간 한 칸.</summary>
    public sealed record Bucket(string Label, int Count);

    private int _year;
    private int _month;

    private MonthlyReport? _report;
    private IReadOnlyList<DailyRequestStat> _daily = [];
    private IReadOnlyList<Bucket> _spread = [];

    protected override async Task OnInitializedAsync()
    {
        // 지난달로 연다 — 이번 달은 아직 진행 중이라 숫자가 매일 바뀐다.
        var last = DateTime.Today.AddMonths(-1);
        _year = last.Year;
        _month = last.Month;

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        await Context.LoadIdentityAsync();

        // 고객으로 연결된 계정은 자기 회사만 본다. 관리자는 비워 전체를 본다.
        _report = Context.CompanyId is { } companyId
            ? await Api.GetAsync<MonthlyReport>(
                "requests/report/monthly", new { year = _year, month = _month, companyId })
            : await Api.GetAsync<MonthlyReport>(
                "requests/report/monthly", new { year = _year, month = _month });

        _daily = _report?.DailyStats ?? [];

        _spread = _report?.ResolutionTimeDistribution is { Count: > 0 } dist
            ? [.. dist.Select(kv => new Bucket(kv.Key, kv.Value))]
            : [];

        // 전부 0 이면 보여 줄 것이 없다. 구간 이름만 늘어놓으면 자료가 있는
        // 것처럼 읽힌다.
        if (_spread.All(b => b.Count == 0))
        {
            _spread = [];
        }

        return _report?.TotalRequests ?? 0;
    }, "그 달에 접수된 요청이 없습니다.", "현황을 읽지 못했습니다");
}
