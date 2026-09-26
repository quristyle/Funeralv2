using Microsoft.AspNetCore.Components;
using System.Data;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestManage
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _status),
        SchSummary.NameOf(Context.AdminOptions, o => o.Value, o => o.Label, _adminId),
        SchSummary.On(_onlyOpen, "처리 중인 것만"));

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    private string? _keyword;
    private string? _status;
    private string? _adminId;
    private bool _onlyOpen = true;

    private static readonly SchOption[] StatusOptions =
    [
        new("Pending", "대기"),
        new("InProgress", "진행"),
        new("Completed", "완료"),
        new("UserCompleted", "종료"),
        new("Consultation", "협의"),
        new("Negotiation", "논의"),
        new("Rejected", "반려"),
    ];

    /// <summary>
    /// 처리 중으로 볼 상태들. 「처리 중인 것만」 이 이 목록을 쓴다.
    ///
    /// 서버에 보낼 때는 <c>|</c> 로 잇는다 — 쉼표가 아니다.
    /// </summary>
    private static readonly string[] OpenStatuses =
        ["Pending", "InProgress", "Consultation", "Negotiation"];

    protected override async Task OnInitializedAsync()
    {
        // 담당자 고르개가 쓸 목록. 신원과 함께 받아 둔다.
        await Context.LoadIdentityAsync();
        await Context.LoadOrganizationsAsync();

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var query = new Dictionary<string, object?>
        {
            ["page"] = 1,
            ["pageSize"] = 300,
            ["remove"] = "description,content",
            // 최근 순. **`isEmergency` 로는 정렬하지 못한다** — 서버가 그 칸으로
            // 정렬하면 EF 가 질의를 번역하지 못해 400 이 난다. 긴급을 앞으로
            // 올리는 것은 받아 온 뒤에 이 화면에서 한다(`Ordered`).
            ["sorts"] = new[]
            {
                new { field = "createdAt", dir = "desc" },
            },
        };

        if (!string.IsNullOrWhiteSpace(_keyword))
        {
            query["title_or_like"] = _keyword.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_status))
        {
            query["status"] = _status;
        }
        else if (_onlyOpen)
        {
            // 상태를 따로 고르지 않았을 때만 「처리 중」으로 좁힌다. 둘을 함께
            // 걸면 고른 상태가 조용히 무시되어 화면과 결과가 어긋난다.
            //
            // **여러 값의 구분자는 `|` 다.** 쉼표로 이으면 서버가 그 전체를
            // 값 하나로 읽어 400 이 난다(DynamicFilterHelper 의 `in`).
            query["status_in"] = string.Join("|", OpenStatuses);
        }

        if (!string.IsNullOrWhiteSpace(_adminId))
        {
            query["adminId"] = _adminId;
        }

        var page = await Api.SearchAsync<ImprovementRequest>("requests/srch", query);

        // 긴급을 맨 앞으로. 서버가 이 칸으로 정렬하지 못해 여기서 한다.
        // 한 번에 300건까지만 받으므로 브라우저에서 줄 세워도 무겁지 않다.
        _rows =
        [
            .. page.Items
                .OrderByDescending(r => r.IsEmergency == true)
                .ThenByDescending(r => r.CreatedAt)
        ];

        _total = page.TotalCount;

        // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
        // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
        if (_total > _rows.Count)
        {
            Say($"전체 {_total}건 중 {_rows.Count}건을 읽었습니다. 조건을 좁히면 나머지가 보입니다.");
        }

        return _rows.Count;
    }, "조건에 맞는 요청이 없습니다.", "요청 목록을 읽지 못했습니다");

    /// <summary>접수하고 얼마나 지났는가. 끝난 것은 처리에 걸린 시간.</summary>
    private static string Elapsed(ImprovementRequest r)
    {
        if (r.CreatedAt is not { } from)
        {
            return "-";
        }

        var to = r.CompletededAt ?? r.CompletedAt ?? DateTime.Now;
        var span = to - from;

        return span < TimeSpan.Zero ? "-"
            : span.TotalDays>= 1 ? $"{(int)span.TotalDays}일"
            : $"{(int)span.TotalHours}시간";
    }

    private static string StatusText(ImprovementRequest r) =>
        !string.IsNullOrWhiteSpace(r.StatusName) ? r.StatusName! : StatusText(r.Status);

    private static string StatusText(string? status) => status switch
    {
        "Pending" => "대기",
        "InProgress" => "진행",
        "Rejected" => "반려",
        "Completed" => "완료",
        "UserCompleted" => "종료",
        "Consultation" => "협의",
        "Negotiation" => "논의",
        _ => status ?? "-",
    };

    private static string StatusClass(string? status) => status switch
    {
        "Completed" or "UserCompleted" => "jsini-badge--on",
        "InProgress" or "Consultation" or "Negotiation" => "jsini-badge--warn",
        "Rejected" => "jsini-badge--off",
        _ => "",
    };
}
