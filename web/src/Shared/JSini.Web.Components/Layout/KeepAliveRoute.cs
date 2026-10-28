using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

/// <summary>라우터가 고른 화면을 KeepAliveScreens 에 적는다. 그리는 것은 없다. Routes 에서 ScreenGate 보다 앞에 둬야 레이아웃이 그리기 전에 적힌다.</summary>
public sealed class KeepAliveRoute : ComponentBase
{
    [Inject] private KeepAliveScreens Screens { get; set; } = default!;

    [Parameter] public RouteData? RouteData { get; set; }

    protected override void OnParametersSet() => Screens.Route = RouteData;
}
