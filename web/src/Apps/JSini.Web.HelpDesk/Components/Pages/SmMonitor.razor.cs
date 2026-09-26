using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Http;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class SmMonitor
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => $"{_year} 년 {_month} 월";

    /// <summary>이름과 숫자 한 쌍. 상태·유형 분포가 이 모양이다.</summary>
    public sealed record Pair(string Label, int Value);

    /// <summary>날짜별 접수·완료. 그래프의 재료다.</summary>
    public sealed record DailyStat(int Day, int Created, int Completed);

    private static readonly Dictionary<string, string> EmergencyCaptions = new(StringComparer.Ordinal)
    {
        ["title"] = "제목",
        ["requestedAt"] = "접수",
        ["status"] = "상태",
        ["customerName"] = "요청자",
    };

    private static readonly Dictionary<string, string> WorkloadCaptions = new(StringComparer.Ordinal)
    {
        ["adminName"] = "담당자",
        ["assignedCount"] = "배정",
        ["completedCount"] = "완료",
        ["inProgressCount"] = "진행 중",
    };

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;

    private MonthlyReport? _report;
    private DataTable _emergency = JsonTable.Empty;
    private DataTable _workload = JsonTable.Empty;

    private IReadOnlyList<Pair> _byStatus = [];
    private IReadOnlyList<Pair> _byType = [];
    private IReadOnlyList<DailyStat> _daily = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var period = new { year = _year, month = _month };

        // 셋을 나란히. **하나가 실패해도 나머지는 보여 준다** —
        // 현황판은 떠 있는 것이 중요하다.
        var report = SafeAsync(() => Api.GetAsync<MonthlyReport>("requests/report/monthly", period));
        var emergency = SafeAsync(() => Api.GetAsync<JsonElement>("requests/report/emergency"));
        var workload = SafeAsync(() => Api.GetAsync<JsonElement>("dashboard/admin-contribution-stats", period));

        await Task.WhenAll(report, emergency, workload);

        _report = report.Result;
        _emergency = JsonTable.From(emergency.Result);
        _workload = JsonTable.From(workload.Result);

        _byStatus = Pairs(_report?.RequestsByStatus);
        _byType = Pairs(_report?.RequestsByType);

        _daily = _report?.DailyStats is { Count: > 0 } days
            ? [.. days.Select(d => new DailyStat(d.Day, d.RequestCount, d.CompletedCount))]
            : [];

        return (_report?.TotalRequests ?? 0) + _emergency.Rows.Count;
    }, "그 달에 접수된 요청이 없습니다.", "현황을 읽지 못했습니다");

    /// <summary>실패를 삼키고 <c>default</c> 를 돌려준다. 까닭은 화면 머리말에 있다.</summary>
    private static async Task<T?> SafeAsync<T>(Func<Task<T?>> load)
    {
        try
        {
            return await load();
        }
        catch (ApiException)
        {
            return default;
        }
    }

    /// <summary>서버가 준 (이름, 건수) 배열을 많은 차례로 세운다.</summary>
    private static IReadOnlyList<Pair> Pairs(List<KeyCount>? source) =>
        source is null ? [] : [.. source.Select(kv => new Pair(kv.Key, kv.Value)).OrderByDescending(p => p.Value)];
}
