using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class PaymentList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(SchSummary.Period(_from, _to));

    private IReadOnlyList<AdminPayment> _rows = [];

    private DateTime? _from = DateTime.Today.AddDays(-30);
    private DateTime? _to = DateTime.Today;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetPaymentsAsync(_from, _to);
        return _rows.Count;
    }, "기간 안에 결제 기록이 없습니다.", "결제 기록을 읽지 못했습니다");
}
