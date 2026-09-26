using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class LocationAskPopup
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private GeoLocator Geo { get; set; } = default!;
    [Inject] private PushEnroll Enroll { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<LocationAskPopup> Log { get; set; } = default!;

    /// <summary>방금 읽어 온 내 설정. 저장된 좌표가 여기 있다.</summary>
    private NotificationSettingsDto? _settings;

    private bool _open;
    private bool _busy;

    /// <summary>
    /// <b>첫 렌더 뒤에</b> 살핀다. 프리렌더 중에는 JS 를 부를 수 없고
    /// (브라우저가 아직 없다) 이 창의 판단은 전부 브라우저에서 온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>순서가 값이다.</b> 이 부품은 업무를 옮길 때마다 다시 만들어지므로
    /// (<see cref="PortalBoot"/> 머리말) 여기서 내는 왕복은 <b>화면 전환마다</b>
    /// 난다. 그래서 싼 것부터 본다 — 브라우저 저장소(레이아웃이 내는 공용
    /// 읽기에 얹혀 온다) → JS 한 번 → 게이트웨이. 게이트웨이까지 가는 것은
    /// <b>한 시간에 한 번</b>뿐이다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // ① 브라우저 저장소. **왕복을 따로 내지 않는다** — 레이아웃이 내는
        //    공용 읽기에 열쇠 셋을 보탠 것이 전부다(`PortalBoot`).
        var saved = await Boot.ReadAsync();

        if (saved.ScreenLocked)
        {
            return;
        }

        // **한 시간 안에 이미 다룬 브라우저면 여기서 끝이다.** 물었든 쟀든
        // 「이미 줬더라」를 알아냈든, 그 표시가 남아 있으면 더 할 일이 없다.
        if (!IsDue(saved))
        {
            return;
        }

        // ② 브라우저가 위치를 내줄 사정인가. **차단으로 굳었으면** 창을 띄워도
        //    조용히 확인해도 할 수 있는 일이 없다.
        var permission = await Geo.PermissionAsync();

        if (!permission.Supported || !permission.Secure || permission.State == "denied")
        {
            return;
        }

        var canAsk = !saved.GeoAskClosed && !saved.GeoAskNever;

        // ③ 물을 사람인가 — **게이트웨이보다 먼저 브라우저에게 묻는다.**
        //    알림을 안 받는 사람에게 위치를 물으면 받아 두고도 쓸 데가 없다
        //    (머리말). 권한이 이미 허용돼 있으면 물을 일이 아니라 잴 일이라
        //    이 물음을 건너뛴다.
        if (canAsk && !permission.Granted)
        {
            var browser = await Enroll.StatusAsync();
            canAsk = browser is { Supported: true, Subscribed: true };
        }

        if (!canAsk && !permission.Granted)
        {
            return;
        }

        // ④ 여기서 처음으로 서버를 부른다. 좌표가 이미 있는지가 갈림길이다.
        if (!await LoadSettingsAsync())
        {
            return;
        }

        if (HasLocation)
        {
            // 이미 준 사람에게는 묻지 않는다. 대신 조용히 확인한다.
            if (permission.Granted)
            {
                await SyncQuietlyAsync();
            }
            else
            {
                // 잴 수는 없지만 **알아낸 것은 있다** — 이 사람은 이미 줬다.
                // 표시를 남겨 한 시간 동안 이 물음을 되풀이하지 않는다.
                await StampSyncAsync();
            }

            return;
        }

        // ⑤ 좌표가 없다. 권한이 이미 허용돼 있어도 **묻고 나서** 받는다 —
        //    물음창이 안 뜬다고 조용히 가져가면 그것은 받는 것이 아니라
        //    집어 가는 것이다. 단추 하나가 더 필요할 뿐이고, 그 단추가
        //    「무엇에 쓰는 위치인지」를 말한다.
        //
        //    **여기서 물러나는 길은 모두 표시를 남긴다.** 서버까지 갔다 온
        //    뒤이므로, 안 남기면 화면을 옮길 때마다 같은 왕복이 되풀이된다.
        if (!canAsk || _settings is not { PushAvailable: true })
        {
            // 「다시 묻지 않기」를 눌렀거나, 서버에 VAPID 키가 없어 켜도 알림이
            // 안 오는 경우다. 뒤엣것에서 위치를 묻는 것은 줄 것 없이 받기만
            // 하는 일이다.
            await StampSyncAsync();
            return;
        }

        if (permission.Granted)
        {
            // 권한은 허용됐는데 좌표가 없는 사람에게도 창을 띄운다. 그러려면
            // 위에서 건너뛴 자격(알림을 받고 있는가)을 여기서 본다.
            var browser = await Enroll.StatusAsync();

            if (browser is not { Supported: true, Subscribed: true })
            {
                await StampSyncAsync();
                return;
            }
        }

        // **창을 띄울 때는 찍지 않는다.** 사람이 닫거나 눌러야 끝난 것이고,
        // 그때 찍는다 — 지금 찍으면 못 보고 화면을 옮긴 사람에게 한 시간 동안
        // 다시 안 뜬다.
        _open = true;
        StateHasChanged();
    }

    /// <summary>
    /// 이 브라우저에서 위치를 다룬 지 <b>고른 간격</b>만큼 지났나
    /// (<see cref="PortalBoot.BrowserState.GeoSyncInterval"/> — 고른 적이 없으면
    /// <see cref="GeoLocator.SyncInterval"/> 과 같은 한 시간이다).
    /// <b>서버를 부르기 전에</b> 이것으로 거른다.
    /// </summary>
    private static bool IsDue(PortalBoot.BrowserState saved) =>
        saved.GeoSyncedAt is not { } last
        || DateTime.UtcNow - last >= saved.GeoSyncInterval;

    /// <summary>저장된 좌표가 있나.</summary>
    private bool HasLocation =>
        _settings?.Preference is { WeatherLat: not null, WeatherLon: not null };

    /// <summary>
    /// 「위치 확인」. <b>이 클릭에서 브라우저 권한 요청이 이어진다</b> —
    /// 절차는 <see cref="GeoLocator"/> 가 갖고 있다(알림 판과 같은 한 벌).
    /// </summary>
    private async Task LocateAsync()
    {
        if (_busy || _settings is null)
        {
            return;
        }

        _busy = true;

        try
        {
            var geo = await Geo.LocateAsync();

            if (!geo.Ok)
            {
                // **창을 닫지 않는다.** 실패의 대부분은 권한 거절이고, 주소창의
                // 자물쇠에서 풀고 다시 누를 수 있어야 한다.
                Toasts.Show(geo.Error ?? "위치를 받지 못했습니다.", NoticeTone.Warning);
                return;
            }

            // 여기서 「내 위치 날씨」를 함께 켠다 — 이 창에서 위치를 내주는
            // 뜻이 곧 그것이다. 창의 본문이 그렇게 말하고 있다.
            var result = await Geo.SaveAsync(_settings.Preference, geo, enableLocalWeather: true);

            Toasts.Show(result.Message, result.Tone);

            if (!result.Ok)
            {
                return;
            }

            _open = false;
            await RememberAsync(session: true);
            await StampSyncAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 좌표를 이미 준 사람의 자리를 <b>조용히</b> 다시 잰다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>아무 말도 하지 않는다.</b> 사람이 시킨 일이 아니라 곁다리로 도는
    /// 일이고, 결과는 설정 화면의 「확인한 때」에 남는다. 토스트를 띄우면
    /// 화면을 옮길 때마다 「위치를 확인했습니다」가 뜬다.
    /// </para>
    /// <para>
    /// <b>좌표를 이미 준 사람에게만 부른다.</b> 권한이 허용돼 있다는 것만으로
    /// 좌표를 받아 두지는 않는다 — 준 적 없는 사람에게는 창이 먼저 뜬다.
    /// </para>
    /// </remarks>
    private async Task SyncQuietlyAsync()
    {
        var geo = await Geo.QuietAsync();

        if (!geo.Ok)
        {
            // 못 쟀으면 표시도 남기지 않는다 — 다음 화면에서 다시 해 본다.
            return;
        }

        var result = await Geo.SaveAsync(_settings!.Preference, geo, enableLocalWeather: false);

        if (result.Ok)
        {
            await StampSyncAsync();
        }
    }

    /// <summary>설정을 읽어 둔다. 못 읽었으면 거짓이고, 그때는 아무것도 하지 않는다.</summary>
    private async Task<bool> LoadSettingsAsync()
    {
        if (_settings is not null)
        {
            return true;
        }

        try
        {
            _settings = await Api.GetMyPreferencesAsync();
        }
        catch (Exception ex)
        {
            // 설정을 못 읽었다고 화면을 세우지 않는다. 권유를 못 한 것뿐이다.
            Log.LogDebug(ex, "알림 설정을 읽지 못해 위치 권유를 건너뛴다.");
            return false;
        }

        return _settings is not null;
    }

    /// <summary>「나중에」와 X. 이 탭에서만 그만 묻는다.</summary>
    private async Task LaterAsync(bool visible)
    {
        if (visible)
        {
            return;
        }

        _open = false;
        await RememberAsync(session: true);
        await StampSyncAsync();
    }

    /// <summary>「다시 묻지 않기」. 이 브라우저에 영영 적어 둔다.</summary>
    private async Task NeverAsync()
    {
        _open = false;
        await RememberAsync(session: false);
        await StampSyncAsync();

        // **아무 말도 하지 않는다.** 끈 것이 아니라 안 묻기로 한 것이라
        // 알릴 결과가 없고, 토스트를 띄우면 방금 치운 말이 다시 뜬다.
    }

    /// <summary>
    /// 닫았다는 표시를 브라우저에 남긴다. <b>부품 수명에 기대지 않는다</b> —
    /// 업무를 옮기면 레이아웃이 통째로 다시 만들어져 <c>firstRender</c> 가
    /// 또 참이 된다(<c>PushAskPopup</c> 머리말).
    /// </summary>
    private async Task RememberAsync(bool session)
    {
        try
        {
            if (session)
            {
                await Js.InvokeVoidAsync("sessionStorage.setItem", PortalBoot.GeoAskClosedKey, "1");
            }
            else
            {
                await Js.InvokeVoidAsync("localStorage.setItem", PortalBoot.GeoAskNeverKey, "1");
            }
        }
        catch (JSException ex)
        {
            // 사생활 보호 모드에서는 setItem 이 던진다. 이번 탭에서 한 번 더
            // 뜨는 것이 전부라 사용자에게 말할 일이 아니다.
            Log.LogDebug(ex, "위치 권유를 닫았다는 표시를 남기지 못했습니다.");
        }
    }

    /// <summary>
    /// 방금 확인했다고 브라우저에 적어 둔다. <b>UTC 로 적는다</b> — 읽는 쪽이
    /// UTC 「지금」과 빼서 한 시간 문턱을 잰다(<see cref="PortalBoot.GeoSyncedAtKey"/>).
    /// </summary>
    private async Task StampSyncAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("localStorage.setItem", PortalBoot.GeoSyncedAtKey,
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (JSException ex)
        {
            // 못 적었으면 다음 화면에서 한 번 더 잰다. 조용히 넘어간다.
            Log.LogDebug(ex, "위치를 확인한 때를 남기지 못했습니다.");
        }
    }
}
