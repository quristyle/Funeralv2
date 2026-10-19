using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Settings;
using JSini.Web.Models;

namespace JSini.Web.Funeral.Components.Pages;

public partial class EnvironmentSettingPage
{
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private HomePathClient Home { get; set; } = default!;
    [Inject] private HeaderIconManager HeaderIcons { get; set; } = default!;

    // ── 홈 화면 ─────────────────────────────────────────────────
    // 읽기는 부트스트랩이 실어 온 값을 쓴다(왕복 없음). 쓰기와 뒷정리는 HomePathClient 한 자리에서 한다.

    /// <summary>고르개가 켜 둘 줄. 담긴 값이 아니라 목록의 값이다.</summary>
    private string _homeChoice = PortalHome.NoneValue;

    protected override void OnInitialized()
    {
        Me.Changed += OnMeChanged;
        _homeChoice = PortalHome.ToChoice(Me.HomePath);

        HeaderIcons.Changed += OnHeaderIconsChanged;

        // 떠다니는 단추는 다른 자리(FabHoldMenu)에서도 고쳐진다. 휴대폰에서는 이 화면을 보는 동안에도 떠 있으니 듣지 않으면 옛 값을 말한다.
        Boot.FabPositionChanged += OnFabPositionChangedFromElsewhere;
        Boot.FabHiddenChanged += OnFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabPositionChanged += OnHelpDeskFabPositionChangedFromElsewhere;
        Boot.HelpDeskFabHiddenChanged += OnHelpDeskFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabDesktopChanged += OnHelpDeskFabDesktopChangedFromElsewhere;
    }

    public void Dispose()
    {
        Me.Changed -= OnMeChanged;
        HeaderIcons.Changed -= OnHeaderIconsChanged;

        Boot.FabPositionChanged -= OnFabPositionChangedFromElsewhere;
        Boot.FabHiddenChanged -= OnFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabPositionChanged -= OnHelpDeskFabPositionChangedFromElsewhere;
        Boot.HelpDeskFabHiddenChanged -= OnHelpDeskFabHiddenChangedFromElsewhere;
        Boot.HelpDeskFabDesktopChanged -= OnHelpDeskFabDesktopChangedFromElsewhere;
    }

    // 이 화면이 고친 것도 같은 알림을 타고 돌아온다(방금 고른 값이라 흔들리지 않는다).

    private void OnFabPositionChangedFromElsewhere(string position) =>
        Apply(() => _selectedFabPosition = position);

    private void OnFabHiddenChangedFromElsewhere(bool hidden) =>
        Apply(() => _fabHidden = hidden);

    private void OnHelpDeskFabPositionChangedFromElsewhere(string position) =>
        Apply(() => _selectedHelpDeskFabPosition = position);

    private void OnHelpDeskFabHiddenChangedFromElsewhere(bool hidden) =>
        Apply(() => _helpDeskFabHidden = hidden);

    private void OnHelpDeskFabDesktopChangedFromElsewhere(bool shown) =>
        Apply(() => _helpDeskFabDesktop = shown);

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

    private void OnHeaderIconsChanged() => _ = InvokeAsync(StateHasChanged);

    /// <summary>현재 사용자에게 권한이 부여된 상단 아이콘 목록.</summary>
    private IReadOnlyList<LayoutHeaderIconDto> PermittedHeaderIcons =>
        HeaderIcons.GetPermittedIcons(Permissions);

    private async Task OnHeaderIconToggledAsync(string iconKey, bool enabled)
    {
        await HeaderIcons.SetIconEnabledAsync(iconKey, enabled);
    }

    /// <summary>이 목록을 만들 때 본 메뉴 트리. 참조로 비교한다.</summary>
    private IReadOnlyList<MenuNode>? _homeSource;

    private IReadOnlyList<PortalHomeChoice> _homeChoices = [];

    /// <summary>고르개에 놓을 것들. 메뉴 트리가 실제로 바뀌었을 때만 다시 만든다 — 매 렌더마다 새 참조를 주면 DevExpress 고르개가 열어 둔 목록을 닫고 친 글자를 버린다.</summary>
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

    /// <summary>첫 화면을 바꿨다. 이 판에는 저장 단추가 없어 그 자리에서 저장한다. 서버까지 가는 유일한 조작이라 RunAsync 로 감싸고, 실패하면 고르개를 되돌린다.
    /// 성공하면 말한다 — 바뀐 것이 화면에 안 보여 아무 말이 없으면 저장됐는지 알 수 없다.</summary>
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

    /// <summary>토스트가 뜰 자리. 가운데 옵션이 더 있다 — 토스트는 가로로 넓어 네 귀퉁이로는 가릴 것을 못 피하는 화면이 있다. 값은 PortalBoot.NormalizeToastPosition 과 같아야 한다.</summary>
    private static readonly IReadOnlyList<FabPosOption> _toastPositions =
    [
        new("top-left", "왼쪽 위"),
        new("top-center", "가운데 위"),
        new("top-right", "오른쪽 위"),
        new("top-center-1-5", "1/5 위치 상단중앙"),
        new("top-center-1-4", "1/4 위치 상단중앙"),
        new("top-center-1-3", "1/3 위치 상단중앙"),
        new("center", "화면 중앙"),
        new("bottom-center-2-3", "2/3 위치 하단중앙"),
        new("bottom-center-1-4", "1/4 위치 하단중앙"),
        new("bottom-center-1-5", "1/5 위치 하단중앙"),
        new("bottom-left", "왼쪽 아래"),
        new("bottom-center", "가운데 아래"),
        new("bottom-right", "오른쪽 아래 (기본)"),
    ];

    private string _selectedToastPosition = "bottom-right";

    private async Task OnToastPositionChangedAsync(string value)
    {
        _selectedToastPosition = PortalBoot.NormalizeToastPosition(value);
        await Boot.SetToastPositionAsync(_selectedToastPosition);
        Say("알림이 표시될 위치입니다.");
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
        _helpDeskFabDesktop = state.HelpDeskFabDesktop;
        _selectedToastPosition = state.ToastPosition;
        _zoomUnlocked = state.ZoomUnlocked;
        _bottomNavHidden = state.BottomNavHidden;
        _bottomNavLabelsHidden = state.BottomNavLabelsHidden;
        _bottomNavDesktopHidden = state.BottomNavDesktopHidden;
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
    // 메뉴 단추와 같은 두 가지(감출까·어느 귀퉁이에)를 다루되, 아무나 볼 수 있는 줄이 아니다.

    /// <summary>요청 등록 단추가 여는 화면. 권한을 묻는 열쇠도 이 경로다.</summary>
    private const string HelpDeskNewPath = "/helpdesk/request/new";

    /// <summary>이 사람에게 그 줄을 보여도 되는가. 셸의 MainLayout.HelpDeskFabAllowed 와 같은 판정을 쓴다 — 갈라 두면 설정은 있는데 단추가 안 나오는 일이 생긴다.
    /// 값을 들고 있지 않고 물을 때마다 묻는다.</summary>
    private bool CanSetHelpDeskFab =>
        Permissions.IsLoaded && Permissions.CanView(HelpDeskNewPath);

    /// <summary>고를 수 있는 귀퉁이 넷. 메뉴 단추와 같고 「(기본)」이 붙는 줄만 다르다 — 같은 목록을 쓰면 두 고르개가 서로 다른 값을 기본이라 말한다.</summary>
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

    /// <summary>넓은 화면에도 띄울지. 기본은 false — 휴대폰에서만 뜬다.</summary>
    private bool _helpDeskFabDesktop;

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

    /// <summary>넓은 화면 띄우기 설정을 바꿨다. 감춰 둔 동안에는 이 줄이 잠겨 있다 — 켜 두어도 단추가 안 나오면 「설정이 안 먹는다」로 읽힌다.</summary>
    private async Task OnHelpDeskFabDesktopChangedAsync(bool value)
    {
        _helpDeskFabDesktop = value;
        await Boot.SetHelpDeskFabDesktopAsync(value);
    }

    /// <summary>확대 잠금을 풀어 두었는가. 기본은 false, 즉 잠근다. 화면 스위치는 이것을 뒤집어 보인다.</summary>
    private bool _zoomUnlocked;

    /// <summary>스위치는 「잠글까」를 말하고 저장되는 것은 「풀었나」다. 뒤집는 자리는 여기 하나로 둔다 — 두 곳에서 뒤집으면 한쪽만 고치는 날 스위치가 거꾸로 선다.</summary>
    private async Task OnZoomLockedChangedAsync(bool locked)
    {
        _zoomUnlocked = !locked;
        await Boot.SetZoomUnlockedAsync(_zoomUnlocked);
    }

    // ── 휴대폰 아래 띠 ──────────────────────────────────────────

    /// <summary>띠를 <b>쓰지 않기로</b> 했는가. 화면의 스위치는 이것을 뒤집어 보인다.</summary>
    private bool _bottomNavHidden;

    /// <summary>그 칸에서 이름을 감추기로 했는가. 화면 스위치는 이것을 그대로 보인다(「…감추기」라고 묻으므로 뒤집지 않는다).</summary>
    private bool _bottomNavLabelsHidden;

    /// <summary>같은 칸들을 넓은 화면 헤더에 세우지 않기로 했는가. 화면 스위치는 뒤집어 보인다.</summary>
    private bool _bottomNavDesktopHidden;

    /// <summary>아래 칸 고르개를 통째로 잠글 것인가. 둘 다 꺼야 잠근다 — 휴대폰 스위치 하나로 잠그면 헤더에 선 칸이 있을 때 판정이 틀려 고르개가 영영 회색으로 남는다.</summary>
    private bool NavLocked => _bottomNavHidden && _bottomNavDesktopHidden;

    /// <summary>띠에 세워 둔 칸들. 고치는 자리라 <b>읽기 전용이 아니다.</b></summary>
    private List<BottomNavItem> _navItems = [.. BottomNav.Defaults];

    /// <summary>이 목록을 만들 때 본 메뉴 트리. 참조로 비교한다.</summary>
    private IReadOnlyList<MenuNode>? _choiceSource;

    private IReadOnlyList<BottomNavChoice> _navChoices = [];

    /// <summary>고르개에 놓을 것들. 메뉴가 늦게 올 수 있어 속성으로 둔다 — 부트스트랩 전에 그리면 고를 것이 홈과 설정뿐인 목록이 굳는다.</summary>
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

    /// <summary>띠 칸의 이름 감추기를 바꿨다. 띠를 안 쓰면 이 줄이 잠겨 있다 — 아무 일도 안 하는 스위치는 「안 먹는다」로 읽힌다.</summary>
    private async Task OnBottomNavLabelsHiddenChangedAsync(bool hidden)
    {
        _bottomNavLabelsHidden = hidden;
        await Boot.SetBottomNavLabelsHiddenAsync(hidden);
    }

    /// <summary>같은 칸들을 넓은 화면 헤더에 세울지를 바꿨다. 휴대폰 스위치와 따로 산다 — 둘은 서로 다른 이유로 거슬린다.</summary>
    private async Task OnBottomNavDesktopUsedChangedAsync(bool used)
    {
        _bottomNavDesktopHidden = !used;
        await Boot.SetBottomNavDesktopHiddenAsync(_bottomNavDesktopHidden);
    }

    /// <summary>이 줄이 가리킬 메뉴를 바꿨다. 이름도 함께 갈아 준다. 아이콘은 비운다 — 기본 아이콘을 그대로 두면 메뉴를 바꿔도 옛 그림이 남는다.</summary>
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

    /// <summary>칸 하나를 뺀다. 바닥은 BottomNav.MinItems 다. 다 빼서 띠를 없앨 길은 없다 — 띠를 없애려면 위 스위치를 끈다.</summary>
    private async Task OnNavRemoveAsync(int index)
    {
        if (index >= _navItems.Count || _navItems.Count <= BottomNav.MinItems)
        {
            return;
        }

        _navItems.RemoveAt(index);

        await SaveNavAsync();
    }

    /// <summary>칸의 차례를 한 자리 옮긴다. 옮긴 즉시 저장한다. 위가 왼쪽이다(BottomNav.Move).</summary>
    private async Task OnNavMoveAsync(int index, int delta)
    {
        if (NavLocked || !BottomNav.Move(_navItems, index, delta))
        {
            return;
        }

        await SaveNavAsync();
    }

    /// <summary>칸을 하나 더한다. 아직 안 쓴 것 중 첫 번째로 채운다 — 빈 줄로 두면 고르기 전까지 저장할 수 없는 줄이 생긴다.</summary>
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

    /// <summary>메뉴가 아닌 것(홈·설정 서랍·메뉴 단추·프로필 아바타)을 바로 넣을 수 있는가. 이미 놓았으면 잠근다 — 같은 칸이 둘이면 띠에 같은 그림이 나란히 선다.</summary>
    private bool CanQuickAdd(BottomNavChoice choice) =>
        !NavLocked
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

    /// <summary>칸을 하나 붙이고 저장한다. 아이콘은 담지 않는다 — 그릴 때 찾으므로 관리자가 아이콘을 바꾸면 띠도 따라간다.</summary>
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

    /// <summary>고른 것을 버리고 기본값으로. 열쇠를 지운다 — 기본값을 글자로 적어 두면 기본이 바뀌어도 그 사람만 옛것에 묶인다.</summary>
    private async Task OnNavResetAsync()
    {
        _navItems = [.. BottomNav.Defaults];
        await Boot.SetBottomNavItemsAsync(null);
    }

    private Task SaveNavAsync() => Boot.SetBottomNavItemsAsync(BottomNav.Serialize(_navItems));

    /// <summary>칸 수를 적는 한 줄(예: 5 / 3~8칸). 잠긴 추가·빼기 단추가 왜 잠겼는지를 말해 주는 유일한 자리라 남긴다.</summary>
    private string NavCountText =>
        $"{_navItems.Count} / {BottomNav.MinItems}~{BottomNav.MaxItems}칸";
}
