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

    /// <summary>사진을 크게 보는 공통 창. 레이아웃이 한 벌 들고 있다.</summary>
    [Inject] private ImagePreview ImgView { get; set; } = default!;

    /// <summary>사진은 서른 장까지. Vue 의 <c>:limit="30"</c> 을 그대로 옮겼다.</summary>
    private const int PhotoLimit = 30;

    /// <summary>탭. 선언 순서가 <c>DxTabs</c> 안의 <c>DxTabPage</c> 순서와 같아야 한다.
    /// 어긋나면 <c>?tab=</c> 이 엉뚱한 탭을 연다.</summary>
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

    /// <summary>휴대폰 폭인가(≤767px). 첫 그림은 언제나 <c>false</c> 다 — 서버가 그릴 때는 화면 크기를 몰라 데스크톱 배치가 먼저 나간다.</summary>
    private bool _isPhone;

    /// <summary>고정탭의 두 판이 위아래로 쌓였는가(≤991px). 휴대폰 경계(767px)와 다른 값이다.</summary>
    private bool _isPinsStacked;

    /// <summary>탭 머리에 적는 이름. 휴대폰에서는 짧은 쪽을 쓴다.</summary>
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

    /// <summary>줄인 이름. 휴대폰 탭 머리는 CSS 가 감추므로, CSS 가 안 실렸을 때를 위해 남긴다.</summary>
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

    /// <summary>휴대폰이고 주소에 <c>?tab=</c> 이 없을 때 목록을 보인다. 상태를 들지 않고 주소로 판정한다.</summary>
    private bool ShowHub => _isPhone && !_tabInUrl;

    /// <summary>주소에 <c>?tab=</c> 이 있었는가.</summary>
    private bool _tabInUrl;

    /// <param name="Tab">눌렀을 때 열리는 조각.</param>
    /// <param name="Icon">줄 왼쪽의 그림. 사용자 메뉴(<c>UserMenu</c>)와 같은 그림을 쓴다.</param>
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
                    _activity is { RecentFailCount: > 0 }
                        ? $"실패 {_activity.RecentFailCount}"
                        : (_info?.IsSocialMerged == true ? "소셜결합" : (_info?.IsSocialLinked == true ? "소셜연동" : null)),
                    _activity is { RecentFailCount: > 0 }
                        ? "jsini-badge--warn"
                        : (_info?.IsSocialMerged == true ? "jsini-badge--info" : "jsini-badge--muted")),

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

    /// <summary>기본 설정 줄의 요약. 이름은 바로 위 얼굴 판이 이미 적으므로 쓰지 않는다.</summary>
    private string BasicSummary
    {
        get
        {
            var emailPart = _info!.Emails.Count > 1
                ? $"이메일 {_info.Emails.Count}개"
                : _info.Email;
            var phonePart = _info.Phones.Count > 1
                ? $"연락처 {_info.Phones.Count}개"
                : _info.Phone;

            var parts = new[] { emailPart, phonePart }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            return parts.Length == 0 ? "이메일 · 전화번호 · 생년월일" : string.Join(" · ", parts);
        }
    }

    private string AccountSummary
    {
        get
        {
            var baseText = _info!.LastLoginAt is null
                ? $"가입 {Dt(_info.CreatedAt)}"
                : $"최근 로그인 {Dt(_info.LastLoginAt)}";

            if (_info.IsSocialMerged && !string.IsNullOrWhiteSpace(_info.SocialProviderText))
            {
                return $"{baseText} · 결합: {_info.SocialProviderText}";
            }

            if (_info.IsSocialLinked && !string.IsNullOrWhiteSpace(_info.SocialProviderText))
            {
                return $"{baseText} · 연동: {_info.SocialProviderText}";
            }

            return baseText;
        }
    }

    /// <summary>보안 줄의 요약. 없는 것은 세지 않고 있는 것만 말한다.</summary>
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

    /// <summary>기본 설정 탭에서 직접 수정/추가 가능한 전화번호 목록</summary>
    private List<PhoneEditItem> _phoneEntries = [];

    // ── 이메일 인증 관련 상태 ──
    private bool _showEmailChange;
    private string _newEmail = string.Empty;
    private string _emailVerificationCode = string.Empty;
    private bool _emailCodeSent;
    private bool _emailVerified;
    private string? _emailVerifyMessage;
    private bool _emailVerifySuccess;
    private bool _isEmailSending;
    private bool _isEmailVerifying;

    // ── 고정탭 ──────────────────────────────────────────────

    /// <summary>내 메뉴를 펼쳐 「열 수 있는 화면」만 남긴 것.</summary>
    private IReadOnlyList<MenuNode> _screens = [];

    /// <summary>담아 뒀지만 지금 내 메뉴에 없는 개수.</summary>
    private int _hiddenPins;

    /// <summary>왼쪽 목록에서 고른 것들.</summary>
    private IEnumerable<MenuFavorite> _pickedPinsSource = [];

    /// <summary>오른쪽 목록에서 고른 것들.</summary>
    private IEnumerable<MenuNode> _pickedMenusSource = [];

    /// <summary>고른 것을 목록으로 굳혀 둔 것. <c>DxListBox.Values</c> 는 IEnumerable 이어야 해서 부품에 물리는 값과 갈라 둔다.</summary>
    /// 화면이 개수와 첫 항목을 자주 묻으니 지연 열거를 피한다.
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

    // ── 켬·끔 한 줄 ─────────────────────────────────────────

    /// <summary>보안 설정과 알림 설정이 같은 모양을 쓴다 — 서버가 둘을 같은 표에 이름·값으로 쌓는다.</summary>
    /// <param name="Name">줄 이름.</param>
    /// <param name="Desc">아래에 옅게 붙는 설명.</param>
    /// <param name="Value">지금 켜져 있는가.</param>
    /// <param name="Field">저장 이름. <c>nameof(UserInfoDto.…)</c> 로 만든다 — 글자로 적으면 조회와 저장 칸이 어긋날 수 있다.</param>
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

    /// <summary>순서 단추는 한 건만 골랐을 때만 켠다 — 여러 건을 올리고 내리는 규칙은 기대가 사람마다 갈린다.</summary>
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

        // 얼굴은 레이아웃이 읽는다. 이 화면이 먼저 떠도 알림을 받아 사진으로 바뀐다.
        Me.Changed += OnMeChanged;

        RebuildScreens();

        await ReloadAsync();

        // 소셜 연결에서 되돌아온 결과를 말한다. 목록을 다 읽은 뒤에 불러야 연결한 것이 목록에 없는 순간이 안 생긴다.
        SaySocialResult();
    }

    /// <summary>주소가 바뀌면 탭도 따라간다. 이미 떠 있는 화면은 <c>OnInitializedAsync</c> 를 다시 타지 않는다.</summary>
    protected override void OnParametersSet() => ReadTabFromUrl();

    /// <summary>브라우저가 패스키를 쓸 수 있는지 묻는다. <c>OnInitializedAsync</c> 에서는 JS 가 없어 부르지 않는다.
    /// 예외는 삼킨다 — 못 물어본 것은 「이 브라우저는 못 쓴다」와 같다.</summary>
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

    /// <summary>주소에서 탭을 읽는다. 「없다」도 답이다 — 휴대폰에서 못 읽으면 목록을 보이도록 <c>_tabInUrl</c> 을 내려 둔다.</summary>
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

        // 소셜 공급자에 다녀오면 <c>?social=linked</c> 만 붙어 돌아온다. 휴대폰에서 목록으로 떨어뜨리면 결과가 안 보이므로 보안 설정에 머문다.
        if (parsed.ContainsKey("social"))
        {
            _tab = Tab.Security;
            _tabInUrl = true;
            return;
        }

        _tabInUrl = false;
    }

    /// <summary>DxTabs 에서 탭을 눌렀을 때. 번호를 열거형으로 되돌린다.</summary>
    private void OnTabIndexChanged(int index)
    {
        if (Enum.IsDefined(typeof(Tab), index))
        {
            Go((Tab)index);
        }
    }

    /// <summary>탭을 바꾸면서 주소도 함께 바꾼다 — 새로고침이나 링크로 같은 자리가 열려야 한다.</summary>
    private void Go(Tab tab)
    {
        _tab = tab;
        _tabInUrl = true;

        var name = TabNames.First(pair => pair.Value == tab).Key;

        // 데스크톱은 replace 다. 휴대폰에서만 쌓는다 — 목록에서 한 층 들어가는 것이라 기기 뒤로 가기가 목록으로 돌아와야 한다.
        Navigation.NavigateTo($"/admin/profile?tab={name}", replace: !_isPhone);
    }

    /// <summary>목록으로 되돌아간다(휴대폰). replace 로 되돌려야 기기 뒤로 가기가 방금 나온 조각으로 다시 들어가지 않는다.</summary>
    private void BackToHub()
    {
        _tabInUrl = false;
        Navigation.NavigateTo("/admin/profile", replace: true);
    }

    // ── 조회 ────────────────────────────────────────────────

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var info = Api.GetMyInfoAsync();

        // 접속 기록을 못 받아도 계정 값은 보여 준다.
        var activity = SafeAsync(Api.GetMyActivityAsync());

        // 즐겨찾기는 이 화면이 고치는 자리라 들어올 때 최신으로 맞춘다.
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
            Emails = _info.Emails.ToList(),
            Phone = _info.Phone,
            Phones = _info.Phones.ToList(),
            Introduction = _info.Introduction,
        };

        _phoneEntries = _info.Phones is { Count: > 0 }
            ? _info.Phones.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => new PhoneEditItem { Number = p }).ToList()
            : (!string.IsNullOrWhiteSpace(_info.Phone) ? [new PhoneEditItem { Number = _info.Phone }] : [new PhoneEditItem()]);

        _showEmailChange = false;
        _newEmail = string.Empty;
        _emailVerificationCode = string.Empty;
        _emailCodeSent = false;
        _emailVerified = false;
        _emailVerifyMessage = null;
        _emailVerifySuccess = false;

        _birthDate = DateTime.TryParse(_info.BirthDate, out var d) ? d : null;
        _isLunar = _info.BirthDateIsLunar;
    }

    private static string SocialBadgeClass(string provider) => provider.ToLowerInvariant() switch
    {
        "kakao" => "jsini-badge--warn",
        "naver" => "jsini-badge--on",
        "google" => "jsini-badge--info",
        _ => "jsini-badge--muted"
    };

    private static string SocialProviderName(string provider) => provider.ToLowerInvariant() switch
    {
        "kakao" => "카카오",
        "naver" => "네이버",
        "google" => "구글",
        var other => other
    };

    // ── 전화번호 편집 ──────────────────────────────────────

    private void AddPhoneEntry()
    {
        _phoneEntries.Add(new PhoneEditItem());
    }

    private void RemovePhoneEntry(PhoneEditItem item)
    {
        _phoneEntries.Remove(item);
        if (_phoneEntries.Count == 0)
        {
            _phoneEntries.Add(new PhoneEditItem());
        }
    }

    private void SetPrimaryPhone(PhoneEditItem item)
    {
        if (_phoneEntries.Remove(item))
        {
            _phoneEntries.Insert(0, item);
        }
    }

    // ── 이메일 인증 ────────────────────────────────────────

    private void ToggleEmailChange()
    {
        _showEmailChange = !_showEmailChange;
        _newEmail = string.Empty;
        _emailVerificationCode = string.Empty;
        _emailCodeSent = false;
        _emailVerified = false;
        _emailVerifyMessage = null;
        _emailVerifySuccess = false;
    }

    private void CancelEmailChange()
    {
        _showEmailChange = false;
        _newEmail = string.Empty;
        _emailVerificationCode = string.Empty;
        _emailCodeSent = false;
        _emailVerified = false;
        _emailVerifyMessage = null;
        _emailVerifySuccess = false;
    }

    private async Task SendEmailCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newEmail) || !_newEmail.Contains('@') || !_newEmail.Contains('.'))
        {
            _emailVerifyMessage = "올바른 이메일 주소를 입력해 주십시오.";
            _emailVerifySuccess = false;
            return;
        }

        if (string.Equals(_newEmail.Trim(), _edit.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _emailVerifyMessage = "현재 등록된 대표 이메일과 동일합니다.";
            _emailVerifySuccess = false;
            return;
        }

        _isEmailSending = true;
        _emailVerifyMessage = "인증번호 발송 중입니다…";
        _emailVerifySuccess = false;

        try
        {
            var res = await Api.SendEmailVerificationCodeAsync(_newEmail.Trim());
            _emailCodeSent = true;
            _emailVerifySuccess = true;
            var devInfo = !string.IsNullOrWhiteSpace(res?.DevCode) ? $" (테스트 코드: {res.DevCode})" : string.Empty;
            _emailVerifyMessage = (res?.Message ?? "인증번호를 발송했습니다. 5분 이내에 입력해 주십시오.") + devInfo;
        }
        catch (ApiException ex)
        {
            _emailVerifyMessage = ex.Message;
            _emailVerifySuccess = false;
        }
        catch (Exception)
        {
            _emailVerifyMessage = "인증번호 발송에 실패했습니다. 다시 시도해 주십시오.";
            _emailVerifySuccess = false;
        }
        finally
        {
            _isEmailSending = false;
        }
    }

    private async Task VerifyEmailCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(_emailVerificationCode) || _emailVerificationCode.Trim().Length != 6)
        {
            _emailVerifyMessage = "6자리 인증번호를 정확히 입력해 주십시오.";
            _emailVerifySuccess = false;
            return;
        }

        _isEmailVerifying = true;
        _emailVerifyMessage = "인증번호 확인 중입니다…";

        try
        {
            var res = await Api.VerifyEmailCodeAsync(_newEmail.Trim(), _emailVerificationCode.Trim());
            _emailVerified = true;
            _emailVerifySuccess = true;
            _edit.EmailVerificationToken = res?.VerificationToken;

            // 인증 완료된 새 이메일을 기본 이메일로 반영
            var updatedEmail = _newEmail.Trim();
            _edit.Email = updatedEmail;
            var emails = _edit.Emails.Where(e => !string.Equals(e, updatedEmail, StringComparison.OrdinalIgnoreCase)).ToList();
            emails.Insert(0, updatedEmail);
            _edit.Emails = emails;

            _emailVerifyMessage = "이메일 인증이 완료되었습니다! [저장] 단추를 누르면 프로필에 최종 반영됩니다.";
        }
        catch (ApiException ex)
        {
            _emailVerifyMessage = ex.Message;
            _emailVerifySuccess = false;
        }
        catch (Exception)
        {
            _emailVerifyMessage = "인증번호 확인에 실패했습니다. 다시 시도해 주십시오.";
            _emailVerifySuccess = false;
        }
        finally
        {
            _isEmailVerifying = false;
        }
    }

    // ── 기본 설정 저장 ──────────────────────────────────────

    /// <summary>저장한다. 생일은 <c>yyyy-MM-dd</c> 글자로 보내고, 비우면 빈 문자열이다 — <c>null</c> 은 서버가 「건드리지 말라」로 읽는다.</summary>
    private async Task SaveBasicAsync()
    {
        // 만약 새 이메일을 입력 중인데 인증이 완료되지 않았으면 저장 차단
        if (_showEmailChange && !_emailVerified && !string.IsNullOrWhiteSpace(_newEmail))
        {
            _emailVerifyMessage = "이메일 변경을 완료하려면 먼저 이메일 인증을 진행해 주십시오.";
            _emailVerifySuccess = false;
            return;
        }

        // 전화번호 목록 정리
        var validPhones = _phoneEntries
            .Select(p => p.Number.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        _edit.Phones = validPhones;
        _edit.Phone = validPhones.FirstOrDefault();

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

    /// <summary>이 브라우저가 패스키를 다룰 수 있는가. 기본은 「못 한다」다 — 프리렌더 때는 값이 없고, 「할 수 있다」로 두면 단추가 잠깐 열렸다 잠긴다.</summary>
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

    /// <summary>이 기기에 패스키를 만든다. 서버가 도전값을 주고, 브라우저(<c>passkey.js</c>)가 지문을 받아 열쇠를 만들고, 서버가 검증·저장한다.
    /// 단추의 <c>Click</c> 에서 불러야 기기 확인 창이 열린다. 취소하면 서버에 반쪽 줄이 남지 않는다.</summary>
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
                // 스크립트가 아예 없을 때 여기로 온다 — 기기 거절은 passkey.js 가 <c>Ok: false</c> 로 돌려준다.
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

    /// <summary>이름을 안 지어 주면 목록에서 줄을 구분할 수 없어 순번으로 갈라 둔다.</summary>
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

        // 마지막 기기를 지웠는데 우선순위가 지문으로 남으면 로그인 화면이 반드시 실패하는 기기 확인을 연다.
        if (_passkeys.Count == 0 && _browser.Priority == PasskeyPriority.Passkey)
        {
            await SetAuthPriorityAsync(PasskeyPriority.Password, quiet: true);
        }

        if (_passkeys.Count == 0)
        {
            await ForgetPasskeyUserAsync();
        }
    }

    /// <summary>이 브라우저가 패스키로 들어갈 아이디. 기기가 열쇠를 스스로 들고 있지 않으면 아이디가 있어야 후보를 받는다. 계정이 아니라 브라우저에 적는다.</summary>
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
            // 못 적어도 로그인은 아이디를 한 번 치면 된다. 등록은 이미 끝났다.
        }
    }

    /// <summary>마지막 기기를 지우면 적어 둔 아이디도 지운다 — 그 아이디일 때만(passkey.js 의 <c>forgetUser</c>).</summary>
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

    /// <summary>고를 수 있는 두 가지. 이름을 글자로 두 번 적지 않는다 — 딱지와 저장값이 갈리면 안 된다.</summary>
    private sealed record AuthPriorityOption(string Value, string Text);

    private static readonly AuthPriorityOption[] AuthPriorityOptions =
    [
        new(PasskeyPriority.Password, "비밀번호 먼저"),
        new(PasskeyPriority.Passkey, "지문 · 얼굴 먼저"),
    ];

    /// <summary>고를 수 있는가. 등록한 기기가 없으면 잠근다 — 고르게 두면 로그인 화면이 반드시 실패한다.</summary>
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

    /// <summary>우선순위를 이 기기에 적는다. 서버로 가지 않는다(로그인 전에 읽어야 하므로).
    /// 적고 나서 다시 읽은 값을 화면에 넣는다 — 사생활 보호 모드에서는 저장이 던져 화면과 로그인이 어긋난다.</summary>
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
            // 마지막 기기를 지워 되돌린 자리다. 토스트를 더 쌓지 않고 까닭만 한 줄로 알린다.
            Say("등록된 기기가 없어져 비밀번호 먼저로 되돌렸습니다.", NoticeTone.Warning);
            return;
        }

        Say(saved == PasskeyPriority.Passkey
            ? "이 기기에서는 지문·얼굴을 먼저 묻습니다."
            : "이 기기에서는 비밀번호를 먼저 묻습니다.");
    }

    /// <summary>기기 목록을 다시 읽는다. 못 읽어도 화면을 세우지 않는다 — 패스키는 이 화면의 한 줄일 뿐이다.</summary>
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

    /// <summary>아직 안 붙인 공급자. 이미 붙인 것에는 「연결」 단추를 세우지 않는다 — 눌러 봐야 서버가 같은 줄을 돌려줄 뿐이다.</summary>
    private IReadOnlyList<SocialProviderDto> UnlinkedProviders =>
        [.. _socialProviders.Where(p => !_socialLinks.Any(l =>
            string.Equals(l.Provider, p.Provider, StringComparison.OrdinalIgnoreCase)))];

    private string SocialSummary => _socialLinks.Count switch
    {
        0 => "연결해 두면 아이디를 치지 않고 그 계정으로 들어올 수 있습니다.",
        1 => $"{_socialLinks[0].DisplayName} 계정 하나가 연결되어 있습니다.",
        var n => $"{n}개 계정이 연결되어 있습니다.",
    };

    /// <summary>목록에서 어느 줄인지 알아보는 단서. 공급자가 이름도 이메일도 안 주면 말을 아낀다.</summary>
    private static string SocialWho(SocialLinkDto link) =>
        (link.AccountName, link.Email) switch
        {
            ({ Length: > 0 } name, { Length: > 0 } email) => $"{name} ({email})",
            ({ Length: > 0 } name, _) => name,
            (_, { Length: > 0 } email) => email,
            _ => "계정 정보 없음",
        };

    /// <summary>연결하러 공급자에 다녀온다. <c>forceLoad</c> 가 있어야 한다 — 셸의 엔드포인트로 가므로 회로 안 이동은 404 가 된다.</summary>
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

    /// <summary>연결 목록과 공급자 목록을 함께 읽는다. 못 읽어도 나머지를 막지 않는다.</summary>
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

    /// <summary>소셜 연결 뒤 되돌아온 표시를 읽는다. 짧은 표시만 주고받는다 — 문구를 주소에 실으면 남이 만든 링크로 아무 말이나 띄울 수 있다.</summary>
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
                // 까닭이 여럿이라(이미 다른 계정에 붙음 · 인증 만료 · 공급자 무응답) 사용자에게는 「다시 눌러 본다」로만 말한다.
                Say("소셜 계정을 연결하지 못했습니다. 다시 시도해 주세요.", NoticeTone.Error);
                break;
        }
    }

    // ── 보안·알림 켬끔 ──────────────────────────────────────

    /// <summary>켬·끔 하나를 저장한다. 화면 값을 먼저 바꾸지 않는다 — 실패했는데 스위치가 켜져 있으면 안 된다. 성공하면 다시 읽어 맞춘다.</summary>
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

    /// <summary>내 메뉴를 펼쳐 목록에 놓을 화면만 남긴다. 묶음·숨긴 메뉴·외부 링크는 뺀다.
    /// 자식이 있어도 자기 화면이 있는 메뉴는 남긴다 — 건너뛰면 헤더의 별로 담은 것을 여기서 해제할 수 없다.</summary>
    private void RebuildScreens()
    {
        var screens = new List<MenuNode>();
        Walk(Menus.VisibleMenus, screens);
        _screens = screens;

        // 담아 뒀는데 목록에 없는 것을 센다. 권한이 바뀌었거나 메뉴가 지워진 경우다 — 조용히 빼면 해제가 안 되어 보인다.
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
                // 한 건씩 보낸다 — 서버가 여러 건을 받는 경로가 없다. 중간에 실패하면 앞엣것은 담긴 채로 남는다.
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

    /// <summary>고른 한 건을 한 칸 위·아래로 옮긴다. 순서를 통째로 보내야 중간 실패 때 서버와 화면이 다른 순서로 남지 않는다.</summary>
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

    /// <summary>사진을 크게 본다. 창은 공통 미리보기(<see cref="ImagePreview"/>)에 맡기고 목록을 넘긴다 — 확대·회전이 있어야 세로로 찍힌 증명사진도 바로 세워 본다.</summary>
    private void Preview(GroupFileDto photo)
    {
        // 아이디가 없는 줄은 뺀 뒤의 목록에서 자리(index)를 센다.
        var photos = _photos.Where(p => !string.IsNullOrWhiteSpace(p.Id)).ToList();

        ImgView.OpenAll(
            photos.Select(p => new ImagePreviewItem(
                FileDownload.UrlFor(p.Id!, p.OriginalName), p.OriginalName)),
            Math.Max(0, photos.FindIndex(p => p.Id == photo.Id)));
    }

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
            // 그룹이 지워졌을 수 있다. 사진을 못 읽는다고 화면 전체를 막지 않는다.
            _photos = [];
        }
    }

    /// <summary>한 장이 올라갔을 때 다시 읽는다. 올리는 일은 셸 중계(<see cref="ProfilePhotoUpload"/>)가 하고, 여러 장이면 장마다 불린다.</summary>
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

    /// <summary>사진 한 장을 지운다. 대표를 지웠으면 남은 첫 장을 대표로 올린다 — 안 하면 헤더가 빈 동그라미가 된다.</summary>
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

        // 지운 것을 크게 보고 있었으면 창을 닫는다 — 없는 파일을 가리키는 창이 깨진 채로 남는다.
        if (ImgView.Current?.Url.Contains(fileId, StringComparison.OrdinalIgnoreCase) == true)
        {
            ImgView.Close();
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

    /// <summary>대표 사진을 계정의 <c>Avatar</c> 로 저장하고 헤더에도 알린다. 대표는 파일 그룹이, 헤더 얼굴은 계정이 들고 있어 맞춰 줘야 한다.</summary>
    private async Task SyncRepresentativeAsync()
    {
        var rep = _photos.FirstOrDefault(p => p.IsRepresentative) ?? _photos.FirstOrDefault();

        // 저장하는 값은 서버가 준 주소 그대로다 — 셸 중계 경로로 바꿔 저장하면 다른 서비스가 그 값을 못 읽는다.
        var avatar = rep?.DownloadUrl ?? string.Empty;

        try
        {
            await Api.UpdateMyProfileAsync(new UpdateProfileDto { Avatar = avatar });
        }
        catch (ApiException)
        {
            // 대표는 이미 바뀌었다. 헤더가 늦게 따라오는 것이 전부다.
        }

        // 화면이 그리는 것도 이 값이다 — 따로 들고 있지 않는다(Me.Changed 로 헤더와 함께 다시 그린다).
        Me.SetAvatar(avatar);
    }

    // ── 잔손질 ──────────────────────────────────────────────

    /// <summary>시각을 사람이 읽게 바꾼다. 값이 없을 때 빈칸으로 두지 않는다 — 「기록이 없다」와 「못 불러왔다」가 구분되지 않는다.</summary>
    private static string Dt(DateTime? value) =>
        value is null ? "기록 없음" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
