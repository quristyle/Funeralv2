using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class TransactionNewPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>거래처 상세에서 넘어올 때 채울 거래처.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "companyId")]
    public long? CompanyId { get; set; }

    private TransactionDraft _draft = new();
    private long? _prefilled;

    protected override async Task OnParametersSetAsync()
    {
        if (CompanyId is null || CompanyId == _prefilled)
        {
            return;
        }

        _prefilled = CompanyId;

        // 거래처 정보만 필요하지만 한 건 조회 통로가 상세뿐이다. 기간은 가장 짧게 준다 —
        // 통계는 여기서 쓰지 않는다.
        await LoadOneAsync(
            () => Api.GetCompanyAsync(CompanyId.Value, 30),
            d =>
            {
                if (d is not null)
                {
                    _draft.Company = CompanyPick.From(d.Company);
                }
            },
            "거래처를 찾지 못했습니다. 검색해서 고르십시오.",
            "거래처를 읽지 못했습니다");
    }

    private void Reset()
    {
        // 거래처는 남긴다 — 같은 곳과 여러 건을 잇달아 올리는 일이 흔하다.
        _draft = new TransactionDraft { Company = _draft.Company };
    }

    private async Task SaveAsync()
    {
        if (_draft.Problem() is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        MyTransaction? saved = null;
        if (!await RunAsync(async () => saved = await Api.CreateTransactionAsync(_draft.ToRequest()),
                "거래를 등록했습니다.", "거래를 등록하지 못했습니다")
            || saved is null)
        {
            return;
        }

        if (saved.ReviewStatus == "FLAGGED")
        {
            Say("등록했지만 검토 대상으로 표시되었습니다 — 운송일이 오늘보다 뒤이거나 "
                + "같은 날 같은 거래처로 등록한 건이 많을 때 그렇게 됩니다.", NoticeTone.Warning);
        }

        Navigation.NavigateTo($"/cargotrust/transactions/{saved.TransactionId}");
    }
}
