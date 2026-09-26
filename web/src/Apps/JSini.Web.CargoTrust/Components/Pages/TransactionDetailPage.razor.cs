using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class TransactionDetailPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public long TransactionId { get; set; }

    private long _loadedFor;
    private TransactionDetail? _detail;

    private PaymentPopup? _payment;
    private ReviewPopup? _review;
    private TransactionEditPopup? _edit;
    private ReportPopup? _report;
    private ConfirmDialog? _confirm;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == TransactionId)
        {
            return;
        }

        _loadedFor = TransactionId;
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadOneAsync(
        () => Api.GetTransactionAsync(TransactionId),
        d => _detail = d,
        "거래를 찾지 못했습니다.",
        "거래를 읽지 못했습니다");

    private static string Summary(MyTransaction t) =>
        $"{t.CompanyName} · {CargoCodes.Day(t.TransportDate)} · {CargoCodes.Won(t.Amount)}";

    private async Task DeleteAsync(MyTransaction t)
    {
        var ok = await _confirm!.AskAsync(
            $"{Summary(t)} 거래를 지웁니다.\n지운 거래는 거래처 통계에서도 빠집니다.");

        if (!ok)
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteTransactionAsync(t.TransactionId), "거래를 지웠습니다.", "거래를 지우지 못했습니다"))
        {
            Navigation.NavigateTo("/cargotrust/transactions");
        }
    }
}
