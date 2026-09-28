using System.Drawing;
using System.Globalization;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class HelpDeskDashboard
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    /// <summary>
    /// 서버가 접어 준 현황 한 판. <b>조회 전에도 빈 판을 들고 있는다</b> —
    /// null 로 두면 화면 스물몇 자리에 `?.` 를 붙이게 되고, 한 곳만 빠뜨려도
    /// 프리렌더에서 널 참조로 죽는다.
    /// </summary>
    private DashboardOverview Data { get; set; } = new();

    // ── 조회 조건 ───────────────────────────────────────────

    /// <summary>고른 고객사. 「전체」면 null 이다.</summary>
    private string? _companyId;

    private string _days = "30";
    private string _months = "12";

    /// <summary>
    /// 회사를 고를 수 있는가. <b>시스템관리자만</b>이고, 서버가 이미 회사
    /// 하나로 묶어 버린 계정(고객)에게는 고를 것이 없다.
    /// </summary>
    private bool CanPickCompany => Context.IsSystemAdmin && !Data.Scope.CompanyScoped;

    private static readonly SchOption[] DayOptions =
    [
        new("14", "14일"),
        new("30", "30일"),
        new("60", "60일"),
        new("90", "90일"),
    ];

    private static readonly SchOption[] MonthOptions =
    [
        new("6", "6개월"),
        new("12", "12개월"),
        new("24", "24개월"),
    ];

    /// <summary>접힌 조회줄에 적을 한 줄. 고른 것을 가운뎃점으로 잇는다.</summary>
    private string ConditionSummary => SchSummary.Of(
        CanPickCompany
            ? (_companyId is null ? SchSummary.Any : Context.CompanyName(_companyId))
            : Data.Scope.Label,
        SchSummary.NameOf(DayOptions, o => o.Value, o => o.Text, _days),
        SchSummary.NameOf(MonthOptions, o => o.Value, o => o.Text, _months));

    /// <summary>구역 머리에 적는 곁말 — 이 숫자가 누구의 것인지.</summary>
    private string ScopeHint => Data.Scope.CompanyScoped
        ? $"{Data.Scope.Label} 한 곳"
        : $"고객사 {Data.Summary.ActiveCompanies}곳 · 요청자 {Data.Summary.Requesters}명";

    private string GeneratedLabel => Data.Summary.Total == 0
        ? string.Empty
        : $"{Data.Scope.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm} 기준";

    // ── 타일 ────────────────────────────────────────────────

    /// <summary>타일 한 칸. 값 아래 곁말이 붙는다.</summary>
    private sealed record Tile(string Label, string Value, string? Note = null);

    /// <summary>얼마나 들어오고 얼마나 나갔나.</summary>
    private IReadOnlyList<Tile> VolumeTiles =>
    [
        new("전체", Num(Data.Summary.Total), $"완료 {Data.Summary.Completed + Data.Summary.UserCompleted}건 · 반려 {Data.Summary.Rejected}건"),
        new("대기", Num(Data.Summary.Pending), $"미배정 {Data.Summary.Unassigned}건"),
        new("진행", Num(Data.Summary.InProgress), null),
        new("협의", Num(Data.Summary.Consultation + Data.Summary.Negotiation), $"협의 {Data.Summary.Consultation} · 논의 {Data.Summary.Negotiation}"),
        new("완료", Num(Data.Summary.Completed), $"종료 {Data.Summary.UserCompleted}건"),
        new("반려", Num(Data.Summary.Rejected), null),
        new("오늘 접수", Num(Data.Summary.Today), $"어제 {Data.Summary.Yesterday}건"),
        new("이번 주", Num(Data.Summary.ThisWeek), "월요일부터"),
        new("이번 달", Num(Data.Summary.ThisMonth), MonthOverMonthNote),
        new("오늘 완료", Num(Data.Summary.CompletedToday), $"이번 달 {Data.Summary.CompletedThisMonth}건"),
    ];

    /// <summary>얼마나 빨리, 얼마나 제대로 처리했나.</summary>
    private IReadOnlyList<Tile> QualityTiles =>
    [
        new("완료율", Rate(Data.Summary.CompletionRate), "반려는 모수에서 뺀다"),
        new("평균 접수까지", Hours(Data.Summary.AvgResponseHours), Samples(Data.Summary.ResponseSamples)),
        new("평균 처리까지", Hours(Data.Summary.AvgResolutionHours), Samples(Data.Summary.ResolutionSamples)),
        new("처리 중앙값", Hours(Data.Summary.MedianResolutionHours), "긴 한 건에 안 끌린다"),
        new("24시간 내 접수", Rate(Data.Summary.Sla24Rate), Samples(Data.Summary.ResponseSamples)),
        new("72시간 내 완료", Rate(Data.Summary.Sla72Rate), Samples(Data.Summary.ResolutionSamples)),
    ];

    /// <summary>지금 손에 남아 있는 짐.</summary>
    private IReadOnlyList<Tile> BacklogTiles =>
    [
        new("미처리", Num(Data.Summary.Open), $"전체의 {Rate(Share(Data.Summary.Open, Data.Summary.Total))}"),
        new("미배정", Num(Data.Summary.Unassigned), "담당자가 아직 없다"),
        new("긴급 미처리", Num(Data.Summary.EmergencyOpen), "유형이 긴급/장애"),
        new("최장 대기", Days(Data.Summary.OldestPendingDays), "가장 오래 기다린 건"),
        new("평균 경과", Days(Data.Summary.AvgOpenAgeDays), "미처리 건의 평균"),
        new("무응답", Num(Data.Summary.NoReplyOpen), "댓글이 한 줄도 없다"),
        new("댓글", Num(Data.Summary.Comments), $"건당 {Data.Summary.CommentsPerRequest:0.#}개"),
        new("담당자", Num(Data.Summary.ActiveAdmins), "한 건이라도 맡은 사람"),
    ];

    private string? MonthOverMonthNote
    {
        get
        {
            if (Data.Summary.LastMonth == 0)
            {
                return "지난달 자료 없음";
            }

            var rate = Data.Summary.MonthOverMonthRate;
            var arrow = rate > 0 ? "▲" : rate < 0 ? "▼" : "―";
            return $"지난달 {Data.Summary.LastMonth}건 {arrow} {Math.Abs(rate):0.#}%";
        }
    }

    // ── 차트 재료 ───────────────────────────────────────────

    /// <summary>
    /// 도넛 조각. <b>0 건인 상태는 뺀다</b> — 조각은 안 그려지는데 범례만 남아
    /// 도넛과 범례가 어긋나 보인다.
    /// </summary>
    private List<DashboardSlice> StatusSlices => [.. Data.Status.Where(s => s.Count > 0)];

    /// <summary>옛 화면(customer.vue)의 상태 팔레트 그대로. 열쇠는 상태 enum 이름.</summary>
    private static readonly Dictionary<string, string> StatusColors = new(StringComparer.Ordinal)
    {
        ["Pending"] = "#3B82F6",
        ["InProgress"] = "#F97316",
        ["Consultation"] = "#EAB308",
        ["Negotiation"] = "#CA8A04",
        ["Completed"] = "#22C55E",
        ["UserCompleted"] = "#16A34A",
        ["Rejected"] = "#EF4444",
    };

    /// <summary>
    /// 조각 색. <see cref="StatusSlices"/> 와 <b>같은 조건·순서</b>로 골라야
    /// 어긋나지 않는다 — 한쪽만 거르면 색이 한 칸씩 밀린다.
    /// </summary>
    private string[] StatusPalette =>
        [.. StatusSlices.Select(s => StatusColors.GetValueOrDefault(s.Key, "#94A3B8"))];

    /// <summary>
    /// 막대·선 색. <b><c>Palette</c> 가 아니라 계열마다 <c>Color</c> 로 준다</b> —
    /// 접수는 늘 파랑, 완료는 늘 초록이어야 차트 예닐곱 개가 같은 말을 한다.
    /// DevExpress 가 받는 것은 글자가 아니라 <c>System.Drawing.Color</c> 다.
    /// 접수·완료 두 색은 옛 화면(customer.vue)의 월별 차트 색 그대로다.
    /// </summary>
    private static readonly Color RequestedColor = ColorTranslator.FromHtml("#42A5F5");

    private static readonly Color CompletedColor = ColorTranslator.FromHtml("#66BB6A");

    private static readonly Color BacklogColor = ColorTranslator.FromHtml("#EF4444");

    private static readonly Color TypeColor = ColorTranslator.FromHtml("#7E57C2");

    private static readonly Color HourColor = ColorTranslator.FromHtml("#26A69A");

    private static readonly Color AgingColor = ColorTranslator.FromHtml("#F97316");

    // ── 조회 ────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        // 신원과 회사 목록을 먼저 안다. 회사 고르개와 「담당자/고객」 배지가
        // 그것을 본다. 둘 다 한 번 받아 캐싱되므로 화면을 오갈 때 다시 안 나간다.
        await Context.LoadIdentityAsync();
        await Context.LoadOrganizationsAsync();
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var overview = await Api.GetAsync<DashboardOverview>("dashboard/overview", new
        {
            companyId = _companyId,
            days = _days,
            months = _months,
            topN = 15,
        });

        Data = overview ?? new DashboardOverview();

        // 집계가 비어도 타일은 0 으로 그린다 — 그것이 사실이다.
        // 「없습니다」 는 요청이 한 건도 없을 때만 띄운다.
        return Data.Summary.Total;
    }, "아직 접수된 요청이 없습니다.", "현황을 읽지 못했습니다");

    // ── 표에서 상세로 ───────────────────────────────────────

    /// <summary>
    /// 오른쪽 클릭 창의 「열기」가 볼 줄. 두 번 누르기와 같은 동작을 창에도
    /// 둔다 — 두 번 누르기는 화면 어디에도 안 보이는 몸짓이다.
    /// </summary>
    private DashboardRequest? _selectedRecent;

    private Task OnRecentSelected(DashboardRequest? row)
    {
        _selectedRecent = row;
        return Task.CompletedTask;
    }

    private Task OpenRequestAsync(DashboardRequest row)
    {
        Navigation.NavigateTo($"/helpdesk/request/detail/{row.Id}");
        return Task.CompletedTask;
    }

    private Task OpenSelectedAsync() =>
        _selectedRecent is null ? Task.CompletedTask : OpenRequestAsync(_selectedRecent);

    // ── 글자 맞추기 ─────────────────────────────────────────

    private static string Num(int value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    /// <summary>비율. 소수 첫째 자리까지만 — 그 아래는 읽는 사람에게 뜻이 없다.</summary>
    private static string Rate(double value) => $"{value:0.#}%";

    /// <summary>
    /// 걸린 시간. 하루가 넘으면 날로 바꾼다 — 「312시간」은 길다는 것 말고는
    /// 아무것도 알려 주지 않는다.
    /// </summary>
    private static string Hours(double hours) => hours switch
    {
        <= 0 => "-",
        < 1 => $"{hours * 60:0}분",
        < 48 => $"{hours:0.#}시간",
        _ => $"{hours / 24:0.#}일",
    };

    private static string Days(double days) => days switch
    {
        <= 0 => "-",
        < 1 => $"{days * 24:0.#}시간",
        _ => $"{days:0.#}일",
    };

    private static string? Samples(int count) => count == 0 ? "잴 수 있는 건이 없다" : $"표본 {count}건";

    private static double Share(int part, int whole) =>
        whole > 0 ? Math.Round((double)part / whole * 100, 1) : 0;

    /// <summary>상태 배지 색. 열쇠는 서버가 준 enum 이름이다.</summary>
    private static string StatusClass(string? status) => status switch
    {
        "Completed" or "UserCompleted" => "jsini-badge--on",
        "InProgress" or "Consultation" or "Negotiation" => "jsini-badge--warn",
        "Rejected" => "jsini-badge--off",
        _ => string.Empty,
    };
}
