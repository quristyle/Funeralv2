using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class PaymentPopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>등록한 뒤 서버가 돌려준 거래(바뀐 상태가 담겨 있다).</summary>
    [Parameter] public EventCallback<MyTransaction> OnSaved { get; set; }

    private bool _visible;
    private PaymentTarget? _target;
    private PaymentDraft _draft = new();

    public void Open(PaymentTarget target)
    {
        _target = target;
        _draft = new PaymentDraft { PaidAmount = target.Outstanding };
        _visible = true;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (_target is null)
        {
            return;
        }

        if (_draft.Problem() is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        MyTransaction? saved = null;
        if (await RunAsync(async () => saved = await Api.RegisterPaymentAsync(_target.TransactionId, _draft.ToRequest()),
                okMessage: string.Empty, failMessage: "결제 결과를 등록하지 못했습니다"))
        {
            // 결과 상태는 서버가 정한 것을 말한다 — 미리 보여 준 짐작과 다를 수 있다.
            Say(saved is null
                ? "결제 결과를 등록했습니다."
                : $"결제 결과를 등록했습니다 — {CargoCodes.PaymentStatusName(saved.PaymentStatus)}");

            _visible = false;
            if (saved is not null)
            {
                await OnSaved.InvokeAsync(saved);
            }
        }
    }
}
