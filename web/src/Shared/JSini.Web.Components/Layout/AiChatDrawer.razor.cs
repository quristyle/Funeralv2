using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class AiChatDrawer
{
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AiChatReveal Reveal { get; set; } = default!;

    /// <summary>
    /// 휴대폰인가. 책상에서는 고정핀을 쓸 수 있지만 휴대폰에서는 핀을 안 그리고
    /// 화면을 옮길 때마다 판을 접는다(<see cref="OnLocationChanged"/>).
    /// </summary>
    [Parameter] public bool IsPhone { get; set; }

    private const string ChatRouteKey = "site.ai.chat";
    private static readonly string[] ChatPaths = ["/ai/chat", "/site/ai/chat"];

    private MenuNode? ChatMenu =>
        MenuLookup.Find(Menus.AllMenus, ChatRouteKey, ChatPaths) is { } node
        && Permissions.CanView(node.Path)
            ? node
            : null;

    /// <summary>
    /// 고정핀이 꽂혀 있나. <b>판이 아니라 여기가 들고 있다</b> —
    /// 까닭은 <see cref="RightSideDrawer.IsPinned"/> 머리말에.
    /// </summary>
    private bool _pinned;

    /// <summary>
    /// <b>휴대폰에서 화면을 옮기면 접는다.</b>
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (!IsPhone || !Reveal.IsOpen) return;

        Reveal.Close();
    }

    protected override void OnInitialized()
    {
        Menus.MenusChanged += OnChanged;
        Navigation.LocationChanged += OnLocationChanged;
        Reveal.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Menus.MenusChanged -= OnChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        Reveal.Changed -= OnChanged;
    }
}
