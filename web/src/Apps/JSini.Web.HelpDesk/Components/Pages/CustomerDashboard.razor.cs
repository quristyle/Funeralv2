using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class CustomerDashboard
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다. 칸 이름은 서버 응답과 대조했다 — 머리 주석 참고.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["companyName"] = "고객사",
        ["pendingCount"] = "대기",
        ["inProgressCount"] = "진행",
        ["consultationCount"] = "협의",
        ["negotiationCount"] = "논의",
        ["completedCount"] = "완료",
        ["rejectedCount"] = "반려",
        ["completionRate"] = "완료율(%)",
        ["lastPendingDate"] = "최근 대기 접수",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record CompanyPoint(
        string Company, int Pending, int InProgress, int Consultation,
        int Negotiation, int Completed, int Rejected);

    /// <summary>옛 화면(customer.vue)의 상태 팔레트 — 시리즈 순서와 같아야 한다.</summary>
    private static readonly string[] StatusPalette =
        ["#3B82F6", "#F97316", "#EAB308", "#A855F7", "#22C55E", "#EF4444"];

    private IReadOnlyList<CompanyPoint> _chart = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Api.GetAsync<JsonElement>("dashboard/company-stats");
        _rows = JsonTable.From(json);
        _chart = ToChart(_rows);
        return _rows.Rows.Count;
    }, "집계할 고객사가 없습니다.", "고객사 현황을 읽지 못했습니다");

    /// <summary>
    /// 표와 같은 응답에서 차트 재료를 뽑는다. 칸이 있는지 먼저 본다 —
    /// AutoGrid 방식 화면이라 서버가 칸을 바꿔도 표는 살아야 하고,
    /// 그때 차트만 조용히 빠지는 쪽이 화면이 죽는 쪽보다 낫다.
    /// </summary>
    private static IReadOnlyList<CompanyPoint> ToChart(DataTable rows)
    {
        if (!rows.Columns.Contains("companyName"))
        {
            return [];
        }

        return [.. rows.Rows.Cast<DataRow>().Select(r => new CompanyPoint(
            r["companyName"] as string ?? "-",
            Cell(r, "pendingCount"),
            Cell(r, "inProgressCount"),
            Cell(r, "consultationCount"),
            Cell(r, "negotiationCount"),
            Cell(r, "completedCount"),
            Cell(r, "rejectedCount")))];
    }

    private static int Cell(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToInt32(row[column])
            : 0;
}
