using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class ReviewPopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public EventCallback<ReviewInfo> OnSaved { get; set; }

    private bool _visible;
    private bool _editing;
    private long _transactionId;
    private string? _summary;
    private string? _content;

    /// <param name="transactionId">후기를 붙일 거래</param>
    /// <param name="summary">창 위에 적을 거래 요약(거래처 · 운송일 · 운송료)</param>
    /// <param name="existing">이미 쓴 후기. 있으면 고치는 창이 된다.</param>
    public void Open(long transactionId, string summary, string? existing = null)
    {
        _transactionId = transactionId;
        _summary = summary;
        _content = existing;
        _editing = !string.IsNullOrWhiteSpace(existing);
        _visible = true;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_content))
        {
            Say("내용을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        ReviewInfo? saved = null;
        if (await RunAsync(async () => saved = await Api.SaveReviewAsync(_transactionId, _content.Trim()),
                "후기를 저장했습니다.", "후기를 저장하지 못했습니다"))
        {
            _visible = false;
            if (saved is not null)
            {
                await OnSaved.InvokeAsync(saved);
            }
        }
    }
}
