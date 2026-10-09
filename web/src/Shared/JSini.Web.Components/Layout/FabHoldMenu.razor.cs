using JSini.Web.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 떠다니는 단추를 <b>오래 누르면</b> 뜨는 창. 왜 생겼는지는 마크업 머리말에 있다.
/// </summary>
/// <remarks>
/// <para>
/// <b>스스로 손짓을 건다.</b> 셸(<see cref="MainLayout"/>)은 이 부품을 한 줄
/// 적어 둘 뿐이고, 어느 단추가 눌렸는지는 브라우저가 알려 준다
/// (<c>js/fab-hold.js</c> → <see cref="OpenForAsync"/>). 셸에 거는 자리를
/// 두면 <b>단추 둘의 설정이 셸과 여기로 갈라져</b> 한쪽만 고치는 날이 온다.
/// </para>
/// <para>
/// <b>값은 <see cref="PortalBoot"/> 한 곳에만 담긴다.</b> 환경설정 화면
/// (<c>EnvironmentSettingPage</c>)이 쓰는 그 열쇠 그대로라, 여기서 옮긴 자리가
/// 거기서도 보이고 그 반대도 같다. 두 화면이 서로를 모르고도 어긋나지 않는
/// 까닭이 그것이다.
/// </para>
/// </remarks>
public partial class FabHoldMenu : IAsyncDisposable
{
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>고를 수 있는 귀퉁이 넷. <b>차례가 화면의 차례다</b>(위 둘 · 아래 둘).</summary>
    /// <remarks>
    /// 환경설정의 목록과 달리 「(기본)」을 붙이지 않는다. 여기서는 지금 자리에
    /// 표시가 붙으므로(<c>is-on</c>) 기본값이 무엇인지는 물을 일이 아니고,
    /// 네모 안에 들어갈 글자는 짧을수록 좋다.
    /// </remarks>
    private static readonly IReadOnlyList<FabCorner> Corners =
    [
        new("top-left", "왼쪽 위"),
        new("top-right", "오른쪽 위"),
        new("bottom-left", "왼쪽 아래"),
        new("bottom-right", "오른쪽 아래"),
    ];

    private sealed record FabCorner(string Value, string Name);

    private IJSObjectReference? _module;
    private DotNetObjectReference<FabHoldMenu>? _self;

    private bool _visible;

    /// <summary>지금 다루고 있는 것이 <b>요청 등록 단추</b>인가. 아니면 메뉴 단추다.</summary>
    private bool _help;

    /// <summary>
    /// 두 단추의 자리를 <b>따로</b> 들고 있는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 하나로 두면 메뉴 단추를 옮긴 뒤 요청 등록 단추를 길게 눌렀을 때
    /// <b>남의 자리에 표시가 붙는다.</b>
    /// </para>
    /// <para>
    /// <b><see cref="PortalBoot.ReadAsync"/> 를 다시 부르지 않는다.</b> 그것은
    /// 회로마다 한 번 읽어 둔 것을 그대로 돌려주는 자리라
    /// (몇 번을 불러도 왕복은 한 번이다), 창을 열 때마다 불러도 <b>처음 그
    /// 값</b>이다. 그래서 첫 그림에서 한 번 읽고, 그 뒤로는 바뀔 때마다 오는
    /// 알림(<see cref="PortalBoot.FabPositionChanged"/> 들)으로 따라간다 —
    /// 환경설정 화면에서 고친 것도 같은 알림을 타고 여기로 온다.
    /// </para>
    /// </remarks>
    private string _menuPosition = "bottom-left";

    private string _helpPosition = "bottom-right";

    /// <summary>지금 눌린 단추의 자리.</summary>
    private string Position => _help ? _helpPosition : _menuPosition;

    private string Title => _help ? "요청 등록 단추" : "메뉴 단추";

    private string Lead => _help
        ? "헬프데스크 요청 등록으로 바로 가는 단추입니다."
        : "메뉴를 여닫는 단추입니다.";

    /// <summary>
    /// 환경설정 화면의 주소. 메뉴에 없으면(권한이 없으면) <c>null</c> 이다.
    /// </summary>
    /// <remarks>
    /// <b>주소를 글자로 박지 않는다.</b> <c>@page</c> 는 모듈이 소유한 값이라
    /// 옮겨 갈 수 있고, 그때 여기 사본이 조용히 어긋나면 누른 사람이
    /// 「준비 중」을 본다(<see cref="PortalHome.SettingsRouteKey"/> 머리말).
    /// </remarks>
    private string? SettingsHref => PortalHome.Resolve(null, Menus.VisibleMenus);

    protected override void OnInitialized()
    {
        // 환경설정 화면에서 고친 것도 여기로 온다 — 그래야 다음에 길게 눌렀을 때
        // 표시가 거기 붙어 있다.
        Boot.FabPositionChanged += OnMenuPositionChanged;
        Boot.HelpDeskFabPositionChanged += OnHelpPositionChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var state = await Boot.ReadAsync();
        var changed = false;

        if (state.FabPosition != _menuPosition)
        {
            _menuPosition = state.FabPosition;
            changed = true;
        }

        if (state.HelpDeskFabPosition != _helpPosition)
        {
            _helpPosition = state.HelpDeskFabPosition;
            changed = true;
        }

        if (changed)
        {
            StateHasChanged();
        }

        // 회로가 이미 닫혔으면 조용히 넘어간다 — 이 부품은 모든 화면에 실려
        // 있어서, 여기서 예외가 새면 **사람이 창을 닫는 것만으로** 오류가 난다
        // (`NotificationInboxDrawer` 와 같은 까닭).
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/fab-hold.js");
            _self ??= DotNetObjectReference.Create(this);

            await _module.InvokeVoidAsync("attachFabHold", _self);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// 브라우저가 <b>길게 누른 단추</b>를 알려 왔다. <c>menu</c> 아니면 <c>help</c> 다.
    /// </summary>
    /// <remarks>
    /// 모르는 값이 오면 메뉴 단추로 본다 — 길게 눌렀는데 아무 일도 안 일어나는
    /// 것보다 낫고, 보내는 쪽이 둘 중 하나만 보낸다(<c>js/fab-hold.js</c>).
    /// </remarks>
    [JSInvokable]
    public Task OpenForAsync(string kind)
    {
        _help = string.Equals(kind, "help", StringComparison.OrdinalIgnoreCase);
        _visible = true;

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>바깥을 눌렀거나 Esc 로 닫았다. 아무것도 고치지 않는다.</summary>
    private void OnVisibleChanged(bool visible) => _visible = visible;

    /// <summary>
    /// 고른 귀퉁이로 옮긴다. <b>옮기면서 창을 닫는다.</b>
    /// </summary>
    /// <remarks>
    /// 열어 두면 창이 화면 가운데를 덮고 있어서 <b>옮겨 간 결과가 안 보인다</b> —
    /// 고르는 일의 목적이 바로 그 그림을 보는 것이다.
    /// </remarks>
    private async Task MoveAsync(string corner)
    {
        _visible = false;

        if (_help)
        {
            await Boot.SetHelpDeskFabPositionAsync(corner);
        }
        else
        {
            await Boot.SetFabPositionAsync(corner);
        }
    }

    /// <summary>
    /// 이 단추를 감춘다. <b>되돌리는 길을 함께 말한다.</b>
    /// </summary>
    /// <remarks>
    /// 감춘 단추는 그려지지 않으므로 <b>길게 누를 수도 없다</b> — 이 창으로
    /// 돌아올 길이 없다. 말해 주지 않으면 「감췄더니 영영 사라졌다」가 되고,
    /// 그것을 되돌리려고 사람이 찾아가는 곳은 환경설정이 아니라 전화기다.
    /// </remarks>
    private async Task HideAsync()
    {
        _visible = false;

        if (_help)
        {
            await Boot.SetHelpDeskFabHiddenAsync(true);
            Toasts.Show("요청 등록 단추를 감췄습니다. 환경설정 › 휴대폰 에서 다시 켤 수 있습니다.");
            return;
        }

        await Boot.SetFabHiddenAsync(true);
        Toasts.Show("메뉴 단추를 감췄습니다. 메뉴는 왼쪽 위 로고로 열고, 환경설정 › 휴대폰 에서 다시 켤 수 있습니다.");
    }

    /// <summary>나머지 설정이 있는 환경설정 화면으로 간다.</summary>
    private Task OpenSettingsAsync()
    {
        _visible = false;

        if (SettingsHref is { Length: > 0 } href)
        {
            Navigation.NavigateTo(href);
        }

        return Task.CompletedTask;
    }

    private void OnMenuPositionChanged(string position)
    {
        _menuPosition = position;
        InvokeAsync(StateHasChanged);
    }

    private void OnHelpPositionChanged(string position)
    {
        _helpPosition = position;
        InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        Boot.FabPositionChanged -= OnMenuPositionChanged;
        Boot.HelpDeskFabPositionChanged -= OnHelpPositionChanged;

        _self?.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
            {
            }
        }
    }
}
