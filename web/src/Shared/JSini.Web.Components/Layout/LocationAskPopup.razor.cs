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
    /// <b>간격에 한 번</b>뿐이다.
    /// </para>
    /// <para>
    /// 살핀 뒤에는 <b>시계를 건다</b>(<see cref="ScheduleNextSync"/>). 화면
    /// 전환에만 기대면 한 화면을 열어 두고 일하는 사람은 영영 안 재기 때문이다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await ConsiderAsync();

        // **살핀 결과와 무관하게 건다.** 지금 못 잰 까닭(권한을 아직 안 줬다,
        // 창을 띄워 두었다)은 대개 조금 뒤에 풀린다.
        ScheduleNextSync();
    }

    /// <summary>
    /// 지금 무엇을 할 때인가를 살핀다. <b>첫 렌더에서만 부른다</b> — 시계가
    /// 깨울 때 가는 길은 <see cref="SyncOnScheduleAsync"/> 이고, 그쪽은 창을
    /// 띄우지 않는다.
    /// </summary>
    private async Task ConsiderAsync()
    {
        // ① 브라우저 저장소. **왕복을 따로 내지 않는다** — 레이아웃이 내는
        //    공용 읽기에 열쇠 셋을 보탠 것이 전부다(`PortalBoot`).
        var saved = await Boot.ReadAsync();

        if (saved.ScreenLocked)
        {
            return;
        }

        // **간격 안에 이미 다룬 브라우저면 여기서 끝이다.** 물었든 쟀든
        // 「이미 줬더라」를 알아냈든, 그 표시가 남아 있으면 더 할 일이 없다.
        if (!IsDue())
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
                // 표시를 남겨 한 간격 동안 이 물음을 되풀이하지 않는다.
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
        // 그때 찍는다 — 지금 찍으면 못 보고 화면을 옮긴 사람에게 한 간격
        // 동안 다시 안 뜬다.
        _open = true;
        StateHasChanged();
    }

    /// <summary>
    /// 이 브라우저에서 위치를 다룬 지 <b>고른 간격</b>만큼 지났나
    /// (<see cref="PortalBoot.GeoSyncInterval"/> — 고른 적이 없으면
    /// <see cref="GeoLocator.SyncInterval"/> 과 같은 30분이다).
    /// <b>서버를 부르기 전에</b> 이것으로 거른다.
    /// </summary>
    /// <remarks>
    /// <b>읽어 온 상태(<c>BrowserState</c>)가 아니라 <see cref="PortalBoot"/> 이
    /// 든 값을 본다.</b> 저쪽은 회로가 붙던 순간의 사진이라 몇 번을 읽어도
    /// 그대로다 — 화면 전환마다 새로 생기던 동안에는 드러나지 않았지만, 같은
    /// 회로 안에서 시계가 되풀이 묻게 되면 <b>쟀는데도 영영 잴 때</b>가 된다.
    /// </remarks>
    private bool IsDue() =>
        Boot.GeoSyncedAt is not { } last
        || DateTime.UtcNow - last >= Boot.GeoSyncInterval;

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
    /// 화면을 옮길 때마다, 그리고 간격마다 「위치를 확인했습니다」가 뜬다.
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
            // 못 쟀으면 표시도 남기지 않는다 — 다음 화면이나 다음 시계에서
            // 다시 해 본다.
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
    /// 방금 확인했다고 적어 둔다. <b>글자를 굽는 일은 <see cref="PortalBoot"/>
    /// 가 한다</b> — 열쇠와 꼴을 아는 자리가 둘이면 한쪽만 고치는 날이 온다.
    /// 그쪽이 회로가 든 「마지막으로 다룬 때」도 함께 고치므로, 시계가 다음에
    /// 깰 때를 셈할 수 있다.
    /// </summary>
    private Task StampSyncAsync() => Boot.StampGeoSyncAsync();

    // ── 다음에 잴 때를 기다리는 시계 ──────────────────────────────
    //
    // **화면 전환에만 기대면 안 재는 사람이 생긴다.** 이 부품의 살핌은 지금까지
    // 레이아웃이 다시 만들어질 때(업무를 옮길 때)만 돌았다. 한 화면을 열어 두고
    // 일하는 사람에게는 그 계기가 없어서, 간격이 지나도 아무 일도 안 일어났다 —
    // 운영 기록의 103분짜리 틈이 그것이다.
    //
    // **촘촘히 훑지는 않는다.** 1분마다 살피면 회로 하나가 한 시간에 왕복
    // 예순을 낸다(`PushAskPopup` 의 시계와 같은 까닭). 기다리는 것은 「잴 때가
    // 됐는가」 하나뿐이라 **그 시각에 한 번만** 깨고, 깨서 한 일이 끝나면
    // 다음 시각을 다시 센다.

    /// <summary>시계. 부품이 사라지면 걷는다.</summary>
    private CancellationTokenSource? _clock;

    /// <summary>
    /// 아무리 바빠도 이보다 촘촘히는 깨지 않는다. 시각 셈이 어긋났을 때
    /// (기기 시계를 되돌린 경우 따위) <b>쉬지 않고 도는 것</b>을 막는 바닥이다.
    /// </summary>
    private static readonly TimeSpan MinWait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 다음에 깰 때까지. <b>마지막으로 다룬 때부터</b> 센다 — 방금 깨어 아무것도
    /// 못 했으면 거기서 한 간격을 더 기다린다(그래야 쉬지 않고 돌지 않는다).
    /// </summary>
    private TimeSpan NextWait()
    {
        var interval = Boot.GeoSyncInterval;
        var now = DateTime.UtcNow;

        var wait = Boot.GeoSyncedAt is { } last && now - last < interval
            ? last + interval - now
            : interval;

        return wait < MinWait ? MinWait : wait;
    }

    /// <summary>
    /// 시계를 건다. <b>앞서 걸어 둔 것이 있으면 걷는다</b> — 겹쳐 두면 간격마다
    /// 둘씩 잰다.
    /// </summary>
    private void ScheduleNextSync()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = new CancellationTokenSource();

        _ = RunClockAsync(_clock.Token);
    }

    /// <summary>
    /// 잘 때가 되면 깨어 한 번 재고 다시 잔다. <b>간격은 깰 때마다 다시
    /// 읽는다</b> — 사람이 환경설정에서 고친 값이 다음 잠부터 바로 듣는다.
    /// </summary>
    private async Task RunClockAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(NextWait(), token);

                // **회로의 차례로 돌아와서** 화면을 건드린다. 시계는 회로 밖에서
                // 도므로 여기서 바로 JS 를 부르면 렌더링이 엉킨다.
                await InvokeAsync(SyncOnScheduleAsync);
            }
        }
        catch (OperationCanceledException)
        {
            // 부품이 사라졌다. 할 일이 없다.
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException)
        {
            // 회로가 먼저 끊겼다. 다음에 열 때 처음부터 다시 살핀다.
            Log.LogDebug(ex, "위치를 다시 재려는데 회로가 이미 끊겼다.");
        }
        catch (Exception ex)
        {
            Log.LogDebug(ex, "위치를 다시 재지 못했다.");
        }
    }

    /// <summary>
    /// 시계가 깨웠을 때 가는 길. <b>창을 띄우지 않는다</b> — 좌표를 아직 안 준
    /// 사람에게는 화면 전환에서 창이 먼저 뜨고, 그 창을 닫은 사람에게 30분 뒤
    /// 같은 창을 다시 미는 것은 묻는 것이 아니라 조르는 것이다.
    /// </summary>
    private async Task SyncOnScheduleAsync()
    {
        if (_open || _busy)
        {
            return;
        }

        // **탭이 둘일 수 있다.** 표시는 탭끼리 나눠 쓰지만 회로는 탭마다 따로
        // 돈다 — 옆 탭이 방금 쟀으면 여기서 물러난다(왕복 하나).
        var last = await Boot.ReadGeoSyncedAtAsync();

        if (last is { } at && DateTime.UtcNow - at < Boot.GeoSyncInterval)
        {
            return;
        }

        var permission = await Geo.PermissionAsync();

        // 조용히 재는 길이라 **허용된 경우에만** 간다. 아직 안 준 사람에게
        // 물음창을 띄우는 것은 사람이 누른 사슬에서만 할 일이다.
        if (!permission.Supported || !permission.Secure || !permission.Granted)
        {
            return;
        }

        if (!await LoadSettingsAsync() || !HasLocation)
        {
            return;
        }

        await SyncQuietlyAsync();
    }

    /// <summary>시계를 걷는다. 업무를 옮기면 이 부품이 통째로 사라진다.</summary>
    public void Dispose()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = null;
    }
}
