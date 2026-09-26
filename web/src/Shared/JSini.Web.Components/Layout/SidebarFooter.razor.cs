using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Components.Security;

namespace JSini.Web.Components.Layout;

public partial class SidebarFooter
{
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private ClientAddress Address { get; set; } = default!;
    [Inject] private PersistentComponentState PersistedState { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 로그아웃 폼의 아이디. <b>헤더의 것과 달라야 한다</b> —
    /// 한 쪽에 같은 아이디가 둘이면 JS 가 엉뚱한 것을 제출한다.
    /// </summary>
    private const string FormId = "jsini-sidebar-logout";

    private ConfirmDialog? _confirm;

    /// <summary>
    /// 묻고, 확인을 받으면 폼을 제출한다.
    /// </summary>
    /// <remarks>
    /// 취소하면 아무 일도 하지 않는다 — 창을 닫는 것(Esc · X)도 취소다
    /// (<see cref="ConfirmDialog"/> 가 그렇게 다룬다).
    /// </remarks>
    private async Task AskThenLogoutAsync()
    {
        var who = string.IsNullOrWhiteSpace(Me.DisplayName) ? null : Me.DisplayName;

        var asked = await _confirm!.AskAsync(
            who is null
                ? "로그아웃합니다. 저장하지 않은 내용은 사라집니다."
                : $"「{who}」 계정에서 로그아웃합니다. 저장하지 않은 내용은 사라집니다.",
            title: "로그아웃",
            confirmText: "로그아웃",

            // 빨강(Danger)으로 두지 않는다. 지우는 것이 아니라 나가는 것이고,
            // 삭제 확인과 같은 색이면 「무언가 없어진다」로 읽힌다.
            confirmStyle: ButtonRenderStyle.Primary);

        if (!asked)
        {
            return;
        }

        // **`document.getElementById('…').submit` 을 그대로 부르면 안 된다.**
        // Blazor 는 식별자를 `.` 으로 쪼개 이름을 하나씩 찾으므로
        // `getElementById('…')` 라는 이름의 속성을 뒤지고, 없으니 던진다.
        // 그 예외는 콘솔에만 찍혀서 **화면은 아무 일도 없는 것처럼 보인다.**
        await Js.InvokeVoidAsync("jsiniForm.submit", FormId);
    }

    /// <summary>프리렌더가 남기는 값의 열쇠. 이 부품 안에서만 쓴다.</summary>
    private const string StateKey = "jsini.client-ip";

    private PersistingComponentStateSubscription _persisting;

    protected override void OnInitialized()
    {
        // 회로가 붙는 길이 먼저다. 프리렌더가 남긴 것이 있으면 그게 정답이고,
        // 그때는 HttpContext 가 없어 요청을 볼 수도 없다.
        Address.Seed(
            PersistedState.TryTakeFromJson<string>(StateKey, out var persisted)
                ? persisted
                : ClientAddress.From(Http.HttpContext));

        _persisting = PersistedState.RegisterOnPersisting(() =>
        {
            PersistedState.PersistAsJson(StateKey, Address.Current);
            return Task.CompletedTask;
        });

        // 이름·소속은 부트스트랩이 늦게 채운다. 그때 다시 그려야 「…」이 이름으로 바뀐다.
        Me.Changed += OnMeChanged;
    }

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Me.Changed -= OnMeChanged;
        _persisting.Dispose();
    }
}
