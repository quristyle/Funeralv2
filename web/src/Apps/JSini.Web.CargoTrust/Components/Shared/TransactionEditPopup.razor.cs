using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class TransactionEditPopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public EventCallback<MyTransaction> OnSaved { get; set; }

    private bool _visible;
    private long _transactionId;
    private TransactionDraft? _draft;

    public void Open(MyTransaction transaction)
    {
        _transactionId = transaction.TransactionId;
        _draft = TransactionDraft.From(transaction);
        _visible = true;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (_draft is null)
        {
            return;
        }

        if (_draft.Problem() is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        MyTransaction? saved = null;
        if (await RunAsync(async () => saved = await Api.UpdateTransactionAsync(_transactionId, _draft.ToRequest()),
                "거래를 고쳤습니다.", "거래를 고치지 못했습니다"))
        {
            _visible = false;
            if (saved is not null)
            {
                await OnSaved.InvokeAsync(saved);
            }
        }
    }
}
