using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Settings;

namespace JSini.Web.Funeral.Components.Pages;

public partial class EnvironmentSettingPage
{
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private HomePathClient Home { get; set; } = default!;

    // ── 홈 화면 ─────────────────────────────────────────────────
    //
    // **읽기는 왕복이 없다.** 담긴 값은 부트스트랩이 이미 실어 왔다
    // (`CurrentUser.HomePath`) — 여기서 다시 물으면 이 화면을 열 때마다
    // 게이트웨이를 한 번 더 다녀온다. 쓰기와 뒷정리는 `HomePathClient` 한
    // 자리에서 한다(같은 값이 서버·회로·2분짜리 통 셋에 있다).

    /// <summary>고르개가 켜 둘 줄. 담긴 값이 아니라 <b>목록의 값</b>이다.</summary>
    private string _homeChoice = PortalHome.NoneValue;

    /// <remarks>
    /// 부트스트랩이 <b>통에 있던 것</b>을 썼으면 이 시점에 이미 채워져 있고,
    /// 게이트웨이를 다녀와야 했으면 아직 비어 있다. 둘 다 받으려고 지금 한 번
    /// 읽고, 채워지면 알려 달라고 걸어 둔다(셸의 `Home.razor` 와 같은 수법).
    ///
    /// <para>
    /// 저장한 뒤에도 같은 알림이 온다(<c>CurrentUser.SetHomePath</c>) — 그때는
    /// 방금 고른 값이 그대로 돌아오므로 화면이 흔들리지 않는다.
    /// </para>
    /// </remarks>
    protected override void OnInitialized()
    {
        Me.Changed += OnMeChanged;
        _homeChoice = PortalHome.ToChoice(Me.HomePath);

        // 떠다니는 단추는 **이 화면 말고 다른 자리에서도 고쳐진다** — 그 단추를
        // 길게 누르면 그 자리에서 제 설정이 열린다(`FabHoldMenu`). 휴대폰에서는
        // 이 화면을 보는 **동안에도** 그 단추가 떠 있으므로, 안 듣고 있으면
        // 바로 옆에서 옮긴 자리를 아래 고르개가 **옛 값으로 말한다.**
        Boot.FabPositionChanged += OnFabPositionChangedFromElsewhere;
        Boot.FabHiddenChanged += OnFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabPositionChanged += OnHelpDeskFabPositionChangedFromElsewhere;
        Boot.HelpDeskFabHiddenChanged += OnHelpDeskFabHiddenChangedFromElsewhere;
    }

    public void Dispose()
    {
        Me.Changed -= OnMeChanged;

        Boot.FabPositionChanged -= OnFabPositionChangedFromElsewhere;
        Boot.FabHiddenChanged -= OnFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabPositionChanged -= OnHelpDeskFabPositionChangedFromElsewhere;
        Boot.HelpDeskFabHiddenChanged -= OnHelpDeskFabHiddenChangedFromElsewhere;
    }

    // 이 화면이 고친 것도 같은 알림을 타고 돌아온다 — 그때는 방금 고른 값이
    // 그대로 오므로 화면이 흔들리지 않는다(위 `OnMeChanged` 와 같은 꼴).

    private void OnFabPositionChangedFromElsewhere(string position) =>
        Apply(() => _selectedFabPosition = position);

    private void OnFabHiddenChangedFromElsewhere(bool hidden) =>
        Apply(() => _fabHidden = hidden);

    private void OnHelpDeskFabPositionChangedFromElsewhere(string position) =>
        Apply(() => _selectedHelpDeskFabPosition = position);

    private void OnHelpDeskFabHiddenChangedFromElsewhere(bool hidden) =>
        Apply(() => _helpDeskFabHidden = hidden);

    private void Apply(Action change) => _ = InvokeAsync(() =>
    {
        change();
        StateHasChanged();
    });

    private void OnMeChanged() => _ = InvokeAsync(() =>
    {
        _homeChoice = PortalHome.ToChoice(Me.HomePath);
        StateHasChanged();
    });

    /// <summary>이 목록을 만들 때 본 메뉴 트리. 참조로 비교한다.</summary>
    private IReadOnlyList<MenuNode>? _homeSource;

    private IReadOnlyList<PortalHomeChoice> _homeChoices = [];

    /// <summary>
    /// 고르개에 놓을 것들. <b>메뉴 트리가 실제로 바뀌었을 때만 다시 만든다</b> —
    /// 까닭은 아래 <see cref="NavChoices"/> 와 같다(렌더마다 새 참조를 주면
    /// DevExpress 고르개가 열어 둔 목록을 닫고 친 글자를 버린다).
    /// </summary>
    private IReadOnlyList<PortalHomeChoice> HomeChoices
    {
        get
        {
            if (!ReferenceEquals(_homeSource, Menus.VisibleMenus))
            {
                _homeSource = Menus.VisibleMenus;
                _homeChoices = PortalHome.Choices(_homeSource);
            }

            return _homeChoices;
        }
    }

    /// <summary>
    /// 첫 화면을 바꿨다. <b>그 자리에서 저장한다</b> — 이 판에는 「저장」
    /// 단추가 없다(아래 띠 고르개와 같다).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 판에서 <b>서버까지 가는 유일한 조작</b>이라 <see cref="DataPage.RunAsync"/>
    /// 로 감싼다. 나머지는 브라우저 저장소에 적는 일이라(<c>PortalBoot</c>) 실패할
    /// 자리가 없다. 안 감싸면 게이트웨이가 답하지 않을 때 이벤트 처리기에서
    /// 예외가 튀고, 그러면 <b>회로가 끊겨 화면 전체가 「연결 끊김」이 된다</b>
    /// (web/CLAUDE.md).
    /// </para>
    /// <para>
    /// <b>성공했다고 말한다.</b> 여느 저장과 달리 바뀐 것이 화면에 하나도 안
    /// 보이고(다음 로그인에야 드러난다) 단추를 누른 것도 아니라서, 아무 말이
    /// 없으면 저장됐는지 알 길이 없다.
    /// </para>
    /// <para>
    /// 실패하면 <b>고르개를 되돌린다.</b> 고른 채로 두면 화면은 그 화면으로
    /// 간다고 말하는데 서버는 모르는 상태가 되고, 사람은 다음 로그인에야 안다.
    /// </para>
    /// </remarks>
    private async Task OnHomeChangedAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == _homeChoice)
        {
            return;
        }

        var previous = _homeChoice;
        _homeChoice = path;

        var stored = PortalHome.ToStored(path);

        var saved = await RunAsync(
            () => Home.SaveAsync(stored),
            stored is null
                ? "홈 화면 지정을 풀었습니다. 다음 로그인부터 이 화면이 열립니다."
                : "홈 화면을 저장했습니다. 다음 로그인부터 고르신 화면이 열립니다.",
            "홈 화면을 저장하지 못했습니다");

        if (!saved)
        {
            _homeChoice = previous;
        }
    }

    private sealed record FabPosOption(string Value, string Name);

    private static readonly IReadOnlyList<FabPosOption> _fabPositions =
    [
        new("bottom-left", "왼쪽 아래 (기본)"),
        new("top-left", "왼쪽 위"),
        new("bottom-right", "오른쪽 아래"),
        new("top-right", "오른쪽 위"),
    ];

    /// <summary>
    /// 토스트가 뜰 자리 여섯. <b>가운데 둘이 더 있다</b> — FAB 과 달리
    /// 토스트는 가로로 넓어서 네 귀퉁이만으로는 가릴 것을 못 피하는 화면이 있다.
    /// 값은 <see cref="PortalBoot.NormalizeToastPosition"/> 이 아는 것과 같아야 한다.
    /// </summary>
    private static readonly IReadOnlyList<FabPosOption> _toastPositions =
    [
        new("top-left", "왼쪽 위"),
        new("top-center", "가운데 위"),
        new("top-right", "오른쪽 위"),
        new("bottom-left", "왼쪽 아래"),
        new("bottom-center", "가운데 아래"),
        new("bottom-right", "오른쪽 아래 (기본)"),
    ];

    private string _selectedToastPosition = "bottom-right";

    private async Task OnToastPositionChangedAsync(string value)
    {
        _selectedToastPosition = PortalBoot.NormalizeToastPosition(value);
        await Boot.SetToastPositionAsync(_selectedToastPosition);
    }

    private string _selectedFabPosition = "bottom-left";

    /// <summary>떠다니는 단추를 감춰 두었는가. 기본은 <c>false</c> — 보인다.</summary>
    private bool _fabHidden;

    private bool _fabLoaded;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || _fabLoaded)
        {
            return;
        }

        _fabLoaded = true;
        var state = await Boot.ReadAsync();
        _selectedFabPosition = state.FabPosition;
        _fabHidden = state.FabHidden;
        _selectedHelpDeskFabPosition = state.HelpDeskFabPosition;
        _helpDeskFabHidden = state.HelpDeskFabHidden;
        _selectedToastPosition = state.ToastPosition;
        _zoomUnlocked = state.ZoomUnlocked;
        _bottomNavHidden = state.BottomNavHidden;
        _navItems = [.. BottomNav.Parse(state.BottomNavItemsJson)];
        StateHasChanged();
    }

    private async Task OnFabPositionChangedAsync(string value)
    {
        _selectedFabPosition = PortalBoot.NormalizeFabPosition(value);
        await Boot.SetFabPositionAsync(_selectedFabPosition);
    }

    private async Task OnFabHiddenChangedAsync(bool value)
    {
        _fabHidden = value;
        await Boot.SetFabHiddenAsync(value);
    }

    // ── 헬프데스크 요청 등록 단추 ───────────────────────────────
    //
    // 메뉴 단추와 **같은 두 가지**(감출까 · 어느 귀퉁이에)를 다룬다. 다른
    // 점은 하나 — **아무나 볼 수 있는 줄이 아니다.**

    /// <summary>요청 등록 단추가 여는 화면. 권한을 묻는 열쇠도 이 경로다.</summary>
    private const string HelpDeskNewPath = "/helpdesk/request/new";

    /// <summary>
    /// 이 사람에게 그 두 줄을 보여도 되는가 — <c>/helpdesk/request/new</c> 를
    /// 열 수 있는 사람만이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>셸과 같은 판정을 쓴다</b>(<c>MainLayout.HelpDeskFabAllowed</c>). 갈라
    /// 두면 「설정은 있는데 단추가 안 나온다」거나 그 반대가 생기고, 둘 다
    /// 사람 눈에는 <b>설정이 안 먹는 것</b>으로 보인다.
    /// </para>
    /// <para>
    /// <b>값을 들고 있지 않고 물을 때마다 묻는다.</b> 권한표는 부트스트랩이
    /// 실어 오므로 이 화면이 그려지는 시점엔 대개 와 있지만, 아직이면
    /// <see cref="OnAfterRenderAsync"/> 의 다시 그리기에서 살아난다.
    /// </para>
    /// </remarks>
    private bool CanSetHelpDeskFab =>
        Permissions.IsLoaded && Permissions.CanView(HelpDeskNewPath);

    /// <summary>
    /// 고를 수 있는 귀퉁이 넷. <b>메뉴 단추와 같고 「(기본)」이 붙는 줄만 다르다</b> —
    /// 이쪽 기본은 오른쪽 아래다(<see cref="PortalBoot.HelpDeskFabPositionKey"/>).
    /// 목록을 나눠 둔 것은 그 한 글자 때문이다. 같은 목록을 함께 쓰면 두 고르개가
    /// <b>서로 다른 값을 「기본」이라고 말한다.</b>
    /// </summary>
    private static readonly IReadOnlyList<FabPosOption> _helpDeskFabPositions =
    [
        new("bottom-right", "오른쪽 아래 (기본)"),
        new("bottom-left", "왼쪽 아래"),
        new("top-left", "왼쪽 위"),
        new("top-right", "오른쪽 위"),
    ];

    private string _selectedHelpDeskFabPosition = "bottom-right";

    /// <summary>요청 등록 단추를 감춰 두었는가. 기본은 <c>false</c> — 보인다.</summary>
    private bool _helpDeskFabHidden;

    private async Task OnHelpDeskFabPositionChangedAsync(string value)
    {
        _selectedHelpDeskFabPosition = PortalBoot.NormalizeHelpDeskFabPosition(value);
        await Boot.SetHelpDeskFabPositionAsync(_selectedHelpDeskFabPosition);
    }

    private async Task OnHelpDeskFabHiddenChangedAsync(bool value)
    {
        _helpDeskFabHidden = value;
        await Boot.SetHelpDeskFabHiddenAsync(value);
    }

    /// <summary>
    /// 확대 잠금을 <b>풀어 두었는가</b>. 기본은 <c>false</c> — 즉 잠근다.
    /// 화면의 스위치는 이것을 뒤집어 보인다.
    /// </summary>
    private bool _zoomUnlocked;

    /// <summary>
    /// 스위치가 말하는 것은 <b>「잠글까」</b>이고 저장되는 것은 <b>「풀었나」</b>다.
    /// <b>뒤집는 자리를 여기 하나로 둔다</b> — 화면 쪽과 저장 쪽 양쪽에서
    /// 뒤집으면 한쪽만 고치는 날 스위치가 거꾸로 선다.
    /// </summary>
    private async Task OnZoomLockedChangedAsync(bool locked)
    {
        _zoomUnlocked = !locked;
        await Boot.SetZoomUnlockedAsync(_zoomUnlocked);
    }

    // ── 휴대폰 아래 띠 ──────────────────────────────────────────

    /// <summary>띠를 <b>쓰지 않기로</b> 했는가. 화면의 스위치는 이것을 뒤집어 보인다.</summary>
    private bool _bottomNavHidden;

    /// <summary>띠에 세워 둔 칸들. 고치는 자리라 <b>읽기 전용이 아니다.</b></summary>
    private List<BottomNavItem> _navItems = [.. BottomNav.Defaults];

    /// <summary>이 목록을 만들 때 본 메뉴 트리. 참조로 비교한다.</summary>
    private IReadOnlyList<MenuNode>? _choiceSource;

    private IReadOnlyList<BottomNavChoice> _navChoices = [];

    /// <summary>
    /// 고르개에 놓을 것들.
    /// </summary>
    /// <remarks>
    /// <b>메뉴 트리가 실제로 바뀌었을 때만 다시 만든다.</b> 렌더마다 만들면
    /// 179건을 훑은 결과가 매번 새 참조가 되고, DevExpress 고르개는 자료가
    /// 다른 것으로 바뀌면 열어 둔 목록을 닫고 친 글자를 버린다 — 사이드바
    /// 트리에서 이미 밟은 함정이다(web/CLAUDE.md).
    ///
    /// <para>
    /// 속성으로 둔 이유는 <b>메뉴가 늦게 올 수 있어서</b>다. 부트스트랩이
    /// 끝나기 전에 그리면 고를 것이 홈과 설정 둘뿐인 목록이 굳는다.
    /// </para>
    /// </remarks>
    private IReadOnlyList<BottomNavChoice> NavChoices
    {
        get
        {
            if (!ReferenceEquals(_choiceSource, Menus.VisibleMenus))
            {
                _choiceSource = Menus.VisibleMenus;
                _navChoices = BottomNav.Choices(_choiceSource);
            }

            return _navChoices;
        }
    }

    private async Task OnBottomNavUsedChangedAsync(bool used)
    {
        _bottomNavHidden = !used;
        await Boot.SetBottomNavHiddenAsync(_bottomNavHidden);
    }

    /// <summary>
    /// 이 줄이 가리킬 메뉴를 바꿨다. <b>이름도 함께 갈아 준다</b> — 메뉴를
    /// 바꾸는 사람은 그 메뉴의 이름을 바라지, 앞 메뉴에 붙여 둔 이름을
    /// 바라지 않는다. 마음에 안 들면 옆 칸에서 고친다.
    /// </summary>
    /// <remarks>
    /// 아이콘은 <b>비운다.</b> 기본 다섯이 들고 있던 것(<c>jsini-icon-*</c>)을
    /// 그대로 두면 메뉴를 바꿔도 옛 그림이 남는다 — 비워 두면 그릴 때마다
    /// 고른 메뉴에서 찾는다(<see cref="BottomNav.IconClass"/>).
    /// </remarks>
    private async Task OnNavMenuChangedAsync(int index, string? path)
    {
        if (index >= _navItems.Count || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var choice = NavChoices.FirstOrDefault(c =>
            string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));

        _navItems[index] = new BottomNavItem
        {
            Path = path,
            RouteKey = choice?.RouteKey,
            Title = choice?.Title ?? _navItems[index].Title,
        };

        await SaveNavAsync();
    }

    private async Task OnNavTitleChangedAsync(int index, string? text)
    {
        if (index >= _navItems.Count)
        {
            return;
        }

        var title = (text ?? string.Empty).Trim();

        // 비우면 저장할 때 걸러진다(`BottomNav.Parse`) — 그러면 지운 적 없는
        // 칸이 다음 새로고침에 조용히 사라진다. 빈 이름은 그냥 무른다.
        if (title.Length == 0 || title == _navItems[index].Title)
        {
            StateHasChanged();
            return;
        }

        _navItems[index] = _navItems[index] with { Title = title };
        await SaveNavAsync();
    }

    /// <summary>
    /// 칸 하나를 뺀다. <b><see cref="BottomNav.MinItems"/> 에서 멈춘다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 바닥에 닿으면 화면이 빼기 단추를 잠그므로 여기까지 오지 않지만,
    /// 판정을 양쪽에 두는 것은 「추가」와 같다(천장은 단추의 <c>Enabled</c> 와
    /// <see cref="OnNavAddAsync"/> 가 함께 본다).
    /// </para>
    /// <para>
    /// <b>다 빼서 띠를 없애는 길은 없다.</b> 전에는 마지막 칸을 빼면 기본값으로
    /// 되돌렸는데, 바닥이 생긴 지금은 그 자리에 닿지 않는다 — 띠를 없애려면
    /// 위 스위치를 끈다.
    /// </para>
    /// </remarks>
    private async Task OnNavRemoveAsync(int index)
    {
        if (index >= _navItems.Count || _navItems.Count <= BottomNav.MinItems)
        {
            return;
        }

        _navItems.RemoveAt(index);

        await SaveNavAsync();
    }

    /// <summary>
    /// 칸의 차례를 한 자리 옮긴다. <b>옮긴 즉시 저장한다</b> — 이 판에는
    /// 「저장」 단추가 없고 고르개·이름·빼기가 모두 그 자리에서 저장한다.
    /// </summary>
    /// <remarks>
    /// 목록의 차례가 곧 띠의 차례다 — <b>위가 왼쪽</b>이다
    /// (<see cref="BottomNav.Move"/>). 끝에서는 단추가 잠겨 있어 여기까지
    /// 오지 않지만, 못 옮겼으면 저장도 하지 않는다.
    /// </remarks>
    private async Task OnNavMoveAsync(int index, int delta)
    {
        if (_bottomNavHidden || !BottomNav.Move(_navItems, index, delta))
        {
            return;
        }

        await SaveNavAsync();
    }

    /// <summary>
    /// 칸을 하나 더한다. <b>아직 안 쓴 것 중 첫 번째</b>로 채워 둔다 —
    /// 빈 줄로 두면 고르기 전까지 저장할 수 없는 줄이 하나 생기고, 그 자리에서
    /// 사람은 「추가가 안 먹는다」고 읽는다.
    /// </summary>
    private async Task OnNavAddAsync()
    {
        if (_navItems.Count >= BottomNav.MaxItems)
        {
            return;
        }

        var used = _navItems.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pick = NavChoices.FirstOrDefault(c => !used.Contains(c.Path)) ?? NavChoices.FirstOrDefault();

        if (pick is null)
        {
            return;
        }

        await AddAsync(pick);
    }

    /// <summary>
    /// 메뉴가 아닌 것(홈 · 설정 서랍 · 메뉴 단추 · 프로필 아바타)을 바로
    /// 넣을 수 있는가. <b>이미 놓았으면 잠근다</b> — 같은 칸이 둘이면 띠에
    /// 같은 그림이 나란히 서고, 얼마 안 되는 칸 하나를 헛되이 쓴다.
    /// </summary>
    private bool CanQuickAdd(BottomNavChoice choice) =>
        !_bottomNavHidden
        && _navItems.Count < BottomNav.MaxItems
        && !_navItems.Any(i => string.Equals(
            i.Path, choice.Path, StringComparison.OrdinalIgnoreCase));

    /// <summary>「바로 넣기」 단추. 고르개를 열지 않고 칸을 하나 붙인다.</summary>
    private async Task OnNavQuickAddAsync(BottomNavChoice choice)
    {
        if (!CanQuickAdd(choice))
        {
            return;
        }

        await AddAsync(choice);
    }

    /// <summary>
    /// 칸을 하나 붙이고 저장한다. <b>아이콘은 담지 않는다</b> — 그릴 때마다
    /// 찾으므로(<see cref="BottomNav.IconClass"/>) 관리자가 메뉴 아이콘을
    /// 바꾸면 띠도 따라간다.
    /// </summary>
    private async Task AddAsync(BottomNavChoice choice)
    {
        _navItems.Add(new BottomNavItem
        {
            Path = choice.Path,
            RouteKey = choice.RouteKey,
            Title = choice.Title,
        });

        await SaveNavAsync();
    }

    /// <summary>
    /// 고른 것을 버리고 기본값으로(지금은 다섯). <b>열쇠를 지운다</b> —
    /// 기본값을 글자로 적어 두면 나중에 기본이 바뀌어도 그 사람만 옛것에 묶인다.
    /// </summary>
    private async Task OnNavResetAsync()
    {
        _navItems = [.. BottomNav.Defaults];
        await Boot.SetBottomNavItemsAsync(null);
    }

    private Task SaveNavAsync() => Boot.SetBottomNavItemsAsync(BottomNav.Serialize(_navItems));

    /// <summary>
    /// 칸 수를 적는 한 줄 — 「5 / 3~8칸」.
    /// </summary>
    /// <remarks>
    /// <b>값이라 남긴다.</b> 설명을 걷어내는 규칙(web/CLAUDE.md)에서 남기는
    /// 일곱 중 하나다 — 추가·빼기 단추가 어느 자리에서 왜 잠기는지를 말해 주는
    /// 것이 이 숫자뿐이고, 없으면 잠긴 단추가 고장으로 읽힌다.
    /// </remarks>
    private string NavCountText =>
        $"{_navItems.Count} / {BottomNav.MinItems}~{BottomNav.MaxItems}칸";
}
