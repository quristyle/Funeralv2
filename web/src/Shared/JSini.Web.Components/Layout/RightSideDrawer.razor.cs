using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

public partial class RightSideDrawer
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Id { get; set; } = $"drawer-{Guid.NewGuid():N}";
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? FooterContent { get; set; }
    [Parameter] public string CssClass { get; set; } = "";

    /// <summary>
    /// 휴대폰인가. 참이면 <b>고정핀을 안 그리고 꽂힌 핀도 뽑는다</b> —
    /// 까닭은 머리말의 「고정핀은 책상 화면 것이다」.
    /// </summary>
    [Parameter] public bool IsPhone { get; set; }

    /// <summary>
    /// 못 박아 두었나.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>상태를 부모가 들고 있다.</b> 이 판이 들고 있으면 부모는 지금 못이
    /// 박혀 있는지를 몰라서, 화면을 옮길 때 접을지 말지를 가릴 수가 없다 —
    /// 셋 다 바로 그 판단을 해야 한다(<c>QuickAskDrawer.OnLocationChanged</c> ·
    /// <c>NotificationInboxDrawer.OpenAsync</c>). 여닫힘(<see cref="IsOpen"/>)을
    /// 부모가 들고 있는 것과 같은 구도다.
    /// </para>
    /// </remarks>
    [Parameter] public bool IsPinned { get; set; }

    [Parameter] public EventCallback<bool> IsPinnedChanged { get; set; }

    private IJSObjectReference? _module;
    private DotNetObjectReference<RightSideDrawer>? _objRef;

    /// <summary>
    /// <b>지금 실제로 못이 박혀 있나.</b> 휴대폰에서는 언제나 거짓이다 —
    /// 화면이 좁아진 순간 핀을 뽑긴 하지만(<see cref="OnParametersSetAsync"/>),
    /// 그 한 번의 되알림이 부모에게 닿기 전에도 바깥 클릭이 들어올 수 있다.
    /// </summary>
    private bool Pinned => IsPinned && !IsPhone;

    /// <summary>
    /// 화면이 좁아지면 꽂아 둔 핀을 뽑는다. 책상에서 고정해 둔 채 창을 줄이면
    /// 판이 본문을 통째로 덮는데, 못이 박힌 채라면 바깥을 눌러도 메뉴를 옮겨도
    /// 안 닫혀서 <b>빠져나오는 길이 머리의 ✕ 하나</b>만 남는다.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (IsPhone && IsPinned)
        {
            IsPinned = false;
            await IsPinnedChanged.InvokeAsync(false);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.Components/js/drawer-click.js");
            _objRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", Id, _objRef);
        }

        if (_module != null)
        {
            await _module.InvokeVoidAsync("updateState", Id, IsOpen, Pinned);
        }
    }

    private async Task TogglePin()
    {
        IsPinned = !IsPinned;
        await IsPinnedChanged.InvokeAsync(IsPinned);

        if (_module != null)
        {
            await _module.InvokeVoidAsync("updateState", Id, IsOpen, Pinned);
        }
    }

    private async Task OnCloseClick()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    /// <summary>
    /// 판 바깥을 눌렀다. <b>못이 박혀 있으면 아무 일도 하지 않는다.</b>
    /// </summary>
    [JSInvokable]
    public async Task CloseFromOutside()
    {
        if (IsOpen && !Pinned)
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _objRef?.Dispose();
        if (_module != null)
        {
            _ = _module.InvokeVoidAsync("dispose", Id);
            _ = _module.DisposeAsync();
        }
    }
}
