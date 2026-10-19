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

    /// <summary>서버가 접어 준 현황 한 판. 조회 전에도 빈 판을 들고 있는다 — null 이면 화면 곳곳에 ?. 을 붙여야 하고 한 곳만 빠져도 프리렌더에서 죽는다.</summary>
    private DashboardOverview Data { get; set; } = new();

    // ── 조회 조건 ───────────────────────────────────────────

    /// <summary>고른 고객사. 「전체」면 null 이다.</summary>
    private string? _companyId;

    private string _days = "30";
    private string _months = "12";

    /// <summary>회사를 고를 수 있는가. 시스템관리자만이고, 서버가 회사 하나로 묶은 계정(고객)에게는 고를 것이 없다.</summary>
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

    /// <summary>타일 한 칸. Note 는 숫자 아래 곁말. Href 가 null 이면 안 눌린다 — 눌리지 않는 것을 눌리는 것처럼 그리면 아무 일도 안 난다.</summary>
    private sealed record Tile(string Label, string Value, string? Note = null, string? Href = null);

    /// <summary>들어옴·나감 타일. 열 칸 모두 같은 목록으로 가고 주소의 조건은 타일이 센 조건과 같아야 한다(어긋나면 숫자와 목록 건수가 다르다).
    /// 협의는 협의+논의, 완료는 담당자 완료만, 오늘 완료는 끝난 날 기준, 전체는 Delete 를 뺀다. 셈은 DashboardOverviewService 와 나란히 맞춘다.</summary>
    private IReadOnlyList<Tile> VolumeTiles =>
    [
        new("전체", Num(Data.Summary.Total), $"완료 {Data.Summary.Completed + Data.Summary.UserCompleted}건 · 반려 {Data.Summary.Rejected}건",
            ManageHref()),
        new("대기", Num(Data.Summary.Pending), $"미배정 {Data.Summary.Unassigned}건",
            ManageHref("Pending")),
        new("진행", Num(Data.Summary.InProgress), null,
            ManageHref("InProgress")),
        new("협의", Num(Data.Summary.Consultation + Data.Summary.Negotiation), $"협의 {Data.Summary.Consultation} · 논의 {Data.Summary.Negotiation}",
            ManageHref("Consultation|Negotiation")),
        new("완료", Num(Data.Summary.Completed), $"종료 {Data.Summary.UserCompleted}건",
            ManageHref("Completed")),
        new("반려", Num(Data.Summary.Rejected), null,
            ManageHref("Rejected")),
        new("오늘 접수", Num(Data.Summary.Today), $"어제 {Data.Summary.Yesterday}건",
            ManageHref(from: Today, to: Today)),
        new("이번 주", Num(Data.Summary.ThisWeek), "월요일부터",
            ManageHref(from: WeekStart, to: Today)),
        new("이번 달", Num(Data.Summary.ThisMonth), MonthOverMonthNote,
            ManageHref(from: MonthStart, to: Today)),
        new("오늘 완료", Num(Data.Summary.CompletedToday), $"이번 달 {Data.Summary.CompletedThisMonth}건",
            ManageHref("Completed|UserCompleted", Today, Today, byResolved: true)),
    ];

    // ── 타일 → 요청 처리 목록 ───────────────────────────────

    private const string ManagePath = "/helpdesk/request/manage";

    /// <summary>오늘. 한국 달력의 오늘이다 — 서버도 Kst.Today 로 센다. 운영 컨테이너 시계는 UTC 라 DateTime.Today 를 쓰면 아홉 시간 동안 다른 날을 센다.</summary>
    private static DateTime Today => AppTime.TodayDate;

    /// <summary>이번 주 월요일. 서버의 주 시작과 같아야 「이번 주」가 맞는다.</summary>
    private static DateTime WeekStart => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    private static DateTime MonthStart => new(Today.Year, Today.Month, 1);

    /// <summary>이 타일이 센 것과 같은 것을 여는 목록 주소. 상태는 | 로 잇고 비우면 거르지 않는다. byResolved 가 거짓이면 기준은 접수 시각이다.</summary>
    private string ManageHref(
        string? statuses = null,
        DateTime? from = null,
        DateTime? to = null,
        bool byResolved = false,
        string? overrideCompanyId = null,
        int? adminId = null,
        bool? open = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(statuses))
        {
            parts.Add($"status={Uri.EscapeDataString(statuses)}");
        }

        if (byResolved)
        {
            parts.Add("basis=resolved");
        }

        if (from is { } f)
        {
            parts.Add($"from={Day(f)}");
        }

        if (to is { } t)
        {
            parts.Add($"to={Day(t)}");
        }

        // 보고 있는 회사를 함께 싣는다 — 안 실으면 회사로 좁힌 현황판에서 타일을 눌렀을 때 전체가 열린다. 고객은 서버가 제 회사로 묶으므로 안 싣는다.
        var targetCompanyId = overrideCompanyId ?? (CanPickCompany ? _companyId : null);
        if (!string.IsNullOrEmpty(targetCompanyId))
        {
            parts.Add($"company={Uri.EscapeDataString(targetCompanyId)}");
        }

        if (adminId.HasValue)
        {
            parts.Add($"admin={adminId.Value}");
        }

        // 「처리 중인 것만」을 반드시 꺼서 보낸다 — 목록의 기본값이 켜짐이라 안 끄면 완료·반려 타일이 빈 목록으로 열린다.
        if (open.HasValue)
        {
            parts.Add($"open={open.Value.ToString().ToLowerInvariant()}");
        }
        else
        {
            parts.Add("open=false");
        }

        return $"{ManagePath}?{string.Join("&", parts)}";
    }

    private string ManageCommentHref()
    {
        var parts = new List<string> { "all=true" };

        var targetCompanyId = CanPickCompany ? _companyId : null;
        if (!string.IsNullOrEmpty(targetCompanyId))
        {
            parts.Add($"company={Uri.EscapeDataString(targetCompanyId)}");
        }

        return $"/helpdesk/request/my-comments?{string.Join("&", parts)}";
    }

    private string ManageCompanyHref(string companyId, string? statuses = null) => ManageHref(statuses: statuses, overrideCompanyId: companyId);
    private string ManageAdminHref(int adminId, string? statuses = null) => ManageHref(statuses: statuses, adminId: adminId);

    private static string Day(DateTime at) => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
        new("미처리", Num(Data.Summary.Open), $"전체의 {Rate(Share(Data.Summary.Open, Data.Summary.Total))}", ManageHref(open: true)),
        new("미배정", Num(Data.Summary.Unassigned), "담당자가 아직 없다", ManageHref(open: true)),
        new("긴급 미처리", Num(Data.Summary.EmergencyOpen), "유형이 긴급/장애", ManageHref(open: true)),
        new("최장 대기", Days(Data.Summary.OldestPendingDays), "가장 오래 기다린 건", ManageHref(open: true)),
        new("평균 경과", Days(Data.Summary.AvgOpenAgeDays), "미처리 건의 평균", ManageHref(open: true)),
        new("무응답", Num(Data.Summary.NoReplyOpen), "댓글이 한 줄도 없다", ManageHref(open: true)),
        new("댓글", Num(Data.Summary.Comments), $"건당 {Data.Summary.CommentsPerRequest:0.#}개", ManageCommentHref()),
        new("담당자", Num(Data.Summary.ActiveAdmins), "한 건이라도 맡은 사람", null),
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

    /// <summary>도넛 조각. 0 건인 상태는 뺀다 — 조각은 안 그려지는데 범례만 남으면 도넛과 범례가 어긋나 보인다.</summary>
    private List<DashboardSlice> StatusSlices => [.. Data.Status.Where(s => s.Count > 0)];

    /// <summary>상태 팔레트. 열쇠는 상태 enum 이름.</summary>
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

    /// <summary>조각 색. StatusSlices 와 같은 조건·순서로 골라야 한쪽만 거를 때 색이 한 칸씩 밀리지 않는다.</summary>
    private string[] StatusPalette =>
        [.. StatusSlices.Select(s => StatusColors.GetValueOrDefault(s.Key, "#94A3B8"))];

    /// <summary>막대·선 색은 팔레트가 아니라 계열마다 Color 로 준다 — 접수는 늘 파랑, 완료는 늘 초록. DevExpress 는 System.Drawing.Color 를 받는다.</summary>
    private static readonly Color RequestedColor = ColorTranslator.FromHtml("#42A5F5");

    private static readonly Color CompletedColor = ColorTranslator.FromHtml("#66BB6A");

    private static readonly Color BacklogColor = ColorTranslator.FromHtml("#EF4444");

    private static readonly Color TypeColor = ColorTranslator.FromHtml("#7E57C2");

    private static readonly Color HourColor = ColorTranslator.FromHtml("#26A69A");

    private static readonly Color AgingColor = ColorTranslator.FromHtml("#F97316");

    // ── 조회 ────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        // 신원과 회사 목록을 먼저 안다 — 회사 고르개와 담당자/고객 배지가 쓴다. 한 번 받아 캐싱된다.
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

        // 집계가 비어도 타일은 0 으로 그린다 — 「없습니다」는 요청이 한 건도 없을 때만 띄운다.
        return Data.Summary.Total;
    }, "아직 접수된 요청이 없습니다.", "현황을 읽지 못했습니다");

    // ── 표에서 상세로 ───────────────────────────────────────

    /// <summary>오른쪽 클릭 창의 「열기」가 볼 줄. 두 번 누르기와 같은 동작을 창에도 둔다 — 두 번 누르기는 화면에 안 보이는 몸짓이다.</summary>
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

    // ── 카드 (고객사별 · 담당자별) ──────────────────────────

    /// <summary>카드 머리에 적는 곁말. 카드는 「총 N건」 띠가 없어 몇 장인지 먼저 말해야 몇 곳인지 안다.</summary>
    private string CompanyHint => $"{Data.Companies.Count}곳";

    private string AdminHint => $"{Data.Admins.Count}명";

    /// <summary>상태 띠의 조각 하나. 색은 도넛과 같은 팔레트에서 꺼낸다.</summary>
    private sealed record Seg(string Label, int Count, string Color, string? Href = null);

    /// <summary>고객사 한 곳의 상태 분해. 다섯 칸을 더하면 DashboardCompany.Total 과 같다 — 서버가 상태 일곱을 다섯 칸에 빠짐없이 접는다(완료에 종료, 협의에 논의).</summary>
    private IReadOnlyList<Seg> CompanySegments(DashboardCompany c) =>
    [
        new("대기", c.Pending, StatusColors["Pending"], ManageCompanyHref(c.CompanyId, "Pending")),
        new("진행", c.InProgress, StatusColors["InProgress"], ManageCompanyHref(c.CompanyId, "InProgress")),
        new("협의", c.Talking, StatusColors["Consultation"], ManageCompanyHref(c.CompanyId, "Consultation|Negotiation")),
        new("완료", c.Completed, StatusColors["Completed"], ManageCompanyHref(c.CompanyId, "Completed|UserCompleted")),
        new("반려", c.Rejected, StatusColors["Rejected"], ManageCompanyHref(c.CompanyId, "Rejected")),
    ];

    /// <summary>담당자 한 사람의 상태 분해. 대기 칸은 서버가 안 주므로 남은 짐(Open)에서 진행·협의를 덜어 낸 나머지다. 음수로 떨어지지 않게 묶는다.</summary>
    private IReadOnlyList<Seg> AdminSegments(DashboardAdmin a) =>
    [
        new("대기", Math.Max(0, a.Open - a.InProgress - a.Talking), StatusColors["Pending"], ManageAdminHref(a.AdminId, "Pending")),
        new("진행", a.InProgress, StatusColors["InProgress"], ManageAdminHref(a.AdminId, "InProgress")),
        new("협의", a.Talking, StatusColors["Consultation"], ManageAdminHref(a.AdminId, "Consultation|Negotiation")),
        new("완료", a.Completed, StatusColors["Completed"], ManageAdminHref(a.AdminId, "Completed|UserCompleted")),
        new("반려", a.Rejected, StatusColors["Rejected"], ManageAdminHref(a.AdminId, "Rejected")),
    ];

    /// <summary>조각 폭. 반드시 InvariantCulture 다 — 쉼표 소수점 문화권에서 width: 33,3% 가 나가면 브라우저가 선언을 버려 띠가 빈 막대가 된다.</summary>
    private static string Pct(int part, int whole) => whole <= 0
        ? "0%"
        : ((double)part / whole * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>띠를 소리 내어 읽을 때의 한 줄. 띠 자체는 span 이라 화면 낭독기에는 뜻이 없다.</summary>
    private static string BarLabel(IEnumerable<Seg> shown) =>
        string.Join(" · ", shown.Select(s => $"{s.Label} {s.Count}건"));

    /// <summary>날짜 한 칸. 서버가 준 값을 그대로 찍는다 — 시간대를 옮기면 표와 카드가 하루 어긋난다. 없으면 빈 칸이 아니라 「-」다.</summary>
    private static string Date(DateTime? at) => at?.ToString("yyyy-MM-dd") ?? "-";

    // ── 글자 맞추기 ─────────────────────────────────────────

    private static string Num(int value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    /// <summary>비율. 소수 첫째 자리까지만 — 그 아래는 읽는 사람에게 뜻이 없다.</summary>
    private static string Rate(double value) => $"{value:0.#}%";

    /// <summary>걸린 시간. 하루가 넘으면 날로 바꾼다 — 「312시간」은 길다는 것 말고는 알려 주는 게 없다.</summary>
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
