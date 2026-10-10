using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

public partial class RightSideDrawer
{
    [Parameter] public string Id { get; set; } = $"drawer-{Guid.NewGuid():N}";
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? FooterContent { get; set; }
    [Parameter] public string CssClass { get; set; } = "";

    private bool _isPinned;
    private IJSObjectReference? _module;
    private DotNetObjectReference<RightSideDrawer>? _objRef;

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
            await _module.InvokeVoidAsync("updateState", Id, IsOpen, _isPinned);
        }
    }

    private async Task TogglePin()
    {
        _isPinned = !_isPinned;
        if (_module != null)
        {
            await _module.InvokeVoidAsync("updateState", Id, IsOpen, _isPinned);
        }
    }

    private async Task OnCloseClick()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    [JSInvokable]
    public async Task CloseFromOutside()
    {
        if (IsOpen && !_isPinned)
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
