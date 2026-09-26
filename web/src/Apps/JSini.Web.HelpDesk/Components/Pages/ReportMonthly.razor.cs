using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportMonthly
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["period"] = "기간",
        ["companyName"] = "고객사",
        ["openCount"] = "접수",
        ["closedCount"] = "완료",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record DayPoint(string Label, int Requested, int Completed);

    /// <summary>접수·완료 색 — 옛 화면의 월별 차트 색 그대로.</summary>
    private static readonly string[] DailyPalette = ["#42A5F5", "#66BB6A"];

    private IReadOnlyList<DayPoint> _daily = [];

    private string _periodLabel = string.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 보고서는 `dashboard` 가 아니라 **`requests` 묶음** 밑에 있다.
        // `year`·`month` 가 필수다 — 없으면 400 이 난다.
        var now = DateTime.Now;
        _periodLabel = $"{now.Year}년 {now.Month}월";

        var json = await Api.GetAsync<JsonElement>(
            "requests/report/monthly", new { year = now.Year, month = now.Month });
        _rows = JsonTable.From(json);
        _daily = ToDaily(json);

        // 응답이 객체라 표는 비고(위 주석) 차트만 남는다.
        // 차트에 그릴 것이 있으면 「없습니다」 를 띄우지 않는다.
        return _rows.Rows.Count + (_daily.Sum(d => d.Requested + d.Completed) > 0 ? 1 : 0);
    }, "집계할 자료가 없습니다.", "월간 리포트를 읽지 못했습니다");

    /// <summary>
    /// 객체 응답에서 `dailyStats` 만 뽑는다. 칸 이름은 서버
    /// <c>DailyRequestStatDto</c>(day·requestCount·completedCount)와 대조했다.
    /// </summary>
    private static IReadOnlyList<DayPoint> ToDaily(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("dailyStats", out var daily)
            || daily.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. daily.EnumerateArray()
            .Where(d => d.ValueKind == JsonValueKind.Object)
            .Select(d => new DayPoint($"{Num(d, "day")}일", Num(d, "requestCount"), Num(d, "completedCount")))];
    }

    private static int Num(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.TryGetInt32(out var n) ? n : 0;
}
