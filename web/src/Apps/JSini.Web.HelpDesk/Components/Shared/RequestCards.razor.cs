using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class RequestCards : RequestRowList, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>줄이 하나도 없을 때 적을 말. CommGrd 의 빈 표와 같은 글자를 기본값으로 둔다 — 기기를 바꿨다고 달라지면 안 된다.</summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    // ── 밀어서 접수 · 삭제 ──
    // 할 수 있는지는 화면이 정한다 — 상세의 접수·삭제 잣대와 같아야 하므로 그 잣대는 RequestManage 의 CanAccept · CanDelete 에만 적는다.

    /// <summary>밀어서 접수할 수 있는가. 화면이 신원과 권한을 보고 넘긴다.</summary>
    [Parameter] public bool CanAccept { get; set; }

    /// <summary>밀어서 지울 수 있는가. 화면이 신원을 보고 넘긴다.</summary>
    [Parameter] public bool CanDelete { get; set; }

    /// <summary>오른쪽으로 민 줄을 접수한다. 참을 돌려주면 처리된 것이다 — EventCallback 이 아니라 Func 인 까닭은 서버가 막았을 때(권한·상태·통신) 부른 쪽이 그것을 알아야 해서다.</summary>
    [Parameter] public Func<ImprovementRequest, Task<bool>>? Accept { get; set; }

    /// <summary>왼쪽으로 민 줄을 지운다. 돌려주는 값의 뜻은 Accept 와 같다.</summary>
    [Parameter] public Func<ImprovementRequest, Task<bool>>? Delete { get; set; }

    /// <summary>손짓을 거는 상자의 이름. 부품마다 다르다 — 고정 이름이면 같은 목록의 두 상자 중 하나에만 걸린다.</summary>
    private string DomId { get; } = $"hd-req-cards-{Guid.NewGuid():N}";

    private IJSObjectReference? _swipe;
    private DotNetObjectReference<RequestCards>? _self;

    /// <summary>밀 수 있는 줄인가 — 어느 한 방향이라도 열려 있나.</summary>
    private bool Swipeable(ImprovementRequest r) => AcceptableOf(r) || RemovableOf(r);

    /// <summary>오른쪽으로 밀어 접수할 수 있는 줄인가 — 「대기」일 때만. 이미 누가 맡은 건을 가로채는 길을 열지 않는다(상세의 접수 단추와 같은 조건).</summary>
    private bool AcceptableOf(ImprovementRequest r) =>
        CanAccept && Accept is not null && r.Status is PendingStatus;

    /// <summary>왼쪽으로 밀어 지울 수 있는 줄인가 — 「대기」일 때만. 상세의 삭제는 상태를 보지 않지만 손짓에는 「대기」만 내놓는다 — 진행·끝난 건은 댓글과 기록이 딸려 있고 서버 삭제는 되돌릴 수 없다.</summary>
    private bool RemovableOf(ImprovementRequest r) =>
        CanDelete && Delete is not null && r.Status is PendingStatus;

    /// <summary>아직 아무도 맡지 않은 상태. 서버가 이름으로 실어 보낸다.</summary>
    private const string PendingStatus = "Pending";

    /// <summary>누름쇠로도 열린다 — Enter 와 Space. 줄은 div 라 저절로 눌리지 않으므로 그 일을 여기서 한다.
    /// 줄을 진짜 단추로 만들지 않는 까닭은 안에 또 눌리는 그림이 있어서다(단추 안의 단추는 HTML 이 금한다).</summary>
    private Task OnKeyDownAsync(KeyboardEventArgs e, ImprovementRequest r) =>
        e.Key is "Enter" or " " ? RowClick.InvokeAsync(r) : Task.CompletedTask;

    /// <summary>손짓을 건다. 한 번만 건다 — 뿌리 상자라 다시 그려져도 그대로 살아 있다.</summary>
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first)
        {
            return;
        }

        try
        {
            _swipe ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-swipe.js");
            _self ??= DotNetObjectReference.Create(this);

            await _swipe.InvokeVoidAsync("attachSwipe", $"#{DomId}", _self);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
                        // 브라우저에 못 걸었거나(프리렌더 · 회로가 닫히는 중) 모듈을 못 받았다. 밀기만 못 하고 목록은 살아 있다 — 누르면 상세에서 접수·삭제할 수 있다.
        }
    }

    /// <summary>민 줄을 처리한다. 브라우저가 띠를 끝까지 열어 둔 채로 부른다. 번호(id)로 다시 찾는다 — 자리로 찾으면 미는 사이 목록이 바뀌었을 때 엉뚱한 건이 지워진다.
    /// 상태와 권한을 여기서 한 번 더 본다(data-* 는 브라우저에서 고칠 수 있다). 렌더러의 일감 줄에 올려 부른다(InvokeAsync).</summary>
    [JSInvokable]
    public async Task<bool> SwipedAsync(int id, string way)
    {
        var done = false;

        await InvokeAsync(async () => done = await HandleSwipeAsync(id, way));

        return done;
    }

    private async Task<bool> HandleSwipeAsync(int id, string way)
    {
        if (Rows.FirstOrDefault(r => r.Id == id) is not { } row)
        {
            return false;
        }

        var act = way switch
        {
            "accept" when AcceptableOf(row) => Accept,
            "remove" when RemovableOf(row) => Delete,
            _ => null,
        };

        if (act is null)
        {
            return false;
        }

        var done = await act(row);

        StateHasChanged();

        return done;
    }

    /// <summary>걸어 둔 것을 거둔다. 모듈을 놓는 것은 회로가 닫혔으면 JSDisconnectedException 이 날 수 있어 삼키고, 우리 손잡이(_self)는 반드시 놓는다 — 안 놓으면 부품이 회로가 사는 내내 남는다.</summary>
    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;

        if (_swipe is null)
        {
            return;
        }

        try
        {
            await _swipe.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
        finally
        {
            _swipe = null;
        }
    }
}
