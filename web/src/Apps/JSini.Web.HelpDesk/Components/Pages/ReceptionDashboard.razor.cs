using System.Globalization;
using System.Text.Json.Serialization;
using JSini.Web.Components.Data;
using JSini.Web.Http;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReceptionDashboard
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;

    /// <summary>60초 자동 조회. 스위치를 켠 동안에만 돈다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(60);

    /// <summary>대기 목록에 세우는 줄 수(넷).</summary>
    private const int PendingRows = 4;

    // ── 상태 묶음 ──
    // 요청 처리 화면이 받는 글자 그대로다(RequestManage.StatusQuery). 여럿이면 | 로 잇는다.

    private const string PendingStatuses = "Pending";
    private const string DoneStatuses = "Completed|UserCompleted";
    private const string TalkStatuses = "Consultation|Negotiation";

    // ── 색 ──
    // 접수는 검정, 완료는 초록.

    private const string AcceptColor = "#222222";
    private const string DoneColor = "#22c55e";

    /// <summary>고리의 남은 부분. 바탕보다 조금 진한 회색이다.</summary>
    private const string RestColor = "#d4d4d8";

    private AdminStats _mine = new();
    private IReadOnlyList<AdminRow> _admins = [];
    private IReadOnlyList<CompanyRow> _companies = [];
    private IReadOnlyList<ImprovementRequest> _pending = [];

    private bool _auto;

    protected override async Task OnInitializedAsync()
    {
        await Context.LoadIdentityAsync();
        await ReloadAsync();
    }

    // ── 나의 숫자 ───────────────────────────────────────────

    /// <summary>내가 받은 것. 대기는 빼고 센다 — 대기는 아직 아무도 안 받은 것이라 여기 들어갈 수 없다.</summary>
    private int Handled =>
        _mine.InProgressCount + _mine.CompletedCount + _mine.UserCompletedCount
        + _mine.RejectedCount + _mine.ConsultationCount + _mine.NegotiationCount;

    /// <summary>내가 끝낸 것 — 담당자 완료와 요청자 종료 <b>둘</b>이다.</summary>
    private int Closed => _mine.CompletedCount + _mine.UserCompletedCount;

    private double AcceptanceRate => Rate(Handled, _mine.TotalRequests);

    private double CompletionRate => Rate(Closed, Handled);

    /// <summary>나눠서 백분율. <b>분모가 0 이면 0 이다</b> — 나누지 않는다.</summary>
    private static double Rate(int part, int whole) =>
        whole <= 0 ? 0 : Math.Round((double)part / whole * 100, 1);

    /// <summary>띠의 폭. 100 을 넘기지 않는다 — 넘으면 칸 밖으로 비어져 나간다.</summary>
    private static string Clamp(double rate) =>
        Math.Clamp(rate, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);

    // ── 상태 타일 ───────────────────────────────────────────

    /// <summary>타일 하나. 글자·배지·건수와 그것을 여는 조건.</summary>
    private sealed record StateTile(string Label, string Mark, string Badge, int Count, string Statuses);

    private IReadOnlyList<StateTile> StateTiles =>
    [
        new("진행", "진", "jsini-badge--warn", _mine.InProgressCount, "InProgress"),
        new("완료", "완", "jsini-badge--on", _mine.CompletedCount, "Completed"),
        new("반려", "반", "jsini-badge--err", _mine.RejectedCount, "Rejected"),
        new("협의", "협", "jsini-badge--warn", _mine.ConsultationCount, "Consultation"),
        new("논의", "논", "jsini-badge--warn", _mine.NegotiationCount, "Negotiation"),
        new("종료", "종", "jsini-badge--on", _mine.UserCompletedCount, "UserCompleted"),
    ];

    /// <summary>고리 한 조각. 채운 만큼과 남은 만큼 둘뿐이다.</summary>
    public sealed record RingSlice(string Label, double Value);

    private static IReadOnlyList<RingSlice> RingOf(double rate) =>
    [
        new("채움", Math.Clamp(rate, 0, 100)),
        new("남음", Math.Clamp(100 - rate, 0, 100)),
    ];

    // ── 곁말 ────────────────────────────────────────────────

    private string RefreshedLabel => RefreshedAt is { } at
        ? $"{at.Kst("HH:mm:ss")} 기준"
        : string.Empty;

    private string CompanyHint => _companies.Count == 0 ? string.Empty : $"{_companies.Count}개사";

    private string AdminHint => _admins.Count == 0 ? string.Empty : $"{_admins.Count}명";

    /// <summary>이 담당자를 뭐라고 부를까. 포털이 먼저다 — 헬프데스크에 베껴 둔 이름은 포털에서 고쳐도 따라오지 않는다.</summary>
    private string NameOf(AdminRow admin) =>
        Faces.NameOf(admin.LoginId) is { Length: > 0 } name ? name : admin.AdminName;

    private static string LastPending(CompanyRow company) =>
        company.LastPendingDate is { } at ? $"마지막 접수 {at.Kst("yyyy-MM-dd")}" : " ";

    /// <summary>요청이 올라온 시각. 서버 값은 UTC 라 보여 주기 직전에 한 번만 한국 시각으로 옮긴다.</summary>
    private static string Stamp(DateTime? at) => at.Kst("MM-dd HH:mm");

    // ── 타일 → 요청 처리 목록 ───────────────────────────────

    private const string ManagePath = "/helpdesk/request/manage";

    /// <summary>이 숫자가 센 것과 같은 것을 여는 목록 주소. open=false 를 늘 싣는다 — 목록의 기본값이 켜짐이라 안 끄면 완료·반려 타일이 빈 목록으로 열린다.
    /// mine 에는 서버가 센 담당자 번호(AdminStats.AdminId)를 쓴다 — 신원의 헬프데스크 번호는 대개 고객 줄을 가리켜 엉뚱한 사람의 요청이 열린다.</summary>
    private string ManageHref(
        string? statuses, bool mine = false, int? adminId = null, string? companyId = null)
    {
        var parts = new List<string> { "open=false" };

        if (!string.IsNullOrWhiteSpace(statuses))
        {
            parts.Add($"status={Uri.EscapeDataString(statuses)}");
        }

        var who = adminId ?? (mine ? _mine.AdminId : null);
        if (who is { } id)
        {
            parts.Add($"admin={id}");
        }

        if (!string.IsNullOrWhiteSpace(companyId))
        {
            parts.Add($"company={Uri.EscapeDataString(companyId)}");
        }

        return $"{ManagePath}?{string.Join("&", parts)}";
    }

    // ── 조회 ────────────────────────────────────────────────

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        await FetchAsync();
        return Handled + _companies.Count + _admins.Count + _pending.Count;
    }, "아직 집계할 요청이 없습니다.", "접수 현황을 읽지 못했습니다");

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다.</summary>
    protected override Task RefreshAsync() => FetchAsync();

    /// <summary>넷을 나란히 부른다 — 차례로 기다리면 왕복이 쌓이고, 그 사이 자료가 바뀌면 타일과 카드가 서로 다른 순간을 가리킨다. 하나가 실패해도 나머지는 보여 준다.</summary>
    private async Task FetchAsync()
    {
        var mine = SafeAsync(() => Api.GetAsync<AdminStats>("dashboard/admin-stats"));
        var admins = SafeListAsync(() => Api.GetListAsync<AdminRow>("dashboard/all-admin-stats"));
        var companies = SafeListAsync(() => Api.GetListAsync<CompanyRow>("dashboard/company-stats"));
        var pending = SafePageAsync(() => Api.SearchAsync<ImprovementRequest>("requests/srch", PendingQuery));

        await Task.WhenAll(mine, admins, companies, pending);

        _mine = mine.Result ?? new AdminStats();
        _admins = admins.Result;
        _pending = pending.Result;

        // 건수 0 인 회사까지 카드를 세우면 묶음이 한 화면을 넘는다.
        _companies = [.. companies.Result.OrderByDescending(c => c.Total).ThenBy(c => c.CompanyName, StringComparer.CurrentCulture)];

        // 이름과 얼굴은 포털에 한꺼번에 묻는다(UserFaceClient). 실패는 그 부품이 삼키고 헬프데스크에 베껴 둔 이름으로 물러선다.
        await Faces.EnsureAsync(_admins.Select(a => a.LoginId));
    }

    /// <summary>아직 아무도 안 받은 요청 넉 줄. 늦게 올라온 것이 위다. description·content 는 안 받는다 — 서식 본문이라 줄마다 수십 KB 다. 대표 그림은 mainPhoto 로 안다(RequestThumb).</summary>
    private static Dictionary<string, object?> PendingQuery => new()
    {
        ["page"] = 1,
        ["pageSize"] = PendingRows,
        ["remove"] = "description,content",
        ["status_in"] = PendingStatuses,
        ["sorts"] = new[] { new { field = "createdAt", dir = "desc" } },
    };

    /// <summary>실패를 삼키고 <c>default</c> 를 돌려준다. 까닭은 위에 있다.</summary>
    private static async Task<T?> SafeAsync<T>(Func<Task<T?>> load)
    {
        try
        {
            return await load();
        }
        catch (ApiException)
        {
            return default;
        }
    }

    /// <summary>쪽 짝. 실패하면 빈 목록이다 — 쪽의 건수는 이 화면이 안 쓴다.</summary>
    private static async Task<IReadOnlyList<T>> SafePageAsync<T>(Func<Task<HelpDeskPage<T>>> load)
    {
        try
        {
            return (await load()).Items;
        }
        catch (ApiException)
        {
            return [];
        }
    }

    /// <summary>목록 짝. 실패하면 빈 목록이다.</summary>
    private static async Task<IReadOnlyList<T>> SafeListAsync<T>(Func<Task<List<T>>> load)
    {
        try
        {
            return await load();
        }
        catch (ApiException)
        {
            return [];
        }
    }

    /// <summary>자동 조회 스위치. 기본은 꺼짐 — 띄워 두는 상황판이 아니라 들여다보는 화면이라, 저 혼자 움직이면 읽던 자리를 놓친다.</summary>
    private void ToggleAuto(bool on)
    {
        _auto = on;

        if (on)
        {
            StartAutoRefresh();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    // ── 서버 응답 ───────────────────────────────────────────

    /// <summary><c>dashboard/admin-stats</c> — 나의 건수.</summary>
    public sealed class AdminStats
    {
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int UserCompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public int ConsultationCount { get; set; }
        public int NegotiationCount { get; set; }

        /// <summary>지운 것 말고 전체. 접수율의 분모다.</summary>
        public int TotalRequests { get; set; }

        /// <summary>「내 것」을 셀 수 있었나. 거짓이면 위의 0 은 「없다」가 아니라 「모른다」다 — 서버가 나를 가리키는 담당자 줄을 못 찾은 것이다.</summary>
        public bool Linked { get; set; }

        /// <summary>서버가 실제로 센 담당자 줄 번호. 이것으로 목록을 거른다 — 신원의 헬프데스크 번호는 대개 고객 줄이라 쓰면 안 된다.</summary>
        public int? AdminId { get; set; }
    }

    /// <summary><c>dashboard/all-admin-stats</c> — 담당자 한 사람.</summary>
    public sealed class AdminRow
    {
        public int AdminId { get; set; }

        /// <summary>헬프데스크에 베껴 둔 이름. <b>포털을 못 읽었을 때만</b> 쓴다.</summary>
        public string AdminName { get; set; } = string.Empty;

        /// <summary>포털 로그인 아이디. 이름과 얼굴을 푸는 열쇠다. 옛 자료로 만든 줄은 비어 있을 수 있다.</summary>
        public string? LoginId { get; set; }

        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }

        /// <summary><b>담당자 완료와 요청자 종료를 합친 것</b>이다(서버가 합쳐 준다).</summary>
        public int CompletedCount { get; set; }

        public int RejectedCount { get; set; }
        public int ConsultationCount { get; set; }
        public int NegotiationCount { get; set; }
        public int TotalHandled { get; set; }
        public double AcceptanceRate { get; set; }
        public double CompletionRate { get; set; }
    }

    /// <summary><c>dashboard/company-stats</c> — 고객사 하나.</summary>
    public sealed class CompanyRow
    {
        public string? Id { get; set; }
        public string? CompanyName { get; set; }
        public DateTime? LastPendingDate { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int ConsultationCount { get; set; }
        public int NegotiationCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public double CompletionRate { get; set; }

        /// <summary>카드를 줄 세우는 값. 서버가 주지 않아 여기서 센다.</summary>
        [JsonIgnore]
        public int Total =>
            PendingCount + InProgressCount + ConsultationCount
            + NegotiationCount + CompletedCount + RejectedCount;
    }
}
