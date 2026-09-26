using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class ReportPopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public EventCallback<ReportInfo> OnSaved { get; set; }

    private bool _visible;
    private string? _summary;
    private string? _reason;
    private string? _content;
    private ReportRequest _request = new();

    /// <param name="targetType"><c>COMPANY</c> · <c>TRANSACTION</c> · <c>REVIEW</c></param>
    /// <param name="targetId">대상 번호</param>
    /// <param name="summary">창 위에 적을 대상 요약</param>
    public void Open(string targetType, long targetId, string summary)
    {
        _request = new ReportRequest { TargetType = targetType, TargetId = targetId };
        _summary = summary;
        _reason = null;
        _content = null;
        _visible = true;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_reason))
        {
            Say("사유를 고르십시오.", NoticeTone.Warning);
            return;
        }

        _request.Reason = _reason;
        _request.Content = string.IsNullOrWhiteSpace(_content) ? null : _content.Trim();

        ReportInfo? saved = null;
        if (await RunAsync(async () => saved = await Api.CreateReportAsync(_request),
                "신고를 접수했습니다. 처리 상태는 「내 신고」에서 볼 수 있습니다.", "신고하지 못했습니다"))
        {
            _visible = false;
            if (saved is not null)
            {
                await OnSaved.InvokeAsync(saved);
            }
        }
    }
}
