using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Security;

public partial class ScreenGate : IDisposable
{
    [Inject] private ScreenAccess Access { get; set; } = default!;
    [Inject] private BlockedAccessRecorder Recorder { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>라우터가 고른 화면. 판정의 열쇠는 <c>PageType</c> 이다.</summary>
    [Parameter] public RouteData? RouteData { get; set; }

    /// <summary>통과했을 때 그릴 것 — 실제 화면이다.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private ScreenVerdict _verdict = ScreenVerdict.Pass;

    /// <summary>권한표·메뉴가 늦게 와서 판정이 바뀌면 그 소식을 듣는다. 첫 그림은 권한표가 오기 전이라 통과이므로, 이 갈고리가 없으면 그 통과가 그대로 굳는다.</summary>
    protected override void OnInitialized() => Access.Changed += OnAccessChanged;

    protected override void OnParametersSet() => Evaluate();

    /// <summary>판정하고, 막혔으면 적는다. 프리렌더에서는 적지 않는다(정적 요청과 회로가 각각 기록기를 가져 한 번 막힌 것이 두 줄이 된다). 막는 일은 프리렌더에서도 한다. 적는 것만 미룬다.</summary>
    private void Evaluate()
    {
        var href = PermissionPath.Current(Navigation);

        _verdict = Access.Enter(RouteData?.PageType, href);

        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        if (_verdict.Allowed)
        {
            // 통과한 자리도 기억해 둔다 — 다음에 막힐 때 「어디서 왔나」가 된다.
            Recorder.Passed(href);
        }
        else
        {
            Recorder.Record(_verdict, href);
        }
    }

    /// <summary>판정이 바뀌었다. 다시 따지고 그린다. 적는 일도 여기서 일어난다. 되돌아 울지 않는다(Evaluate 가 부르는 Enter 는 바뀌었을 때만 알리고, 여기서 받은 값은 방금 바뀐 뒤다).</summary>
    private void OnAccessChanged() => InvokeAsync(() =>
    {
        Evaluate();
        StateHasChanged();
    });

    public void Dispose() => Access.Changed -= OnAccessChanged;
}
