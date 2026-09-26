using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class AiChatDrawer
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;

    private bool _open;

    private void Toggle() => _open = !_open;

    protected override void OnInitialized() => Me.Changed += OnMeChanged;

    /// <summary>내 정보가 늦게 오면 그때 단추를 세운다.</summary>
    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Me.Changed -= OnMeChanged;
}
