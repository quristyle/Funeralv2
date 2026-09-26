using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class PaymentsPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>받을 것이 남은 상태만. 「전체」는 그 넷 모두다.</summary>
    private static readonly IReadOnlyList<SchOption> Statuses =
    [
        new(null, "받을 것 전체"),
        new(CargoCodes.Scheduled, "예정"),
        new(CargoCodes.Partial, "일부 지급"),
        new(CargoCodes.Unpaid, "미지급"),
        new(CargoCodes.Dispute, "분쟁"),
    ];

    private string ConditionSummary =>
        SchSummary.NameOf(Statuses, o => o.Value, o => o.Text, _status, "받을 것 전체");

    private string? _status;
    private IReadOnlyList<MyTransaction> _rows = [];
    private PaymentPopup? _payment;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var rows = await Api.GetMyTransactionsAsync(_status, open: true);

        _rows = [.. rows
            .OrderByDescending(t => t.OverdueDays ?? -1)
            .ThenBy(t => t.ExpectedPaymentDate ?? DateOnly.MaxValue)];

        return _rows.Count;
    }, "아직 받지 않은 거래가 없습니다.", "거래를 읽지 못했습니다");
}
