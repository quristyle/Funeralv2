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

    /// <summary>
    /// 타일 한 칸. 값 아래 곁말이 붙는다.
    /// </summary>
    /// <param name="Label">타일 이름.</param>
    /// <param name="Value">큰 숫자.</param>
    /// <param name="Note">숫자 아래 곁말 — 그 수를 어떻게 읽어야 하는지.</param>
    /// <param name="Href">
    /// 누르면 열리는 목록. <c>null</c> 이면 <b>안 눌린다</b> — 눌리지 않는 타일을
    /// 눌리는 것처럼 그리면 「눌러 봤는데 아무 일도 안 난다」가 된다.
    /// </param>
    private sealed record Tile(string Label, string Value, string? Note = null, string? Href = null);

    /// <summary>
    /// 얼마나 들어오고 얼마나 나갔나.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [타일을 누르면 그 목록이 열린다 (2026-09-28)]
    /// </para>
    /// <para>
    /// 열 칸 모두 <c>/helpdesk/request/manage</c> 로 간다. 주소에 싣는 조건은
    /// <b>이 타일이 센 조건 그대로</b>여야 한다 — 하나라도 어긋나면 타일의
    /// 숫자와 열린 목록의 건수가 다르고, 그 순간 현황판 전체가 못 믿을 것이 된다.
    /// 그래서 셈의 기준(<c>DashboardOverviewService</c>)과 여기를 나란히 적어 둔다.
    /// </para>
    /// <list type="table">
    ///   <item><term>전체</term><description>지운 것 말고 다. 그래서 목록 쪽도 <c>Delete</c> 를 뺀다.</description></item>
    ///   <item><term>협의</term><description>협의 + 논의 <b>둘</b>이다.</description></item>
    ///   <item><term>완료</term><description>담당자 완료만. 요청자 종료는 곁말로 따로 센다.</description></item>
    ///   <item><term>오늘 완료</term><description>끝난 <b>날</b>로 센다 — 접수한 날이 아니다.</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// 오늘. <b>한국 달력의 오늘</b>이다 — 서버도 같은 기준으로 센다
    /// (<c>Kst.Today</c>). 운영 컨테이너 시계는 UTC 라
    /// (<c>deploy/docker</c> · <c>docs/utc-time.md</c>)
    /// <see cref="DateTime.Today"/> 를 쓰면 한국의 오전 9시 전 아홉 시간 동안
    /// 서버와 다른 날을 센다.
    /// </summary>
    private static DateTime Today => AppTime.TodayDate;

    /// <summary>이번 주 월요일. 서버의 주 시작과 같아야 「이번 주」가 맞는다.</summary>
    private static DateTime WeekStart => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    private static DateTime MonthStart => new(Today.Year, Today.Month, 1);

    /// <summary>
    /// 이 타일이 센 것과 같은 것을 여는 목록 주소.
    /// </summary>
    /// <param name="statuses">상태. 여럿이면 <c>|</c> 로 잇는다. 비우면 안 거른다.</param>
    /// <param name="from">기간 시작(그 날 포함).</param>
    /// <param name="to">기간 끝(<b>그 날 포함</b>).</param>
    /// <param name="byResolved">기간을 완료 시각으로 재나. 거짓이면 접수 시각이다.</param>
    /// <param name="overrideCompanyId">조회할 고객사 (없으면 화면 필터 사용).</param>
    /// <param name="adminId">조회할 담당자 번호.</param>
    /// <param name="open">처리 중인 것만 볼지 여부.</param>
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

        // **보고 있는 회사를 함께 싣는다.** 안 실으면 회사 하나로 좁혀 놓고
        // 타일을 눌렀을 때 전체가 열린다 — 숫자가 안 맞는다.
        // 고객 계정은 서버가 제 회사로 묶으므로(`Scope.CompanyScoped`) 안 싣는다.
        var targetCompanyId = overrideCompanyId ?? (CanPickCompany ? _companyId : null);
        if (!string.IsNullOrEmpty(targetCompanyId))
        {
            parts.Add($"company={Uri.EscapeDataString(targetCompanyId)}");
        }

        if (adminId.HasValue)
        {
            parts.Add($"admin={adminId.Value}");
        }

        // **「처리 중인 것만」을 반드시 꺼서 보낸다.** 목록 화면의 기본값이
        // 켜짐이라, 안 끄면 완료·반려 타일이 빈 목록으로 열린다.
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

    // ── 카드 (고객사별 · 담당자별) ──────────────────────────

    /// <summary>
    /// 카드 머리에 적는 곁말. 몇 장인지 먼저 말한다 — 카드는 표와 달리
    /// 「총 N건」을 그려 주는 띠가 없어서(그 일은 <c>CommGrd</c> 가 했다)
    /// 안 적으면 화면을 끝까지 굴려야 몇 곳인지 알 수 있다.
    /// </summary>
    private string CompanyHint => $"{Data.Companies.Count}곳 · 건수 많은 곳부터";

    private string AdminHint => $"{Data.Admins.Count}명 · 완료 많은 사람부터";

    /// <summary>상태 띠의 조각 하나. 색은 도넛과 같은 팔레트에서 꺼낸다.</summary>
    private sealed record Seg(string Label, int Count, string Color, string? Href = null);

    /// <summary>
    /// 고객사 한 곳의 상태 분해. 다섯을 더하면 <see cref="DashboardCompany.Total"/>
    /// 과 정확히 같다 — 서버가 상태 일곱을 이 다섯 칸에 빠짐없이 접어 담기
    /// 때문이다(완료에 「종료」가, 협의에 「논의」가 들어 있다). 그래서 띠가
    /// 폭을 다 채우고, 안 채워진 자리가 보이면 그것이 곧 집계가 새고 있다는 표다.
    /// </summary>
    private IReadOnlyList<Seg> CompanySegments(DashboardCompany c) =>
    [
        new("대기", c.Pending, StatusColors["Pending"], ManageCompanyHref(c.CompanyId, "Pending")),
        new("진행", c.InProgress, StatusColors["InProgress"], ManageCompanyHref(c.CompanyId, "InProgress")),
        new("협의", c.Talking, StatusColors["Consultation"], ManageCompanyHref(c.CompanyId, "Consultation|Negotiation")),
        new("완료", c.Completed, StatusColors["Completed"], ManageCompanyHref(c.CompanyId, "Completed|UserCompleted")),
        new("반려", c.Rejected, StatusColors["Rejected"], ManageCompanyHref(c.CompanyId, "Rejected")),
    ];

    /// <summary>
    /// 담당자 한 사람의 상태 분해. <b>대기 칸은 서버가 안 준다</b> — 남은 짐
    /// (<c>Open</c>)에서 진행·협의를 덜어 낸 나머지가 그것이다.
    /// 음수로 떨어지지 않게 묶는다: 집계가 어긋나도 띠가 뒤집히지는 않게.
    /// </summary>
    private IReadOnlyList<Seg> AdminSegments(DashboardAdmin a) =>
    [
        new("대기", Math.Max(0, a.Open - a.InProgress - a.Talking), StatusColors["Pending"], ManageAdminHref(a.AdminId, "Pending")),
        new("진행", a.InProgress, StatusColors["InProgress"], ManageAdminHref(a.AdminId, "InProgress")),
        new("협의", a.Talking, StatusColors["Consultation"], ManageAdminHref(a.AdminId, "Consultation|Negotiation")),
        new("완료", a.Completed, StatusColors["Completed"], ManageAdminHref(a.AdminId, "Completed|UserCompleted")),
        new("반려", a.Rejected, StatusColors["Rejected"], ManageAdminHref(a.AdminId, "Rejected")),
    ];

    /// <summary>
    /// 조각 폭. <b>반드시 InvariantCulture 다</b> — 소수점을 쉼표로 찍는
    /// 문화권에서 <c>width: 33,3%</c> 가 나가면 브라우저가 그 선언을 통째로
    /// 버려서, 조각이 전부 0 폭이 되고 띠가 빈 회색 막대로만 남는다.
    /// </summary>
    private static string Pct(int part, int whole) => whole <= 0
        ? "0%"
        : ((double)part / whole * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// 띠를 소리 내어 읽을 때의 한 줄. 띠 자체는 <c>span</c> 덩어리라
    /// 화면 낭독기에게는 아무 뜻이 없다.
    /// </summary>
    private static string BarLabel(IEnumerable<Seg> shown) =>
        string.Join(" · ", shown.Select(s => $"{s.Label} {s.Count}건"));

    /// <summary>
    /// 얼굴 자리에 넣을 첫 글자. 이모지처럼 두 칸을 쓰는 글자는 잘라 내면
    /// 깨진 네모가 되므로 짝을 지켜 두 칸을 가져온다.
    /// </summary>
    private static string Initial(string? name)
    {
        var t = name?.Trim();

        if (string.IsNullOrEmpty(t))
        {
            return "?";
        }

        return char.IsHighSurrogate(t[0]) && t.Length > 1 ? t[..2] : t[..1];
    }

    /// <summary>
    /// 날짜 한 칸. <b>표에 있던 그대로 서버가 준 값을 그냥 찍는다</b> —
    /// 시간대를 여기서 옮기면 표를 보던 사람과 카드를 보는 사람이 하루
    /// 어긋난 날짜를 보게 된다. 없으면 빈 칸이 아니라 「-」다(빈 칸은
    /// 「아직 안 불러왔다」로 읽힌다).
    /// </summary>
    private static string Date(DateTime? at) => at?.ToString("yyyy-MM-dd") ?? "-";

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
