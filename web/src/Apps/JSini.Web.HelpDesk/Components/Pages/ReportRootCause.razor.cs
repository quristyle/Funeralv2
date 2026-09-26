using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportRootCause
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["category"] = "분류",
        ["count"] = "건수",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record UserSlice(string Label, double Value);

    /// <summary>옛 화면(root-cause.vue) 도넛의 팔레트에 두 색을 보탠 것 — 조각이 최대 다섯이다.</summary>
    private static readonly string[] SlicePalette =
        ["#3B82F6", "#6366F1", "#22C55E", "#F59E0B", "#94A3B8"];

    private IReadOnlyList<UserSlice> _slices = [];

    /// <summary>사용자 확정률(%). 제목에 함께 적는다.</summary>
    private double _confirmationRate;

    private string _periodLabel = string.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 보고서는 `requests` 묶음 밑이고 `year`·`month` 가 필수다.
        var now = DateTime.Now;
        _periodLabel = $"{now.Year}년 {now.Month}월";

        var json = await Api.GetAsync<JsonElement>(
            "requests/report/collaboration", new { year = now.Year, month = now.Month });
        _rows = JsonTable.From(json);
        _slices = ToSlices(json);
        _confirmationRate = Dbl(json, "confirmationRate");

        // 응답이 객체라 표는 비고(위 주석) 차트만 남는다.
        // 차트에 그릴 것이 있으면 「없습니다」 를 띄우지 않는다.
        return _rows.Rows.Count + _slices.Count;
    }, "집계할 자료가 없습니다.", "원인 분석을 읽지 못했습니다");

    /// <summary>
    /// 객체 응답에서 `topEngagedUsers` 만 뽑는다. 칸 이름(name·company·total)은
    /// 서버 `report/collaboration` 응답과 대조했다. 점수 0 은 뺀다 — 조각은
    /// 안 그려지는데 범례만 남는다.
    /// </summary>
    private static IReadOnlyList<UserSlice> ToSlices(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("topEngagedUsers", out var users)
            || users.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. users.EnumerateArray()
            .Where(u => u.ValueKind == JsonValueKind.Object)
            .Select(u => new UserSlice(
                u.TryGetProperty("name", out var name) ? name.GetString() ?? "-" : "-",
                Dbl(u, "total")))
            .Where(s => s.Value > 0)];
    }

    private static double Dbl(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;
}
