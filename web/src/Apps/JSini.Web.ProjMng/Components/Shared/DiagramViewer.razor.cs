using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class DiagramViewer
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>도구상자 한 칸. <c>Kind</c> 는 JS 에 돌려줄 값, <c>Label</c> 은 사람이 읽는 이름이다.
    /// <c>Group</c> 은 도구상자의 묶음 이름이고 선 목록에는 없다.</summary>
    public sealed record DiagramShape(string Kind, string Label, string? Group = null);

    private ElementReference _container;

    /// <summary>미니맵이 들어갈 빈 칸. 채우는 것은 JS 다.</summary>
    private ElementReference _minimap;

    private IJSObjectReference? _module;
    private IJSObjectReference? _instance;

    /// <summary>CSS 높이. <c>100%</c> 를 주려면 부모에 높이가 정해져 있어야 한다.</summary>
    [Parameter] public string Height { get; set; } = "100%";

    /// <summary>캔버스가 처음 바뀔 때 한 번 불린다 — 저장하지 않은 변경을 화면이 표시하라는 신호다.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    /// <summary>캔버스가 사용자에게 할 말이 생겼을 때 불린다 — 지금은 그림 붙여넣기뿐이다. 빈 글자는 하던 말을 거두라는 뜻이다.</summary>
    [Parameter] public EventCallback<string> OnNotice { get; set; }

    /// <summary>JS 에서 부른다. <b>이름을 바꾸면 그쪽도 함께 고친다.</b></summary>
    [JSInvokable]
    public Task NotifyNotice(string? text) => OnNotice.InvokeAsync(text ?? string.Empty);

    /// <summary>Ctrl+S 를 눌렀다. 이것을 받는 화면만 그 키를 가로챈다. 이름을 고치던 중이었으면 확정한 뒤에 올라온다.</summary>
    [Parameter] public EventCallback OnSave { get; set; }

    /// <summary>JS 에서 부른다. <b>이름을 바꾸면 그쪽도 함께 고친다.</b></summary>
    [JSInvokable]
    public Task NotifySave() => OnSave.InvokeAsync();

    /// <summary>JS 가 <see cref="NotifyChanged"/> 를 부를 수 있게 잡아 둔 참조.</summary>
    private DotNetObjectReference<DiagramViewer>? _self;

    /// <summary>JS 에서 부른다. <b>이름을 바꾸면 그쪽도 함께 고친다.</b></summary>
    [JSInvokable]
    public Task NotifyChanged() => OnChanged.InvokeAsync();

    /// <summary>모델을 캔버스에 그린다. 기존 도형은 모두 지운다. 저장본에 View 가 있으면 보던 자리와 미니맵까지 되돌린다.
    /// 없으면(옛 그림) 화면 상태는 건드리지 않는다.</summary>
    public async Task LoadAsync(ErdModel model)
    {
        var instance = await EnsureAsync();

        // 돌려받은 값으로 맞춘다 — 넘긴 값이 아니라 실제로 적용된 상태다.
        var view = await instance.InvokeAsync<ErdViewport>("load", model);

        MinimapOn = view?.Minimap ?? MinimapOn;
        Background = view?.Background ?? Background;
    }

    /// <summary>캔버스의 현재 상태(옮긴 좌표·크기, 이은 선)를 모델로 되돌린다. 이름·설명은 넣었던 값이 그대로 돌아온다.</summary>
    public async Task<ErdModel> SaveAsync()
    {
        if (_instance is null)
        {
            return ErdModel.Empty;
        }

        return await _instance.InvokeAsync<ErdModel>("save");
    }

    /// <summary>도형을 하나 만든다. 이름은 캔버스에서 고칠 수 있다 — 표에서 온 도형은 DB 가 정본이라 못 고친다.</summary>
    /// <param name="name">첫 이름. 안 주면 도형 이름(「마름모」…).</param>
    /// <param name="kind">도형 종류(<see cref="ShapesAsync"/> 가 주는 값).</param>
    public async Task AddEntityAsync(string? name = null, string? kind = null)
    {
        var instance = await EnsureAsync();
        await instance.InvokeVoidAsync("addEntity", name, kind);
    }

    /// <summary>도구상자에 놓을 도형 목록. 목록은 <b>JS 가 정본</b>이다.</summary>
    public async Task<IReadOnlyList<DiagramShape>> ShapesAsync()
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<DiagramShape[]>("shapes");
    }

    /// <summary>도구상자의 빈 칸에 도형 그림을 채운다. 마크업을 회로로 보내지 않는다 — 한 번에 실으면 수신 한도(32KB)를 넘겨 연결이 끊긴다.</summary>
    public async Task PaintPreviewsAsync()
    {
        var instance = await EnsureAsync();
        await instance.InvokeVoidAsync("paintPreviews");
    }

    /// <summary>도구상자의 「선」 칸 목록.</summary>
    public async Task<IReadOnlyList<DiagramShape>> EdgeStylesAsync()
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<DiagramShape[]>("edgeStyles");
    }

    /// <summary>고른 선의 모양을 바꾼다. 선을 안 골랐으면 0 을 돌려준다 — 화면이 그것으로 「선을 먼저 고르라」고 말한다.</summary>
    public async Task<int> StyleEdgeAsync(string kind)
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<int>("styleEdge", kind);
    }

    /// <summary>겹친 것만 밀어내 고루 펼친다. 배치를 새로 짜지 않는다 — 놓아 둔 자리는 그대로 두고 겹침만 푼다.</summary>
    /// <returns>옮긴 도형 수.</returns>
    public async Task<int> SpreadAsync(int margin = 24)
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<int>("spread", margin);
    }

    /// <summary>그림을 보기 좋게 펼친다.</summary>
    /// <param name="kind"><c>hierarchy</c> · <c>organic</c> · <c>circle</c> · <c>grid</c>. 관계선이 없으면 격자로 떨어진다.</param>
    /// <returns>실제로 쓴 방식. 격자로 떨어졌는지 화면이 알 수 있다.</returns>
    public async Task<string?> ArrangeAsync(string kind)
    {
        var instance = await EnsureAsync();
        return await instance.InvokeAsync<string>("arrange", kind);
    }

    /// <summary>고른 도형·선을 지운다. 도형은 다시 불러오면 돌아온다 — 정본은 DB 의 테이블 목록이고, 선은 그림의 것이라 저장 뒤 사라진다.</summary>
    public Task DeleteSelectionAsync() => InvokeIfReadyAsync("deleteSelection");

    /// <summary>지금 바탕(<c>none</c> · <c>grid</c> · <c>dots</c>). 도구상자가 어느 칸에 불을 켤지 읽는다.</summary>
    public string Background { get; private set; } = "none";

    /// <summary>바탕을 고른다. 자석이 딸려 있다 — 보이는 것과 붙는 것을 갈라 두면 둘 다 나쁘다. 잠시 안 붙이려면 Alt 를 누른 채 끈다.</summary>
    /// <param name="kind"><c>none</c> · <c>grid</c> · <c>dots</c>.</param>
    /// <returns>실제로 적용된 이름. <b>JS 가 돌려준 값을 그대로 쓴다.</b></returns>
    public async Task<string> SetBackgroundAsync(string kind)
    {
        var instance = await EnsureAsync();

        Background = await instance.InvokeAsync<string>("setBackground", kind);
        return Background;
    }

    /// <summary>미니맵이 켜져 있나. 도구상자가 단추를 켜고 끄는 데 읽는다.</summary>
    public bool MinimapOn { get; private set; }

    /// <summary>미니맵을 켜거나 끈다. 기본은 켬이고, 끈 채로 저장하면 다음에도 꺼진 채로 열린다.</summary>
    /// <returns>바뀐 뒤 상태. JS 가 돌려준 값을 그대로 쓴다 — 켜지 못했으면(그릴 자리가 없다) 켜진 것으로 보이면 안 된다.</returns>
    public async Task<bool> ToggleMinimapAsync()
    {
        var instance = await EnsureAsync();

        MinimapOn = await instance.InvokeAsync<bool>("minimap", !MinimapOn);
        return MinimapOn;
    }

    public Task ZoomInAsync() => InvokeIfReadyAsync("zoomIn");

    public Task ZoomOutAsync() => InvokeIfReadyAsync("zoomOut");

    /// <summary>전체가 보이게 맞추고 가운데로 둔다.</summary>
    public Task FitAsync() => InvokeIfReadyAsync("fit");

    /* 만드는 일은 한 번뿐이다 — 일감을 첫 await 전에 잡아 두어야 같은 그래프가 두세 개 생기지 않는다. */
    private Task<IJSObjectReference>? _creating;

    private Task<IJSObjectReference> EnsureAsync() => _creating ??= CreateAsync();

    private async Task<IJSObjectReference> CreateAsync()
    {
        // RCL 정적자원 경로로 부른다 — 상대 경로는 문서 주소 기준으로 풀려 /projmng/… 에서 404 가 나고, import 가 던져 회로가 끊긴다.
        _module ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/JSini.Web.ProjMng/js/diagram-viewer.js");
        _self ??= DotNetObjectReference.Create(this);

        try
        {
            _instance = await _module.InvokeAsync<IJSObjectReference>(
                "create", _container, _self, _minimap);

            // 미니맵은 켜고 시작한다 — 저장본에 끈 채로 남아 있으면 LoadAsync 가 그때 끈다.
            MinimapOn = await _instance.InvokeAsync<bool>("minimap", true);

            // Ctrl+S 는 **받을 화면에서만** 가로챈다.
            await _instance.InvokeVoidAsync("saveShortcut", OnSave.HasDelegate);
        }
        catch
        {
            // 실패한 일감을 들고 있으면 다시는 못 만든다 — 비워 두고 다음 기회를 준다.
            _creating = null;
            throw;
        }

        return _instance;
    }

    private async Task InvokeIfReadyAsync(string method)
    {
        if (_instance is not null)
        {
            await _instance.InvokeVoidAsync(method);
        }
    }

    /// <summary>회로가 끊기거나 화면을 떠날 때 그래프를 파괴한다 — 안 하면 maxgraph 의 리스너가 document 에 남는다. 회로가 이미 끊겼으면 조용히 넘어간다.</summary>
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
        _creating = null;
        MinimapOn = true;
        Background = "none";

    }
}
