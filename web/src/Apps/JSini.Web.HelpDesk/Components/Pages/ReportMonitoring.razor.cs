using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportMonitoring
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["adminName"] = "담당자",
        ["openCount"] = "진행 중",
        ["closedCount"] = "완료",
        ["avgHours"] = "평균 처리(h)",
    };

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record StatusBar(string Label, int Count);

    private static readonly string[] BarPalette = ["#42A5F5"];

    private IReadOnlyList<StatusBar> _bars = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Api.GetAsync<JsonElement>("dashboard/admin-stats");
        _rows = JsonTable.From(json);
        _bars = ToBars(json);

        // 응답이 객체라 표는 비고(위 주석) 차트만 남는다.
        // 차트에 그릴 것이 있으면 「없습니다」 를 띄우지 않는다.
        return _rows.Rows.Count + (_bars.Sum(b => b.Count) > 0 ? 1 : 0);
    }, "집계할 자료가 없습니다.", "운영 모니터링을 읽지 못했습니다");

    /// <summary>
    /// 상태별 건수 막대. 칸 이름은 서버 <c>AdminStatsDto</c> 와 대조했다.
    /// 대기는 「내 것」이 아니라 <b>팀 관할 미배정</b> 건수라(서버 주석) 이름을
    /// 따로 붙인다 — 「대기」로만 적으면 내 배정 대기로 읽힌다.
    /// </summary>
    private static IReadOnlyList<StatusBar> ToBars(JsonElement json) =>
    [
        new("미배정(팀)", Num(json, "pendingCount")),
        new("진행", Num(json, "inProgressCount")),
        new("협의", Num(json, "consultationCount")),
        new("논의", Num(json, "negotiationCount")),
        new("완료", Num(json, "completedCount")),
        new("종료", Num(json, "userCompletedCount")),
        new("반려", Num(json, "rejectedCount")),
    ];

    private static int Num(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var n)
            ? n
            : 0;
}
