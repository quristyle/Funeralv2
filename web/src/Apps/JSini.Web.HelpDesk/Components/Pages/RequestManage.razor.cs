using Microsoft.AspNetCore.Components;
using System.Data;
using System.Globalization;
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

    // ── 현황판이 싣고 오는 조건 ─────────────────────────────
    //
    // 현황판(`/helpdesk/dashboard`)의 「한눈에 보기」 타일을 누르면 그 타일이
    // 센 것과 **같은 것**이 여기 목록으로 열려야 한다. 그래서 타일은 조건을
    // 주소에 실어 보내고, 이 화면은 들어올 때 그것을 조회 조건으로 푼다.
    //
    // 주소에 적는 이름은 조회 조건 칸과 일대일이다 —
    // 주소를 그대로 즐겨찾기에 넣어도 같은 목록이 열린다.

    /// <summary>상태. 여럿이면 <c>|</c> 로 잇는다(<c>Consultation|Negotiation</c>).</summary>
    [SupplyParameterFromQuery(Name = "status")] public string? StatusQuery { get; set; }

    /// <summary>기간을 무엇으로 재나 — <c>requested</c>(접수) · <c>resolved</c>(완료).</summary>
    [SupplyParameterFromQuery(Name = "basis")] public string? BasisQuery { get; set; }

    /// <summary>기간 시작(<c>yyyy-MM-dd</c>). 그 날을 포함한다.</summary>
    [SupplyParameterFromQuery(Name = "from")] public string? FromQuery { get; set; }

    /// <summary>기간 끝(<c>yyyy-MM-dd</c>). <b>그 날을 포함한다</b>(하루 끝까지).</summary>
    [SupplyParameterFromQuery(Name = "to")] public string? ToQuery { get; set; }

    /// <summary>회사. 시스템관리자가 아니면 제 회사로 덮인다.</summary>
    [SupplyParameterFromQuery(Name = "company")] public string? CompanyQuery { get; set; }

    /// <summary>
    /// 「처리 중인 것만」. <b>타일은 언제나 <c>false</c> 로 보낸다</b> — 이 화면의
    /// 기본값이 켜짐이라, 안 끄고 보내면 완료·반려 타일이 빈 목록으로 열린다.
    /// </summary>
    [SupplyParameterFromQuery(Name = "open")] public string? OpenQuery { get; set; }

    /// <summary>담당자.</summary>
    [SupplyParameterFromQuery(Name = "admin")] public string? AdminQuery { get; set; }

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
    /// <param name="Statuses">조건 — 상태. <b>여럿 고를 수 있다.</b></param>
    /// <param name="AdminId">조건 — 담당자.</param>
    /// <param name="Keyword">조건 — 제목.</param>
    /// <param name="OnlyOpen">조건 — 「처리 중인 것만」.</param>
    /// <param name="Basis">조건 — 기간을 무엇으로 재나(접수 · 완료).</param>
    /// <param name="From">조건 — 기간 시작.</param>
    /// <param name="To">조건 — 기간 끝.</param>
    /// <param name="Rows">그 조건으로 읽어 둔 줄들. 돌아오면 다시 묻지 않는다.</param>
    /// <param name="Total">서버가 알려 준 전체 건수. 잘렸는지 알려면 함께 든다.</param>
    /// <param name="SelectedId">
    /// 보고 있던 줄. 줄 자체가 아니라 번호로 적는다 — 돌아와서 다시 조회하게
    /// 되면(<paramref name="Rows"/> 를 잃었을 때) 객체는 다른 물건이 되기 때문이다.
    /// </param>
    private sealed record Kept(
        string? CompanyId,
        string? RequesterId,
        IReadOnlyList<string> Statuses,
        string? AdminId,
        string? Keyword,
        bool OnlyOpen,
        string Basis,
        DateTime? From,
        DateTime? To,
        IReadOnlyList<ImprovementRequest> Rows,
        int Total,
        int? SelectedId);

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        Context.IsSystemAdmin ? SchSummary.NameOf(CompanyFilterOptions, o => o.Value, o => o.Label, _companyId) : null,
        SchSummary.NameOf(RequesterFilterOptions, o => o.Value, o => o.Label, _requesterId),
        StatusSummary,
        SchSummary.NameOf(Context.AdminOptions, o => o.Value, o => o.Label, _adminId),
        SchSummary.Or(_keyword),
        PeriodSummary,
        SchSummary.On(_onlyOpen, "처리 중인 것만"));

    /// <summary>
    /// 고른 상태를 한 줄로. 아무것도 안 골랐으면 「전체」다 —
    /// <see cref="SchSummary.NameOf"/> 가 하나짜리 고르개에 해 주던 일을
    /// 여럿짜리로 옮긴 것이다.
    /// </summary>
    private string StatusSummary => _statuses.Count == 0
        ? SchSummary.Any
        : string.Join(", ", _statuses.Select(StatusText));

    /// <summary>
    /// 기간을 한 줄로. <b>기준(접수·완료)을 앞에 붙인다</b> — 날짜만 적으면
    /// 「9.28 ~ 9.28」이 접수인지 완료인지 알 수 없다.
    /// </summary>
    private string? PeriodSummary => SchSummary.Period(_from, _to) is { } period
        ? $"{BasisText} {period}"
        : null;

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    private string? _keyword;
    private IReadOnlyList<string> _statuses = [];
    private string? _companyId;
    private string? _requesterId;
    private string? _adminId;
    private bool _onlyOpen = true;

    /// <summary>기간을 무엇으로 재나. <see cref="RequestedBasis"/> · <see cref="ResolvedBasis"/>.</summary>
    private string _basis = RequestedBasis;

    private DateTime? _from;
    private DateTime? _to;

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

    // ── 기간 ────────────────────────────────────────────────

    /// <summary>기간을 <b>접수</b> 시각으로 잰다.</summary>
    private const string RequestedBasis = "requested";

    /// <summary>기간을 <b>완료·종료</b> 시각으로 잰다.</summary>
    private const string ResolvedBasis = "resolved";

    private static readonly SchOption[] BasisOptions =
    [
        new(RequestedBasis, "접수"),
        new(ResolvedBasis, "완료"),
    ];

    private bool ByResolved => string.Equals(_basis, ResolvedBasis, StringComparison.Ordinal);

    private string BasisText => ByResolved ? "완료" : "접수";

    /// <summary>
    /// 아무도 이보다 먼저 올린 요청은 없다. <c>between</c> 은 양쪽 값을 다
    /// 요구해서, 한쪽만 고른 기간에 반대쪽을 채워 넣을 값이 필요하다.
    /// </summary>
    private static readonly DateTime Dawn = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc cref="Dawn"/>
    private static readonly DateTime Dusk = new(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 쓰던 모습을 되찾는다. <b>첫 <c>await</c> 앞이어야 한다</b> —
    /// <see cref="OnInitializedAsync"/> 로 미루면 그 사이에 빈 표가 한 번
    /// 그려지고, 표(<c>DxGrid</c>)는 그 빈 상태로 자기 모습을 잡는다.
    /// </summary>
    /// <remarks>
    /// <b>주소로 조건을 싣고 왔으면 그것이 맡겨 둔 짐보다 먼저다</b>
    /// (<see cref="ApplyQueryConditions"/>). 현황판 타일을 눌러 들어온 길인데
    /// 맡겨 둔 짐을 먼저 풀면 <b>눌러서 고른 조건이 지난번 조건에 덮여</b>
    /// 엉뚱한 목록이 열린다 — 누른 사람은 왜 그 숫자가 안 나오는지 알 길이 없다.
    /// </remarks>
    protected override void OnInitialized()
    {
        if (ApplyQueryConditions())
        {
            return;
        }

        if (Screen.Get<Kept>(StateKey) is not { } kept)
        {
            return;
        }

        _companyId = kept.CompanyId;
        _requesterId = kept.RequesterId;
        _statuses = kept.Statuses;
        _adminId = kept.AdminId;
        _keyword = kept.Keyword;
        _onlyOpen = kept.OnlyOpen;
        _basis = kept.Basis;
        _from = kept.From;
        _to = kept.To;
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
            _companyId, _requesterId, _statuses, _adminId, _keyword, _onlyOpen,
            _basis, _from, _to,
            _rows, _total, _selected?.Id));

    // ── 주소로 들어온 조건 ──────────────────────────────────

    /// <summary>
    /// 주소에 실려 온 조건을 조회 조건으로 푼다. <b>하나라도 실려 있었으면
    /// 참</b>을 돌려주고, 그때는 맡겨 둔 짐을 풀지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>모르는 값은 버린다.</b> 고르개에 없는 상태 글자를 그대로 실으면
    /// 고르개는 빈칸인데 서버는 그 글자로 걸러 빈 목록을 준다 — 왜 비었는지
    /// 화면 어디에도 안 보인다.
    /// </para>
    /// <para>
    /// 요청자·제목은 주소로 안 받는다. 현황판 타일이 그것으로는 세지
    /// 않기 때문이고, 받을 곳이 늘면 그만큼 「주소와 화면이 어긋나는」 자리가
    /// 늘어난다.
    /// </para>
    /// </remarks>
    private bool ApplyQueryConditions()
    {
        var statuses = ParseStatuses(StatusQuery);
        var from = ParseDay(FromQuery);
        var to = ParseDay(ToQuery);
        var basis = string.Equals(BasisQuery, ResolvedBasis, StringComparison.OrdinalIgnoreCase)
            ? ResolvedBasis
            : null;

        var carried = statuses.Count > 0
            || from is not null
            || to is not null
            || basis is not null
            || !string.IsNullOrWhiteSpace(CompanyQuery)
            || !string.IsNullOrWhiteSpace(OpenQuery)
            || !string.IsNullOrWhiteSpace(AdminQuery);

        if (!carried)
        {
            return false;
        }

        _statuses = statuses;
        _from = from;
        _to = to;
        _basis = basis ?? RequestedBasis;
        _companyId = string.IsNullOrWhiteSpace(CompanyQuery) ? null : CompanyQuery;
        _adminId = string.IsNullOrWhiteSpace(AdminQuery) ? null : AdminQuery;

        // 「처리 중인 것만」은 **적어 보냈을 때만** 건드린다. 안 적었으면 이
        // 화면의 기본값(켜짐)이 그대로다.
        if (!string.IsNullOrWhiteSpace(OpenQuery))
        {
            _onlyOpen = !string.Equals(OpenQuery, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(OpenQuery, "0", StringComparison.Ordinal);
        }

        return true;
    }

    /// <summary>
    /// <c>Consultation|Negotiation</c> 을 고르개가 아는 값들로 푼다.
    /// 쉼표로 이어 보내도 받는다 — 주소를 손으로 적는 사람이 있다.
    /// </summary>
    private static IReadOnlyList<string> ParseStatuses(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => StatusOptions
                    .FirstOrDefault(o => string.Equals(o.Value, token, StringComparison.OrdinalIgnoreCase))?.Value)
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// <c>yyyy-MM-dd</c> 하루. 못 읽으면 <c>null</c> 이라 그 조건이 없는 것과 같다.
    /// </summary>
    private static DateTime? ParseDay(string? raw) =>
        DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day)
            ? day
            : null;

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
                // **`_or_` 가 아니라 그냥 `_like` 다.** 서버의 OR 자리는 하나뿐이라
                // (`DynamicFilterHelper` 의 orGroupParts), 거기에 제목을 넣어 두면
                // 아래 「완료 기준」 기간 조건과 한 묶음으로 OR 되어
                // 「제목이 맞거나 **또는** 그 기간에 끝났거나」가 된다.
                // 조각이 하나뿐일 때 두 표기는 결과가 같으므로 이쪽으로 적는다.
                query["title_like"] = _keyword.Trim();
            }

            if (_statuses.Count > 0)
            {
                // **여러 값의 구분자는 `|` 다.** 쉼표로 이으면 서버가 그 전체를
                // 값 하나로 읽어 400 이 난다(DynamicFilterHelper 의 `in`).
                query["status_in"] = string.Join("|", _statuses);
            }
            else if (_onlyOpen)
            {
                // 상태를 따로 고르지 않았을 때만 「처리 중」으로 좁힌다. 둘을 함께
                // 걸면 고른 상태가 조용히 무시되어 화면과 결과가 어긋난다.
                query["status_in"] = string.Join("|", OpenStatuses);
            }
            else
            {
                // **지운 요청은 안 센다.** 상태를 안 고르고 「처리 중인 것만」도
                // 끄면 여기는 여태 `Delete` 까지 실어 왔다. 현황판은 그것을 빼고
                // 세므로(`DashboardOverviewService`), 「전체」 타일을 눌렀을 때
                // 숫자가 안 맞는 자리가 바로 여기다.
                query["status_nin"] = "Delete";
            }

            ApplyPeriod(query);

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

    /// <summary>
    /// 고른 기간을 조회 조건으로 얹는다. 고른 것이 없으면 아무것도 안 얹는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [날짜는 우리 시계로 고르고, 서버에는 순간으로 보낸다]
    /// </para>
    /// <para>
    /// 고르개에서 고른 「9월 28일」은 <b>한국 시각의 하루</b>다. DB 는 UTC 로
    /// 적혀 있으므로 그 하루의 시작·끝을 순간으로 바꿔 보낸다. 날짜 글자를
    /// 그대로 보내면 서버가 UTC 0시로 읽어 <b>한국의 오전 아홉 시간이 전날로
    /// 밀린다</b> — 「오늘 접수」가 어제 것까지 세는 꼴이다.
    /// (운영 컨테이너는 전부 <c>TZ=Asia/Seoul</c> 이다 — <c>deploy/docker</c>.)
    /// </para>
    /// <para>
    /// [완료 기준은 칸이 둘이다]
    /// </para>
    /// <para>
    /// 끝난 시각은 <c>completededAt</c>(담당자가 완료) 아니면
    /// <c>userCompletededAt</c>(요청자가 종료)에 적힌다. 둘 중 <b>하나라도</b>
    /// 그 기간에 들면 끝난 것이므로 OR 로 묶는다 — 서버의 <c>_or_</c> 표기가
    /// 그것이고, 그래서 위에서 제목을 그 자리에서 빼 두었다.
    /// </para>
    /// </remarks>
    private void ApplyPeriod(IDictionary<string, object?> query)
    {
        var start = StartUtc(_from);

        // **끝 날짜는 그 날을 포함한다.** 다음 날 0시 직전까지다 — 고른 사람은
        // 「28일까지」를 28일 하루를 다 넣는 뜻으로 읽는다.
        var end = StartUtc(_to?.AddDays(1));

        if (start is null && end is null)
        {
            return;
        }

        if (ByResolved)
        {
            // `between` 은 양쪽 값을 다 요구한다(한쪽만 주면 조건이 통째로
            // 버려져 **기간을 안 건 것처럼 보인다**). 안 고른 쪽은 끝까지 연다.
            var span = $"{Iso(start ?? Dawn)}|{Iso((end ?? Dusk).AddTicks(-1))}";

            query["completededAt_or_between"] = span;
            query["userCompletededAt_or_between"] = span;
            return;
        }

        if (start is { } from)
        {
            query["requestedAt_gte"] = Iso(from);
        }

        if (end is { } to)
        {
            query["requestedAt_lt"] = Iso(to);
        }
    }

    /// <summary>우리 시계의 그 날 0시를 UTC 순간으로.</summary>
    private static DateTime? StartUtc(DateTime? day) => day is { } d
        ? DateTime.SpecifyKind(d.Date, DateTimeKind.Local).ToUniversalTime()
        : null;

    /// <summary>
    /// 서버가 읽는 시각 글자. <b>끝의 <c>Z</c> 가 반드시 있어야 한다</b> —
    /// 시간대를 안 적으면 서버가 그 값을 시간대 없는 시각으로 읽고,
    /// 그 상태로는 <c>timestamptz</c> 칸에 못 쓴다(질의가 통째로 터진다).
    /// </summary>
    private static string Iso(DateTime at) =>
        at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private Task OnStatusesChangedAsync(IEnumerable<string> picked)
    {
        _statuses = [.. picked];
        return ReloadAsync();
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

        // **UTC 로 잰다.** 서버가 주는 시각은 UTC 인데 `DateTime.Now` 는 우리
        // 시계라, 섞어 빼면 아직 안 끝난 건의 경과가 통째로 아홉 시간 부풀었다.
        var to = r.CompletededAt ?? r.CompletedAt ?? DateTime.UtcNow;
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
