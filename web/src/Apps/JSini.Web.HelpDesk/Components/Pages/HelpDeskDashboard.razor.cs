using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Http;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class HelpDeskDashboard
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    private sealed record Tile(string Label, int Count);

    /// <summary>서버가 준 상태별 건수. 칸 이름이 담당자·고객 응답에서 같다.</summary>
    private Dictionary<string, int> _counts = [];

    private IReadOnlyList<ImprovementRequest> _recent = [];

    /// <summary>
    /// 타일 여섯. 옛 화면과 같은 차례다.
    ///
    /// 「전체」를 서버가 준 값(<c>TotalCount</c>/<c>TotalRequests</c>)으로 쓰지
    /// 않고 더해서 만든다 — 두 응답에서 그 칸 이름이 다르고, 담당자 쪽의
    /// <c>TotalRequests</c> 는 「내 것」이 아니라 시스템 전체라 나머지 타일과
    /// 합이 안 맞아 보인다.
    /// </summary>
    private IReadOnlyList<Tile> Tiles =>
    [
        new("대기", Count("pendingCount")),
        new("진행", Count("inProgressCount")),
        new("완료", Count("completedCount")),
        new("종료", Count("userCompletedCount")),
        new("협의", Count("consultationCount") + Count("negotiationCount")),
        new("반려", Count("rejectedCount")),
    ];

    private int Count(string key) => _counts.GetValueOrDefault(key);

    // ── 차트 재료 ───────────────────────────────────────────

    private sealed record Slice(string Label, int Count);

    private sealed record MonthPoint(string Label, int Total, int Completed);

    /// <summary>옛 화면(customer.vue)의 상태 팔레트 그대로. 열쇠는 타일 이름.</summary>
    private static readonly Dictionary<string, string> StatusColors = new(StringComparer.Ordinal)
    {
        ["대기"] = "#3B82F6",
        ["진행"] = "#F97316",
        ["완료"] = "#22C55E",
        ["종료"] = "#16A34A",
        ["협의"] = "#EAB308",
        ["반려"] = "#EF4444",
    };

    /// <summary>접수·완료 막대 색 — 옛 화면의 월별 차트 색 그대로.</summary>
    private static readonly string[] MonthlyPalette = ["#42A5F5", "#66BB6A"];

    /// <summary>
    /// 도넛 조각. 0 건인 상태는 뺀다 — 조각은 안 그려지는데 범례만 남아
    /// 도넛과 범례가 어긋나 보인다.
    /// </summary>
    private IReadOnlyList<Slice> Slices =>
        [.. Tiles.Where(t => t.Count > 0).Select(t => new Slice(t.Label, t.Count))];

    /// <summary>조각 색. <see cref="Slices" /> 와 같은 조건·순서로 골라야 어긋나지 않는다.</summary>
    private string[] SlicePalette =>
        [.. Tiles.Where(t => t.Count > 0).Select(t => StatusColors[t.Label])];

    private int TotalTileCount => Tiles.Sum(t => t.Count);

    private IReadOnlyList<MonthPoint> _monthly = [];

    protected override async Task OnInitializedAsync()
    {
        // 신원을 먼저 안다. 담당자냐 고객이냐에 따라 부를 주소가 갈린다.
        await Context.LoadIdentityAsync();
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _counts = await LoadCountsAsync();
        _recent = await LoadRecentAsync();
        _monthly = await LoadMonthlyAsync();

        // 집계가 비어도 화면은 타일 여섯을 0 으로 그린다 — 그것이 사실이다.
        // 「없습니다」 는 최근 접수까지 비었을 때만 띄운다.
        return _counts.Count + _recent.Count;
    }, "아직 접수된 요청이 없습니다.", "현황을 읽지 못했습니다");

    /// <summary>
    /// 월별 접수·완료 (최근 12개월). <b>고객으로 연결된 계정만</b> 부를 수 있는
    /// 주소라(담당자는 403) 신원을 보고 건너뛴다 — 머리 주석 참고.
    /// </summary>
    private async Task<IReadOnlyList<MonthPoint>> LoadMonthlyAsync()
    {
        if (!Context.IsLinked || Context.IsAdmin)
        {
            return [];
        }

        try
        {
            var rows = await Api.GetListAsync<MonthlyStat>("dashboard/my-monthly-stats");
            return [.. rows.Select(r => new MonthPoint(MonthLabel(r.Month), r.TotalCount, r.CompletedCount))];
        }
        catch (ApiException)
        {
            // 곁들이는 차트다. 이것이 거절당했다고(권한 판정이 서버와 어긋나는
            // 경우 등) 타일·최근 접수까지 오류로 덮지 않는다 — 차트만 뺀다.
            return [];
        }
    }

    /// <summary>서버의 <c>yyyy-MM</c> 을 옛 화면과 같은 <c>n월</c> 로 줄인다.</summary>
    private static string MonthLabel(string? month)
    {
        var parts = (month ?? string.Empty).Split('-');
        return parts.Length == 2 && int.TryParse(parts[1], out var m) ? $"{m}월" : month ?? string.Empty;
    }

    private sealed class MonthlyStat
    {
        public string? Month { get; set; }
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
    }

    /// <summary>
    /// 상태별 건수를 읽는다. <b>부를 주소가 신원에 따라 셋으로 갈린다.</b>
    ///
    ///   이어진 담당자   <c>dashboard/admin-stats</c>       내가 맡은 것
    ///   이어진 고객     <c>dashboard/my-company-stats</c>  우리 회사가 올린 것
    ///   연결이 없음     <c>dashboard/requests/status-count</c> 전체 집계
    ///
    /// **연결이 없으면 앞의 둘을 부르면 안 된다.** 서버가 「연결된 계정만
    /// 조회할 수 있습니다」로 거절하고, 화면에는 그 문구가 오류처럼 뜬다.
    /// 담당자만 챙기고 넘어갔다가 실제로 그렇게 됐다 — 연결 없는 계정으로
    /// 열면 「현황을 읽지 못했습니다」만 보였다.
    ///
    /// 전체 집계로 떨어질 때는 화면이 그 사실을 안내로 밝힌다. 안 밝히면
    /// 남의 것까지 센 숫자를 자기 것으로 읽는다.
    /// </summary>
    private async Task<Dictionary<string, int>> LoadCountsAsync()
    {
        if (!Context.IsLinked)
        {
            var rows = await Api.GetListAsync<StatusCount>("dashboard/requests/status-count");

            return rows
                .Where(r => r.Status is not null)
                .GroupBy(r => StatusKey(r.Status!))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        }

        var path = Context.IsAdmin ? "dashboard/admin-stats" : "dashboard/my-company-stats";
        var stats = await Api.GetAsync<JsonElement>(path);

        return ToCounts(stats);
    }

    /// <summary>
    /// 통계 응답을 이름→숫자로 편다.
    ///
    /// 두 응답의 모양이 같아 보여도 서버가 칸을 더할 수 있어 DTO 로 못박지
    /// 않았다. 숫자 칸만 골라 담으면 새 칸이 생겨도 화면이 깨지지 않는다.
    /// </summary>
    private static Dictionary<string, int> ToCounts(JsonElement stats)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (stats.ValueKind != JsonValueKind.Object)
        {
            return counts;
        }

        foreach (var property in stats.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out var value))
            {
                counts[property.Name] = value;
            }
        }

        return counts;
    }

    private async Task<IReadOnlyList<ImprovementRequest>> LoadRecentAsync()
    {
        // `topN` 은 필수다 — 안 보내면 서버가 400 을 준다.
        var rows = await Api.GetListAsync<ImprovementRequest>(
            "dashboard/requests/recent", new { topN = 10 });

        return rows;
    }

    /// <summary>
    /// 상태 이름을 타일 열쇠로 맞춘다.
    ///
    /// `status-count` 는 enum 이름(`Pending`)을 주고 통계 응답은 칸 이름
    /// (`pendingCount`)을 준다. 둘을 한 표에 담으려면 한쪽으로 맞춰야 한다.
    /// </summary>
    private static string StatusKey(string status) =>
        char.ToLowerInvariant(status[0]) + status[1..] + "Count";

    private static string StatusText(ImprovementRequest r) =>
        !string.IsNullOrWhiteSpace(r.StatusName) ? r.StatusName! : StatusText(r.Status);

    private static string StatusText(string? status) => status switch
    {
        "Pending" or "0" => "대기",
        "InProgress" or "1" => "진행",
        "Rejected" or "2" => "반려",
        "Completed" or "3" => "완료",
        "Consultation" => "협의",
        "Negotiation" => "논의",
        "UserCompleted" => "종료",
        _ => status ?? "-",
    };

    private static string StatusClass(string? status) => status switch
    {
        "Completed" or "UserCompleted" => "jsini-badge--on",
        "InProgress" or "Consultation" or "Negotiation" => "jsini-badge--warn",
        "Rejected" => "jsini-badge--off",
        _ => "",
    };

    private sealed class StatusCount
    {
        public string? Status { get; set; }
        public int Count { get; set; }
    }
}
