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

    /// <summary>
    /// 권한표·메뉴가 늦게 와서 판정이 바뀌면 그 소식을 듣는다.
    /// </summary>
    /// <remarks>
    /// 첫 그림은 권한표가 오기 전이라 언제나 통과다(<c>ScreenAccess</c> 머리말).
    /// 부트스트랩이 채워 넣는 순간 저쪽이 다시 따져 이 갈고리를 당기고,
    /// 그때 비로소 막힌다 — <b>없으면 그 첫 통과가 그대로 굳는다.</b>
    /// </remarks>
    protected override void OnInitialized() => Access.Changed += OnAccessChanged;

    protected override void OnParametersSet() => Evaluate();

    /// <summary>
    /// 판정하고, 막혔으면 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>프리렌더에서는 적지 않는다.</b> 첫 그림은 정적 SSR 요청에서 한 번
    /// 그려지고 회로가 붙은 뒤 또 한 번 그려지는데, 그 둘은 범위가 달라 각자 제
    /// 기록기를 갖는다 — 시각으로 가려내는 규칙이 서로를 못 봐서 한 번 막힌 것이
    /// 두 줄로 남는다(<c>MenuUsageRecorder</c> 가 같은 자리에서 같은 일을 겪었다).
    /// <b>막는 일은 프리렌더에서도 한다</b> — 적는 것만 미룬다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 판정이 바뀌었다. <b>다시 따지고 그린다</b> — 적는 일도 여기서 일어난다
    /// (막히는 순간이 대개 이 자리다. 첫 그림은 아직 통과였다).
    /// </summary>
    /// <remarks>
    /// 되돌아 울지 않는다. <see cref="Evaluate"/> 가 부르는
    /// <c>ScreenAccess.Enter</c> 는 판정이 <b>달라졌을 때만</b> 알리는데,
    /// 여기 왔다는 것은 방금 그 값으로 바뀐 뒤라 두 번째는 같은 값이다.
    /// </remarks>
    private void OnAccessChanged() => InvokeAsync(() =>
    {
        Evaluate();
        StateHasChanged();
    });

    public void Dispose() => Access.Changed -= OnAccessChanged;
}
