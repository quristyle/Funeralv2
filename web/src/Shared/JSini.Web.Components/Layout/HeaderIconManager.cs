using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Settings;
using JSini.Web.Models;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 레이아웃 상단 도구 아이콘 관리자.
/// 
/// <para>
/// 1. DB(scom.layout_header_icons)에 정의된 기본 아이콘 목록과 default_enabled를 관리한다.
/// 2. 사용자 권한(VisibleMenus / IPermissionContext)에 따라 권한이 부여된 아이콘 목록만 추려낸다.
/// 3. 환경설정에서 제어하는 사용 여부(portal.headerIcon.{iconKey})를 계정 설정(PortalPrefs)과 연동한다.
/// </para>
/// </summary>
public sealed class HeaderIconManager : IDisposable
{
    private readonly PortalPrefs _prefs;
    private readonly IMenuProvider _menus;
    private readonly IPermissionContext _permissions;
    private readonly ILogger<HeaderIconManager> _logger;

    public event Action? Changed;

    public static readonly IReadOnlyList<LayoutHeaderIconDto> DefaultIcons =
    [
        new("quick_ask", "quick_ask", "빠른 지시", "jsini-icon-bolt", "projmng.ai.ask", "/projmng/ai/ask", true, 1, "AI 에게 한 줄 보내기 (서랍 또는 화면)"),
        new("ai_chat", "ai_chat", "AI 대화", "jsini-icon-robot", "site.ai.chat", "/ai/chat", true, 2, "AI 대화 서랍 열기"),
        new("note", "note", "쪽지", "jsini-icon-mail", "admin.note.write", "/admin/note/write", true, 3, "쪽지 쓰기 및 안 읽은 쪽지 확인"),
        new("notification", "notification", "알림", "jsini-icon-bell", "admin.push.history", "/system/push/history", true, 4, "알림함 서랍 열기"),
        new("favorite", "favorite", "즐겨찾기", "jsini-icon-star", null, null, true, 5, "현재 화면 즐겨찾기 추가/해제"),
        new("refresh", "refresh", "화면 새로 고침", "jsini-icon-refresh", null, null, true, 6, "현재 화면 새로 고침"),
        new("fullscreen", "fullscreen", "전체화면", "jsini-icon-expand", null, null, true, 7, "브라우저 전체화면 전환"),
        new("theme", "theme", "테마 변경", "jsini-icon-palette", null, null, true, 8, "테마 및 색상 설정 서랍 열기"),
    ];

    private IReadOnlyList<LayoutHeaderIconDto> _icons = DefaultIcons;
    private PortalPrefsData _prefsData = PortalPrefsData.Empty;

    public HeaderIconManager(
        PortalPrefs prefs,
        IMenuProvider menus,
        IPermissionContext permissions,
        ILogger<HeaderIconManager> logger)
    {
        _prefs = prefs;
        _menus = menus;
        _permissions = permissions;
        _logger = logger;

        _prefs.Changed += OnPrefsChanged;
        _menus.MenusChanged += OnMenusChanged;
        _ = LoadPrefsAsync();
    }

    private async Task LoadPrefsAsync()
    {
        try
        {
            _prefsData = await _prefs.ReadAsync();
            Changed?.Invoke();
        }
        catch
        {
            // 기본값 유지
        }
    }

    public IReadOnlyList<LayoutHeaderIconDto> AllIcons => _icons;

    public void Apply(IReadOnlyList<LayoutHeaderIconDto>? icons)
    {
        if (icons is { Count: > 0 })
        {
            _icons = icons;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 해당 사용자가 이 아이콘에 대한 권한을 가지고 있는가.
    /// 메뉴 및 라우트 키가 지정되지 않은 일반 도구는 모든 로그인 사용자에게 권한이 부여된다.
    /// </summary>
    public bool HasPermission(LayoutHeaderIconDto icon) => HasPermission(icon, null);

    public bool HasPermission(LayoutHeaderIconDto icon, IPermissionContext? permissions)
    {
        if (string.IsNullOrWhiteSpace(icon.RouteKey) && string.IsNullOrWhiteSpace(icon.Path))
        {
            return true;
        }

        var perm = permissions ?? _permissions;

        var menu = MenuLookup.Find(_menus.VisibleMenus, icon.RouteKey ?? "", icon.Path ?? "");
        if (menu != null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(icon.Path) && perm.CanView(icon.Path))
        {
            return true;
        }

        if (icon.IconKey == "ai_chat" && (perm.CanView("/ai/chat") || perm.CanView("/site/ai/chat")))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 사용자에게 권한이 부여된 상단 아이콘 목록만 정렬 순서대로 반환한다.
    /// </summary>
    public IReadOnlyList<LayoutHeaderIconDto> GetPermittedIcons(IPermissionContext? permissions = null)
    {
        return _icons.Where(i => HasPermission(i, permissions)).OrderBy(x => x.OrderNo).ToList();
    }

    /// <summary>
    /// 상단 아이콘 사용 여부를 확인한다.
    /// 계정 설정(portal.headerIcon.{iconKey})이 있으면 그 값을 따르고,
    /// 설정된 적이 없으면 DB의 기본값(DefaultEnabled)을 따른다.
    /// </summary>
    public bool IsIconEnabled(string iconKey)
    {
        var flag = _prefsData.Flag(PrefKey(iconKey));
        if (flag.HasValue)
        {
            return flag.Value;
        }

        var icon = _icons.FirstOrDefault(x => string.Equals(x.IconKey, iconKey, StringComparison.OrdinalIgnoreCase));
        return icon?.DefaultEnabled ?? true;
    }

    /// <summary>
    /// 상단 아이콘 사용 여부를 변경하고 계정에 저장한다.
    /// </summary>
    public async Task SetIconEnabledAsync(string iconKey, bool enabled)
    {
        _prefsData = _prefsData.With(PrefKey(iconKey), enabled);
        Changed?.Invoke();
        await _prefs.SetAsync(PrefKey(iconKey), enabled);
    }

    public static string PrefKey(string iconKey) => $"portal.headerIcon.{iconKey}";

    private void OnPrefsChanged(System.Text.Json.JsonElement payload)
    {
        _prefsData = PortalPrefsData.From(payload);
        Changed?.Invoke();
    }

    private void OnMenusChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _prefs.Changed -= OnPrefsChanged;
        _menus.MenusChanged -= OnMenusChanged;
    }
}
