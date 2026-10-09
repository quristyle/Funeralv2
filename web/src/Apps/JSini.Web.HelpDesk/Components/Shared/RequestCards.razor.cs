using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class RequestCards : RequestRowList, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// 줄이 하나도 없을 때 적을 말. <c>CommGrd</c> 의 빈 표와 <b>같은 글자</b>를
    /// 기본값으로 둔다 — 기기를 바꿨다고 없다는 말이 달라지면 안 된다.
    /// </summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    // ── 밀어서 접수 · 삭제 (2026-10-09) ─────────────────────────
    //
    // 할 수 있는지는 **화면이 정한다.** 이 부품은 권한표도 신원도 보지 않는다 —
    // 상세 화면이 「접수」·「삭제」 단추를 내놓는 잣대와 **같은 것**이어야
    // 하는데, 그 잣대를 두 곳에 적으면 한쪽만 고치는 날이 오고 그날
    // 휴대폰에서만 못 하는(또는 해서는 안 되는데 되는) 일이 생긴다.
    // 적어 둔 자리는 `RequestManage` 의 `CanAccept` · `CanDelete` 다.

    /// <summary>밀어서 접수할 수 있는가. 화면이 신원과 권한을 보고 넘긴다.</summary>
    [Parameter] public bool CanAccept { get; set; }

    /// <summary>밀어서 지울 수 있는가. 화면이 신원을 보고 넘긴다.</summary>
    [Parameter] public bool CanDelete { get; set; }

    /// <summary>
    /// 오른쪽으로 민 줄을 <b>접수</b>한다. <b>참을 돌려주면 처리된 것</b>이다.
    /// </summary>
    /// <remarks>
    /// <see cref="EventCallback{T}"/> 가 아니라 <see cref="Func{T, TResult}"/>
    /// 인 까닭은 <b>처리됐는지가 부른 쪽으로 돌아가야</b> 하기 때문이다 —
    /// 서버가 막았으면(권한 · 상태 · 통신) 그것을 알아야 하고,
    /// <c>EventCallback</c> 으로는 그 답을 받을 수 없다.
    /// </remarks>
    [Parameter] public Func<ImprovementRequest, Task<bool>>? Accept { get; set; }

    /// <summary>
    /// 왼쪽으로 민 줄을 <b>지운다</b>. 돌려주는 값의 뜻은
    /// <see cref="Accept"/> 와 같다.
    /// </summary>
    [Parameter] public Func<ImprovementRequest, Task<bool>>? Delete { get; set; }

    /// <summary>
    /// 손짓을 거는 상자의 이름. <b>부품마다 다르다</b> — 같은 목록이 화면에
    /// 둘 설 일은 없지만, 고정한 이름은 그날 두 상자 중 하나에만 걸린다.
    /// </summary>
    private string DomId { get; } = $"hd-req-cards-{Guid.NewGuid():N}";

    private IJSObjectReference? _swipe;
    private DotNetObjectReference<RequestCards>? _self;

    /// <summary>
    /// 밀 수 있는 줄인가 — <b>어느 한 방향이라도</b> 열려 있나.
    /// </summary>
    private bool Swipeable(ImprovementRequest r) => AcceptableOf(r) || RemovableOf(r);

    /// <summary>
    /// 오른쪽으로 밀어 접수할 수 있는 줄인가 — <b>「대기」일 때만</b>.
    /// </summary>
    /// <remarks>
    /// 이미 누가 맡은 건을 밀어서 가로채는 길을 열지 않는다. 상세 화면의
    /// 「접수」 단추도 같은 조건이다(<c>RequestDetail.IsPending</c>).
    /// </remarks>
    private bool AcceptableOf(ImprovementRequest r) =>
        CanAccept && Accept is not null && r.Status is PendingStatus;

    /// <summary>
    /// 왼쪽으로 밀어 지울 수 있는 줄인가 — <b>「대기」일 때만</b>.
    /// </summary>
    /// <remarks>
    /// 상세 화면의 「삭제」는 상태를 보지 않지만(시스템관리자는 어느 건이든
    /// 지운다) <b>손짓에는 「대기」만 내놓는다.</b> 진행 중이거나 끝난 건은
    /// 댓글과 처리 기록이 딸려 있고 서버의 삭제는 <b>되돌릴 수 없다</b>
    /// (줄을 통째로 지운다 · <c>DELETE requests/{id}</c>). 그런 것을 스쳐
    /// 지나가는 손짓에 매달지 않는다 — 지울 일이 있으면 상세로 들어간다.
    /// </remarks>
    private bool RemovableOf(ImprovementRequest r) =>
        CanDelete && Delete is not null && r.Status is PendingStatus;

    /// <summary>아직 아무도 맡지 않은 상태. 서버가 이름으로 실어 보낸다.</summary>
    private const string PendingStatus = "Pending";

    /// <summary>
    /// 누름쇠로도 열린다 — Enter 와 Space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 줄은 <c>div</c> 라 저절로 눌리지 않는다(<c>role="button"</c> ·
    /// <c>tabindex="0"</c> 로 눌리는 자리라고 알릴 뿐이다). 그래서 진짜 단추가
    /// 저절로 해 주던 일을 여기서 해 준다.
    /// </para>
    /// <para>
    /// <b>줄을 진짜 단추로 만들지 않은 까닭</b>은 안에 또 눌리는 것(그림)이
    /// 있어서다 — 단추 안의 단추는 HTML 이 금하고, 브라우저마다 다르게 풀린다.
    /// </para>
    /// </remarks>
    private Task OnKeyDownAsync(KeyboardEventArgs e, ImprovementRequest r) =>
        e.Key is "Enter" or " " ? RowClick.InvokeAsync(r) : Task.CompletedTask;

    /// <summary>
    /// 손짓을 건다. <b>한 번만 건다</b> — 거는 자리가 목록이 아니라 이 부품의
    /// 뿌리 상자라(머리말) 다시 그려져도 그대로 살아 있다.
    /// </summary>
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
            // 브라우저에 못 걸었거나(프리렌더 · 회로가 닫히는 중) 모듈을 못
            // 받았다. 밀기만 못 하고 목록은 그대로 산다 — 누르면 상세가 열리고
            // 거기 「접수」·「삭제」가 있다.
        }
    }

    /// <summary>
    /// <b>민 줄을 처리한다.</b> 브라우저가 띠를 끝까지 열어 둔 채로 부른다.
    /// </summary>
    /// <param name="id">민 요청의 번호.</param>
    /// <param name="way"><c>accept</c>(오른쪽) · <c>remove</c>(왼쪽).</param>
    /// <returns>
    /// 처리했으면 <c>true</c>. <b>브라우저가 그 값을 쓰지는 않는다</b> —
    /// 어느 쪽이든 띠를 닫는다(처리됐으면 그 줄은 이미 걷혔거나 새 상태로
    /// 다시 그려져 있다). 돌려주는 것은 <b>시험과 기록을 위한 것</b>이고,
    /// 앞으로 「되돌리기」를 붙인다면 여기가 그 갈림이다.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>번호로 다시 찾는다.</b> 미는 동안 목록이 다시 그려졌으면(「더보기」·
    /// 조회) 브라우저가 들고 있던 번호가 지금 목록에 없을 수 있다 — 그때는
    /// 아무 일도 하지 않는다. 자리(몇 번째 줄)로 찾으면 <b>엉뚱한 건이
    /// 지워진다.</b>
    /// </para>
    /// <para>
    /// <b>상태와 권한을 여기서 한 번 더 본다.</b> <c>data-*</c> 는 거드는 것일
    /// 뿐이고 브라우저에서 고칠 수 있는 값이다. 못 박는 자리는 화면이 넘긴
    /// <see cref="Accept"/> · <see cref="Delete"/> 와 여기 조건이다.
    /// </para>
    /// <para>
    /// <b>렌더러의 일감 줄에 올려서 부른다</b>(<c>InvokeAsync</c>). JS 가
    /// 부르는 길은 그 줄 밖이라, 그대로 서버 호출과 <c>StateHasChanged</c> 를
    /// 하면 다른 갈래와 겹쳐 「회로가 두 곳에서 같은 화면을 그리는」 자리가 된다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 걸어 둔 것을 거둔다.
    /// </summary>
    /// <remarks>
    /// <b>모듈을 놓는 것은 터질 수 있다</b> — 회로가 이미 닫혔으면
    /// <c>JSDisconnectedException</c> 이 난다(화면을 옮기는 길에서 흔하다).
    /// 거두지 못해도 잃는 것이 없으므로 삼킨다. 우리 쪽 손잡이
    /// (<see cref="_self"/>)는 터지지 않으므로 반드시 놓는다 — 안 놓으면
    /// 부품이 회로가 사는 내내 남는다.
    /// </remarks>
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
