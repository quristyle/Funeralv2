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

    // ── 현황판이 싣고 오는 조건 ──
    // 타일이 센 것과 같은 목록을 열려고 주소로 받는다. 주소 이름은 조회 칸과 일대일이다.

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

    /// <summary>「처리 중인 것만」. 타일은 항상 false 로 보낸다 — 화면 기본값이 켜짐이라 안 끄면 완료·반려 타일이 빈 목록이 된다.</summary>
    [SupplyParameterFromQuery(Name = "open")] public string? OpenQuery { get; set; }

    /// <summary>담당자.</summary>
    [SupplyParameterFromQuery(Name = "admin")] public string? AdminQuery { get; set; }

    /// <summary>이 화면이 맡겨 두는 짐의 이름. 표 모습은 CommGrd 가 같은 이름으로 따로 맡는다(StateKey).</summary>
    internal const string StateKey = "helpdesk/request/manage";

    /// <summary>떠날 때 챙기는 조건과 그 조건으로 읽어 둔 줄. 돌아오면 다시 묻지 않는다.
    /// 줄은 번호(SelectedId)로, 더보기 줄 수(Take)·묶음 수(Batches)도 함께 든다.</summary>
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

    /// <summary>고른 상태를 한 줄로. 아무것도 안 골랐으면 「전체」(하나짜리 NameOf 를 여럿으로 옮긴 것).</summary>
    private string StatusSummary => _statuses.Count == 0
        ? SchSummary.Any
        : string.Join(", ", _statuses.Select(RequestRowText.Status));

    /// <summary>기간을 한 줄로. 기준(접수·완료)을 앞에 붙인다 — 날짜만이면 접수인지 완료인지 알 수 없다.</summary>
    private string? PeriodSummary => SchSummary.Period(_from, _to) is { } period
        ? $"{BasisText} {period}"
        : null;

    private IReadOnlyList<ImprovementRequest> _rows = [];
    private int _total;

    // ── 「더보기」와 「더 읽기」 ──
    // 남은 수는 받아 둔 것이 아니라 서버 전체(_total)로 센다 — 묶음으로 세면 300줄째에서 단추가 사라진다.

    /// <summary>한 쪽에 깔리는 줄 수. 페이저와 더보기가 같은 값을 써야 기기마다 끊기는 자리가 달라지지 않는다.</summary>
    private const int PageRows = 25;

    /// <summary>서버에서 한 번에 받는 줄 수. PageRows 의 배수여야 한다 — 아니면 묶음 끝 더보기가 한 쪽보다 적게 깔린다.</summary>
    private const int BatchRows = 300;

    /// <summary>휴대폰인가. OnPhoneChanged 가 채운다 — Blazor Server 는 브라우저 폭을 모른다.</summary>
    private bool _isPhone;

    /// <summary>휴대폰에서 꺼내 깔아 둔 줄 수. 더보기가 한 쪽씩 늘린다. 데스크톱에서는 쓰지 않는다.</summary>
    private int _take = PageRows;

    /// <summary>서버에서 받아 온 묶음 수. 0 이면 한 번도 안 읽은 것이고, 다음 요청 쪽 번호는 이 값 + 1 이다.</summary>
    private int _batches;

    /// <summary>표에 넣을 줄. 휴대폰은 꺼낸 만큼만, 데스크톱은 읽어 둔 것 전부(자르기는 페이저가 한다).</summary>
    private IReadOnlyList<ImprovementRequest> Shown =>
        _isPhone && _take < _rows.Count ? [.. _rows.Take(_take)] : _rows;

    /// <summary>아직 안 깔린 줄 수. 더보기 단추에 적는다. 서버 전체(_total)에서 센다.
    /// 받아 둔 묶음으로 세면 300줄째에서 0 이 되어 단추가 사라진다.</summary>
    private int Rest => Math.Max(0, _total - _take);

    /// <summary>서버에 아직 안 받아 온 줄이 남았는가.</summary>
    private bool HasUnread => _rows.Count < _total;

    /// <summary>다음 「더 읽기」가 받아 올 줄 수. 데스크톱 단추에 적는다.</summary>
    private int NextBatch => Math.Min(BatchRows, Math.Max(0, _total - _rows.Count));

    /// <summary>한 쪽 더 깐다. 모자라면 그 자리에서 다음 묶음을 받는다.
    /// 받기가 실패하면 _take 가 멈춰 단추가 남는다(실패 표시는 LoadAsync 가 한다).</summary>
    private async Task ShowMoreAsync()
    {
        if (_take + PageRows > _rows.Count && HasUnread)
        {
            await ReadMoreAsync();
        }

        _take = Math.Min(_take + PageRows, Math.Max(_rows.Count, PageRows));
    }

    /// <summary>휴대폰 경계(≤767px)를 넘었다. 회로가 붙은 뒤 처음 한 번 울리므로 여기서 _take 를 되돌리면 맡겨 둔 값이 덮인다.</summary>
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

    /// <summary>조건이 이미 정해졌는가(주소로 실렸거나 맡겨 둔 짐에서 되찾았나). 참이면 「내 요청」 기본값을 얹지 않는다.
    /// _restored 로는 가를 수 없다 — 그 값은 줄이 있을 때만 참이라 조건만 되찾은 길에서는 거짓이다.</summary>
    private bool _seeded;

    /// <summary>요청자 칸을 잠갔는가 — 고객이면 참(관리자가 아닌 모든 사람).
    /// 고객 번호 유무로 가르면 번호 없는 고객까지 칸이 열려 같은 회사 전체가 보인다.</summary>
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

    /// <summary>처리 중으로 볼 상태들. 「처리 중인 것만」이 쓴다. 서버에는 | 로 잇는다(쉼표가 아니다).</summary>
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

    /// <summary>아무도 이보다 먼저 올린 요청은 없다. between 은 양쪽 값이 필요해 한쪽만 고른 기간의 반대쪽을 채운다.</summary>
    private static readonly DateTime Dawn = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc cref="Dawn"/>
    private static readonly DateTime Dusk = new(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>쓰던 모습을 되찾는다. 첫 await 앞이어야 한다 — 미루면 빈 표가 한 번 그려져 DxGrid 가 빈 모습으로 자리를 잡는다.
    /// 주소 조건이 있으면 맡겨 둔 짐보다 먼저 푼다(아니면 누른 타일 조건이 지난번 조건에 덮인다).</summary>
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

        // 꺼내 둔 줄 수도 되찾는다. 한 쪽 아래로는 내려가지 않는다(프리렌더의 0 을 받으면 휴대폰 목록이 빈 채로 열린다).
        _take = Math.Max(kept.Take, PageRows);

        // 받아 둔 묶음 수도 되찾는다 — 안 되찾으면 더 읽기가 쪽 1 을 다시 받아 이미 든 줄을 또 받는다.
        _batches = kept.Batches;

        // 빈손으로 돌아왔으면 조건만 되찾고 다시 묻는다 — 빈 것을 다 읽은 것으로 보면 빈 표가 영영 남는다.
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

        // 고객이면 요청자를 나로 고정하고 「처리 중인 것만」을 끈다. 가르는 것은 관리자 여부(IsAdmin) 하나다.
        // 요청자 칸은 잠겨 있어 주소·돌아온 길에서도 언제나 나이고, 「처리 중인 것만」은 기본값만 바꾼다.
        if (Context.CustomerId is { } me)
        {
            _requesterId = me.ToString(CultureInfo.InvariantCulture);

            if (!_seeded)
            {
                _onlyOpen = false;
            }
        }

        // 돌아온 길이면 다시 묻지 않는다 — 300건을 다시 읽으면 조건을 다시 거는 것보다 느리고 표가 맨 위로 돌아간다.
        // 오래된 것은 사람이 「조회」나 「다시 읽기」로 정한다.
        if (_restored)
        {
            if (_selected is not null)
            {
                try
                {
                    _selected.Comments = await Api.GetListAsync<ImprovementComment>($"requests/{_selected.Id}/comments");
                }
                catch
                {
                    // 실패해도 표를 그리는 데는 지장이 없으므로 무시한다.
                }
            }
            return;
        }

        await ReloadAsync();
    }

    /// <summary>떠나면서 쓰던 모습을 맡긴다. 떠날 때 한 번만 담는다 — 읽어 둔 줄이 커서 글자마다 담으면 기록이 는다.
    /// 프리렌더에서도 불리며 그때는 빈 것을 맡긴다.</summary>
    public void Dispose() =>
        Screen.Set(StateKey, new Kept(
            _companyId, _requesterId, _statuses, _adminId, _keyword, _onlyOpen,
            _basis, _from, _to,
            _rows, _total, _selected?.Id, _take, _batches));

    // ── 주소로 들어온 조건 ──────────────────────────────────

    /// <summary>주소에 실린 조건을 조회 조건으로 푼다. 하나라도 실렸으면 참을 돌려주고 맡겨 둔 짐은 풀지 않는다.
    /// 모르는 상태 글자는 버린다 — 고르개에 없는 글자를 실으면 빈칸인데 서버는 그 글자로 걸러 빈 목록을 준다.</summary>
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

        // 「처리 중인 것만」은 적어 보냈을 때만 건드린다(안 적었으면 기본값이 그대로다).
        if (!string.IsNullOrWhiteSpace(OpenQuery))
        {
            _onlyOpen = !string.Equals(OpenQuery, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(OpenQuery, "0", StringComparison.Ordinal);
        }

        return true;
    }

    /// <summary>Consultation|Negotiation 을 고르개 값으로 푼다. 쉼표로 이어 보내도 받는다.</summary>
    private static IReadOnlyList<string> ParseStatuses(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => StatusOptions
                    .FirstOrDefault(o => string.Equals(o.Value, token, StringComparison.OrdinalIgnoreCase))?.Value)
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)];

    /// <summary>yyyy-MM-dd 하루. 못 읽으면 null(조건이 없는 것과 같다).</summary>
    private static DateTime? ParseDay(string? raw) =>
        DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day)
            ? day
            : null;

    private Task ReloadAsync()
    {
        // 조건이 바뀌면 더보기도 처음 한 쪽으로 되돌린다 — 안 그러면 좁힌 결과를 한 번에 통째로 받는다.
        _take = PageRows;

        // 받아 둔 묶음도 버린다 — 안 버리면 다음 더 읽기가 새 조건의 쪽 넷을 달라고 한다.
        _batches = 0;

        // 「내 것」을 가려낼 수 없으면 묻지 않는다 — 고객 줄이 없는 고객이 조건 없이 부르면 같은 회사 남의 요청이 나온다.
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

            // 고른 줄은 번호로 다시 잡는다 — 방금 받은 것은 같은 요청이라도 다른 객체라 그대로 두면 강조가 붙지 않는다.
            _selected = _selected is { } was ? _rows.FirstOrDefault(r => r.Id == was.Id) : null;

            return _rows.Count;
        }, "조건에 맞는 요청이 없습니다.", "요청 목록을 읽지 못했습니다");
    }

    /// <summary>다음 묶음을 이어 받는다. 조건은 그대로 두고 쪽 번호만 넘긴다.
    /// 두 번 부르는 사이 창이 밀리므로 이미 읽은 번호는 걸러 낸다. 긴급은 묶음 안에서만 앞으로 온다(전체를 다시 줄 세우면 보고 지나간 자리로 끼어든다).</summary>
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
            // 서버 전체보다 실제가 적으면 들고 있는 만큼으로 고친다 — 아니면 눌러도 아무 일 없는 더보기가 남는다.
            _total = _rows.Count;
            Say("더 읽을 요청이 없습니다.");
        }

        return _rows.Count;
    }, "더 읽을 요청이 없습니다.", "요청을 더 읽지 못했습니다");

    /// <summary>긴급을 맨 앞으로, 그다음 최근 순. 서버가 isEmergency 로는 정렬하지 못해(EF 질의 400) 여기서 한다.</summary>
    private static IReadOnlyList<ImprovementRequest> Ordered(IEnumerable<ImprovementRequest> items) =>
    [
        .. items
            .OrderByDescending(r => r.IsEmergency == true)
            .ThenByDescending(r => r.CreatedAt)
    ];

    /// <summary>지금 조건을 서버가 읽는 모양으로 옮긴다. 쪽 번호만 다르고 나머지는 늘 같아야 이어 붙인 줄이 같은 목록의 뒷부분이 된다.</summary>
    private Dictionary<string, object?> BuildQuery(int page)
    {
        var query = new Dictionary<string, object?>
        {
            ["page"] = page,
            ["pageSize"] = BatchRows,
            ["remove"] = "description,content",
            // 최근 순. isEmergency 로는 정렬할 수 없다 — 서버가 그 칸으로 정렬하면 EF 가 400 을 낸다. 긴급은 받은 뒤 여기서 앞으로 올린다.
            ["sorts"] = new[]
            {
                new { field = "createdAt", dir = "desc" },
            },
        };

        if (!string.IsNullOrWhiteSpace(_keyword))
        {
            // 제목은 _or_ 가 아니라 _like 로 건다 — 서버 OR 자리에 넣으면 완료 기준 기간과 한 묶음으로 OR 되어 엉뚱해진다.
            query["title_like"] = _keyword.Trim();
        }

        if (_statuses.Count > 0)
        {
            // 여러 값의 구분자는 | 다(쉼표는 서버가 값 하나로 읽어 400).
            query["status_in"] = string.Join("|", _statuses);
        }
        else if (_onlyOpen)
        {
            // 상태를 따로 고르지 않았을 때만 「처리 중」으로 좁힌다 — 둘을 함께 걸면 고른 상태가 조용히 무시된다.
            query["status_in"] = string.Join("|", OpenStatuses);
        }
        else
        {
            // 지운 요청(Delete)은 뺀다 — 현황판이 그것을 빼고 세므로 「전체」 타일 숫자를 맞추려는 자리다.
            query["status_nin"] = "Delete";
        }

        ApplyPeriod(query);

        // 고객은 제 것만 본다 — 서버는 권한으로 거르지 않으므로 화면이 조건을 빠뜨리면 그대로 샌다.
        // 맡겨 둔 짐의 번호도 여기서 덮인다. 번호가 없는 고객은 위에서 돌려보낸다.
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

        // 잘렸다는 말은 여기서 하지 않는다 — 남은 것이 있다는 말과 받는 길은 목록 아래 단추(Rest · ReadMoreAsync)가 든다.
        return query;
    }

    /// <summary>고른 기간을 조회 조건으로 얹는다. 날짜는 한국 시각 하루로 고르고 서버에는 UTC 순간으로 보낸다.
    /// 날짜 글자를 그대로 보내면 UTC 0시로 읽혀 한국 오전이 전날로 밀린다. 완료 기준은 두 칸 중 하나라도 기간에 들면 끝난 것이라 OR 로 묶는다.</summary>
    private void ApplyPeriod(IDictionary<string, object?> query)
    {
        var start = StartUtc(_from);

        // 끝 날짜는 그 날을 포함한다 — 다음 날 0시 직전까지다.
        var end = StartUtc(_to?.AddDays(1));

        if (start is null && end is null)
        {
            return;
        }

        if (ByResolved)
        {
            // between 은 양쪽 값이 다 필요하다(한쪽만 주면 조건이 버려져 기간을 안 건 것처럼 보인다). 안 고른 쪽은 끝까지 연다.
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

    /// <summary>서버가 읽는 시각 글자. 끝의 Z 가 있어야 한다 — 없으면 시간대 없는 값이라 timestamptz 칸에 못 써서 질의가 터진다.</summary>
    private static string Iso(DateTime at) =>
        at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private Task OnStatusesChangedAsync(IEnumerable<string> picked)
    {
        _statuses = [.. picked];
        return ReloadAsync();
    }

    /// <summary>줄을 눌렀다 — 상세로 간다. 고른 줄을 여기서도 적어 둔다(휴대폰 카드 목록에는 고르기가 없어 SelectedId 가 비고 돌아와 줄을 잃는다).</summary>
    private void OnRowClick(ImprovementRequest r)
    {
        _selected = r;
        Navigation.NavigateTo($"/helpdesk/request/detail/{r.Id}");
    }

    // ── 휴대폰에서 밀어서 접수 · 삭제 ──
    // 오른쪽은 접수(PUT requests/accept/{id}), 왼쪽은 삭제(DELETE requests/{id}). 「대기」인 줄만 민다.

    // 묻지 않고 바로 처리한다. 문턱은 줄 폭의 1/4 로 두어 비낀 손가락을 막는다(js/request-swipe.js).

    // 삭제는 되돌릴 수 없어 시스템관리자에게만 열고(CanDelete), 목록은 그 줄만 고쳐 쓴다 — ReloadAsync 는 꺼낸 줄 수를 되감는다.

    /// <summary>밀어서 접수할 수 있는가 — 담당자이고 이 화면에 수정 권한이 있는가. 상세의 접수 단추와 같은 잣대다.
    /// IsLinked 가 아니라 IsAdmin 을 보는 까닭: 포털 역할로만 담당자인 사람의 admin 줄은 서버가 접수하는 순간 만들어진다.</summary>
    private bool CanAccept => Context.IsAdmin && Can(MenuAction.Update);

    /// <summary>밀어서 지울 수 있는가 — 시스템관리자만. 상세의 삭제 단추와 같은 역할 하나로 가른다.</summary>
    private bool CanDelete => Context.IsSystemAdmin;

    /// <summary>오른쪽으로 민 줄을 접수한다. 상태만 보내고 접수자는 서버가 정한다(화면의 HelpdeskUserId 는 담당자 계정일 때만 admin.id 라 실으면 남이 박힌다).
    /// 접수한 줄은 목록에 남긴다 — 걷으면 접수와 삭제가 똑같이 사라져 무슨 일이 있었는지 알 수 없다.</summary>
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

        // 서버가 적어 준 상태 이름을 지운다 — 남아 있으면 딱지가 그것을 먼저 써서 상태를 바꿔도 「대기」가 떠 있다.
        r.Status = "InProgress";
        r.StatusName = null;

        // 접수자 이름은 담당자 고르개가 아는 것에서 푼다 — PUT 응답에는 담당자 줄이 딸려 오지 않는다.
        if (saved?.AdminId is { } adminId)
        {
            r.AdminId = adminId;
            r.Admin = AssigneeOf(adminId);
        }

        StateHasChanged();
        return true;
    }

    /// <summary>접수자 번호를 이름으로. 고르개에 없으면(방금 서버가 만든 admin 줄) 내 이름이 맞다 — 접수자를 정한 것이 지금 부른 사람이기 때문이다.</summary>
    private Admin AssigneeOf(int adminId) => new()
    {
        Id = adminId,
        UserName = Context.AdminOptions
            .FirstOrDefault(o => o.Value == adminId.ToString(CultureInfo.InvariantCulture))?.Label
            ?? Context.Identity?.UserName
            ?? "담당자",
    };

    /// <summary>왼쪽으로 민 줄을 지운다. 되돌릴 수 없다 — 서버가 줄을 통째로 지우고 그림 폴더까지 치운다.
    /// 전체 건수도 함께 줄인다(안 줄이면 더보기 남은 수가 옛 숫자라 누를 수 없는 단추가 남는다). 꺼내 둔 줄 수(_take)는 그대로 둔다 — 줄이면 본 적 없는 줄이 밀려 내려간다.</summary>
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

        // 보고 있던 줄이 사라졌으니 비운다 — 안 비우면 맡기는 짐의 SelectedId 가 없는 요청을 가리킨다.
        if (_selected?.Id == r.Id)
        {
            _selected = null;
        }

        StateHasChanged();
        return true;
    }
}
