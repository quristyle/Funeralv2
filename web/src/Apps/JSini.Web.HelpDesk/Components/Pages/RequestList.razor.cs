using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestList
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _status));

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    private string? _keyword;
    private string? _status;

    /// <summary>
    /// 상태 고르개.
    ///
    /// 값이 <b>enum 이름</b>이다(숫자가 아니다). 서버의 DynamicFilterHelper 가
    /// 이름으로 견주고, 숫자를 보내면 조건이 통째로 무시되어 전체가 나온다.
    /// </summary>
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

    protected override async Task OnInitializedAsync()
    {
        await Context.LoadIdentityAsync();
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 연결이 없으면 「내 것」을 가려낼 수 없다. 조건 없이 부르면 남의
        // 요청이 통째로 보이므로 아예 부르지 않는다.
        if (Context.HelpdeskUserId is not { } customerId)
        {
            _rows = [];
            _total = 0;
            return 0;
        }

        var query = new Dictionary<string, object?>
        {
            ["customerId"] = customerId,
            ["page"] = 1,
            ["pageSize"] = 200,

            // 본문은 목록에 안 쓴다. 빼면 응답이 훨씬 가볍다.
            ["remove"] = "description,content",
            ["sorts"] = new[] { new { field = "createdAt", dir = "desc" } },
        };

        if (!string.IsNullOrWhiteSpace(_keyword))
        {
            query["title_or_like"] = _keyword.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_status))
        {
            query["status"] = _status;
        }

        var page = await Api.SearchAsync<ImprovementRequest>("requests/srch", query);

        _rows = page.Items;
        _total = page.TotalCount;

        // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
        // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
        if (_total > _rows.Count)
        {
            Say($"전체 {_total}건 중 최근 {_rows.Count}건입니다. "
                + "더 좁히려면 제목이나 상태로 거르십시오.");
        }

        return _rows.Count;
    }, "올린 요청이 없습니다.", "요청 목록을 읽지 못했습니다");

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
