using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class ReceivablesPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private ReceivablesInfo? _data;
    private IReadOnlyList<ReceivableItem> _items = [];
    private PaymentPopup? _payment;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _data = await Api.GetReceivablesAsync();
        _items = [.. (_data?.Items ?? [])
            .OrderByDescending(r => r.OverdueDays ?? -1)
            .ThenBy(r => r.ExpectedPaymentDate ?? DateOnly.MaxValue)];

        // 비어 있으면 안내 줄이 말한다.
        return -1;
    }, failMessage: "미수금을 읽지 못했습니다");

    private static string DaysLeft(DateOnly due)
    {
        var left = due.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        return left <= 0 ? "오늘이 예정 지급일" : $"예정일까지 {left}일";
    }
}
