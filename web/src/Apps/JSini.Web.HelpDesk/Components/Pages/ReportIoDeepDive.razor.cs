using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportIoDeepDive
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 칸 이름은 서버 `report/emergency` 응답과 대조했다 — 머리 주석 참고.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["title"] = "제목",
        ["status"] = "상태",
        ["time"] = "접수 시각",
        ["severity"] = "심각도",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record StatusBar(string Label, int Count);

    /// <summary>긴급 건이라 옛 화면의 경고 팔레트 첫 색(붉은색)을 쓴다.</summary>
    private static readonly string[] BarPalette = ["#EF4444"];

    private IReadOnlyList<StatusBar> _bars = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 긴급 건 보고서. `dashboard` 가 아니라 `requests` 묶음 밑이다.
        // 이것만 기간 인자가 없다.
        var json = await Api.GetAsync<JsonElement>("requests/report/emergency");
        _rows = JsonTable.From(json);
        _bars = ToBars(_rows);
        return _rows.Rows.Count;
    }, "진행 중인 긴급 건이 없습니다.", "IO 심층 분석을 읽지 못했습니다");

    /// <summary>
    /// 표와 같은 응답을 상태별로 센다. 완료·종료는 서버가 이미 걸러서 안 온다.
    /// 칸이 없으면 차트만 조용히 빠지고 표는 남는다.
    /// </summary>
    private static IReadOnlyList<StatusBar> ToBars(DataTable rows)
    {
        if (!rows.Columns.Contains("status"))
        {
            return [];
        }

        return [.. rows.Rows.Cast<DataRow>()
            .GroupBy(r => StatusText(r["status"] as string))
            .Select(g => new StatusBar(g.Key, g.Count()))];
    }

    /// <summary>서버는 enum 이름(`Pending`)을 준다. 다른 화면들과 같은 한글 이름으로.</summary>
    private static string StatusText(string? status) => status switch
    {
        "Pending" => "대기",
        "InProgress" => "진행",
        "Consultation" => "협의",
        "Negotiation" => "논의",
        "Rejected" => "반려",
        _ => status ?? "-",
    };
}
