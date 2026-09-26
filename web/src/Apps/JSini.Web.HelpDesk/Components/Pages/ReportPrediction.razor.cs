using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportPrediction
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["companyName"] = "고객사",
        ["score"] = "지표",
        ["trend"] = "추세",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record RatioBar(string Label, double Value);

    /// <summary>위험 신호 막대라 옛 화면의 경고 팔레트 첫 색(붉은색)을 쓴다.</summary>
    private static readonly string[] BarPalette = ["#EF4444"];

    private IReadOnlyList<RatioBar> _bars = [];

    /// <summary>분석 대상 건수(<c>totalAnalyzed</c>). 0 이면 그릴 것이 없다.</summary>
    private int _analyzed;

    /// <summary>재작업(재오픈) 건수(<c>rollbackCount</c>). 제목에 함께 적는다.</summary>
    private int _rollback;

    private string _periodLabel = string.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 보고서는 `requests` 묶음 밑이고 `year`·`month` 가 필수다.
        var now = DateTime.Now;
        _periodLabel = $"{now.Year}년 {now.Month}월";

        var json = await Api.GetAsync<JsonElement>(
            "requests/report/quality", new { year = now.Year, month = now.Month });
        _rows = JsonTable.From(json);
        _bars = ToBars(json);
        _analyzed = Num(json, "totalAnalyzed");
        _rollback = Num(json, "rollbackCount");

        // 응답이 객체라 표는 비고(위 주석) 차트만 남는다.
        // 차트에 그릴 것이 있으면 「없습니다」 를 띄우지 않는다.
        return _rows.Rows.Count + (_analyzed > 0 ? 1 : 0);
    }, "집계할 자료가 없습니다.", "장애 예측을 읽지 못했습니다");

    /// <summary>위험 신호 두 비율. 칸 이름은 서버 `report/quality` 응답과 대조했다.</summary>
    private static IReadOnlyList<RatioBar> ToBars(JsonElement json) =>
    [
        new("결함 비율", Dbl(json, "bugRatio")),
        new("재오픈율", Dbl(json, "reopenRate")),
    ];

    private static double Dbl(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    private static int Num(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var n)
            ? n
            : 0;
}
