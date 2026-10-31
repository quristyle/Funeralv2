using System.Text.Json;
using JSini.Web.Components.Settings;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 회로가 붙을 때 브라우저 저장소 값을 한 번의 왕복으로 읽는다. 누가 먼저 불러도 같은 <c>Task</c> 를 기다린다.
/// 열쇠는 이 클래스에만 적는다 — theme.js 에 또 적으면 한쪽만 고치는 날이 온다.
/// </summary>
public sealed class PortalBoot(IJSRuntime js, PortalPrefs prefs, ILogger<PortalBoot> logger)
{
    // 브라우저 저장소 열쇠의 정본. 계정으로 옮긴 옛 값은 AdoptBrowserPrefsAsync 가 한 번 올린 뒤 지운다.

    /// <summary>
    /// 잠금화면. 기기에 남기고(localStorage) 로그인 화면을 지날 때만 지운다 — 세션이면 앱 재시작으로 풀린다.
    /// </summary>
    public const string ScreenLockedKey = "jsini.screen-locked";

    /// <summary>로그인 뒤 공지를 이 탭에서 닫았다.</summary>
    public const string NoticeClosedUserKey = "jsini-notice-closed:user";

    /// <summary>
    /// 공개 공지를 이 탭에서 닫았다. 로그인용과 따로 둔다 — 하나면 로그인 전에 닫은 공지를 로그인 뒤에 못 본다.
    /// 읽고 쓰는 것은 theme.js 의 jsiniNotice 이고 여기는 열쇠 글자의 정본이다.
    /// </summary>
    public const string NoticeClosedPublicKey = "jsini-notice-closed:public";

    /// <summary>「오늘 하루 보지 않기」. 날짜가 바뀌면 없던 일이 되므로 localStorage 다.</summary>
    public const string NoticeDismissedKey = "jsini-notice-dismissed";

    /// <summary>고정해 둔 탭. 이 브라우저에만 남는다 — 서버에 두면 즐겨찾기가 된다.</summary>
    public const string PinnedTabsKey = "jsini-tabs-pinned";

    /// <summary>알림 구독 권유를 이 탭에서 닫았다(「나중에」). 탭을 닫으면 다시 묻는다.</summary>
    public const string PushAskClosedKey = "jsini-push-ask-closed";

    /// <summary>「다시 묻지 않기」. 기기에 남는다 — 이 창이 묻는 것은 「이 브라우저로 받겠는가」라서 계정에 두지 않는다.</summary>
    public const string PushAskNeverKey = "jsini-push-ask-never";

    /// <summary>
    /// 휴대폰에서 권유 창을 접어 둘 기한(UTC, o 꼴). 기기에 남긴다 — 탭에만 두면 앱을 껐다 켜는 것만으로 기한이 사라진다.
    /// </summary>
    public const string PushAskSnoozeKey = "jsini-push-ask-snooze";

    /// <summary>위치 권유를 이 탭에서 닫았다(「나중에」).</summary>
    public const string GeoAskClosedKey = "jsini-geo-ask-closed";

    /// <summary>「다시 묻지 않기」. 기기에 남는다 — 위치 권한 자체가 브라우저마다 따로다.</summary>
    public const string GeoAskNeverKey = "jsini-geo-ask-never";

    /// <summary>AI 대화를 사내 자료(RAG)로만 할지. 적혀 있으면 전용이다. 기기에 남긴다 — 자리마다 묻는 것이 다르다.</summary>
    public const string AiRagOnlyKey = "jsini-ai-rag-only";

    /// <summary>「사내 자료 전용」에서 찾을 범위. <c>카테고리</c> 또는 <c>카테고리/분류id</c>. 없으면 모든 카테고리.</summary>
    public const string AiRagScopeKey = "jsini-ai-rag-scope";

    /// <summary>
    /// 이 브라우저에서 위치를 마지막으로 다룬 시각(UTC, o 꼴). 저장·닫힘·이미 줬음 모두 찍는다.
    /// 서버를 부르기 전에 판단하려고 기기에 둔다 — 화면을 옮길 때마다 서버를 부르지 않게 한다.
    /// </summary>
    public const string GeoSyncedAtKey = "jsini-geo-synced-at";

    /// <summary>끌어 넓혀 둔 사이드바 폭(px). 화면 크기에 따라 달라 기기에 남는다.</summary>
    public const string SidebarWidthKey = "jsini-sidebar-width";

    /// <summary>데스크탑에서 끌어 넓힌 오른쪽 서랍 폭(px). 브라우저와 계정에 함께 보존한다.</summary>
    public const string RightDrawerWidthKey = "jsini_right_drawer_width";

    /// <summary>빠른 지시 서랍의 저장 너비 열쇠.</summary>
    public const string QuickAskDrawerWidthKey = "jsini_drawer_width_jsini-quick-ask-drawer";

    /// <summary>모바일 메뉴 단추(FAB) 위치. bottom-left(기본) · top-left · bottom-right · top-right.</summary>
    public const string FabPositionKey = "jsini-fab-position";

    /// <summary>
    /// 모바일 메뉴 단추를 감출지. 열쇠가 있으면 감춘다. 값은 계정(PortalPrefs.FabHiddenKey)으로 옮겼고 여기는 옛 값 올림용이다.
    /// 감추면 헤더의 ☰ 가 휴대폰에서 되살아나므로(app.css) 메뉴를 열 길이 없어지지 않는다.
    /// </summary>
    public const string FabHiddenKey = "jsini-fab-hidden";

    /// <summary>요청 등록 단추 위치. 기본이 메뉴 단추와 반대 귀퉁이다 — 둘 다 왼쪽 아래면 기본값끼리 겹친다.</summary>
    public const string HelpDeskFabPositionKey = "jsini-hdfab-position";

    /// <summary>
    /// 요청 등록 단추를 감출지. 열쇠가 있으면 감춘다(없으면 보인다). 그릴지는 권한이 먼저 정하고(MainLayout.HelpDeskFabAllowed) 이 값은 권한자가 안 보겠다고 한 것만 뜻한다.
    /// </summary>
    public const string HelpDeskFabHiddenKey = "jsini-hdfab-hidden";

    /// <summary>요청 등록 단추를 넓은 화면에서도 띄울지. 없는 것이 기본이라 휴대폰에서만 뜬다. 감춤(HelpDeskFabHiddenKey)이 이것보다 세다.</summary>
    public const string HelpDeskFabDesktopKey = "jsini-hdfab-desktop";

    /// <summary>휴대폰 아래 띠(MobileBottomNav)를 쓰지 않을지. 열쇠가 있으면 안 쓴다 — 없는 것이 기본이다. 값은 계정으로 옮겼고 여기는 옛 값 올림용이다.</summary>
    public const string BottomNavHiddenKey = "jsini-bottomnav-hidden";

    /// <summary>아래 띠에 놓을 칸 목록(JSON). 없으면 BottomNav.Defaults. 계정으로 옮겼고 여기는 옛 값 올림용이다.</summary>
    public const string BottomNavItemsKey = "jsini-bottomnav-items";

    /// <summary>토스트 위치(bottom-right 기본). 계정으로 옮겼고 여기는 옛 값 올림용이다.</summary>
    public const string ToastPositionKey = "jsini-toast-position";

    /// <summary>위치 자동 재확인 간격(분). 없으면 DefaultGeoSyncMinutes. 기기에 남는다 — 배터리와 재는 일이 기기의 것이다.</summary>
    public const string GeoSyncIntervalKey = "jsini-geo-sync-interval";

    /// <summary>휴대폰 두 손가락 확대를 풀어 두었는가. 없는 것이 기본이라 잠근다 — 열쇠가 있으면 푼 것. 실제로 읽는 것은 js/zoom.js 다.</summary>
    public const string ZoomUnlockedKey = "jsini-zoom-unlocked";

    /// <summary>고르지 않았을 때의 확인 간격 — 30분. 이동 경로가 쌓이면서 한 시간으로는 점심에 나갔다 온 자리가 빠진다.</summary>
    public const int DefaultGeoSyncMinutes = 30;

    private static readonly string[] SessionKeys =
    [
        NoticeClosedUserKey,
        PushAskClosedKey,
        GeoAskClosedKey,
    ];

    private static readonly string[] LocalKeys =
    [
        ScreenLockedKey,
        NoticeDismissedKey,
        PinnedTabsKey,
        SidebarWidthKey,
        RightDrawerWidthKey,
        QuickAskDrawerWidthKey,
        PushAskNeverKey,
        PushAskSnoozeKey,
        GeoAskNeverKey,
        GeoSyncedAtKey,
        AiRagOnlyKey,
        AiRagScopeKey,
        FabPositionKey,
        FabHiddenKey,
        HelpDeskFabPositionKey,
        HelpDeskFabHiddenKey,
        HelpDeskFabDesktopKey,
        BottomNavHiddenKey,
        BottomNavItemsKey,
        ToastPositionKey,
        GeoSyncIntervalKey,
        ZoomUnlockedKey,
    ];

    /// <summary>계정으로 올라간 다섯. 올린 뒤에는 브라우저에서 지운다 — 두 곳에 남으면 기기마다 참이 갈린다.</summary>
    private static readonly string[] MovedToAccountKeys =
    [
        BottomNavHiddenKey,
        BottomNavItemsKey,
        FabHiddenKey,
        HelpDeskFabHiddenKey,
        ToastPositionKey,
    ];

    /// <summary>읽어 온 것. 못 읽었으면 <see cref="BrowserState.Empty"/> — null 을 돌려주지 않아 부르는 쪽이 null 을 빠뜨리지 않게 한다.</summary>
    private Task<BrowserState>? _reading;

    /// <summary>워터마크에 쓸 이름. 읽기 전에 정해지면 같은 왕복에 태운다.</summary>
    private string? _watermark;
    private bool _watermarkAsked;

    /// <summary>워터마크 이름을 알린다. JS 를 부르지 않고 다음 읽기 왕복에 태운다. 이미 읽은 뒤라면 그때만 혼자 한 번 부른다.</summary>
    public void UseWatermark(string? name)
    {
        _watermark = name;
        _watermarkAsked = true;

        if (_reading is null)
        {
            return;
        }

        _ = ShowWatermarkLateAsync(name);
    }

    /// <summary>브라우저 상태를 읽는다. 회로가 붙은 뒤(OnAfterRenderAsync)에 불러야 한다. 여러 번 불러도 왕복은 한 번이다.</summary>
    public Task<BrowserState> ReadAsync() => _reading ??= ReadOnceAsync();

    /// <summary>읽어 둔 한 벌에 방금 고친 값을 반영한다. 아직 다 안 읽었으면 할 일이 없다 — 다음 읽기가 새 값을 가져온다.</summary>
    private void Remember(Func<BrowserState, BrowserState> change)
    {
        if (_reading is { IsCompletedSuccessfully: true } done)
        {
            _reading = Task.FromResult(change(done.Result));
        }
    }

    private async Task<BrowserState> ReadOnceAsync()
    {
        // 회로는 한 번에 하나만 돌아서 그사이 UseWatermark 가 끼어들지 않는다.
        var request = new BootRequest
        {
            Watermark = _watermarkAsked ? _watermark : null,
            Session = SessionKeys,
            Local = LocalKeys,
        };

        try
        {
            var wire = await js.InvokeAsync<BootWire>("jsiniBoot.read", request);

            var state = wire is null ? BrowserState.Empty : BrowserState.From(wire);

            SeedGeoSync(state);

            // 다섯은 계정이 이긴다 — 브라우저 값은 옛 값이라 올라간 적이 없으면 여기서 한 번 올린다.
            return await WithAccountPrefsAsync(state);
        }
        catch (JSException ex)
        {
            // theme.js 가 없거나 저장소가 막힌 브라우저다. 빈 값으로 넘긴다 — 특히 잠금은 못 읽었을 때 덮으면 풀 길이 없다.
            logger.LogDebug(ex, "브라우저 상태를 읽지 못했다. 없는 것으로 본다.");

            // 계정에 담긴 값은 저장소를 막아도 읽는다 — 아래 띠와 토스트 자리는 저장소에 기대지 않는다.
            return await WithAccountPrefsAsync(BrowserState.Empty);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // 모양이 어긋나거나 회로가 이미 끊긴 경우다. 위와 같이 다룬다.
            logger.LogDebug(ex, "브라우저 상태를 옮기지 못했다. 없는 것으로 본다.");
            return await WithAccountPrefsAsync(BrowserState.Empty);
        }
    }

    /// <summary>브라우저 값 위에 계정에 담긴 다섯을 얹는다. 계정에 열쇠가 없으면 브라우저의 옛 값을 한 번 올리고 그 자리를 걷는다 — 안 걷으면 기본값으로 되돌린 사람이 옛 값을 다시 받는다.</summary>
    private async Task<BrowserState> WithAccountPrefsAsync(BrowserState state)
    {
        var account = await prefs.AdoptFromBrowserAsync(
            state.BottomNavHidden,
            state.BottomNavItemsJson,
            state.FabHidden,
            state.HelpDeskFabHidden,
            state.ToastPositionStored);

        if (account is null)
        {
            account = await prefs.ReadAsync();
        }
        else
        {
            await ForgetMovedKeysAsync();
        }

        return state.WithAccountPrefs(account);
    }

    /// <summary>계정으로 올린 다섯을 브라우저에서 지운다. 못 지워도 된다 — 계정에 열쇠가 이미 있어 다시 올라가지 않는다.</summary>
    private async Task ForgetMovedKeysAsync()
    {
        foreach (var key in MovedToAccountKeys)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.removeItem", key);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                logger.LogDebug(ex, "계정으로 옮긴 설정을 브라우저에서 지우지 못했다.");
                return;
            }
        }
    }

    /// <summary>워터마크를 지금 걸거나 걷는다(name 이 null 이면 걷는다). 담아 둔 이름도 함께 고쳐야 아직 안 나간 읽기 왕복이 옛 이름으로 걷힌 것을 덮어쓰지 않는다.</summary>
    public Task ApplyWatermarkAsync(string? name)
    {
        var wanted = string.IsNullOrWhiteSpace(name) ? null : name;

        _watermark = wanted;
        _watermarkAsked = true;

        if (wanted is null)
        {
            // 걷는 것은 읽기와 무관하게 지금 부른다 — 화면에 걸린 것은 남아 있고 읽기는 거는 쪽만 태운다.
            return HideWatermarkLateAsync();
        }

        // 거는 쪽은 읽기가 아직이면 맡긴다 — 왕복 하나를 아낀다.
        return _reading is null ? Task.CompletedTask : ShowWatermarkLateAsync(wanted);
    }

    private async Task ShowWatermarkLateAsync(string? name)
    {
        try
        {
            await js.InvokeVoidAsync("jsiniWatermark.show", name);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "워터마크를 걸지 못했다.");
        }
    }

    private async Task HideWatermarkLateAsync()
    {
        try
        {
            await js.InvokeVoidAsync("jsiniWatermark.hide");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "워터마크를 걷지 못했다.");
        }
    }

    /// <summary>설치·구독 권유 창을 span 동안 접는다. 지나면 다시 뜬다(휴대폰에서 권유를 되풀이하는 길).</summary>
    /// <returns>적어 둔 기한. 저장에 실패해도 이 탭에서는 그 시각을 지킨다.</returns>
    public async Task<DateTime> SnoozePushAskAsync(TimeSpan span)
    {
        var until = DateTime.UtcNow.Add(span);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", PushAskSnoozeKey,
                until.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 사생활 보호 모드에서는 setItem 이 던진다 — 이 탭에서만 기한을 지키면 충분하다.
            logger.LogDebug(ex, "설치·구독 권유를 접어 둘 기한을 남기지 못했다.");
        }

        return until;
    }

    /// <summary>모바일 메뉴 단추(FAB) 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? FabPositionChanged;

    /// <summary>모바일 메뉴 단추(FAB) 위치를 저장하고 화면에 알린다.</summary>
    public async Task SetFabPositionAsync(string position)
    {
        var normalized = NormalizeFabPosition(position);

        try
        {
            // 열쇠와 값 둘뿐이다. 토스트 열쇠를 끼워 넣으면 그 글자가 저장 열쇠가 된다.
            await js.InvokeVoidAsync("localStorage.setItem", FabPositionKey, normalized);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "모바일 메뉴 단추 위치를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithFabPosition(normalized));
        FabPositionChanged?.Invoke(normalized);
    }

    /// <summary>모바일 메뉴 단추를 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? FabHiddenChanged;

    /// <summary>모바일 메뉴 단추를 감출지를 계정에 저장하고 화면에 알린다. 보이기로 하면 열쇠를 지운다(null).</summary>
    public async Task SetFabHiddenAsync(bool hidden)
    {
        Remember(state => state.WithFabHidden(hidden));
        FabHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.FabHiddenKey, hidden ? true : null);
    }

    /// <summary>헬프데스크 요청 등록 단추 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? HelpDeskFabPositionChanged;

    /// <summary>헬프데스크 요청 등록 단추 위치를 저장하고 화면에 알린다.</summary>
    public async Task SetHelpDeskFabPositionAsync(string position)
    {
        var normalized = NormalizeHelpDeskFabPosition(position);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", HelpDeskFabPositionKey, normalized);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "헬프데스크 요청 등록 단추 위치를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithHelpDeskFabPosition(normalized));
        HelpDeskFabPositionChanged?.Invoke(normalized);
    }

    /// <summary>헬프데스크 요청 등록 단추를 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? HelpDeskFabHiddenChanged;

    /// <summary>헬프데스크 요청 등록 단추를 감출지를 계정에 저장하고 화면에 알린다. 보이기로 하면 열쇠를 지운다.</summary>
    public async Task SetHelpDeskFabHiddenAsync(bool hidden)
    {
        Remember(state => state.WithHelpDeskFabHidden(hidden));
        HelpDeskFabHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.HelpDeskFabHiddenKey, hidden ? true : null);
    }

    /// <summary>요청 등록 단추를 넓은 화면에도 띄울지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? HelpDeskFabDesktopChanged;

    /// <summary>요청 등록 단추를 넓은 화면에도 띄울지를 저장한다. 끄기로 하면 열쇠를 지운다 — "0" 을 적으면 「있으면 그렇다」 판정에 걸려 켠 것으로 읽힌다.</summary>
    public async Task SetHelpDeskFabDesktopAsync(bool shown)
    {
        try
        {
            if (shown)
            {
                await js.InvokeVoidAsync("localStorage.setItem", HelpDeskFabDesktopKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", HelpDeskFabDesktopKey);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "헬프데스크 요청 등록 단추를 넓은 화면에 띄울지를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithHelpDeskFabDesktop(shown));
        HelpDeskFabDesktopChanged?.Invoke(shown);
    }

    /// <summary>확대 잠금을 풀지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? ZoomUnlockedChanged;

    /// <summary>휴대폰 확대 잠금을 풀지 저장하고 지금 화면에 곧바로 바른다. zoom.js 는 문서가 열릴 때 한 번만 읽으므로 jsiniZoom.lock 을 여기서 불러야 한다.</summary>
    public async Task SetZoomUnlockedAsync(bool unlocked)
    {
        try
        {
            if (unlocked)
            {
                await js.InvokeVoidAsync("localStorage.setItem", ZoomUnlockedKey, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", ZoomUnlockedKey);
            }

            await js.InvokeVoidAsync("jsiniZoom.lock", !unlocked);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "화면 확대 잠금 여부를 브라우저에 반영하지 못했다.");
        }

        Remember(state => state.WithZoomUnlocked(unlocked));
        ZoomUnlockedChanged?.Invoke(unlocked);
    }

    /// <summary>AI 대화를 사내 자료로만 할지 저장한다. 기본이 끔이라 켤 때만 적고, 끄면 열쇠를 지운다.</summary>
    public async Task SetAiRagOnlyAsync(bool only)
    {
        try
        {
            if (only) await js.InvokeVoidAsync("localStorage.setItem", AiRagOnlyKey, "1");
            else await js.InvokeVoidAsync("localStorage.removeItem", AiRagOnlyKey);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "사내 자료 전용 여부를 브라우저에 남기지 못했다.");
        }

        // 읽어 둔 한 벌도 함께 고친다 — 안 그러면 레이아웃이 새로 생길 때 옛 값이 되살아난다.
        Remember(state => state.WithAiRagOnly(only));
    }

    /// <summary>「사내 자료 전용」의 찾을 범위를 저장한다. 비우면(모든 카테고리) 열쇠를 지운다.</summary>
    public async Task SetAiRagScopeAsync(string? scope)
    {
        scope = string.IsNullOrWhiteSpace(scope) ? null : scope;
        try
        {
            if (scope is not null) await js.InvokeVoidAsync("localStorage.setItem", AiRagScopeKey, scope);
            else await js.InvokeVoidAsync("localStorage.removeItem", AiRagScopeKey);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "사내 자료 범위를 브라우저에 남기지 못했다.");
        }

        Remember(state => state.WithAiRagScope(scope));
    }

    /// <summary>아래 띠를 쓸지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavHiddenChanged;

    /// <summary>휴대폰 아래 띠를 쓰지 않을지를 계정에 저장한다. 칸 목록과 같은 곳에 담아야 기기마다 띠가 갈리지 않는다.</summary>
    public async Task SetBottomNavHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavHidden(hidden));
        BottomNavHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavHiddenKey, hidden ? true : null);
    }

    /// <summary>띠의 이름을 감출지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavLabelsHiddenChanged;

    /// <summary>띠 칸의 이름을 감출지를 계정에 저장한다. 보이기로 하면 열쇠를 지운다 — 거짓을 적으면 기본값을 바꿀 때 옛 값에 붙박인다.</summary>
    public async Task SetBottomNavLabelsHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavLabelsHidden(hidden));
        BottomNavLabelsHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavLabelsHiddenKey, hidden ? true : null);
    }

    /// <summary>같은 칸들을 넓은 화면 헤더에 세울지가 바뀌었을 때 알린다.</summary>
    public event Action<bool>? BottomNavDesktopHiddenChanged;

    /// <summary>띠 칸을 넓은 화면 헤더 오른쪽에 세우지 않을지를 계정에 저장한다. 칸 목록과 같은 곳에 담는다.</summary>
    public async Task SetBottomNavDesktopHiddenAsync(bool hidden)
    {
        Remember(state => state.WithBottomNavDesktopHidden(hidden));
        BottomNavDesktopHiddenChanged?.Invoke(hidden);

        await prefs.SetAsync(PortalPrefs.BottomNavDesktopHiddenKey, hidden ? true : null);
    }

    /// <summary>띠에 놓을 칸이 바뀌었을 때 알린다. 날것 JSON 을 그대로 준다.</summary>
    public event Action<string?>? BottomNavItemsChanged;

    /// <summary>띠에 놓을 칸들을 계정에 저장하고 화면에 알린다. 비우면 열쇠를 지운다(기본값으로 되돌림). 글자가 아니라 배열로 담는다.</summary>
    public async Task SetBottomNavItemsAsync(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json) ? null : json;

        Remember(state => state.WithBottomNavItems(value));
        BottomNavItemsChanged?.Invoke(value);

        await prefs.SetAsync(PortalPrefs.BottomNavItemsKey, AsArray(value));
    }

    /// <summary>날것 JSON 을 배열로 바꾼다. 비었거나 배열이 아니면 null — 그때 SetAsync 가 열쇠를 지운다.</summary>
    private static JsonElement? AsArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var element = JsonDocument.Parse(json).RootElement.Clone();
            return element.ValueKind == JsonValueKind.Array ? element : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>토스트 알림 위치가 바뀌었을 때 알린다.</summary>
    public event Action<string>? ToastPositionChanged;

    /// <summary>토스트 위치를 계정에 저장하고 화면에 알린다. 알림이 뜨는 자리는 사람의 버릇이라 기기가 아닌 계정에 둔다.</summary>
    public async Task SetToastPositionAsync(string position)
    {
        var normalized = NormalizeToastPosition(position);

        Remember(state => state.WithToastPosition(normalized));
        ToastPositionChanged?.Invoke(normalized);

        await prefs.SetAsync(PortalPrefs.ToastPositionKey, normalized);
    }

    /// <summary>토스트 위치 식별자를 검증한다. 모르는 값이면 bottom-right.</summary>
    public static string NormalizeToastPosition(string? position) => position switch
    {
        "top-left" => "top-left",
        "top-center" => "top-center",
        "top-right" => "top-right",
        "top-center-1-5" => "top-center-1-5",
        "top-center-1-4" => "top-center-1-4",
        "top-center-1-3" => "top-center-1-3",
        "bottom-left" => "bottom-left",
        "bottom-center" => "bottom-center",
        "bottom-right" => "bottom-right",
        "bottom-center-2-3" => "bottom-center-2-3",
        "bottom-center-1-3" => "bottom-center-2-3",
        "bottom-center-1-4" => "bottom-center-1-4",
        "bottom-center-3-4" => "bottom-center-1-4",
        "bottom-center-1-5" => "bottom-center-1-5",
        "bottom-center-4-5" => "bottom-center-1-5",
        "center" => "center",
        _ => "bottom-right",
    };

    /// <summary>확인 간격이 바뀌었을 때 알린다. 듣지 않아도 다음 확인부터 새 간격을 따르고, 듣는 쪽은 자고 있는 시계를 당장 고친다.</summary>
    public event Action<int>? GeoSyncIntervalChanged;

    /// <summary>위치 확인 간격(분)을 저장한다.</summary>
    public async Task SetGeoSyncIntervalAsync(int minutes)
    {
        var normalized = NormalizeGeoSyncMinutes(minutes);

        // 회로가 든 값을 먼저 고친다 — 브라우저 저장이 실패해도 이 탭의 시계는 방금 고른 간격을 따라야 한다.
        _geoSyncMinutes = normalized;
        _geoSeeded = true;

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", GeoSyncIntervalKey,
                normalized.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치 확인 간격을 브라우저에 저장하지 못했다.");
        }

        GeoSyncIntervalChanged?.Invoke(normalized);
    }

    // 읽기(ReadAsync)가 든 값은 회로가 붙을 때의 것이라 그 뒤에 찍은 표시를 비추지 않는다. 마지막 다룬 때와 간격은 여기서 든다.

    private DateTime? _geoSyncedAt;
    private int? _geoSyncMinutes;
    private bool _geoSeeded;

    /// <summary>이 브라우저에서 위치를 마지막으로 다룬 때(UTC). 없으면 null. ReadAsync 가 심고 StampGeoSyncAsync 가 고친다.</summary>
    public DateTime? GeoSyncedAt => _geoSyncedAt;

    /// <summary>지금 쓰는 확인 간격. 고른 적이 없으면 DefaultGeoSyncMinutes.</summary>
    public TimeSpan GeoSyncInterval =>
        TimeSpan.FromMinutes(_geoSyncMinutes ?? DefaultGeoSyncMinutes);

    /// <summary>읽어 온 상태에서 위 둘을 심는다. 이미 심었으면 건드리지 않는다 — 늦게 끝난 읽기가 방금 한 일을 덮지 않게.</summary>
    private void SeedGeoSync(BrowserState state)
    {
        if (_geoSeeded)
        {
            return;
        }

        _geoSeeded = true;
        _geoSyncedAt = state.GeoSyncedAt;
        _geoSyncMinutes = state.GeoSyncMinutes;
    }

    /// <summary>위치 확인·기록이 성공했을 때 알린다. 이동 경로 화면 등이 보고 있는 오늘 지도를 갱신할 수 있다.</summary>
    public event Action? GeoSynced;

    /// <summary>방금 다뤘다고 브라우저에 적는다(UTC). 못 적어도 회로가 든 값은 고친다 — 사생활 보호 모드에서 setItem 이 던져도 시계는 멈추지 않는다.</summary>
    public async Task<DateTime> StampGeoSyncAsync()
    {
        var now = DateTime.UtcNow;

        _geoSyncedAt = now;
        _geoSeeded = true;

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", GeoSyncedAtKey,
                now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치를 확인한 때를 브라우저에 적지 못했다.");
        }

        GeoSynced?.Invoke();

        return now;
    }

    /// <summary>브라우저에서 그 표시를 다시 읽는다. 탭마다 회로가 따로 도므로 옆 탭이 방금 잰 것을 보고 중복으로 재지 않게 한다.</summary>
    public async Task<DateTime?> ReadGeoSyncedAtAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", GeoSyncedAtKey);

            if (BrowserState.Moment(raw) is { } at)
            {
                _geoSyncedAt = at;
                _geoSeeded = true;
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "위치를 확인한 때를 다시 읽지 못했다.");
        }

        return _geoSyncedAt;
    }

    /// <summary>고를 수 있는 확인 간격(분). 화면과 판정이 같은 목록을 봐야 화면에만 있는 값이 조용히 기본값으로 돌아가지 않는다.</summary>
    public static readonly int[] GeoSyncMinuteChoices = [30, 60, 120, 180, 360];

    /// <summary>확인 간격을 고를 수 있는 값으로 다듬는다. 모르는 값이면 DefaultGeoSyncMinutes.</summary>
    public static int NormalizeGeoSyncMinutes(int? minutes) =>
        minutes is { } m && Array.IndexOf(GeoSyncMinuteChoices, m) >= 0
            ? m
            : DefaultGeoSyncMinutes;

    /// <summary>모바일 메뉴 단추 위치를 검증한다. 모르는 값이면 bottom-left.</summary>
    public static string NormalizeFabPosition(string? position) =>
        NormalizeCorner(position, "bottom-left");

    /// <summary>요청 등록 단추 위치를 검증한다. 모르는 값이면 bottom-right. 아는 귀퉁이 목록은 NormalizeCorner 한 곳에 둔다.</summary>
    public static string NormalizeHelpDeskFabPosition(string? position) =>
        NormalizeCorner(position, "bottom-right");

    /// <summary>떠다니는 단추가 설 수 있는 네 귀퉁이. 모르는 값이면 <paramref name="fallback"/> 이다.</summary>
    private static string NormalizeCorner(string? position, string fallback) => position switch
    {
        "top-left" => "top-left",
        "top-right" => "top-right",
        "bottom-right" => "bottom-right",
        "center" => "center",
        "bottom-left" => "bottom-left",
        _ => fallback,
    };

    /// <summary>오른쪽 서랍 너비가 바뀌었을 때 알린다.</summary>
    public event Action<int>? RightDrawerWidthChanged;

    /// <summary>오른쪽 서랍 너비(px)를 브라우저와 계정에 저장하고 알린다.</summary>
    public async Task SetRightDrawerWidthAsync(int widthPx)
    {
        var clamped = Math.Clamp(widthPx, 300, 1200);
        try
        {
            var raw = clamped.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await js.InvokeVoidAsync("localStorage.setItem", RightDrawerWidthKey, raw);
            await js.InvokeVoidAsync("localStorage.setItem", QuickAskDrawerWidthKey, raw);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogDebug(ex, "오른쪽 서랍 너비를 브라우저에 저장하지 못했다.");
        }

        Remember(state => state.WithRightDrawerWidth(clamped));
        RightDrawerWidthChanged?.Invoke(clamped);

        await prefs.SetAsync(PortalPrefs.RightDrawerWidthKey, clamped);
    }

    // ── 오가는 모양 ───────────────────────────────────────────

    /// <summary>theme.js 의 <c>jsiniBoot.read</c> 에 넘기는 것.</summary>
    private sealed class BootRequest
    {
        public string? Watermark { get; init; }
        public string[] Session { get; init; } = [];
        public string[] Local { get; init; } = [];
    }

    /// <summary>응답. 사전 둘과 테마 하나. internal 이라야 BrowserState.From 이 받는다.</summary>
    internal sealed class BootWire
    {
        public Dictionary<string, string?> Session { get; init; } = [];
        public Dictionary<string, string?> Local { get; init; } = [];
        public ThemeWire? Theme { get; init; }
    }

    /// <summary>jsiniTheme.current() 의 모양 그대로. ThemeToggle 이 Current 로 옮겨 담는다.</summary>
    public sealed class ThemeWire
    {
        public string? Mode { get; init; }
        public string? Color { get; init; }
        public string? Base { get; init; }
        public string? Radius { get; init; }
        public string? Size { get; init; }
        public string? Font { get; init; }
    }

    /// <summary>브라우저에서 읽은 한 벌. 부품은 이 모양만 본다 — 열쇠 글자를 부품에 다시 적지 않는다.</summary>
    public sealed class BrowserState
    {
        /// <summary>못 읽었을 때. 전부 「없다」다.</summary>
        public static readonly BrowserState Empty = new();

        /// <summary>이 탭이 잠겨 있었는가.</summary>
        public bool ScreenLocked { get; private init; }

        /// <summary>로그인 뒤 공지를 이 탭에서 닫았는가. 공개 공지 쪽은 로그인 화면에 회로가 없어 여기 실리지 않는다.</summary>
        public bool NoticeClosed { get; private init; }

        /// <summary>「오늘 하루 보지 않기」로 적어 둔 것. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? NoticeDismissedJson { get; private init; }

        /// <summary>구독 권유 창을 이 탭에서 닫았는가(「나중에」).</summary>
        public bool PushAskClosed { get; private init; }

        /// <summary>그 창을 이 브라우저에서 영영 안 보기로 했는가.</summary>
        public bool PushAskNever { get; private init; }

        /// <summary>휴대폰에서 그 창을 언제까지 접어 두기로 했는가(UTC). 없거나 못 읽으면 null — 지금 물어도 된다.</summary>
        public DateTime? PushAskSnoozedUntil { get; private init; }

        /// <summary>위치 권유 창을 이 탭에서 닫았는가(「나중에」).</summary>
        public bool GeoAskClosed { get; private init; }

        /// <summary>그 창을 이 브라우저에서 영영 안 보기로 했는가.</summary>
        public bool GeoAskNever { get; private init; }

        /// <summary>이 브라우저에서 위치 일을 마지막으로 다룬 때(UTC). 없으면 null — 한 번도 안 한 것으로 본다.</summary>
        public DateTime? GeoSyncedAt { get; private init; }

        /// <summary>고정해 둔 탭. 날것 JSON 이고 없으면 <c>null</c>.</summary>
        public string? PinnedTabsJson { get; private init; }

        /// <summary>끌어 넓혀 둔 사이드바 폭(px). 못 읽으면 null — 기본 폭을 쓴다. 여기서 숫자로 옮겨 부품마다 파싱이 갈리지 않게 한다.</summary>
        public int? SidebarWidthPx { get; private init; }

        /// <summary>데스크탑에서 조절해 둔 오른쪽 서랍 폭(px). 없거나 못 읽으면 null — 기본 416px.</summary>
        public int? RightDrawerWidthPx { get; private set; }

        /// <summary>지금 고른 테마. theme.js 가 안 실렸으면 <c>null</c>.</summary>
        public ThemeWire? Theme { get; private init; }

        /// <summary>모바일 메뉴 단추 위치. 없거나 잘못되면 bottom-left.</summary>
        public string FabPosition { get; private set; } = "bottom-left";

        /// <summary>모바일 메뉴 단추를 감췄는가. 고른 적이 없으면 false(보인다).</summary>
        public bool FabHidden { get; private set; }

        /// <summary>요청 등록 단추 위치. 없거나 잘못되면 bottom-right.</summary>
        public string HelpDeskFabPosition { get; private set; } = "bottom-right";

        /// <summary>요청 등록 단추를 감췄는가. 고른 적이 없으면 false(보인다). 권한이 있을 때에 한한다.</summary>
        public bool HelpDeskFabHidden { get; private set; }

        /// <summary>요청 등록 단추를 넓은 화면에도 띄우기로 했는가. 고른 적이 없으면 false — 휴대폰에서만 뜬다.</summary>
        public bool HelpDeskFabDesktop { get; private set; }

        /// <summary>휴대폰 아래 띠를 쓰지 않기로 했는가. 고른 적이 없으면 false — 쓴다.</summary>
        public bool BottomNavHidden { get; private set; }

        /// <summary>띠에 놓을 칸들(날것 JSON). 고른 적이 없으면 null. 옮기는 일은 BottomNav.Parse 가 한다.</summary>
        public string? BottomNavItemsJson { get; private set; }

        /// <summary>띠 칸에서 이름을 감추기로 했는가. 고른 적이 없으면 false. 계정에만 있어 WithAccountPrefs 로만 실린다.</summary>
        public bool BottomNavLabelsHidden { get; private set; }

        /// <summary>칸들을 넓은 화면 헤더에 세우지 않기로 했는가. 고른 적이 없으면 false. 계정에만 있어 WithAccountPrefs 로만 실린다.</summary>
        public bool BottomNavDesktopHidden { get; private set; }

        /// <summary>토스트 알림 위치. 없거나 잘못되면 bottom-right.</summary>
        public string ToastPosition { get; private set; } = "bottom-right";

        /// <summary>브라우저에 적힌 그대로의 토스트 자리. 없으면 null. ToastPosition 과 갈라 두어야 「고르지 않음」과 「기본값을 고름」이 구분된다.</summary>
        internal string? ToastPositionStored { get; private init; }

        /// <summary>위치 자동 확인 간격(분). 고른 적이 없으면 DefaultGeoSyncMinutes.</summary>
        public int GeoSyncMinutes { get; private init; } = DefaultGeoSyncMinutes;

        /// <summary>그 간격을 <see cref="TimeSpan"/> 으로. 판정하는 쪽이 이것을 쓴다.</summary>
        public TimeSpan GeoSyncInterval => TimeSpan.FromMinutes(GeoSyncMinutes);

        /// <summary>휴대폰 확대 잠금을 풀어 두었는가. 고른 적이 없으면 false — 잠근다.</summary>
        public bool ZoomUnlocked { get; private set; }

        /// <summary>AI 대화를 사내 자료(RAG)로만 할지. 기본은 끔.</summary>
        public bool AiRagOnly { get; private set; }

        /// <summary>「사내 자료 전용」의 찾을 범위(<see cref="AiRagScopeKey"/>). null 이면 모든 카테고리.</summary>
        public string? AiRagScope { get; private set; }

        /// <summary>이 한 벌에서 한 칸만 갈아 끼운 새 것. 다시 읽지 않는다 — 회로당 한 번 읽은 값이라 고치기 전 값이 되살아난다.</summary>
        private BrowserState Patch(Action<BrowserState> change)
        {
            var copy = (BrowserState)MemberwiseClone();
            change(copy);
            return copy;
        }

        internal BrowserState WithRightDrawerWidth(int value) =>
            Patch(c => c.RightDrawerWidthPx = value);

        internal BrowserState WithFabPosition(string value) => Patch(c => c.FabPosition = value);

        internal BrowserState WithFabHidden(bool value) => Patch(c => c.FabHidden = value);

        internal BrowserState WithHelpDeskFabPosition(string value) =>
            Patch(c => c.HelpDeskFabPosition = value);

        internal BrowserState WithHelpDeskFabHidden(bool value) =>
            Patch(c => c.HelpDeskFabHidden = value);

        internal BrowserState WithHelpDeskFabDesktop(bool value) =>
            Patch(c => c.HelpDeskFabDesktop = value);

        internal BrowserState WithBottomNavHidden(bool value) => Patch(c => c.BottomNavHidden = value);

        internal BrowserState WithBottomNavItems(string? value) =>
            Patch(c => c.BottomNavItemsJson = value);

        internal BrowserState WithBottomNavLabelsHidden(bool value) =>
            Patch(c => c.BottomNavLabelsHidden = value);

        internal BrowserState WithBottomNavDesktopHidden(bool value) =>
            Patch(c => c.BottomNavDesktopHidden = value);

        internal BrowserState WithToastPosition(string value) => Patch(c => c.ToastPosition = value);

        internal BrowserState WithZoomUnlocked(bool value) => Patch(c => c.ZoomUnlocked = value);

        internal BrowserState WithAiRagOnly(bool value) => Patch(c => c.AiRagOnly = value);

        internal BrowserState WithAiRagScope(string? value) => Patch(c => c.AiRagScope = value);

        /// <summary>계정에 담긴 것을 얹은 새 한 벌. 계정에 열쇠가 없는 칸(기본값)은 건드리지 않는다.</summary>
        internal BrowserState WithAccountPrefs(PortalPrefsData account) => Patch(c =>
        {
            c.BottomNavHidden = account.Flag(PortalPrefs.BottomNavHiddenKey) ?? c.BottomNavHidden;
            c.BottomNavItemsJson = account.Raw(PortalPrefs.BottomNavItemsKey) ?? c.BottomNavItemsJson;
            c.BottomNavLabelsHidden =
                account.Flag(PortalPrefs.BottomNavLabelsHiddenKey) ?? c.BottomNavLabelsHidden;
            c.BottomNavDesktopHidden =
                account.Flag(PortalPrefs.BottomNavDesktopHiddenKey) ?? c.BottomNavDesktopHidden;
            c.FabHidden = account.Flag(PortalPrefs.FabHiddenKey) ?? c.FabHidden;
            c.HelpDeskFabHidden = account.Flag(PortalPrefs.HelpDeskFabHiddenKey) ?? c.HelpDeskFabHidden;
            c.ToastPosition = NormalizeToastPosition(
                account.Text(PortalPrefs.ToastPositionKey) ?? c.ToastPosition);
            c.RightDrawerWidthPx = account.Number(PortalPrefs.RightDrawerWidthKey) ?? c.RightDrawerWidthPx;
        });

        internal static BrowserState From(BootWire wire) => new()
        {
            // 값이 무엇이든 있으면 그렇다는 뜻이다.
            ScreenLocked = Has(wire.Local, ScreenLockedKey),
            NoticeClosed = Has(wire.Session, NoticeClosedUserKey),
            NoticeDismissedJson = Get(wire.Local, NoticeDismissedKey),
            PushAskClosed = Has(wire.Session, PushAskClosedKey),
            PushAskNever = Has(wire.Local, PushAskNeverKey),
            PushAskSnoozedUntil = Moment(Get(wire.Local, PushAskSnoozeKey)),
            GeoAskClosed = Has(wire.Session, GeoAskClosedKey),
            GeoAskNever = Has(wire.Local, GeoAskNeverKey),
            GeoSyncedAt = Moment(Get(wire.Local, GeoSyncedAtKey)),
            PinnedTabsJson = Get(wire.Local, PinnedTabsKey),
            SidebarWidthPx = Pixels(Get(wire.Local, SidebarWidthKey)),
            RightDrawerWidthPx = Pixels(Get(wire.Local, QuickAskDrawerWidthKey))
                ?? Pixels(Get(wire.Local, RightDrawerWidthKey)),
            Theme = wire.Theme,
            FabPosition = NormalizeFabPosition(Get(wire.Local, FabPositionKey)),
            FabHidden = Has(wire.Local, FabHiddenKey),
            HelpDeskFabPosition = NormalizeHelpDeskFabPosition(Get(wire.Local, HelpDeskFabPositionKey)),
            HelpDeskFabHidden = Has(wire.Local, HelpDeskFabHiddenKey),
            HelpDeskFabDesktop = Has(wire.Local, HelpDeskFabDesktopKey),
            BottomNavHidden = Has(wire.Local, BottomNavHiddenKey),
            BottomNavItemsJson = Get(wire.Local, BottomNavItemsKey),
            ToastPosition = NormalizeToastPosition(Get(wire.Local, ToastPositionKey)),
            ToastPositionStored = Get(wire.Local, ToastPositionKey),
            GeoSyncMinutes = NormalizeGeoSyncMinutes(Minutes(Get(wire.Local, GeoSyncIntervalKey))),
            ZoomUnlocked = Has(wire.Local, ZoomUnlockedKey),
            AiRagOnly = Has(wire.Local, AiRagOnlyKey),
            AiRagScope = Get(wire.Local, AiRagScopeKey),
        };

        /// <summary>저장해 둔 시각을 UTC 로 푼다. 이상하면 null. AdjustToUniversal 을 빼면 기기 시간대로 풀려 시차만큼 어긋난다.</summary>
        internal static DateTime? Moment(string? value) =>
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;

        /// <summary>저장해 둔 간격(숫자). 이상하면 null — NormalizeGeoSyncMinutes 가 기본값으로 되돌린다.</summary>
        private static int? Minutes(string? value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes)
                ? minutes
                : null;

        /// <summary>저장해 둔 폭을 숫자로. 이상한 값이면 <c>null</c>.</summary>
        private static int? Pixels(string? value) =>
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var px) && px > 0
                ? px
                : null;

        private static bool Has(Dictionary<string, string?> from, string key) =>
            Get(from, key) is { Length: > 0 };

        private static string? Get(Dictionary<string, string?> from, string key) =>
            from.TryGetValue(key, out var value) ? value : null;
    }
}
