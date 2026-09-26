using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class CompanyPicker
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public CompanyPick? Value { get; set; }

    [Parameter] public EventCallback<CompanyPick?> ValueChanged { get; set; }

    /// <summary>못 찾으면 그 자리에서 등록하게 할지. 수정 팝업 안에서는 끈다 — 팝업 위에 팝업이 겹친다.</summary>
    [Parameter] public bool AllowCreate { get; set; } = true;

    private string? _q;
    private bool _searched;
    private IReadOnlyList<CompanySummary> _results = [];
    private CompanyCreatePopup? _create;

    private async Task OnKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_q))
        {
            Say("회사명이나 사업자번호를 넣으십시오.", NoticeTone.Warning);
            return;
        }

        IReadOnlyList<CompanySummary> found = [];
        if (await RunAsync(async () => found = await Api.SearchCompaniesAsync(_q.Trim()),
                okMessage: string.Empty, failMessage: "거래처를 찾지 못했습니다"))
        {
            _results = found;
            _searched = true;
        }
    }

    private async Task PickAsync(CompanyPick pick)
    {
        _results = [];
        _searched = false;
        await ValueChanged.InvokeAsync(pick);
    }

    private async Task ClearAsync()
    {
        _q = null;
        await ValueChanged.InvokeAsync(null);
    }
}
