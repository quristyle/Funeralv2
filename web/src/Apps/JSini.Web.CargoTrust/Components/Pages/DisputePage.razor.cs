using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class DisputePage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private MeInfo? _me;
    private IReadOnlyList<PublicTransaction> _transactions = [];
    private IReadOnlyList<DisputeInfo> _disputes = [];

    private bool _open;
    private PublicTransaction? _target;
    private string? _reason;
    private string? _content;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _me = await Api.GetMeAsync();

        // 운송사가 아니면 목록을 부르지 않는다 — 403 토스트만 남고 할 일은 안내 줄이 말한다.
        if (_me is not { CanDispute: true })
        {
            return -1;
        }

        var transactions = Api.GetCompanyTransactionsAsync();
        var disputes = Api.GetMyDisputesAsync();
        _transactions = await transactions;
        _disputes = await disputes;
        return _transactions.Count;
    }, "우리 회사에 관해 등록된 거래가 없습니다.", "이의제기 화면을 읽지 못했습니다");

    private void Open(PublicTransaction t)
    {
        _target = t;
        _reason = null;
        _content = null;
        _open = true;
    }

    private async Task SubmitAsync()
    {
        if (_target is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_reason))
        {
            Say("이의 사유를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var request = new DisputeRequest
        {
            TransactionId = _target.TransactionId,
            Reason = _reason,
            Content = string.IsNullOrWhiteSpace(_content) ? null : _content.Trim(),
        };

        if (await RunAsync(() => Api.CreateDisputeAsync(request),
                "이의제기를 접수했습니다. 관리자가 검토합니다.", "이의제기를 접수하지 못했습니다"))
        {
            _open = false;
            _disputes = await Api.GetMyDisputesAsync();
        }
    }
}
