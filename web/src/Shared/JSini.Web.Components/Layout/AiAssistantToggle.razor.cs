using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class AiAssistantToggle
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private HeaderIconManager HeaderIcons { get; set; } = default!;

    private const string IconKey = "ai_assistant";
    private const string RouteKey = "admin.ai.assistant";
    private static readonly string[] Paths = ["/admin/ai/assistant"];

    /// <summary>권한이 있을 때만 메뉴를 돌려준다 — 슈퍼관리자·임원 전용 화면이다.</summary>
    private MenuNode? AssistantMenu =>
        MenuLookup.Find(Menus.AllMenus, RouteKey, Paths) is { } node
        && Permissions.CanView(node.Path)
            ? node
            : null;

    private void Open(MenuNode menu)
    {
        UserDrawer.Close();
        Navigation.NavigateTo(menu.LinkTarget);
    }

    protected override void OnInitialized()
    {
        Menus.MenusChanged += OnChanged;
        HeaderIcons.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Menus.MenusChanged -= OnChanged;
        HeaderIcons.Changed -= OnChanged;
    }
}
