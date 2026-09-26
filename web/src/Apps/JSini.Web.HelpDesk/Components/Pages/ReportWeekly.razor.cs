using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportWeekly
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 두 갈래의 칸을 함께 적는다 — 응답에 없는 칸은 AutoGrid 가 그냥 넘긴다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["month"] = "월",
        ["totalCount"] = "접수",
        ["completedCount"] = "완료",
        ["week"] = "주(시작일)",
        ["total"] = "완료",
        ["admins"] = "담당자별 몫(원문)",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record TrendPoint(string Label, int Total, int Completed);

    /// <summary>접수·완료 색 — 옛 화면의 접수·완료 색 그대로.</summary>
    private static readonly string[] TrendPalette = ["#42A5F5", "#66BB6A"];

    private IReadOnlyList<TrendPoint> _trend = [];

    /// <summary>고객 갈래였는가. 차트의 시리즈 구성이 갈래를 따라간다.</summary>
    private bool _customer;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // `my-monthly-stats` 는 **고객으로 연결된 계정만** 부를 수 있다
        // (담당자가 부르면 403). 이 화면은 담당자도 보므로 신원에 따라 갈린다.
        await Context.LoadIdentityAsync();
        _customer = Context.IsLinked && !Context.IsAdmin;

        var json = _customer
            ? await Api.GetAsync<JsonElement>("dashboard/my-monthly-stats")
            : await Api.GetAsync<JsonElement>("dashboard/admin-contribution-trend");
        _rows = JsonTable.From(json);
        _trend = ToTrend(_rows, _customer);
        return _rows.Rows.Count;
    }, "집계할 자료가 없습니다.", "주간 리포트를 읽지 못했습니다");

    /// <summary>
    /// 표와 같은 응답에서 추이 점을 뽑는다. 칸이 있는지 먼저 본다 —
    /// 서버가 칸을 바꾸면 차트만 조용히 빠지고 표는 남는다.
    /// </summary>
    private static IReadOnlyList<TrendPoint> ToTrend(DataTable rows, bool customer)
    {
        var argColumn = customer ? "month" : "week";
        var totalColumn = customer ? "totalCount" : "total";

        if (!rows.Columns.Contains(argColumn) || !rows.Columns.Contains(totalColumn))
        {
            return [];
        }

        return [.. rows.Rows.Cast<DataRow>().Select(r => new TrendPoint(
            customer ? MonthLabel(r[argColumn] as string) : r[argColumn] as string ?? "-",
            Cell(r, totalColumn),
            Cell(r, "completedCount")))];
    }

    /// <summary>서버의 <c>yyyy-MM</c> 을 옛 화면과 같은 <c>n월</c> 로 줄인다.</summary>
    private static string MonthLabel(string? month)
    {
        var parts = (month ?? string.Empty).Split('-');
        return parts.Length == 2 && int.TryParse(parts[1], out var m) ? $"{m}월" : month ?? string.Empty;
    }

    private static int Cell(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToInt32(row[column])
            : 0;
}
