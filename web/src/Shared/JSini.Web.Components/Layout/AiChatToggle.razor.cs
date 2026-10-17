using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class AiChatToggle
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private AiChatReveal Reveal { get; set; } = default!;
    [Inject] private HeaderIconManager HeaderIcons { get; set; } = default!;

    /// <summary>
    /// 휴대폰인가. 🤖 가 하는 일이 이 값으로 갈린다 — 책상에서는 판을
    /// 여닫고, 휴대폰에서는 그 화면으로 옮겨 간다.
    /// </summary>
    [Parameter] public bool IsPhone { get; set; }

    private const string ChatRouteKey = "site.ai.chat";
    private static readonly string[] ChatPaths = ["/ai/chat", "/site/ai/chat"];

    private MenuNode? ChatMenu =>
        MenuLookup.Find(Menus.AllMenus, ChatRouteKey, ChatPaths) is { } node
        && Permissions.CanView(node.Path)
            ? node
            : null;

    private bool ChatDocks => !IsPhone;

    private string ChatTitle(MenuNode chat)
    {
        if (!ChatDocks)
        {
            return $"{chat.Title} — AI 에게 묻기";
        }

        return Reveal.IsOpen ? $"{chat.Title} 닫기" : $"{chat.Title} — 옆에서 AI 에게 묻기";
    }

    private void OpenChat(MenuNode chat)
    {
        UserDrawer.Close();

        if (ChatDocks)
        {
            Reveal.Toggle();
            return;
        }

        Reveal.Close();
        Navigation.NavigateTo(chat.LinkTarget);
    }

    protected override void OnInitialized()
    {
        Menus.MenusChanged += OnChanged;
        Reveal.Changed += OnChanged;
        HeaderIcons.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Menus.MenusChanged -= OnChanged;
        Reveal.Changed -= OnChanged;
        HeaderIcons.Changed -= OnChanged;
    }
}
