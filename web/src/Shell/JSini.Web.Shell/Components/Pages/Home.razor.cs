using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;

namespace JSini.Web.Shell.Components.Pages;

public partial class Home
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;

    /// <summary>한 번 보냈으면 다시 보내지 않는다.</summary>
    private bool _left;

    protected override void OnInitialized()
    {
        // 부트스트랩이 통에 있던 것을 썼으면 이미 다 채워져 있고, 게이트웨이를 다녀와야 했으면 아직 비어 있다. 채워지면 알려 달라고 걸어 둔다.
        Me.Changed += OnUserChanged;
        GoHome();
    }

    public void Dispose() => Me.Changed -= OnUserChanged;

    /// <summary>부트스트랩이 채워졌다. 레이아웃의 OnInitializedAsync 안에서 나므로 렌더 스레드로 넘겨서 옮긴다.</summary>
    private void OnUserChanged() => _ = InvokeAsync(GoHome);

    private void GoHome()
    {
        // 못 읽었으면 아무것도 하지 않는다. 여기서 잘못 판정하면 엉뚱한 화면으로 보낸다. 모를 때는 있던 자리에 둔다.
        if (_left || !Me.IsLoaded || !IsRoot())
        {
            return;
        }

        if (PortalHome.Resolve(Me.HomePath, Menus.VisibleMenus) is not { } target)
        {
            return;
        }

        _left = true;

        // replace 다. 빈 판을 이력에 남기면 뒤로 가기가 여기로 돌아와 다시 옮겨진다.
        Navigation.NavigateTo(target, forceLoad: false, replace: true);
    }

    /// <summary>지금 열려 있는 것이 / 인가. /workspace · /analytics 는 사람이 메뉴를 눌러 온 자리라 옮기지 않는다.</summary>
    private bool IsRoot()
    {
        var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);

        return (cut < 0 ? relative : relative[..cut]).Length == 0;
    }
}
