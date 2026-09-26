using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Menu;
using Microsoft.AspNetCore.WebUtilities;

namespace JSini.Web.Admin.Components.Pages;

public partial class Profile
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private MenuFavorites Favorites { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;

    /// <summary>사진은 서른 장까지. Vue 의 <c>:limit="30"</c> 을 그대로 옮겼다.</summary>
    private const int PhotoLimit = 30;

    /// <summary>
    /// 탭. <b>선언 순서가 <c>DxTabs</c> 안의 <c>DxTabPage</c> 순서와 같아야 한다</b> —
    /// 그 부품은 자리를 번호로 다루므로 둘이 어긋나면 <c>?tab=</c> 이 엉뚱한 탭을 연다.
    /// </summary>
    private enum Tab { Basic, Account, FixedTabs, Security, Password, Notice, Avatar }

    /// <summary><c>?tab=</c> 로 받는 이름. Vue 가 쓰던 글자와 같다.</summary>
    private static readonly Dictionary<string, Tab> TabNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["basic"] = Tab.Basic,
        ["account"] = Tab.Account,
        ["fixedTabs"] = Tab.FixedTabs,
        ["security"] = Tab.Security,
        ["password"] = Tab.Password,
        ["notice"] = Tab.Notice,
        ["avatar"] = Tab.Avatar,
    };

    private Tab _tab = Tab.Basic;

    /// <summary><c>DxTabs</c> 가 쓰는 번호. 열거형 값이 곧 자리다.</summary>
    private int TabIndex => (int)_tab;

    /// <summary>
    /// 휴대폰 폭인가(≤767px). <c>DxLayoutBreakpoint</c> 가 알려 준다.
    ///
    /// <para>
    /// <b>첫 그림에서는 언제나 <c>false</c> 다.</b> 서버가 그릴 때는 화면
    /// 크기를 모르므로 데스크톱 배치가 한 번 나가고, 회로가 붙은 뒤 이 값이
    /// 갈리며 다시 그려진다. 배치만 갈리는 일이라 그대로 둔다.
    /// </para>
    /// </summary>
    private bool _isPhone;

    /// <summary>
    /// 고정탭의 두 판이 위아래로 쌓였는가(≤991px). 휴대폰 경계와 <b>다른
    /// 값</b>인 이유는 위 <c>DxLayoutBreakpoint</c> 옆에 적어 두었다.
    /// </summary>
    private bool _isPinsStacked;

    /// <summary>
    /// <c>DxTabs</c> 의 탭 머리에 적는 이름. 휴대폰에서는 짧은 쪽을 쓴다.
    ///
    /// <para>
    /// 위로 올라간 탭 머리는 폭을 일곱이 나눠 쓴다. 「프로필 사진 관리」를
    /// 그대로 두면 한 탭이 화면 절반을 차지하고 나머지 여섯은 쓸어 넘겨야만
    /// 닿는다 — <b>지금 어느 탭에 있는지조차 안 보인다.</b>
    /// </para>
    ///
    /// <para>
    /// 그 줄을 결국 통째로 감추고 목록(<c>ad-hub</c>)으로 바꿨으므로
    /// (이 파일 머리말) 휴대폰에서 이 이름이 보이는 자리는 지금 없다.
    /// 지우지 않는 이유는 <see cref="ShortTabText"/> 에 적어 두었다.
    /// </para>
    /// </summary>
    private string TabText(Tab tab) => _isPhone ? ShortTabText(tab) : LongTabText(tab);

    /// <summary>제 이름. 목록(<c>ad-hub</c>)과 되돌아가는 줄도 이것을 쓴다.</summary>
    private static string LongTabText(Tab tab) => tab switch
    {
        Tab.Basic => "기본 설정",
        Tab.Account => "계정 정보",
        Tab.FixedTabs => "고정탭 관리",
        Tab.Security => "보안 설정",
        Tab.Password => "비밀번호 변경",
        Tab.Notice => "새 메시지 알림",
        _ => "프로필 사진 관리",
    };

    /// <summary>
    /// 줄인 이름. 휴대폰에서는 탭 머리를 감추므로(admin.css) 지금은 보이는
    /// 자리가 없지만, <b>CSS 가 안 실렸을 때를 위해 남겨 둔다</b> — 그때
    /// 드러나는 것은 위로 올라간 탭 머리고 거기서는 긴 이름이 안 들어간다.
    /// </summary>
    private static string ShortTabText(Tab tab) => tab switch
    {
        Tab.Basic => "기본",
        Tab.Account => "계정",
        Tab.FixedTabs => "고정탭",
        Tab.Security => "보안",
        Tab.Password => "비밀번호",
        Tab.Notice => "알림",
        _ => "사진",
    };

    // ── 휴대폰 목록(ad-hub) ─────────────────────────────────

    /// <summary>
    /// 목록을 보이고 있는가. <b>휴대폰이고 주소에 <c>?tab=</c> 이 없을 때</b>다.
    ///
    /// <para>
    /// 상태를 따로 들지 않고 주소로 판정한다 — 들고 있으면 사용자 메뉴가
    /// <c>?tab=avatar</c> 로 보냈을 때 목록과 조각 중 어느 쪽을 보일지가
    /// 두 곳에서 갈린다.
    /// </para>
    /// </summary>
    private bool ShowHub => _isPhone && !_tabInUrl;

    /// <summary>주소에 <c>?tab=</c> 이 있었는가.</summary>
    private bool _tabInUrl;

    /// <param name="Tab">눌렀을 때 열리는 조각.</param>
    /// <param name="Icon">
    /// 줄 왼쪽의 그림. 사용자 메뉴(<c>UserMenu</c>)가 같은 자리에 쓰는 것과
    /// 맞춰 둔다 — 두 곳이 같은 일곱을 가리키는데 그림이 다르면 같은 것인 줄
    /// 모른다.
    /// </param>
    /// <param name="Name">줄 이름. 탭의 제 이름과 같다.</param>
    /// <param name="Summary">지금 값 한 줄. 들어가 보지 않아도 알아야 하는 것.</param>
    /// <param name="Badge">눈에 띄어야 하는 것만. 없으면 <c>null</c>.</param>
    /// <param name="BadgeClass">딱지의 색 (<c>jsini-badge--*</c>).</param>
    private sealed record HubRow(
        Tab Tab, string Icon, string Name, string Summary,
        string? Badge = null, string? BadgeClass = null);

    private IReadOnlyList<HubRow> HubRows
    {
        get
        {
            var noticeOn = NoticeRows.Count(row => row.Value);

            return
            [
                new(Tab.Basic, "jsini-icon-user", "기본 설정", BasicSummary),

                new(Tab.Account, "jsini-icon-eye", "계정 정보", AccountSummary,
                    _activity is { RecentFailCount: > 0 } ? $"실패 {_activity.RecentFailCount}" : null,
                    "jsini-badge--warn"),

                new(Tab.FixedTabs, "jsini-icon-star", "고정탭 관리",
                    Pinned.Count == 0 ? "사이드바에 고정한 메뉴가 없습니다" : $"{Pinned.Count}개 고정됨"),

                new(Tab.Security, "jsini-icon-lock", "보안 설정", SecuritySummary),

                new(Tab.Password, "jsini-icon-refresh", "비밀번호 변경",
                    $"마지막 변경 {Dt(_info!.PasswordChangedAt)}",
                    _info.PasswordPolicyOn ? RemainText : null,
                    RemainBadge),

                new(Tab.Notice, "jsini-icon-bell", "새 메시지 알림",
                    noticeOn == 0 ? "받는 알림이 없습니다" : $"{noticeOn}개 켬"),

                new(Tab.Avatar, "jsini-icon-image", "프로필 사진 관리",
                    _photos.Count == 0 ? "올려 둔 사진이 없습니다" : $"{_photos.Count}장 · 최대 {PhotoLimit}장"),
            ];
        }
    }

    /// <summary>
    /// 기본 설정 줄의 요약. <b>이름을 쓰지 않는다</b> — 바로 위 얼굴 판이
    /// 이미 이름을 크게 적고 있어서 같은 글자가 두 번 보인다.
    /// </summary>
    private string BasicSummary
    {
        get
        {
            var parts = new[] { _info!.Email, _info.Phone }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            return parts.Length == 0 ? "이메일 · 전화번호 · 생년월일" : string.Join(" · ", parts);
        }
    }

    private string AccountSummary => _info!.LastLoginAt is null
        ? $"가입 {Dt(_info.CreatedAt)}"
        : $"최근 로그인 {Dt(_info.LastLoginAt)}";

    /// <summary>
    /// 보안 줄의 요약. <b>없는 것을 세지 않고 있는 것만 말한다</b> — 「지문 0 ·
    /// 소셜 0」은 읽는 사람에게 셈이 아니라 잔소리다.
    /// </summary>
    private string SecuritySummary
    {
        get
        {
            var parts = new List<string>(2);

            if (_passkeys.Count > 0)
            {
                parts.Add($"지문 · 얼굴 {_passkeys.Count}개");
            }

            if (_socialLinks.Count > 0)
            {
                parts.Add($"소셜 {_socialLinks.Count}개");
            }

            return parts.Count == 0 ? "지문 · 얼굴 · 소셜 계정을 붙일 수 있습니다" : string.Join(" · ", parts);
        }
    }

    private UserInfoDto? _info;
    private AccountActivityDto? _activity;

    private UpdateProfileDto _edit = new();
    private DateTime? _birthDate;
    private bool _isLunar;

    // ── 고정탭 ──────────────────────────────────────────────

    /// <summary>내 메뉴를 펼쳐 「열 수 있는 화면」만 남긴 것.</summary>
    private IReadOnlyList<MenuNode> _screens = [];

    /// <summary>담아 뒀지만 지금 내 메뉴에 없는 개수.</summary>
    private int _hiddenPins;

    /// <summary>왼쪽 목록에서 고른 것들.</summary>
    private IEnumerable<MenuFavorite> _pickedPinsSource = [];

    /// <summary>오른쪽 목록에서 고른 것들.</summary>
    private IEnumerable<MenuNode> _pickedMenusSource = [];

    /// <summary>
    /// 고른 것을 목록으로 굳혀 둔 것.
    /// </summary>
    /// <remarks>
    /// <c>DxListBox.Values</c> 는 <b><c>IEnumerable&lt;T&gt;</c> 여야 한다</b> —
    /// 그래서 부품에 물리는 값(<c>_picked…Source</c>)과 화면이 읽는 값을 갈라
    /// 두었다. 화면은 개수와 첫 항목을 자주 묻는데(단추 켜기·끄기 · 순서 옮기기),
    /// 지연 열거를 그대로 두면 렌더마다 여러 번 돈다.
    /// </remarks>
    private IReadOnlyList<MenuFavorite> _pickedPins = [];

    private IReadOnlyList<MenuNode> _pickedMenus = [];

    private void PickPins(IEnumerable<MenuFavorite>? values)
    {
        _pickedPinsSource = values ?? [];
        _pickedPins = [.. _pickedPinsSource];
    }

    private void PickMenus(IEnumerable<MenuNode>? values)
    {
        _pickedMenusSource = values ?? [];
        _pickedMenus = [.. _pickedMenusSource];
    }

    // ── 사진 ────────────────────────────────────────────────

    private IReadOnlyList<GroupFileDto> _photos = [];
    private string? _avatarGroupId;

    /// <summary>크게 보고 있는 사진. <c>null</c> 이면 창이 닫혀 있다.</summary>
    private GroupFileDto? _preview;

    // ── 켬·끔 한 줄 ─────────────────────────────────────────

    /// <summary>
    /// 보안 설정과 알림 설정이 같은 모양을 쓴다 — 서버가 둘을 같은 표
    /// (<c>account_profile_details</c>)에 이름·값으로 쌓기 때문이다.
    /// </summary>
    /// <param name="Name">줄 이름.</param>
    /// <param name="Desc">아래에 옅게 붙는 설명.</param>
    /// <param name="Value">지금 켜져 있는가.</param>
    /// <param name="Field">
    /// 저장 이름. <b><c>nameof(UserInfoDto.…)</c> 로 만든다</b> — 글자로 적으면
    /// 조회하는 칸과 저장하는 칸이 어긋날 수 있고, 그때 증상은 「저장은 됐다는데
    /// 값이 그대로」다.
    /// </param>
    private sealed record SettingRow(string Name, string Desc, bool Value, string Field);

    private IReadOnlyList<SettingRow> SecurityRows =>
    [
        new("보안용 휴대전화", "보안용 휴대전화 사용 여부를 설정합니다.",
            _info!.SecurityPhone, nameof(UserInfoDto.SecurityPhone)),
        new("보안 질문", "보안 질문 사용 여부를 설정합니다.",
            _info.SecurityQuestion, nameof(UserInfoDto.SecurityQuestion)),
        new("복구 이메일", "복구용 이메일 사용 여부를 설정합니다.",
            _info.SecurityEmail, nameof(UserInfoDto.SecurityEmail)),
        new("MFA 기기", "2차 인증용 MFA 기기 사용 여부를 설정합니다.",
            _info.SecurityMfa, nameof(UserInfoDto.SecurityMfa)),
    ];

    private IReadOnlyList<SettingRow> NoticeRows =>
    [
        new("계정 비밀번호", "계정·비밀번호와 관련된 알림을 받습니다.",
            _info!.AccountPasswordNotify, nameof(UserInfoDto.AccountPasswordNotify)),
        new("시스템 메시지", "시스템 메시지를 알림으로 받습니다.",
            _info.SystemMessage, nameof(UserInfoDto.SystemMessage)),
        new("할 일", "할 일이 생기면 알림으로 받습니다.",
            _info.TodoTask, nameof(UserInfoDto.TodoTask)),
    ];

    // ── 파생 값 ─────────────────────────────────────────────

    private IReadOnlyList<LoginLogDto> Recent => _activity?.Recent ?? [];

    private IReadOnlyList<MenuFavorite> Pinned => Favorites.Items;

    /// <summary>담기지 않은 화면들. 담긴 것은 <b>DB 경로</b>로 판정한다.</summary>
    private IReadOnlyList<MenuNode> Unpinned =>
        [.. _screens.Where(m => !Favorites.Contains(m.Path))];

    /// <summary>
    /// 순서 단추를 켤 수 있는가. <b>한 건만 골랐을 때만</b> 켠다 —
    /// 여러 건을 한꺼번에 올리고 내리는 규칙은 사람마다 다르게 기대하고,
    /// 어느 쪽으로 정해도 나머지 절반은 틀린 것으로 본다.
    /// </summary>
    private bool CanMoveUp => PickedPinIndex > 0;

    private bool CanMoveDown => PickedPinIndex >= 0 && PickedPinIndex < Pinned.Count - 1;

    /// <summary>고른 한 건의 자리. 하나가 아니면 <c>-1</c>.</summary>
    private int PickedPinIndex
    {
        get
        {
            if (_pickedPins.Count != 1)
            {
                return -1;
            }

            var path = _pickedPins[0].Path;

            for (var i = 0; i < Pinned.Count; i++)
            {
                if (string.Equals(Pinned[i].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    private string Initial
    {
        get
        {
            var source = _info?.RealName ?? _info?.Username;
            return string.IsNullOrWhiteSpace(source) ? "?" : source.Trim()[..1].ToUpperInvariant();
        }
    }

    private string? Affiliation
    {
        get
        {
            var parts = new[] { _info?.CompanyName, _info?.DeptName }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            return parts.Length == 0 ? null : string.Join(" · ", parts);
        }
    }

    private string FailNotice
    {
        get
        {
            var head = $"최근 30일 안에 로그인 실패가 {_activity!.RecentFailCount}번 있었습니다.";

            return _activity.LastFail is { } fail
                ? $"{head} 마지막 실패: {Dt(fail.At)} · {fail.Ip ?? "주소 없음"} ({fail.ResultLabel})"
                : head;
        }
    }

    private string PasswordSummary => _info!.PasswordPolicyOn
        ? $"마지막 변경 {Dt(_info.PasswordChangedAt)} · {_info.PasswordExpiryDays}일마다 바꿔야 합니다"
        : $"마지막 변경 {Dt(_info.PasswordChangedAt)}";

    /// <summary>남은 기간을 색으로도 구분한다 — 이레 이하면 눈에 띄어야 한다.</summary>
    private string RemainBadge => _info!.PasswordExpired || _info.PasswordDaysRemaining == 0
        ? "ad-badge--err"
        : _info.PasswordDaysRemaining <= 7 ? "jsini-badge--warn" : "jsini-badge--on";

    private string RemainText => _info!.PasswordExpired
        ? "만료됨"
        : $"{_info.PasswordDaysRemaining}일 남음";

    private string? ExpiryText
    {
        get
        {
            if (_info!.PasswordExpired)
            {
                return _info.PasswordExpiryDays is int days
                    ? $"비밀번호를 바꾼 지 {days}일이 지났습니다. 지금 바꿔야 다른 화면을 쓸 수 있습니다."
                    : "비밀번호 사용 기간이 지났습니다. 지금 바꿔야 다른 화면을 쓸 수 있습니다.";
            }

            return _info.PasswordDaysRemaining is int left && left <= 7
                ? $"비밀번호 사용 기간이 {left}일 남았습니다."
                : null;
        }
    }

    private NoticeTone ExpiryTone =>
        _info!.PasswordExpired ? NoticeTone.Error : NoticeTone.Warning;

    // ── 여닫기 ──────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        Favorites.Changed += OnFavoritesChanged;
        Menus.MenusChanged += OnMenusChanged;

        // 얼굴은 레이아웃이 읽는다. **이 화면이 먼저 뜰 수 있다** — 그때는
        // 왼쪽 판이 첫 글자만 그리고 있다가, 레이아웃이 다 읽으면 이 알림을
        // 받아 사진으로 바뀐다.
        Me.Changed += OnMeChanged;

        RebuildScreens();

        await ReloadAsync();

        // 소셜 연결에서 되돌아온 참이면 그 결과를 말한다. **읽기가 끝난
        // 뒤에 부른다** — 앞에서 부르면 목록을 다시 읽는 동안 안내가 먼저
        // 뜨고, 「연결했다」고 해 놓고 목록에 없는 순간이 생긴다.
        SaySocialResult();
    }

    /// <summary>
    /// 주소가 바뀌면 탭도 따라간다.
    ///
    /// <para>
    /// 사용자 메뉴가 <c>/admin/profile?tab=avatar</c> 로 보낼 때, 이 화면이
    /// <b>이미 떠 있으면</b> Blazor 는 부품을 다시 만들지 않는다.
    /// <c>OnInitializedAsync</c> 에서만 읽으면 주소는 바뀌었는데 탭이 그대로다.
    /// </para>
    /// </summary>
    protected override void OnParametersSet() => ReadTabFromUrl();

    /// <summary>
    /// 브라우저에게 패스키를 쓸 수 있는지 묻는다.
    ///
    /// <para>
    /// <b><c>OnInitializedAsync</c> 에서 부르지 않는다.</b> 그때는 서버가
    /// 화면을 그리는 중(프리렌더)이라 JS 가 없고, 부르면 예외가 난다.
    /// 첫 렌더 뒤가 브라우저에게 말을 걸 수 있는 가장 이른 때다.
    /// </para>
    ///
    /// <para>
    /// <b>예외를 삼킨다.</b> 못 물어봤다는 것은 「이 브라우저는 못 쓴다」와
    /// 같은 결론이고, 화면이 그렇게 말한다(<see cref="PasskeySummary"/>).
    /// </para>
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _browser = await JS.InvokeAsync<PasskeyBrowserDto>("jsiniPasskey.status") ?? new();
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            _browser = new();
        }

        StateHasChanged();
    }

    public void Dispose()
    {
        Favorites.Changed -= OnFavoritesChanged;
        Menus.MenusChanged -= OnMenusChanged;
        Me.Changed -= OnMeChanged;
    }

    private void OnFavoritesChanged() => InvokeAsync(StateHasChanged);

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    private void OnMenusChanged() => InvokeAsync(() =>
    {
        RebuildScreens();
        StateHasChanged();
    });

    /// <summary>
    /// 주소에서 탭을 읽는다.
    ///
    /// <para>
    /// <b>「없다」도 하나의 답이다.</b> 휴대폰에서 그것이 「목록을 보인다」라서
    /// (<see cref="ShowHub"/>) 못 읽었을 때 <c>_tabInUrl</c> 을 내려 둔다 —
    /// 안 그러면 목록에서 조각으로 한 번 들어간 뒤로는 되돌아올 수 없다.
    /// </para>
    /// </summary>
    private void ReadTabFromUrl()
    {
        var query = new Uri(Navigation.Uri).Query;

        if (string.IsNullOrEmpty(query))
        {
            _tabInUrl = false;
            return;
        }

        var parsed = QueryHelpers.ParseQuery(query);

        if (parsed.TryGetValue("tab", out var values)
            && values.ToString() is { Length: > 0 } name
            && TabNames.TryGetValue(name, out var tab))
        {
            _tab = tab;
            _tabInUrl = true;
            return;
        }

        // 소셜 공급자에 다녀오면 `?social=linked` 만 붙어 돌아온다
        // (`SocialLoginFlow`). 그 결과를 말하는 자리가 보안 설정이라
        // **휴대폰에서 목록으로 떨어뜨리면 안 된다** — 붙였다는 안내는 뜨는데
        // 붙은 것이 어디에도 안 보인다.
        if (parsed.ContainsKey("social"))
        {
            _tab = Tab.Security;
            _tabInUrl = true;
            return;
        }

        _tabInUrl = false;
    }

    /// <summary>
    /// <c>DxTabs</c> 에서 탭을 눌렀을 때. 번호를 열거형으로 되돌려 받는다.
    /// </summary>
    private void OnTabIndexChanged(int index)
    {
        if (Enum.IsDefined(typeof(Tab), index))
        {
            Go((Tab)index);
        }
    }

    /// <summary>
    /// 탭을 바꾼다. <b>주소도 함께 바꾼다</b> — 새로고침하거나 링크로 보냈을 때
    /// 같은 자리가 열려야 한다.
    /// </summary>
    private void Go(Tab tab)
    {
        _tab = tab;
        _tabInUrl = true;

        var name = TabNames.First(pair => pair.Value == tab).Key;

        // 데스크톱은 replace 다. 탭을 일곱 번 눌렀다고 뒤로 가기를 일곱 번 해야
        // 화면을 벗어나는 것은 브라우저 사용에 어긋난다.
        //
        // **휴대폰만 쌓는다.** 거기서 이 한 걸음은 탭 옮기기가 아니라 목록에서
        // 한 층 들어가는 것이고, 기기에 달린 뒤로 가기가 그 목록으로 돌아오지
        // 않으면 화면을 통째로 벗어난다.
        Navigation.NavigateTo($"/admin/profile?tab={name}", replace: !_isPhone);
    }

    /// <summary>
    /// 목록으로 되돌아간다(휴대폰).
    ///
    /// <para>
    /// <b>replace 로 되돌린다.</b> 쌓으면 「목록 → 조각 → 목록」이 되어, 여기서
    /// 기기의 뒤로 가기를 누른 사람이 방금 나온 조각으로 다시 들어간다.
    /// </para>
    /// </summary>
    private void BackToHub()
    {
        _tabInUrl = false;
        Navigation.NavigateTo("/admin/profile", replace: true);
    }

    // ── 조회 ────────────────────────────────────────────────

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var info = Api.GetMyInfoAsync();

        // 접속 기록을 못 받아도 계정 값은 보여 준다 — 화면 전체가 비는 것보다
        // 낫다. Vue 도 여기만 `catch(() => null)` 로 두고 있었다.
        var activity = SafeAsync(Api.GetMyActivityAsync());

        // 즐겨찾기는 레이아웃이 이미 한 번 읽지만, 이 화면은 그 목록을 고치는
        // 자리라 들어올 때 최신으로 맞춘다.
        var favorites = Favorites.ReloadAsync();

        // 등록된 기기. 못 읽어도 나머지를 막지 않는다 — 접속 기록과 같은 규칙이다.
        var passkeys = LoadPasskeysAsync();

        // 연결된 소셜 계정. 같은 규칙이다.
        var social = LoadSocialAsync();

        await Task.WhenAll(info, activity, favorites, passkeys, social);

        _info = info.Result;
        _activity = activity.Result;

        Revert();
        RebuildScreens();

        _avatarGroupId = _info?.AvatarGroupId;

        await LoadPhotosAsync();

        // 단건 조회다 — 「없습니다」로 끝나지 않게 1 을 돌려준다.
        return _info is null ? 0 : 1;
    }, "내 정보를 찾지 못했습니다.", "내 정보를 읽지 못했습니다");

    /// <summary>실패해도 화면을 세우려는 조회. 이유는 부르는 자리에 적었다.</summary>
    private static async Task<T?> SafeAsync<T>(Task<T?> task)
    {
        try
        {
            return await task;
        }
        catch (ApiException)
        {
            return default;
        }
    }

    /// <summary>받아 온 값으로 폼을 되돌린다. 처음 채울 때도 이것을 쓴다.</summary>
    private void Revert()
    {
        if (_info is null)
        {
            return;
        }

        _edit = new UpdateProfileDto
        {
            RealName = _info.RealName,
            Email = _info.Email,
            Phone = _info.Phone,
            Introduction = _info.Introduction,
        };

        _birthDate = DateTime.TryParse(_info.BirthDate, out var d) ? d : null;
        _isLunar = _info.BirthDateIsLunar;
    }

    // ── 기본 설정 저장 ──────────────────────────────────────

    /// <summary>
    /// 저장한다.
    ///
    /// 생일은 <c>yyyy-MM-dd</c> 글자로 보낸다. <b>비웠으면 빈 문자열</b>이다 —
    /// <c>null</c> 로 보내면 서버가 「건드리지 말라」로 읽어 옛 값이 남는다.
    /// </summary>
    private async Task SaveBasicAsync()
    {
        _edit.BirthDate = _birthDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        _edit.BirthDateIsLunar = _isLunar;

        if (await RunAsync(() => Api.UpdateMyProfileAsync(_edit), "저장했습니다.", "저장하지 못했습니다"))
        {
            // 이름이 바뀌면 헤더의 사용자 단추도 따라가야 한다.
            await Me.ReloadAsync();
            await ReloadAsync();
        }
    }

    // ── 패스키 (지문 · 얼굴) ────────────────────────────────

    /// <summary>내 계정에 등록된 기기들.</summary>
    private IReadOnlyList<PasskeyDto> _passkeys = [];

    /// <summary>
    /// 이 브라우저가 패스키를 다룰 수 있는가.
    ///
    /// <para>
    /// <b>기본이 「못 한다」다.</b> 서버가 그릴 때(프리렌더)는 브라우저에게
    /// 물어볼 수 없어 값이 없는데, 그때 「할 수 있다」로 두면 못 하는
    /// 브라우저에서도 단추가 잠깐 열렸다가 잠긴다.
    /// </para>
    /// </summary>
    private PasskeyBrowserDto _browser = new();

    /// <summary>기기가 대답하기를 기다리는 중인가. 단추를 두 번 누르지 못하게 막는다.</summary>
    private bool _passkeyBusy;

    /// <summary>이름을 고치는 중인 기기. 창이 이 값으로 열린다.</summary>
    private PasskeyDto? _renaming;

    private string? _renameLabel;

    /// <summary>「지문 · 얼굴로 로그인」 줄의 설명. 상태 넷을 갈라 말한다.</summary>
    private string PasskeySummary => (_browser.Supported, _passkeys.Count) switch
    {
        // http 로 열었거나 옛 브라우저다. WebAuthn 은 보안 문맥에서만 있다.
        (false, _) => "이 브라우저에서는 쓸 수 없습니다 (https 로 접속했는지 확인하십시오).",
        (true, 0) when !_browser.PlatformAuthenticator =>
            "이 기기에는 지문·얼굴 인식이 없습니다. 보안 열쇠는 등록할 수 있습니다.",
        (true, 0) => "등록된 기기가 없습니다. 등록하면 아이디를 치지 않고 들어올 수 있습니다.",
        (true, var n) => $"기기 {n}대가 등록되어 있습니다.",
    };

    /// <summary>인증기 종류를 사람 말로.</summary>
    private static string AttachmentText(string? attachment) =>
        attachment == "cross-platform" ? "보안 열쇠" : "이 기기의 지문·얼굴";

    private string LastUsedText(DateTime? lastUsedAt) =>
        lastUsedAt is null ? "아직 쓴 적 없음" : $"마지막 사용 {Dt(lastUsedAt)}";

    /// <summary>
    /// 이 기기에 패스키를 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 순서가 셋이고 <b>가운데는 브라우저가 한다.</b>
    /// </para>
    /// <list type="number">
    ///   <item>서버에서 도전값을 받는다.</item>
    ///   <item>브라우저가 기기에게 지문을 받고 열쇠를 만든다(<c>passkey.js</c>).</item>
    ///   <item>그 결과를 서버가 검증하고 저장한다.</item>
    /// </list>
    ///
    /// <para>
    /// ②에서 취소하면 ③이 아예 없으므로 <b>서버에 반쪽짜리 줄이 남지 않는다.</b>
    /// 받아 둔 도전값은 5분 뒤 스스로 사라진다.
    /// </para>
    ///
    /// <para>
    /// <b>단추의 <c>Click</c> 에서 부른다.</b> 기기 확인 창은 웹푸시 권한 요청과
    /// 마찬가지로 <b>사람이 누른 그 사슬에서만</b> 열린다 — 화면이 뜨자마자
    /// 부르면 브라우저가 조용히 거절한다.
    /// </para>
    /// </remarks>
    private async Task RegisterPasskeyAsync()
    {
        if (_passkeyBusy)
        {
            return;
        }

        _passkeyBusy = true;

        try
        {
            var options = await Api.CreatePasskeyOptionsAsync();

            if (options is null)
            {
                Say("서버가 등록 정보를 주지 않았습니다.", NoticeTone.Error);
                return;
            }

            PasskeyRegistrationDto? made;

            try
            {
                made = await JS.InvokeAsync<PasskeyRegistrationDto>(
                    "jsiniPasskey.register", options, DefaultPasskeyLabel());
            }
            catch (JSException ex)
            {
                // 여기 오는 것은 스크립트가 아예 없을 때다 — 기기가 거절한
                // 경우는 passkey.js 가 잡아서 `Ok: false` 로 돌려준다.
                Say($"기기와 이야기하지 못했습니다 — {ex.Message}", NoticeTone.Error);
                return;
            }

            if (made is not { Ok: true })
            {
                // 까닭은 이미 사람 말로 옮겨져 있다(passkey.js 의 `explain`).
                Say(made?.Error ?? "기기 등록에 실패했습니다.", NoticeTone.Warning);
                return;
            }

            if (await RunAsync(
                () => Api.RegisterPasskeyAsync(made),
                "이 기기를 등록했습니다. 다음부터 로그인 화면에서 지문·얼굴로 들어올 수 있습니다.",
                "기기를 등록하지 못했습니다"))
            {
                await RememberPasskeyUserAsync();
                await LoadPasskeysAsync();
            }
        }
        catch (ApiException ex)
        {
            Say($"기기를 등록하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _passkeyBusy = false;
        }
    }

    /// <summary>
    /// 이름을 안 지어 주면 <b>목록에서 어느 줄인지 알 수 없다.</b> 기기가
    /// 이름을 알려 주지는 않으므로 순번으로라도 갈라 둔다.
    /// </summary>
    private string DefaultPasskeyLabel() =>
        _passkeys.Count == 0 ? "내 기기" : $"내 기기 {_passkeys.Count + 1}";

    private void OpenRename(PasskeyDto key)
    {
        _renaming = key;
        _renameLabel = key.Label;
    }

    private async Task RenamePasskeyAsync()
    {
        if (_renaming is null)
        {
            return;
        }

        var label = _renameLabel?.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            Say("이름을 입력하십시오.", NoticeTone.Warning);
            return;
        }

        var id = _renaming.Id;
        _renaming = null;

        if (await RunAsync(
            () => Api.RenamePasskeyAsync(id, label),
            "이름을 바꿨습니다.",
            "이름을 바꾸지 못했습니다"))
        {
            await LoadPasskeysAsync();
        }
    }

    private async Task DeletePasskeyAsync(PasskeyDto key)
    {
        if (!await RunAsync(
            () => Api.DeletePasskeyAsync(key.Id),
            "기기를 지웠습니다. 그 기기로는 더 들어올 수 없습니다.",
            "기기를 지우지 못했습니다"))
        {
            return;
        }

        await LoadPasskeysAsync();

        // 마지막 기기를 지웠는데 우선순위가 지문으로 남아 있으면, 로그인
        // 화면이 열릴 때마다 **반드시 실패하는 기기 확인**을 연다. 그 증상은
        // 「지우고 나니 로그인 화면이 이상해졌다」로 보여 원인과 멀다.
        if (_passkeys.Count == 0 && _browser.Priority == PasskeyPriority.Passkey)
        {
            await SetAuthPriorityAsync(PasskeyPriority.Password, quiet: true);
        }

        if (_passkeys.Count == 0)
        {
            await ForgetPasskeyUserAsync();
        }
    }

    /// <summary>
    /// <b>이 브라우저가 패스키로 들어갈 아이디</b>를 적어 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로그인 화면은 아직 누구인지 모르는 채로 도전값을 받아 와야 한다.
    /// 아이디를 못 주면 서버가 후보 열쇠를 줄 수 없고, 그때는 <b>기기가 스스로
    /// 들고 있는 열쇠</b>(discoverable)만 쓸 수 있다. 등록할 때
    /// <c>residentKey: "preferred"</c> 로 부탁하지만 <b>부탁이지 약속이 아니라</b>
    /// 윈도우 Hello 처럼 안 들고 있기로 하는 기기가 있고, 그런 기기에서는
    /// 아이디 없이 여는 길이 통째로 막힌다.
    /// </para>
    ///
    /// <para>
    /// 그래서 등록한 그 자리에서 적어 둔다. <b>계정이 아니라 브라우저에</b>
    /// 적는 까닭은 우선순위와 같다 — 패스키 자체가 기기마다 따로다.
    /// </para>
    /// </remarks>
    private async Task RememberPasskeyUserAsync()
    {
        var id = _info?.UserId ?? _info?.Username;

        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("jsiniPasskey.rememberUser", id);
        }
        catch (JSException)
        {
            // 못 적어도 로그인은 아이디를 한 번 치는 것으로 된다.
            // 등록은 이미 끝났으므로 여기서 실패를 말할 일이 아니다.
        }
    }

    /// <summary>
    /// 마지막 기기를 지웠다. 적어 둔 아이디도 지운다 —
    /// <b>그 아이디일 때만</b> 지운다(`passkey.js` 의 <c>forgetUser</c>).
    /// </summary>
    private async Task ForgetPasskeyUserAsync()
    {
        var id = _info?.UserId ?? _info?.Username;

        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("jsiniPasskey.forgetUser", id);
        }
        catch (JSException)
        {
            // 남아 있어도 후보가 비어 올 뿐이라 로그인 화면이 알아서 넘어간다.
        }
    }

    // ── 로그인 방식 우선순위 ────────────────────────────────

    /// <summary>
    /// 고를 수 있는 두 가지. <b>이름을 글자로 두 번 적지 않는다</b> —
    /// 딱지와 저장값이 갈리면 「고른 것과 다른 것이 저장된다」가 된다.
    /// </summary>
    private sealed record AuthPriorityOption(string Value, string Text);

    private static readonly AuthPriorityOption[] AuthPriorityOptions =
    [
        new(PasskeyPriority.Password, "비밀번호 먼저"),
        new(PasskeyPriority.Passkey, "지문 · 얼굴 먼저"),
    ];

    /// <summary>
    /// 고를 수 있는가. <b>등록한 기기가 없으면 잠근다</b> — 고르게 두면
    /// 로그인 화면이 아무것도 없는 기기에게 지문을 묻고 반드시 실패한다.
    /// </summary>
    private bool CanPickAuthPriority => _browser.Supported && _passkeys.Count > 0;

    /// <summary>「로그인 방식 우선순위」 줄의 설명. 잠긴 까닭까지 말한다.</summary>
    private string AuthPrioritySummary => (_browser.Supported, _passkeys.Count) switch
    {
        (false, _) => "이 브라우저에서는 지문·얼굴을 쓸 수 없어 비밀번호로 들어갑니다.",
        (true, 0) => "이 기기를 먼저 등록하면 지문·얼굴을 먼저 묻게 할 수 있습니다.",
        _ when _browser.Priority == PasskeyPriority.Passkey =>
            "로그인 화면이 열리는 순간 지문·얼굴을 묻습니다. 취소하면 비밀번호로 들어갑니다.",
        _ => "비밀번호를 먼저 묻습니다. 지문·얼굴은 로그인 화면의 단추로 씁니다.",
    };

    /// <summary>
    /// 우선순위를 이 기기에 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>서버로 가지 않는다.</b> 읽어야 하는 곳이 로그인 화면이고 거기는
    /// 로그인 전이다 — 화면 머리말과 <c>passkey.js</c> 의 같은 절 참고.
    /// </para>
    /// <para>
    /// <b>적고 나서 다시 읽은 값</b>을 화면에 넣는다. 사생활 보호 모드에서는
    /// localStorage 가 던지므로, 고른 값을 그대로 믿으면 화면만 바뀌고
    /// 로그인 화면은 옛 방식대로 도는 <b>거짓말</b>이 된다.
    /// </para>
    /// </remarks>
    /// <param name="value">고른 값. <c>null</c> 이면 비밀번호로 본다.</param>
    /// <param name="quiet">사람이 고른 것이 아니라 화면이 되돌린 것인가.</param>
    private async Task SetAuthPriorityAsync(string? value, bool quiet = false)
    {
        var wanted = value == PasskeyPriority.Passkey
            ? PasskeyPriority.Passkey
            : PasskeyPriority.Password;

        string saved;

        try
        {
            saved = await JS.InvokeAsync<string>("jsiniPasskey.setPreference", wanted)
                ?? PasskeyPriority.Password;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            Say("이 브라우저에 설정을 기억시키지 못했습니다.", NoticeTone.Error);
            return;
        }

        _browser.Priority = saved;

        if (saved != wanted)
        {
            // 던지지는 않았는데 값이 안 남았다 — 사생활 보호 모드다.
            Say("이 브라우저는 설정을 기억하지 못합니다 (사생활 보호 모드).", NoticeTone.Warning);
            return;
        }

        if (quiet)
        {
            // 마지막 기기를 지워 되돌린 자리다. 「지웠습니다」 바로 뒤라
            // 토스트를 하나 더 쌓지 않고 까닭만 한 줄로 알린다.
            Say("등록된 기기가 없어져 비밀번호 먼저로 되돌렸습니다.", NoticeTone.Warning);
            return;
        }

        Say(saved == PasskeyPriority.Passkey
            ? "이 기기에서는 지문·얼굴을 먼저 묻습니다."
            : "이 기기에서는 비밀번호를 먼저 묻습니다.");
    }

    /// <summary>
    /// 기기 목록을 다시 읽는다.
    ///
    /// <b>못 읽어도 화면을 세우지 않는다</b> — 이 화면의 본업은 내 정보이고,
    /// 패스키는 그중 한 줄이다. 조회 한 번이 실패했다고 나머지 여섯 탭이
    /// 함께 비면 잃는 것이 더 크다(접속 기록을 같은 이유로 그렇게 두었다).
    /// </summary>
    private async Task LoadPasskeysAsync()
    {
        try
        {
            _passkeys = await Api.GetPasskeysAsync();
        }
        catch (ApiException)
        {
            _passkeys = [];
        }
    }

    // ── 연결된 소셜 계정 (구글 · 네이버 · 카카오) ────────────

    /// <summary>내 계정에 붙어 있는 것들.</summary>
    private IReadOnlyList<SocialLinkDto> _socialLinks = [];

    /// <summary>붙일 수 있는 공급자. 설정된 것만 서버가 알려 준다.</summary>
    private IReadOnlyList<SocialProviderDto> _socialProviders = [];

    /// <summary>
    /// 아직 안 붙인 공급자. <b>이미 붙인 것에 「연결」 단추를 또 세우지 않는다</b> —
    /// 눌러 봐야 서버가 「이미 자기 것」이라며 같은 줄을 돌려줄 뿐이라, 무엇이
    /// 달라지는지 알 수 없는 단추가 된다.
    /// </summary>
    private IReadOnlyList<SocialProviderDto> UnlinkedProviders =>
        [.. _socialProviders.Where(p => !_socialLinks.Any(l =>
            string.Equals(l.Provider, p.Provider, StringComparison.OrdinalIgnoreCase)))];

    private string SocialSummary => _socialLinks.Count switch
    {
        0 => "연결해 두면 아이디를 치지 않고 그 계정으로 들어올 수 있습니다.",
        1 => $"{_socialLinks[0].DisplayName} 계정 하나가 연결되어 있습니다.",
        var n => $"{n}개 계정이 연결되어 있습니다.",
    };

    /// <summary>
    /// 목록에서 어느 줄인지 알아보는 단서. 공급자가 이름도 이메일도 안 주는
    /// 경우가 있어(카카오에서 동의 항목을 안 켠 경우) 그때는 말을 아낀다.
    /// </summary>
    private static string SocialWho(SocialLinkDto link) =>
        (link.AccountName, link.Email) switch
        {
            ({ Length: > 0 } name, { Length: > 0 } email) => $"{name} ({email})",
            ({ Length: > 0 } name, _) => name,
            (_, { Length: > 0 } email) => email,
            _ => "계정 정보 없음",
        };

    /// <summary>
    /// 연결하러 공급자에 다녀온다.
    /// </summary>
    /// <remarks>
    /// <b><c>forceLoad</c> 가 있어야 한다.</b> 가는 곳이 Blazor 화면이 아니라
    /// 셸의 엔드포인트라, 회로 안에서 이동하면 라우터가 「그런 화면이 없다」로
    /// 읽고 404 를 그린다.
    /// </remarks>
    private void StartSocialLink(SocialProviderDto provider) =>
        Navigation.NavigateTo(
            $"/social/{provider.Provider}/start?mode=link", forceLoad: true);

    private async Task UnlinkSocialAsync(SocialLinkDto link)
    {
        if (!await RunAsync(
            () => Api.UnlinkSocialAsync(link.Id),
            $"{link.DisplayName} 연결을 끊었습니다.",
            "연결을 끊지 못했습니다"))
        {
            return;
        }

        await LoadSocialAsync();
    }

    /// <summary>
    /// 연결 목록과 공급자 목록을 함께 읽는다. <b>못 읽어도 나머지를 막지
    /// 않는다</b> — 접속 기록·패스키와 같은 규칙이다.
    /// </summary>
    private async Task LoadSocialAsync()
    {
        try
        {
            var links = Api.GetSocialLinksAsync();
            var providers = Api.GetSocialProvidersAsync();

            await Task.WhenAll(links, providers);

            _socialLinks = links.Result;
            _socialProviders = providers.Result;
        }
        catch (ApiException)
        {
            _socialLinks = [];
            _socialProviders = [];
        }
    }

    /// <summary>
    /// 소셜 연결에서 되돌아온 표시를 읽는다 — <c>/admin/profile?social=linked</c>.
    /// </summary>
    /// <remarks>
    /// <b>짧은 표시만 주고받는다.</b> 문구를 주소에 그대로 실으면 남이 만든
    /// 링크로 이 화면에 아무 말이나 띄울 수 있다.
    /// </remarks>
    private void SaySocialResult()
    {
        var query = new Uri(Navigation.Uri).Query;

        if (string.IsNullOrEmpty(query)
            || !QueryHelpers.ParseQuery(query).TryGetValue("social", out var values))
        {
            return;
        }

        switch (values.ToString())
        {
            case "linked":
                Say("소셜 계정을 연결했습니다.", NoticeTone.Info);
                break;

            case "linkfailed":
                // 까닭이 여럿이다(남의 계정에 이미 붙었다 · 인증이 만료됐다 ·
                // 공급자가 답하지 않았다). 사용자가 할 수 있는 일은 어느 쪽이든
                // 「다시 눌러 본다」라 갈라 말하지 않는다.
                Say("소셜 계정을 연결하지 못했습니다. 다시 시도해 주세요.", NoticeTone.Error);
                break;
        }
    }

    // ── 보안·알림 켬끔 ──────────────────────────────────────

    /// <summary>
    /// 켬·끔 하나를 저장한다. <b>화면 값을 먼저 바꾸지 않는다</b> — 저장이
    /// 실패했는데 스위치가 켜져 있으면 켠 줄로 남는다. 성공하면 다시 읽어
    /// 서버가 아는 값으로 맞춘다.
    /// </summary>
    private async Task ToggleSettingAsync(string field, bool value)
    {
        if (await RunAsync(
            () => Api.UpdateMySettingAsync(field, value),
            "설정을 바꿨습니다.",
            "설정을 바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    // ── 고정탭 ──────────────────────────────────────────────

    /// <summary>
    /// 내 메뉴를 펼쳐 목록에 놓을 화면만 남긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 묶음(<c>CATALOG</c>) · 숨긴 메뉴 · 외부 링크를 뺀다. 묶음은 열 화면이
    /// 없고, 외부 링크는 앱 라우트가 아니라 즐겨찾기의 열쇠가 될 수 없다.
    /// </para>
    /// <para>
    /// <b>자식이 있는 메뉴도 자기 화면이 있으면 남긴다.</b> Vue 는 자식이
    /// 있으면 그 자리에서 건너뛰었는데, 우리 트리에는 자식이 다섯인
    /// <c>/funeral/status</c> 처럼 <b>묶음이 아닌데 자식이 있는 메뉴</b>가 있다.
    /// 건너뛰면 헤더의 별로 담을 수는 있는데 이 화면에서는 해제할 수 없다.
    /// </para>
    /// </remarks>
    private void RebuildScreens()
    {
        var screens = new List<MenuNode>();
        Walk(Menus.VisibleMenus, screens);
        _screens = screens;

        // 담아 뒀는데 목록에 없는 것을 센다. 권한이 바뀌었거나 메뉴가 지워진
        // 경우다 — 조용히 빼면 「해제가 안 된다」로 보인다.
        var known = screens.Select(m => m.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _hiddenPins = Favorites.Items.Count(f => !known.Contains(f.Path));

        static void Walk(IReadOnlyList<MenuNode> nodes, List<MenuNode> into)
        {
            foreach (var node in nodes)
            {
                if (!node.IsCatalog && !node.HideInMenu && !node.IsExternalLink)
                {
                    into.Add(node);
                }

                if (node.Children.Count > 0)
                {
                    Walk(node.Children, into);
                }
            }
        }
    }

    /// <summary>고른 것들을 담는다. 여러 건을 한 번에 받는다.</summary>
    private async Task PinAsync()
    {
        // 목록이 바뀌면서 고른 것이 사라지므로 먼저 꺼내 둔다.
        var paths = _pickedMenus.Select(m => m.Path).ToList();

        if (paths.Count == 0)
        {
            return;
        }

        PickMenus(null);

        await RunAsync(
            async () =>
            {
                // 한 건씩 보낸다 — 서버가 여러 건을 받는 경로를 주지 않는다.
                // 중간에 실패하면 거기서 멈추고 앞엣것은 담긴 채로 남는다.
                foreach (var path in paths)
                {
                    await Favorites.AddAsync(path);
                }
            },
            $"{paths.Count}개를 고정했습니다.",
            "고정하지 못했습니다");
    }

    /// <summary>고른 것들을 뺀다.</summary>
    private async Task UnpinAsync()
    {
        var paths = _pickedPins.Select(p => p.Path).ToList();

        if (paths.Count == 0)
        {
            return;
        }

        PickPins(null);

        await RunAsync(
            async () =>
            {
                foreach (var path in paths)
                {
                    await Favorites.RemoveAsync(path);
                }
            },
            $"{paths.Count}개를 해제했습니다.",
            "해제하지 못했습니다");
    }

    /// <summary>
    /// 고른 한 건을 한 칸 위·아래로 옮긴다.
    ///
    /// <para>
    /// <b>순서를 통째로 보낸다.</b> 「이것을 저기로」가 아니라 「전체가 이
    /// 순서다」로 보내야 중간에 실패했을 때 서버와 화면이 서로 다른 순서로
    /// 남지 않는다.
    /// </para>
    /// </summary>
    private async Task MoveAsync(int delta)
    {
        var index = PickedPinIndex;
        var target = index + delta;

        if (index < 0 || target < 0 || target >= Pinned.Count)
        {
            return;
        }

        var paths = Pinned.Select(p => p.Path).ToList();
        var moved = paths[index];

        paths.RemoveAt(index);
        paths.Insert(target, moved);

        await RunAsync(
            () => Favorites.ReorderAsync(paths), "순서를 저장했습니다.", "순서를 저장하지 못했습니다");
    }

    // ── 사진 ────────────────────────────────────────────────

    private void Preview(GroupFileDto photo) => _preview = photo;

    private async Task LoadPhotosAsync()
    {
        if (string.IsNullOrWhiteSpace(_avatarGroupId))
        {
            _photos = [];
            return;
        }

        try
        {
            _photos = await Api.GetGroupFilesAsync(_avatarGroupId);
        }
        catch (ApiException)
        {
            // 그룹이 지워졌을 수 있다. 사진을 못 읽는 것으로 화면 전체를 막지
            // 않는다 — 다시 올리면 새 그룹이 생긴다.
            _photos = [];
        }
    }

    /// <summary>
    /// 한 장이 올라갔을 때. <b>올리는 일 자체는 이 화면이 하지 않는다</b> —
    /// 브라우저가 <c>DxUpload</c> 으로 셸 중계에 직접 보내고, 그 중계가
    /// 게이트웨이로 넘기고 그룹·대표까지 계정에 적는다
    /// (<see cref="ProfilePhotoUpload"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 그래서 여기서 할 일은 <b>다시 읽는 것</b>뿐이다. 그룹 아이디는
    /// <c>auth/user/info</c> 에서 새로 받는다 — 첫 장을 올리면 그때 생긴다.
    /// </para>
    /// <para>
    /// 여러 장을 고르면 장마다 한 번씩 불린다. 그때마다 두 번 조회하는 것이
    /// 아깝지만, 장수가 서른까지고 <b>한 장이 올라가는 대로 격자에 나타나는</b>
    /// 편이 다 끝날 때까지 아무 일도 없는 것보다 낫다.
    /// </para>
    /// </remarks>
    private async Task OnPhotoUploadedAsync(FileUploadEventArgs e)
    {
        var info = await Api.GetMyInfoAsync();

        _avatarGroupId = info?.AvatarGroupId ?? _avatarGroupId;

        await LoadPhotosAsync();

        // 헤더 얼굴도 따라가야 한다. 계정의 Avatar 는 중계가 이미 맞춰 두었다.
        await Me.ReloadAsync();

        Say($"{e.FileInfo.Name} 을(를) 올렸습니다.");
    }

    /// <summary>대표를 바꾼다. 그룹 안에서 하나뿐이다.</summary>
    private async Task SetRepresentativeAsync(GroupFileDto photo)
    {
        if (string.IsNullOrWhiteSpace(_avatarGroupId) || string.IsNullOrWhiteSpace(photo.Id))
        {
            return;
        }

        var groupId = _avatarGroupId;
        var fileId = photo.Id;

        if (await RunAsync(
            () => Api.SetRepresentativeFileAsync(groupId, fileId),
            "대표 사진을 바꿨습니다.",
            "대표 사진을 바꾸지 못했습니다"))
        {
            await LoadPhotosAsync();
            await SyncRepresentativeAsync();
        }
    }

    /// <summary>
    /// 사진 한 장을 지운다.
    ///
    /// <para>
    /// 대표를 지웠으면 남은 첫 장을 대표로 올린다. 안 하면 그룹에 사진은 있는데
    /// 대표가 없어 헤더가 빈 동그라미가 된다 — Vue 도 같은 뒤처리를 하고 있었다.
    /// </para>
    /// </summary>
    private async Task DeletePhotoAsync(GroupFileDto photo)
    {
        if (string.IsNullOrWhiteSpace(photo.Id))
        {
            return;
        }

        var fileId = photo.Id;
        var wasRepresentative = photo.IsRepresentative;

        if (!await RunAsync(
            () => Api.DeleteFileAsync(fileId), "사진을 지웠습니다.", "사진을 지우지 못했습니다"))
        {
            return;
        }

        // 지운 것을 크게 보고 있었으면 창을 닫는다.
        if (_preview?.Id == fileId)
        {
            _preview = null;
        }

        await LoadPhotosAsync();

        if (wasRepresentative
            && !string.IsNullOrWhiteSpace(_avatarGroupId)
            && _photos.Count > 0
            && _photos[0].Id is { Length: > 0 } next)
        {
            try
            {
                await Api.SetRepresentativeFileAsync(_avatarGroupId, next);
                await LoadPhotosAsync();
            }
            catch (ApiException)
            {
                // 지우기는 끝났다. 대표가 비는 것뿐이라 화면을 막지 않는다.
            }
        }

        await SyncRepresentativeAsync();
    }

    /// <summary>
    /// 지금 대표 사진을 계정의 <c>Avatar</c> 로 저장하고 헤더에도 알린다.
    /// </summary>
    /// <remarks>
    /// <b>대표 여부는 파일 그룹이 알고, 헤더가 보는 것은 계정의 <c>Avatar</c> 다.</b>
    /// 둘이 따로라 대표가 바뀔 때마다 맞춰 줘야 한다 — 안 맞추면 이 화면의
    /// 「대표」 딱지와 헤더의 얼굴이 서로 다른 사진을 가리킨다.
    /// </remarks>
    private async Task SyncRepresentativeAsync()
    {
        var rep = _photos.FirstOrDefault(p => p.IsRepresentative) ?? _photos.FirstOrDefault();

        // 저장하는 값은 **서버가 준 주소 그대로**다. 화면이 쓰는 셸 중계 경로로
        // 바꿔 저장하면 게이트웨이와 같은 오리진에서 보는 다른 서비스가 그 값을
        // 못 읽는다. 옮기는 일은 보여 줄 때만 한다(CurrentUser · FileDownload).
        var avatar = rep?.DownloadUrl ?? string.Empty;

        try
        {
            await Api.UpdateMyProfileAsync(new UpdateProfileDto { Avatar = avatar });
        }
        catch (ApiException)
        {
            // 대표는 이미 바뀌었다. 헤더가 늦게 따라오는 것이 전부다.
        }

        // 화면이 그리는 것도 이 값이다 — 따로 들고 있지 않는다(Me.Changed 로
        // 헤더와 이 화면이 함께 다시 그려진다).
        Me.SetAvatar(avatar);
    }

    // ── 잔손질 ──────────────────────────────────────────────

    /// <summary>
    /// 시각을 사람이 읽게. <b>값이 없을 때 빈칸으로 두지 않는다</b> —
    /// 「기록이 없다」와 「못 불러왔다」가 구분되지 않는다.
    /// </summary>
    private static string Dt(DateTime? value) =>
        value is null ? "기록 없음" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
