using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace JSini.Web.Components.Layout;

public partial class QuickAskDrawer
{
    [Inject] private QuickAskReveal Ask { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>휴대폰인가. 레이아웃이 내려 준다 — 고정핀을 그릴지와 화면을 옮길 때 접을지가 이 값으로 갈린다.</summary>
    [Parameter] public bool IsPhone { get; set; }

    /// <summary>한 번이라도 열린 적이 있나. 알맹이를 만들지 말지를 가른다 — 한 번 참이 되면 되돌리지 않는다.</summary>
    private bool _ever;

    /// <summary>고정핀이 꽂혀 있나. 판이 아니라 여기가 들고 있다(RightSideDrawer.IsPinned 머리말).</summary>
    private bool _pinned;

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

    /// <summary>화면을 옮겼다. 못 박아 두지 않았을 때만 접는다 — 까닭은 머리말의 「화면을 옮기면 닫는다」.</summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_pinned && !IsPhone) return;

        Ask.Close();
    }

    public void Dispose()
    {
        Ask.Changed -= OnAskChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
