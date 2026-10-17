using System;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class ThemeToggle : IDisposable
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private ThemeDrawer Drawer { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private HeaderIconManager HeaderIcons { get; set; } = default!;

    private bool IsEnabled => HeaderIcons.IsIconEnabled("theme");

    protected override void OnInitialized()
    {
        HeaderIcons.Changed += OnHeaderIconsChanged;
    }

    private void OnHeaderIconsChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        HeaderIcons.Changed -= OnHeaderIconsChanged;
    }

    private void ToggleAsync()
    {
        // 펴 둔 사용자 판을 먼저 접는다.
        UserDrawer.Close();
        Drawer.Toggle();
    }
}
