using JSini.Web.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

/// <summary>떠다니는 단추를 오래 누르면 뜨는 창. 셸은 한 줄만 적고 손짓은 이 부품이 스스로 건다 — 셸에 거면 단추 둘의 설정이 셸과 여기로 갈라진다. 값은 PortalBoot 한 곳에만 담기며 환경설정 화면과 같은 열쇠라 어느 쪽에서 고쳐도 다른 쪽이 따라온다.</summary>
public partial class FabHoldMenu : IAsyncDisposable
{
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>고를 수 있는 귀퉁이 넷. 차례가 화면의 차례다(위 둘 · 아래 둘). 환경설정 목록과 달리 「(기본)」을 붙이지 않는다 — 지금 자리에 표시가 붙으므로 기본값을 물을 일이 아니다.</summary>
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

    /// <summary>두 단추의 자리를 따로 들고 있는다 — 하나로 두면 메뉴 단추를 옮긴 뒤 요청 등록 단추를 길게 눌렀을 때 남의 자리에 표시가 붙는다. PortalBoot.ReadAsync 를 다시 부르지 않는다 — 회로마다 한 번 읽은 값을 돌려주므로 처음 그 값이다. 첫 그림에서 한 번 읽고 바뀔 때마다 오는 알림으로 따라간다 — 환경설정 화면에서 고친 것도 같은 알림을 타고 온다.</summary>
    private string _menuPosition = "bottom-left";

    private string _helpPosition = "bottom-right";

    /// <summary>지금 눌린 단추의 자리.</summary>
    private string Position => _help ? _helpPosition : _menuPosition;

    private string Title => _help ? "요청 등록 단추" : "메뉴 단추";

    private string Lead => _help
        ? "헬프데스크 요청 등록으로 바로 가는 단추입니다."
        : "메뉴를 여닫는 단추입니다.";

    /// <summary>환경설정 화면의 주소. 메뉴에 없으면(권한이 없으면) null. 주소를 글자로 박지 않는다 — @page 가 옮겨 가면 사본이 어긋나 누른 사람이 준비 중을 본다(PortalHome.SettingsRouteKey 머리말).</summary>
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

    /// <summary>브라우저가 길게 누른 단추를 알려 왔다 — menu 아니면 help. 모르는 값은 메뉴 단추로 본다 — 길게 눌렀는데 아무 일도 안 나는 것보다 낫다.</summary>
    [JSInvokable]
    public Task OpenForAsync(string kind)
    {
        _help = string.Equals(kind, "help", StringComparison.OrdinalIgnoreCase);
        _visible = true;

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>바깥을 눌렀거나 Esc 로 닫았다. 아무것도 고치지 않는다.</summary>
    private void OnVisibleChanged(bool visible) => _visible = visible;

    /// <summary>고른 귀퉁이로 옮긴다. 옮기면서 창을 닫는다 — 열어 두면 창이 화면 가운데를 덮어 옮겨 간 결과가 안 보인다.</summary>
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

    /// <summary>이 단추를 감춘다. 되돌리는 길을 함께 말한다 — 감춘 단추는 길게 누를 수도 없어 이 창으로 돌아올 길이 없다. 말하지 않으면 영영 사라진 것으로 읽힌다.</summary>
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
