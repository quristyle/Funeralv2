using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class UnpaidPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>거래처 상세의 「미지급 전체 보기」에서 넘어올 때.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "companyId")]
    public long? CompanyIdQuery { get; set; }

    private string ConditionSummary => SchSummary.Of(
        string.IsNullOrWhiteSpace(_keyword) ? null : _keyword,
        SchSummary.Period(_from, _to),
        _mine ? "내가 등록한 것만" : null,
        _companyId is null ? null : "거래처 한 곳");

    private string? _keyword;
    private DateTime? _from;
    private DateTime? _to;
    private bool _mine;

    /// <summary>
    /// 주소로 넘어온 거래처. 조회 칸에 없으므로 <see cref="ClearCompanyAsync"/>(표 머리의
    /// 「전체 거래처 보기」)나 「초기화」로 푼다 — 「조회」를 눌러도 그대로 남는다.
    /// </summary>
    private long? _companyId;

    private UnpaidList? _data;
    private IReadOnlyList<UnpaidTransaction> _rows = [];

    private PaymentPopup? _payment;
    private ReportPopup? _report;

    protected override Task OnInitializedAsync()
    {
        _companyId = CompanyIdQuery;
        return ReloadAsync();
    }

    /// <summary>거래처 조건만 푼다. 나머지 조건은 그대로 둔다.</summary>
    private Task ClearCompanyAsync()
    {
        _companyId = null;
        return ReloadAsync();
    }

    private Task ResetAsync()
    {
        _keyword = null;
        _from = null;
        _to = null;
        _mine = false;
        _companyId = null;
        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _data = await Api.GetUnpaidTransactionsAsync(
            _companyId,
            _keyword,
            _from is null ? null : DateOnly.FromDateTime(_from.Value),
            _to is null ? null : DateOnly.FromDateTime(_to.Value),
            _mine);

        _rows = _data?.Items ?? [];

        // 빈 목록은 표 안의 안내가 말한다 — 머리 타일이 0 으로 남아야 조건을 되짚을 수 있다.
        return -1;
    }, failMessage: "미지급 거래를 읽지 못했습니다");
}
