using Microsoft.AspNetCore.Components;
using System.Data;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestManage : IDisposable
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;

    /// <summary>떠날 때 쓰던 모습을 맡기는 곳. 돌아오면 그대로 되찾는다.</summary>
    [Inject] private ScreenState Screen { get; set; } = default!;

    /// <summary>
    /// 이 화면이 맡겨 두는 짐의 이름. 표의 모습(칸 너비·정렬·칸별 검색)은
    /// <c>CommGrd</c> 가 같은 이름으로 따로 맡는다(<c>StateKey</c>).
    /// </summary>
    internal const string StateKey = "helpdesk/request/manage";

    /// <summary>
    /// 떠날 때 챙겨 두는 것 — <b>조건과 그 조건으로 읽어 둔 것</b>.
    /// </summary>
    /// <param name="CompanyId">조건 — 회사(시스템관리자만 고를 수 있다).</param>
    /// <param name="RequesterId">조건 — 요청자.</param>
    /// <param name="Status">조건 — 상태.</param>
    /// <param name="AdminId">조건 — 담당자.</param>
    /// <param name="Keyword">조건 — 제목.</param>
    /// <param name="OnlyOpen">조건 — 「처리 중인 것만」.</param>
    /// <param name="Rows">그 조건으로 읽어 둔 줄들. 돌아오면 다시 묻지 않는다.</param>
    /// <param name="Total">서버가 알려 준 전체 건수. 잘렸는지 알려면 함께 든다.</param>
    /// <param name="SelectedId">
    /// 보고 있던 줄. 줄 자체가 아니라 번호로 적는다 — 돌아와서 다시 조회하게
    /// 되면(<paramref name="Rows"/> 를 잃었을 때) 객체는 다른 물건이 되기 때문이다.
    /// </param>
    private sealed record Kept(
        string? CompanyId,
        string? RequesterId,
        string? Status,
        string? AdminId,
        string? Keyword,
        bool OnlyOpen,
        IReadOnlyList<ImprovementRequest> Rows,
        int Total,
        int? SelectedId);

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        Context.IsSystemAdmin ? SchSummary.NameOf(CompanyFilterOptions, o => o.Value, o => o.Label, _companyId) : null,
        SchSummary.NameOf(RequesterFilterOptions, o => o.Value, o => o.Label, _requesterId),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _status),
        SchSummary.NameOf(Context.AdminOptions, o => o.Value, o => o.Label, _adminId),
        SchSummary.Or(_keyword),
        SchSummary.On(_onlyOpen, "처리 중인 것만"));

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    private string? _keyword;
    private string? _status;
    private string? _companyId;
    private string? _requesterId;
    private string? _adminId;
    private bool _onlyOpen = true;

    /// <summary>고른 줄. 돌아왔을 때 강조가 그 자리에 그대로 서게 하려고 든다.</summary>
    private ImprovementRequest? _selected;

    /// <summary>돌아온 것인가 — 맡겨 둔 짐을 되찾았는가.</summary>
    private bool _restored;

    /// <summary>
    /// 썸네일을 못 받은 파일들. 깨진 네모 대신 빈 자리로 돌아간다.
    /// </summary>
    /// <remarks>
    /// 개발 장비에서 올린 그림은 <b>운영 파일 서버에 바이트가 없다</b> —
    /// 표 하나에 그런 줄이 여럿이면 깨진 네모가 줄줄이 선다. 한 번 실패한
    /// 주소는 적어 두고 다시 걸지 않는다.
    /// </remarks>
    private readonly HashSet<string> _brokenThumbs = new(StringComparer.Ordinal);

    private IReadOnlyList<BizOption> RequesterFilterOptions
    {
        get
        {
            var companyId = Context.IsSystemAdmin ? _companyId : Context.CompanyId;
            var items = string.IsNullOrEmpty(companyId) 
                ? Context.CustomerItems 
                : Context.CustomerItems.Where(item => 
                    BizOptionService.GetText(item, "companyId") == companyId);
            var filtered = items.Select(item => new BizOption(
                BizOptionService.GetText(item, "userName") ?? "",
                BizOptionService.GetText(item, "id")
            )).ToList();
            return [new("전체", null), .. filtered];
        }
    }

    private IReadOnlyList<BizOption> CompanyFilterOptions =>
        [new("전체", null), .. Context.CompanyOptions];

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

    /// <summary>
    /// 쓰던 모습을 되찾는다. <b>첫 <c>await</c> 앞이어야 한다</b> —
    /// <see cref="OnInitializedAsync"/> 로 미루면 그 사이에 빈 표가 한 번
    /// 그려지고, 표(<c>DxGrid</c>)는 그 빈 상태로 자기 모습을 잡는다.
    /// </summary>
    protected override void OnInitialized()
    {
        if (Screen.Get<Kept>(StateKey) is not { } kept)
        {
            return;
        }

        _companyId = kept.CompanyId;
        _requesterId = kept.RequesterId;
        _status = kept.Status;
        _adminId = kept.AdminId;
        _keyword = kept.Keyword;
        _onlyOpen = kept.OnlyOpen;
        _rows = kept.Rows;
        _total = kept.Total;
        _selected = kept.SelectedId is { } id ? _rows.FirstOrDefault(r => r.Id == id) : null;

        // **빈손으로 돌아왔으면 조건만 되찾고 다시 묻는다.** 조회가 끝나기 전에
        // 떠났거나(왕복이 빠르다) 프리렌더가 빈 것을 맡겼을 수 있는데, 그것을
        // 「다 읽어 둔 것」으로 읽으면 화면이 영영 빈 표가 된다 — 사람은 조건이
        // 걸려 있는 것을 보고 「그 조건에 맞는 게 없구나」로 읽는다.
        _restored = _rows.Count > 0;
    }

    protected override async Task OnInitializedAsync()
    {
        // 담당자 고르개가 쓸 목록. 신원과 함께 받아 둔다.
        await Context.LoadIdentityAsync();
        await Context.LoadOrganizationsAsync();

        if (!Context.IsSystemAdmin)
        {
            _companyId = Context.CompanyId;
        }

        // [돌아온 길이면 다시 묻지 않는다]
        //
        // 이 화면은 상세를 열었다 닫는 것이 일이라, 한 번 왕복할 때마다 300건을
        // 다시 읽으면 **조건을 다시 걸 때보다 기다리는 시간이 길다.** 그리고
        // 다시 읽으면 표가 맨 위로 돌아가 「그대로」가 깨진다.
        //
        // 오래된 것을 보게 되지만 그것은 **사람이 정할 일**이다 — 「조회」와
        // 오른쪽 클릭의 「다시 읽기」가 그 자리에 그대로 있다.
        if (_restored)
        {
            return;
        }

        await ReloadAsync();
    }

    /// <summary>
    /// 떠나면서 쓰던 모습을 맡긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 조건이 바뀔 때마다 맡기지 않고 <b>떠날 때 한 번</b> 맡긴다 — 여기 담기는
    /// 것에 읽어 둔 줄 수백 개가 들어 있어서, 글자 하나 칠 때마다 담으면
    /// 그만큼 기록이 늘어난다.
    /// </para>
    /// <para>
    /// 프리렌더에서도 불린다(그때는 조회를 안 했으니 빈 것을 맡긴다). 그
    /// <see cref="ScreenState"/> 는 요청의 것이고 회로의 것과 다른 물건이라
    /// 아무 데도 닿지 않는다.
    /// </para>
    /// </remarks>
    public void Dispose() =>
        Screen.Set(StateKey, new Kept(
            _companyId, _requesterId, _status, _adminId, _keyword, _onlyOpen,
            _rows, _total, _selected?.Id));

    private Task ReloadAsync()
    {
        if (!Context.IsSystemAdmin && Context.CompanyId is null)
        {
            _rows = [];
            _total = 0;
            Say("소속 회사 정보를 확인할 수 없어 요청을 조회하지 않았습니다.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
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

            if (!string.IsNullOrWhiteSpace(_requesterId))
            {
                query["customerId"] = _requesterId;
            }

            if (!string.IsNullOrWhiteSpace(_adminId))
            {
                query["adminId"] = _adminId;
            }

            var companyId = Context.IsSystemAdmin ? _companyId : Context.CompanyId;
            if (!string.IsNullOrWhiteSpace(companyId))
            {
                query["customer.companyId"] = companyId;
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

            // 고른 줄은 **번호로** 다시 잡는다. 방금 받은 것은 같은 요청이라도
            // 다른 객체라, 들고 있던 것을 그대로 두면 표에 없는 줄을 가리킨
            // 채로 남는다(강조가 아무 데도 안 걸린다).
            _selected = _selected is { } was ? _rows.FirstOrDefault(r => r.Id == was.Id) : null;

            // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
            // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
            if (_total > _rows.Count)
            {
                Say($"전체 {_total}건 중 {_rows.Count}건을 읽었습니다. 조건을 좁히면 나머지가 보입니다.");
            }

            return _rows.Count;
        }, "조건에 맞는 요청이 없습니다.", "요청 목록을 읽지 못했습니다");
    }

    private void OnRowClick(ImprovementRequest r)
    {
            Navigation.NavigateTo($"/helpdesk/request/detail/{r.Id}");
    }

    /// <summary>
    /// 이 줄을 대신할 그림. 한 번 못 받은 것은 다시 걸지 않는다.
    /// </summary>
    private string? ThumbOf(ImprovementRequest r)
    {
        var url = RequestThumb.UrlOf(r);
        return url is not null && _brokenThumbs.Contains(url) ? null : url;
    }

    /// <summary>
    /// 그림을 못 받았다. 깨진 네모를 지우고 빈 자리로 돌아간다.
    /// </summary>
    private void ThumbFailed(string url)
    {
        if (_brokenThumbs.Add(url))
        {
            StateHasChanged();
        }
    }

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
