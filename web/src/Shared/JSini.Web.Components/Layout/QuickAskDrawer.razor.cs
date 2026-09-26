using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace JSini.Web.Components.Layout;

public partial class QuickAskDrawer
{
    [Inject] private QuickAskReveal Ask { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// 한 번이라도 열린 적이 있나. <b>알맹이를 만들지 말지를 가른다</b> —
    /// 위 머리말 참고. 한 번 참이 되면 되돌리지 않는다.
    /// </summary>
    private bool _ever;

    protected override void OnInitialized()
    {
        // 손잡이가 이 판보다 오래 산다(scoped). 판이 다시 만들어졌는데 그때
        // 이미 펴져 있으면, 안 받아 두는 한 **펴진 채로 빈 판**이 된다.
        _ever = Ask.IsOpen;

        Ask.Changed += OnAskChanged;
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnAskChanged()
    {
        if (Ask.IsOpen)
        {
            _ever = true;
        }

        InvokeAsync(StateHasChanged);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => Ask.Close();

    public void Dispose()
    {
        Ask.Changed -= OnAskChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
