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
        // 부트스트랩이 **통에 있던 것**을 썼으면 이 시점에 이미 다 채워져 있고
        // (PortalBootstrapStore — 2분), 게이트웨이를 다녀와야 했으면 아직 비어
        // 있다. 둘 다 받으려고 지금 한 번 보고, 채워지면 알려 달라고 걸어 둔다.
        Me.Changed += OnUserChanged;
        GoHome();
    }

    public void Dispose() => Me.Changed -= OnUserChanged;

    /// <summary>
    /// 부트스트랩이 채워졌다. <b>렌더 스레드로 넘겨서</b> 옮긴다 — 이 알림은
    /// 레이아웃의 <c>OnInitializedAsync</c> 안에서 나므로 여기서 곧바로
    /// 화면을 건드리면 그리는 중에 끼어든다.
    /// </summary>
    private void OnUserChanged() => _ = InvokeAsync(GoHome);

    private void GoHome()
    {
        // 못 읽었으면 아무것도 하지 않는다. 헤더에 이름이 안 뜨는 것과 달리
        // 여기서 잘못 판정하면 **엉뚱한 화면으로 보낸다** — 모를 때는 있던
        // 자리에 둔다(`CurrentUser.ReloadAsync` 가 실패를 삼키는 것과 같은 결).
        if (_left || !Me.IsLoaded || !IsRoot())
        {
            return;
        }

        if (PortalHome.Resolve(Me.HomePath, Menus.VisibleMenus) is not { } target)
        {
            return;
        }

        _left = true;

        // `replace` 다 — 빈 판을 이력에 남기면 뒤로 가기가 여기로 돌아오고,
        // 돌아오는 순간 다시 옮겨져서 **뒤로 가기가 먹지 않는 것처럼 보인다.**
        Navigation.NavigateTo(target, forceLoad: false, replace: true);
    }

    /// <summary>
    /// 지금 열려 있는 것이 <c>/</c> 인가. <c>/workspace</c> · <c>/analytics</c>
    /// 는 사람이 메뉴를 눌러서 온 자리라 옮기지 않는다(머리말).
    /// </summary>
    private bool IsRoot()
    {
        var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);

        return (cut < 0 ? relative : relative[..cut]).Length == 0;
    }
}
