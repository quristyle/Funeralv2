using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class MindMapCanvas
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>고를 수 있는 마디 모양 한 칸. 목록은 <b>JS 가 정본</b>이다.</summary>
    /// <param name="Kind">JS 에 돌려줄 값(mermaid 이름).</param>
    /// <param name="Label">사람이 읽는 이름.</param>
    public sealed record ShapeOption(string Kind, string Label);

    /// <summary>지금 고른 마디의 상태. 화면이 단추를 켜고 끄는 데 쓴다.</summary>
    /// <param name="Id">마디 이름.</param>
    /// <param name="Text">보이는 글자.</param>
    /// <param name="Shape">모양(mermaid 이름).</param>
    /// <param name="IsRoot">뿌리인가. <b>뿌리는 지울 수도, 형제를 만들 수도 없다.</b></param>
    /// <param name="ChildCount">자식 수.</param>
    /// <param name="Collapsed">접어 두었는가.</param>
    public sealed record NodeSelection(
        string Id,
        string Text,
        string Shape,
        bool IsRoot,
        int ChildCount,
        bool Collapsed);

    private ElementReference _container;
    private IJSObjectReference? _module;
    private IJSObjectReference? _instance;
    private DotNetObjectReference<MindMapCanvas>? _self;

    /// <summary>CSS 높이. <c>100%</c> 를 주려면 부모에 높이가 정해져 있어야 한다.</summary>
    [Parameter] public string Height { get; set; } = "100%";

    /// <summary>
    /// 캔버스가 바뀔 때마다 불린다(머리말). 화면은 「저장 안 한 변경」을
    /// 띄우고, mermaid 글 칸이 열려 있으면 다시 만든다.
    /// </summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    /// <summary>JS 에서 부른다. <b>이름을 바꾸면 그쪽도 함께 고친다.</b></summary>
    [JSInvokable]
    public Task NotifyChanged() => OnChanged.InvokeAsync();

    /// <summary>나무를 캔버스에 그린다. 그리고 전체가 보이게 맞춘다.</summary>
    public async Task LoadAsync(MindMapModel model)
    {
        var instance = await EnsureAsync();
        await instance.InvokeVoidAsync("load", model);
    }

    /// <summary>
    /// 지금 나무를 돌려준다. <b>좌표는 없다</b> — 마인드맵은 자리를 저장하지
    /// 않는다(<see cref="MindMapModel"/> 머리말).
    /// </summary>
    public async Task<MindMapModel> SaveAsync()
    {
        if (_instance is null)
        {
            return MindMapModel.Empty;
        }

        return await _instance.InvokeAsync<MindMapModel>("save");
    }

    /// <summary>고를 수 있는 모양 목록. mermaid 의 여섯이다.</summary>
    public async Task<IReadOnlyList<ShapeOption>> ShapesAsync()
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<ShapeOption[]>("shapes");
    }

    /// <summary>지금 고른 마디. 아무것도 안 골랐으면 <c>null</c>.</summary>
    public async Task<NodeSelection?> SelectionAsync()
    {
        if (_instance is null)
        {
            return null;
        }

        return await _instance.InvokeAsync<NodeSelection?>("selection");
    }

    /// <summary>
    /// 고른 마디 <b>아래</b>에 하나 만든다. 아무것도 안 골랐으면 뿌리 아래다.
    /// 만들자마자 이름을 고치는 상태로 들어간다.
    /// </summary>
    public async Task AddChildAsync()
    {
        var instance = await EnsureAsync();
        await instance.InvokeVoidAsync("addChild");
    }

    /// <summary>
    /// 고른 마디 <b>옆</b>에 하나 만든다. 뿌리를 골랐으면 <b>아래에 만든다</b> —
    /// 마인드맵의 뿌리는 하나뿐이라 형제를 둘 자리가 없다.
    /// </summary>
    public async Task AddSiblingAsync()
    {
        var instance = await EnsureAsync();
        await instance.InvokeVoidAsync("addSibling");
    }

    /// <summary>고른 마디의 이름을 고치는 상태로 들어간다.</summary>
    /// <returns>고를 것이 없으면 <c>false</c>.</returns>
    public async Task<bool> RenameAsync()
    {
        if (_instance is null) return false;
        return await _instance.InvokeAsync<bool>("rename");
    }

    /// <summary>
    /// 고른 마디와 그 아래를 지운다.
    /// </summary>
    /// <returns>
    /// 지운 마디 수. <b>0 이면 지울 수 없었다는 뜻</b>이다 — 아무것도 안
    /// 골랐거나 뿌리를 골랐을 때다. 화면이 그것으로 까닭을 말한다.
    /// </returns>
    public async Task<int> RemoveAsync()
    {
        if (_instance is null) return 0;
        return await _instance.InvokeAsync<int>("remove");
    }

    /// <summary>고른 가지를 접거나 편다.</summary>
    /// <returns>자식이 없어 접을 것이 없으면 <c>false</c>.</returns>
    public async Task<bool> ToggleCollapseAsync()
    {
        if (_instance is null) return false;
        return await _instance.InvokeAsync<bool>("toggleCollapse");
    }

    /// <summary>고른 마디의 모양을 바꾼다.</summary>
    /// <returns>고를 것이 없으면 <c>false</c>.</returns>
    public async Task<bool> SetShapeAsync(string kind)
    {
        if (_instance is null) return false;
        return await _instance.InvokeAsync<bool>("setShape", kind);
    }

    /// <summary>접은 가지를 모두 편다.</summary>
    /// <returns>편 가지 수.</returns>
    public async Task<int> ExpandAllAsync()
    {
        if (_instance is null) return 0;
        return await _instance.InvokeAsync<int>("expandAll");
    }

    /// <summary>첫 가지만 남기고 접는다.</summary>
    /// <returns>접은 가지 수.</returns>
    public async Task<int> CollapseAllAsync()
    {
        if (_instance is null) return 0;
        return await _instance.InvokeAsync<int>("collapseAll");
    }

    public Task ZoomInAsync() => InvokeIfReadyAsync("zoomIn");

    public Task ZoomOutAsync() => InvokeIfReadyAsync("zoomOut");

    /// <summary>전체가 보이게 맞추고 가운데로 둔다.</summary>
    public Task FitAsync() => InvokeIfReadyAsync("fit");

    private async Task<IJSObjectReference> EnsureAsync()
    {
        if (_instance is not null)
        {
            return _instance;
        }

        // **RCL 정적자원 경로로 부른다.** `./js/...` 로 적으면 문서 주소를
        // 기준으로 풀려 `/projmng/design/mind-map` 에서 404 가 되고, import 가
        // 던지면서 **회로가 통째로 끊긴다** — 그림이 안 그려지는 것이 아니라
        // 그때부터 화면의 아무 단추도 안 눌린다. `DiagramViewer` 가 실제로
        // 그 길로 한 번 무너졌다.
        _module ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/JSini.Web.ProjMng/js/mind-map.js");
        _self ??= DotNetObjectReference.Create(this);
        _instance = await _module.InvokeAsync<IJSObjectReference>("create", _container, _self);
        return _instance;
    }

    private async Task InvokeIfReadyAsync(string method)
    {
        if (_instance is not null)
        {
            await _instance.InvokeVoidAsync(method);
        }
    }

    /// <summary>
    /// 회로가 끊기거나 화면을 떠날 때 그래프를 파괴한다. 안 하면 maxgraph 가
    /// document 에 걸어 둔 리스너가 남는다. 회로가 이미 끊긴 뒤라면 JS 를 부를
    /// 수 없으므로 조용히 넘어간다 — 그 경우 브라우저 문서도 함께 사라진다.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_instance is not null)
            {
                await _instance.InvokeVoidAsync("destroy");
                await _instance.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // 회로가 먼저 끊겼다. 정리할 브라우저가 이미 없다.
        }
        catch (ObjectDisposedException)
        {
            // 회로 종료와 경쟁했다. 결과는 같다.
        }

        _self?.Dispose();
        _self = null;
    }
}
