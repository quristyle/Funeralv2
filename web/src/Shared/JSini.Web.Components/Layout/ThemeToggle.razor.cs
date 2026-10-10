using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class ThemeToggle
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private ThemeDrawer Drawer { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;

    private void ToggleAsync()
    {
        // 펴 둔 사용자 판을 먼저 접는다.
        UserDrawer.Close();
        Drawer.Toggle();
    }
}
