using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using System.Data;
using System.Globalization;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using JSini.Web.HelpDesk.Components.Shared;

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
    /// <param name="Take">
    /// 휴대폰에서 「더보기」로 꺼내 둔 줄 수. <b>이것도 쓰던 모습이다</b> —
    /// 안 맡기면 네 번 눌러 찾아 놓은 줄이 상세를 열었다 돌아오는 순간
    /// 처음 한 쪽으로 되감긴다.
    /// </param>
    /// <param name="Batches">
    /// 서버에서 받아 온 묶음 수(<see cref="BatchRows"/> 줄씩). <b>다음에 몇 쪽을
    /// 달라고 할지가 이 값이다</b> — 안 맡기면 돌아온 사람이 「더 읽기」를
    /// 눌렀을 때 이미 들고 있는 첫 묶음을 다시 받는다.
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
        int? SelectedId,
        int Take,
        int Batches);

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
        : string.Join(", ", _statuses.Select(RequestRowText.Status));

    /// <summary>
    /// 기간을 한 줄로. <b>기준(접수·완료)을 앞에 붙인다</b> — 날짜만 적으면
    /// 「9.28 ~ 9.28」이 접수인지 완료인지 알 수 없다.
    /// </summary>
    private string? PeriodSummary => SchSummary.Period(_from, _to) is { } period
        ? $"{BasisText} {period}"
        : null;

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    // ── 「더보기」와 「더 읽기」 ────────────────────────────
    //
    // 자르는 자리가 둘이다 —
    //
    //   꺼내 깔기   읽어 둔 `_rows` 중 **몇 줄을 표에 넣나**(`_take`).
    //               휴대폰만 쓴다. 데스크톱은 그 일을 표의 페이저가 한다.
    //   받아 오기   서버에서 **몇 묶음을 받아 왔나**(`_batches`).
    //               한 묶음이 `BatchRows` 줄이고, 둘 다 쓴다.
    //
    // 처음에는 바깥쪽 자르기가 없었다 — 첫 묶음 300건을 받아 두고 그게 전부인
    // 척했다. 조건에 맞는 것이 그보다 많으면 **토스트 한 줄로 알리고 끝**이라,
    // 휴대폰에서는 「더보기」가 삼백 번째 줄에서 소리 없이 사라졌다. 표
    // 머리줄도 페이저도 없는 화면이라 **거기가 끝인지 잘린 것인지 알 길이
    // 없다** — 사람은 「내 요청은 여기까지구나」로 읽는다.
    //
    // 그래서 다 깔고 나서도 서버에 남은 것이 있으면 **그 자리에서 다음 묶음을
    // 받아 온다.** 단추에 적는 수도 받아 둔 것이 아니라 **서버가 말한 전체**를
    // 기준으로 센다(`Rest`) — 「더보기 940건」이 진짜 남은 수다.

    /// <summary>
    /// 한 쪽에 깔리는 줄 수. <b>페이저와 「더보기」가 같은 수를 쓴다</b> —
    /// 「다음 쪽」이 기기마다 다른 수이면 같은 조건으로 띄운 두 화면이
    /// 서로 다른 자리에서 끊긴다. 표에 적은 <c>PageSize</c> 와 같은 값이다.
    /// </summary>
    private const int PageRows = 25;

    /// <summary>
    /// 한 번에 서버에서 받아 오는 줄 수. <b><see cref="PageRows"/> 의 배수여야
    /// 한다</b> — 아니면 묶음 끝에서 「더보기」 한 번이 한 쪽보다 적게 깔린다.
    /// </summary>
    private const int BatchRows = 300;

    /// <summary>
    /// 휴대폰인가. <see cref="OnPhoneChanged"/> 가 채운다 —
    /// Blazor Server 는 브라우저 폭을 모른다(화면 머리말).
    /// </summary>
    private bool _isPhone;

    /// <summary>
    /// 휴대폰에서 지금까지 꺼내 깔아 둔 줄 수. 「더보기」가 한 쪽씩 늘린다.
    /// </summary>
    /// <remarks>
    /// 데스크톱에서는 쓰이지 않는다 — 그쪽은 표가 쪽으로 나눈다.
    /// </remarks>
    private int _take = PageRows;

    /// <summary>
    /// 서버에서 받아 온 묶음 수. 0 이면 아직 한 번도 안 읽었다.
    /// 다음에 달라고 할 쪽 번호가 이 값 + 1 이다.
    /// </summary>
    private int _batches;

    /// <summary>
    /// 표에 넣을 줄. 휴대폰에서는 <b>꺼내 둔 만큼만</b> 넣고, 나머지는
    /// 「더보기」가 꺼낸다. 데스크톱에서는 읽어 둔 것을 그대로 넘긴다 —
    /// 자르는 일은 표의 페이저가 한다.
    /// </summary>
    private IReadOnlyList<ImprovementRequest> Shown =>
        _isPhone && _take < _rows.Count ? [.. _rows.Take(_take)] : _rows;

    /// <summary>
    /// 아직 안 깔린 줄 수. 「더보기」 단추에 적는다.
    /// </summary>
    /// <remarks>
    /// <b>읽어 둔 것이 아니라 서버가 말한 전체(<see cref="_total"/>)에서 센다.</b>
    /// 받아 둔 묶음만으로 세면 삼백 줄째에서 0 이 되어 단추가 사라지고,
    /// 사람은 거기가 끝인 줄 안다.
    /// </remarks>
    private int Rest => Math.Max(0, _total - _take);

    /// <summary>서버에 아직 안 받아 온 줄이 남았는가.</summary>
    private bool HasUnread => _rows.Count < _total;

    /// <summary>다음 「더 읽기」가 받아 올 줄 수. 데스크톱 단추에 적는다.</summary>
    private int NextBatch => Math.Min(BatchRows, Math.Max(0, _total - _rows.Count));

    /// <summary>
    /// 한 쪽만큼 더 깔고, 깔 것이 모자라면 <b>그 자리에서 다음 묶음을 받아
    /// 온다.</b>
    /// </summary>
    /// <remarks>
    /// 받아 오기가 실패하면 <see cref="_rows"/> 가 안 늘고 <see cref="_take"/>
    /// 도 제자리에 멈춘다(아래 <c>Math.Min</c>) — 단추는 그대로 남아 다시
    /// 누를 수 있다. 실패를 말하는 쪽은 <c>LoadAsync</c>(<c>DataPage</c>) 다.
    /// </remarks>
    private async Task ShowMoreAsync()
    {
        if (_take + PageRows > _rows.Count && HasUnread)
        {
            await ReadMoreAsync();
        }

        _take = Math.Min(_take + PageRows, Math.Max(_rows.Count, PageRows));
    }

    /// <summary>
    /// 휴대폰 경계(≤767px)를 넘었다.
    /// </summary>
    /// <remarks>
    /// <b>여기서 <see cref="_take"/> 를 되돌리지 않는다.</b> 이 갈고리는 회로가
    /// 붙고 나서 **처음 한 번** 참으로 울리므로(그 전에는 폭을 모른다),
    /// 되돌리면 맡겨 둔 짐에서 되찾은 값(<see cref="Kept.Take"/>)이 그 자리에서
    /// 덮인다 — 돌아올 때마다 처음 한 쪽으로 되감긴다.
    /// </remarks>
    private void OnPhoneChanged(bool active) => _isPhone = active;

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
    /// 조건이 이미 정해졌는가 — 주소로 실려 왔거나 맡겨 둔 짐에서 되찾았거나.
    /// </summary>
    /// <remarks>
    /// 그때는 「내 요청」 기본값을 얹지 않는다(<see cref="OnInitializedAsync"/>).
    /// 얹으면 <b>현황판 타일로 고른 조건이나 쓰던 조건이 덮인다</b>.
    /// <see cref="_restored"/> 로는 가를 수 없다 — 그 값은 읽어 둔 줄이 있을 때만
    /// 참이라, 조건만 되찾은 길에서는 거짓이다.
    /// </remarks>
    private bool _seeded;

    /// <summary>
    /// 요청자 칸을 잠갔는가 — <b>고객이면</b> 참(관리자가 아닌 모든 사람).
    /// </summary>
    /// <remarks>
    /// <b>고객 번호가 있는지로 가르지 않는다.</b> 번호가 없는 고객(요청을 한 번도
    /// 올린 적이 없는 사람)까지 잠가야 한다 — 안 잠그면 그 사람에게만 칸이 열려
    /// <b>같은 회사 전체가 보인다</b>. 번호가 없을 때 무엇을 보여 줄지는
    /// <see cref="ReloadAsync"/> 가 정한다(아무것도 안 부른다).
    /// </remarks>
    private bool RequesterLocked => Context.IsCustomer;

    /// <summary>잠긴 칸에 적을 글자. 고르개가 아는 이름을 그대로 쓴다.</summary>
    private string RequesterName =>
        RequesterFilterOptions.FirstOrDefault(o => o.Value == _requesterId)?.Label
        ?? Context.Identity?.UserName
        ?? "나";

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
            _seeded = true;
            return;
        }

        if (Screen.Get<Kept>(StateKey) is not { } kept)
        {
            return;
        }

        _seeded = true;

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

        // 꺼내 둔 줄 수도 쓰던 모습이다(`Kept.Take`). **한 쪽 아래로는 내려가지
        // 않는다** — 프리렌더가 맡긴 0 을 그대로 받으면 휴대폰 목록이 빈 채로
        // 열리고, 「더보기」를 눌러야 첫 줄이 나온다.
        _take = Math.Max(kept.Take, PageRows);

        // 받아 둔 묶음 수도 함께 되찾는다. 안 되찾으면 「더 읽기」가 쪽 2 가
        // 아니라 쪽 1 을 다시 달라고 해서, 이미 들고 있는 삼백 건을 또 받고도
        // 목록이 한 줄도 안 늘어난다(번호로 걸러 내므로 · `ReadMoreAsync`).
        _batches = kept.Batches;

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

        // [고객으로 들어왔으면 「내 요청」으로 선다 (2026-10-05)]
        //
        // 요청자가 제 것만 보던 목록(`/helpdesk/request/list`)을 걷어내고 이
        // 화면이 그 일까지 맡았다. 그 화면이 보여 주던 것은 **끝난 것까지 포함한
        // 내 요청 전부**라, 요청자를 나로 두고 「처리 중인 것만」을 끈다.
        //
        // **가르는 것은 「관리자인가」 하나다**(`HelpDeskContext.IsAdmin` —
        // 포털 역할 `ADMINISTRATOR` · `SYSTEM_ADMINISTRATOR`). 관리자는 여기를
        // 지나가고 요청자가 「전체」 그대로다.
        //
        // **요청자는 언제나 나다** — 주소로 들어왔든 돌아왔든. 칸이 잠겨 있어
        // (`RequesterLocked`) 사람이 바꿀 수 없는 값이고, 현황판 타일을 눌러
        // 들어온 길에서도 그 타일이 센 것 중 내 것만 보는 것이 맞다.
        //
        // **「처리 중인 것만」은 기본값만 바꾼다.** 그쪽은 사람이 끄고 켜는
        // 값이라, 쓰던 것을 되찾은 길에서 덮으면 방금 건 조건이 풀린다.
        if (Context.CustomerId is { } me)
        {
            _requesterId = me.ToString(CultureInfo.InvariantCulture);

            if (!_seeded)
            {
                _onlyOpen = false;
            }
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
            _rows, _total, _selected?.Id, _take, _batches));

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
        // 조건이 바뀌었으면 휴대폰의 「더보기」도 처음 한 쪽으로 되돌린다.
        // 안 되돌리면 백 줄을 꺼내 놓고 조건을 좁힌 사람이 **좁힌 결과를
        // 통째로 한 번에 받는다** — 「더보기」로 조금씩 보던 뜻이 사라진다.
        _take = PageRows;

        // 받아 둔 묶음도 버린다. 조건이 바뀌면 그것들은 다른 질문의 답이다 —
        // 안 버리면 다음 「더 읽기」가 새 조건의 쪽 **넷**을 달라고 한다.
        _batches = 0;

        // **「내 것」을 가려낼 수 없으면 아예 묻지 않는다.**
        //
        // 고객인데 가리킬 고객 줄이 아직 없는 사람이다 — 요청을 한 번도 올린 적이
        // 없으면 그 줄이 없다(서버가 첫 글을 쓸 때 만든다 · `RequesterProvisioner`).
        // 조건 없이 부르면 **같은 회사의 남의 요청이 통째로 나온다** — 걷어낸
        // 「내 요청」(`RequestList`)이 같은 자리에서 같은 판단을 했다.
        if (Context.IsCustomer && Context.CustomerId is null)
        {
            _rows = [];
            _total = 0;
            Say("아직 올리신 요청이 없습니다. 「요청 등록」으로 처음 글을 올리면 여기에 보입니다.");
            return Task.CompletedTask;
        }

        if (!Context.IsSystemAdmin && Context.CompanyId is null)
        {
            _rows = [];
            _total = 0;
            Say("소속 회사 정보를 확인할 수 없어 요청을 조회하지 않았습니다.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var page = await Api.SearchAsync<ImprovementRequest>("requests/srch", BuildQuery(1));

            _rows = Ordered(page.Items);
            _total = page.TotalCount;
            _batches = 1;

            // 고른 줄은 **번호로** 다시 잡는다. 방금 받은 것은 같은 요청이라도
            // 다른 객체라, 들고 있던 것을 그대로 두면 표에 없는 줄을 가리킨
            // 채로 남는다(강조가 아무 데도 안 걸린다).
            _selected = _selected is { } was ? _rows.FirstOrDefault(r => r.Id == was.Id) : null;

            return _rows.Count;
        }, "조건에 맞는 요청이 없습니다.", "요청 목록을 읽지 못했습니다");
    }

    /// <summary>
    /// 다음 묶음을 <b>이어 받는다</b>. 조건은 그대로고 쪽 번호만 넘긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [이미 읽은 줄은 걸러 낸다]
    /// </para>
    /// <para>
    /// 쪽을 번호로 끊어 받는 동안 **창이 밀린다** — 두 번 부르는 사이에 누가
    /// 요청을 하나 올리면 쪽 1 의 맨 끝 줄이 쪽 2 의 맨 앞으로 내려온다.
    /// 그대로 이어 붙이면 같은 줄이 목록에 두 번 선다. 번호로 걸러 둔다.
    /// </para>
    /// <para>
    /// [긴급은 <b>묶음 안에서만</b> 앞으로 온다]
    /// </para>
    /// <para>
    /// 받아 온 것 전체를 다시 줄 세우지 않는다. 그러면 뒤늦게 받은 묶음의
    /// 긴급 건이 <b>이미 보고 지나간 자리로 끼어들어</b>, 「더보기」를 눌렀을
    /// 뿐인데 보던 줄이 아래로 밀린다. 이어 붙이는 쪽은 늘 맨 아래여야 한다.
    /// (한 묶음 안에서 긴급이 앞인 것은 그대로다 — <see cref="Ordered"/>.)
    /// </para>
    /// </remarks>
    private Task ReadMoreAsync() => LoadAsync(async () =>
    {
        var next = _batches + 1;
        var page = await Api.SearchAsync<ImprovementRequest>("requests/srch", BuildQuery(next));

        _batches = next;
        _total = page.TotalCount;

        var seen = _rows.Select(r => r.Id).ToHashSet();
        _rows = [.. _rows, .. Ordered(page.Items).Where(r => seen.Add(r.Id))];

        if (page.Items.Count == 0)
        {
            // 서버가 말한 전체보다 실제가 적다 — 두 번 부르는 사이에 지워졌거나
            // 상태가 바뀌어 조건에서 빠진 것이다. 들고 있는 만큼으로 고쳐 두지
            // 않으면 **눌러도 아무 일이 없는 「더보기 12건」**이 남는다.
            _total = _rows.Count;
            Say("더 읽을 요청이 없습니다.");
        }

        return _rows.Count;
    }, "더 읽을 요청이 없습니다.", "요청을 더 읽지 못했습니다");

    /// <summary>
    /// 긴급을 맨 앞으로, 그다음 최근 순으로. <b>서버가 <c>isEmergency</c> 로는
    /// 정렬하지 못해</b>(EF 가 질의를 못 옮겨 400 이 난다) 여기서 한다.
    /// </summary>
    private static IReadOnlyList<ImprovementRequest> Ordered(IEnumerable<ImprovementRequest> items) =>
    [
        .. items
            .OrderByDescending(r => r.IsEmergency == true)
            .ThenByDescending(r => r.CreatedAt)
    ];

    /// <summary>
    /// 지금 조건을 서버가 읽는 모양으로 옮긴다. <b>쪽 번호만 다르고 나머지는
    /// 늘 같다</b> — 「조회」와 「더 읽기」가 같은 질문을 해야 이어 붙인 줄이
    /// 같은 목록의 뒷부분이 된다.
    /// </summary>
    private Dictionary<string, object?> BuildQuery(int page)
    {
        var query = new Dictionary<string, object?>
        {
            ["page"] = page,
            ["pageSize"] = BatchRows,
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

        // **고객은 제 것만 본다 — 못 박는 자리가 여기다.**
        //
        // 조건 칸을 잠가 두었지만(`RequesterLocked`) 그것은 거드는 것일 뿐이다.
        // 서버는 목록을 권한으로 거르지 않으므로 **화면이 조건을 빠뜨리면
        // 그대로 새어 나간다** — 걷어낸 「내 요청」(`RequestList`)이 같은
        // 까닭으로 같은 일을 했다. 맡겨 둔 짐이 다른 번호를 들고 오거나
        // 조건을 거는 길이 하나 더 생겨도 여기서 덮인다.
        // 번호가 없는 고객은 여기까지 오지 않는다(위에서 돌려보낸다).
        var requesterId = Context.CustomerId?.ToString(CultureInfo.InvariantCulture)
            ?? _requesterId;

        if (!string.IsNullOrWhiteSpace(requesterId))
        {
            query["customerId"] = requesterId;
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

        // **잘렸다는 말은 여기서 하지 않는다.** 예전에는 「전체 1240건 중
        // 300건을 읽었습니다」를 토스트로 띄우고 끝이었는데, 그 말은 몇 초
        // 뒤에 사라지고 **사람이 삼백 번째 줄에 닿는 것은 한참 뒤**다.
        // 남은 것이 있다는 말과 그것을 받는 길은 목록 아래 단추가 함께
        // 들고 있다(`Rest` · `ReadMoreAsync`).
        return query;
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

    /// <summary>
    /// 줄을 눌렀다 — 상세로 간다.
    /// </summary>
    /// <remarks>
    /// <b>고른 줄을 여기서도 적어 둔다.</b> 데스크톱은 표가 알려 주지만
    /// (<c>SelectedChanged</c>) 휴대폰 카드 목록에는 고르기가 없어서,
    /// 안 적으면 떠날 때 맡기는 짐의 <c>SelectedId</c> 가 늘 비고 돌아온
    /// 사람이 보던 줄을 잃는다.
    /// </remarks>
    private void OnRowClick(ImprovementRequest r)
    {
        _selected = r;
        Navigation.NavigateTo($"/helpdesk/request/detail/{r.Id}");
    }

    // ── 휴대폰에서 밀어서 접수 · 삭제 (2026-10-09) ────────────────
    //
    // 담당자가 휴대폰으로 보는 목록에서 가장 잦은 일이 **대기 건을 접수하는
    // 것**이다. 그런데 그러려면 줄을 눌러 상세로 들어가 「접수」를 누르고 다시
    // 돌아와야 했다 — 열 건이면 왕복 열 번이고, 그 왕복마다 300건을 다시 읽지
    // 않으려고 이 화면이 짐을 맡아 두고 있다(`Kept`).
    //
    //   오른쪽으로 민다   접수 — `PUT requests/accept/{id}` (상태 `InProgress`)
    //   왼쪽으로 민다     삭제 — `DELETE requests/{id}`
    //
    // **「대기」인 줄만 민다.** 가려내는 쪽은 카드 목록이고(`RequestCards` 의
    // `AcceptableOf` · `RemovableOf`) 거기서 한 번 더 못 박는다 — 브라우저가
    // 들고 있는 `data-*` 는 고칠 수 있는 값이다.
    //
    // [묻지 않고 바로 한다 (2026-10-10)]
    //
    // 밀면 **그대로 처리된다.** 처음에는 둘 다 확인 창을 거치게 했었는데,
    // 밀어서 하는 일을 만든 까닭이 「열 건이면 왕복 열 번」을 없애려는
    // 것이었다 — 그 자리에 창이 뜨면 손짓마다 한 번씩 더 눌러야 해서 왕복만
    // 짧아진 것이 된다.
    //
    // 비낀 손가락을 막는 것은 **문턱 하나**다(줄 폭의 1/4 · 390px 기기에서
    // 96px). 방향을 처음 몇 px 로 가려 세로로 끌면 손을 떼고, 끌다 문턱을
    // 못 넘기면 줄이 제자리로 돌아간다(`js/request-swipe.js`).
    //
    // 되돌릴 수 없다는 사정 자체는 그대로다 — 삭제는 서버가 줄을 통째로
    // 지운다. 그래서 **지우는 길은 시스템관리자에게만** 열고(`CanDelete`)
    // 「대기」인 줄에만 붙인다. 댓글과 처리 기록이 딸린 건은 아예 밀리지 않는다.
    //
    // [목록은 **그 줄만** 고쳐 쓴다 — 다시 읽지 않는다]
    //
    // `ReloadAsync` 를 부르면 조건은 그대로지만 **꺼내 둔 줄 수가 한 쪽으로
    // 되감긴다**(`_take = PageRows`). 「더보기」를 네 번 눌러 찾아 놓은 자리에서
    // 한 건을 접수한 사람이 처음 스물다섯 줄로 돌아가는 것이다 — 그 화면에서
    // 다음 건을 또 밀 수가 없다.

    /// <summary>
    /// 밀어서 접수할 수 있는가 — <b>담당자</b>이고 이 화면에 수정 권한이 있는가.
    /// </summary>
    /// <remarks>
    /// 상세 화면의 「접수」 단추와 <b>같은 잣대</b>다
    /// (<c>RequestDetail.CanHandle</c>). 거기서 <c>IsLinked</c> 가 아니라
    /// <c>IsAdmin</c> 을 보는 까닭도 그대로다 — 포털 역할로만 담당자인 사람의
    /// <c>admin</c> 줄은 <b>서버가 접수하는 그 순간 만든다.</b>
    /// </remarks>
    private bool CanAccept => Context.IsAdmin && Can(MenuAction.Update);

    /// <summary>
    /// 밀어서 지울 수 있는가 — <b>시스템관리자만</b>.
    /// </summary>
    /// <remarks>
    /// 상세 화면의 「삭제」 단추와 같다(<c>Context.IsSystemAdmin</c>).
    /// 거기도 메뉴 권한을 따로 보지 않는다 — 지우는 길을 가르는 것이
    /// 역할 하나뿐이어야 두 화면이 어긋나지 않는다.
    /// </remarks>
    private bool CanDelete => Context.IsSystemAdmin;

    /// <summary>
    /// 오른쪽으로 민 줄을 <b>접수</b>한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>상태만 보낸다.</b> 접수자는 서버가 지금 부른 사람으로 정한다 —
    /// 화면이 아는 번호(<see cref="HelpDeskContext.HelpdeskUserId"/>)는 담당자로
    /// 이어 둔 계정일 때만 <c>admin.id</c> 라, 그대로 실어 보내면 <b>번호가
    /// 겹치는 남이 접수자로 박힌다</b>(<c>RequestDetail.ChangeStatusAsync</c> 와
    /// 같은 사정이다).
    /// </para>
    /// <para>
    /// [접수한 줄은 **목록에 남는다**]
    /// </para>
    /// <para>
    /// 상태가 「진행」으로 바뀌므로 조건이 「대기」로 좁혀져 있으면 이 줄은
    /// 엄밀히는 더 이상 조건에 맞지 않는다. 그래도 걷지 않는다 — 걷으면
    /// <b>접수한 줄과 지운 줄이 화면에서 똑같이 사라져</b> 어느 쪽을 민
    /// 것인지 알 수가 없다. 딱지가 「대기 → 진행」으로 바뀌고 「담당」에 이름이
    /// 박히는 것이 <b>무슨 일이 일어났는지를 말하는 유일한 자리</b>다.
    /// 조건대로 다시 맞추는 것은 「조회」가 한다.
    /// </para>
    /// </remarks>
    private async Task<bool> AcceptSwipedAsync(ImprovementRequest r)
    {
        if (!CanAccept || r.Status is not "Pending")
        {
            return false;
        }

        ImprovementRequest? saved = null;

        var done = await RunAsync(
            async () => saved = await Api.PutAsync<ImprovementRequest>(
                $"requests/accept/{r.Id}", new { status = "InProgress" }),
            "접수했습니다.", "접수하지 못했습니다");

        if (!done)
        {
            return false;
        }

        // **서버가 적어 준 이름을 지운다.** `StatusName` 이 남아 있으면 딱지가
        // 그것을 먼저 쓰므로(`RequestRowText.Status`) 상태를 바꿔도 「대기」가
        // 그대로 떠 있다.
        r.Status = "InProgress";
        r.StatusName = null;

        // 접수자는 **서버가 정한 번호**로 받는다(`PUT` 의 응답). 그 응답에는
        // 담당자 줄이 딸려 오지 않으므로(엔티티를 그대로 돌려준다) 이름은
        // 담당자 고르개가 아는 것에서 푼다.
        if (saved?.AdminId is { } adminId)
        {
            r.AdminId = adminId;
            r.Admin = AssigneeOf(adminId);
        }

        StateHasChanged();
        return true;
    }

    /// <summary>
    /// 접수자 번호를 <b>이름</b>으로. 담당자 고르개가 아는 이름을 그대로 쓴다.
    /// </summary>
    /// <remarks>
    /// 고르개에 없는 번호일 수 있다 — 포털 역할로만 담당자인 사람의
    /// <c>admin</c> 줄은 <b>방금 서버가 만들었다</b>(<c>IAssigneeProvisioner</c>)
    /// 그래서 이 화면이 들어설 때 받아 둔 목록에는 없다. 그때는 <b>내 이름</b>이
    /// 맞다 — 접수자를 정한 것이 「지금 부른 사람」이기 때문이다.
    /// </remarks>
    private Admin AssigneeOf(int adminId) => new()
    {
        Id = adminId,
        UserName = Context.AdminOptions
            .FirstOrDefault(o => o.Value == adminId.ToString(CultureInfo.InvariantCulture))?.Label
            ?? Context.Identity?.UserName
            ?? "담당자",
    };

    /// <summary>
    /// 왼쪽으로 민 줄을 <b>지운다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>되돌릴 수 없다.</b> 서버는 상태를 <c>Delete</c> 로 바꾸는 것이 아니라
    /// 줄을 통째로 지우고 딸린 그림 폴더까지 치운다
    /// (<c>DELETE requests/{id}</c>). <b>묻지 않고 바로 지우므로</b> 막는 것은
    /// 앞의 세 가지뿐이다 — 시스템관리자일 것 · 「대기」인 줄일 것 · 문턱을
    /// 넘겨 밀 것. 지웠다는 말은 처리 뒤에 알림 줄로 뜬다.
    /// </para>
    /// <para>
    /// <b>전체 건수도 함께 줄인다.</b> 안 줄이면 「더보기」에 적히는 남은 수가
    /// (<see cref="Rest"/>) 서버가 말한 옛 숫자 그대로라, 다 깔고 나서도
    /// 누를 수 있는 단추가 남아 「더 읽을 요청이 없습니다」만 되풀이한다.
    /// </para>
    /// <para>
    /// <b>꺼내 둔 줄 수(<see cref="_take"/>)는 건드리지 않는다.</b> 그대로 두면
    /// 지운 줄의 자리에 아래 줄이 하나 올라와 목록 길이가 유지된다 — 줄이는
    /// 쪽을 고르면 본 적 없는 줄이 밀려 내려가 안 보이게 된다.
    /// </para>
    /// </remarks>
    private async Task<bool> DeleteSwipedAsync(ImprovementRequest r)
    {
        if (!CanDelete || r.Status is not "Pending")
        {
            return false;
        }

        var done = await RunAsync(
            () => Api.DeleteAsync($"requests/{r.Id}"),
            "삭제했습니다.", "삭제하지 못했습니다");

        if (!done)
        {
            return false;
        }

        _rows = [.. _rows.Where(x => x.Id != r.Id)];
        _total = Math.Max(_rows.Count, _total - 1);

        // 보고 있던 줄이 사라졌다. 안 비우면 떠날 때 맡기는 짐의
        // `SelectedId` 가 없는 요청을 가리킨 채로 남는다.
        if (_selected?.Id == r.Id)
        {
            _selected = null;
        }

        StateHasChanged();
        return true;
    }
}
