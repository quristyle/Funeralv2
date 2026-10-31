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
    private bool _savingTrack;
    private DotNetObjectReference<LocationAskPopup>? _selfReference;

    /// <summary>첫 렌더 뒤에 살핀다 — 프리렌더에는 브라우저가 없다. 싼 것부터 본다(브라우저 저장소 → JS → 게이트웨이). 게이트웨이까지 가는 것은 간격마다 한 번뿐이다. 살핀 뒤 시계를 건다 — 화면 전환에만 기대면 한 화면을 오래 여는 사람은 영영 안 잰다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _selfReference = DotNetObjectReference.Create(this);

        await ConsiderAsync();

        // 살핀 결과와 무관하게 건다 — 지금 못 잰 까닭(권한 미허용, 창이 떠 있음)은 조금 뒤에 풀린다.
        ScheduleNextSync();

        // 사용자가 화면을 열고 쓸 때 조용히 위치 변동을 감지하여 수집한다.
        await StartActiveTrackingAsync();
    }

    /// <summary>지금 무엇을 할 때인가를 살핀다. 첫 렌더에서만 부른다 — 시계가 깨울 때는 SyncOnScheduleAsync 가 가고 창을 띄우지 않는다.</summary>
    private async Task ConsiderAsync()
    {
        // ① 브라우저 저장소. 왕복을 따로 내지 않고 레이아웃의 공용 읽기에 열쇠를 보탠 것이다(PortalBoot).
        var saved = await Boot.ReadAsync();

        if (saved.ScreenLocked)
        {
            return;
        }

        // ② 브라우저가 위치를 내줄 사정인가. 차단으로 굳었으면 창을 띄워도 조용히 재도 할 일이 없다.
        var permission = await Geo.PermissionAsync();

        if (!permission.Supported || !permission.Secure || permission.State == "denied")
        {
            return;
        }

        var canAsk = !saved.GeoAskClosed && !saved.GeoAskNever;

        // ③ 물을 사람인가 — 게이트웨이보다 먼저 브라우저에 묻는다. 알림을 안 받는 사람에게 위치를 물으면 받아 두고 쓸 데가 없다. 권한이 이미 허용돼 있으면 잴 일이라 이 물음을 건너뛴다.
        if (canAsk && !permission.Granted)
        {
            // 권한 미허용 상태에서 권유 창 띄우기는 설정 간격(기본 30분)을 지킨다.
            if (!IsDue())
            {
                return;
            }

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
                // 화면을 새로 열었을 때 방금(1분 이내) 잰 것이 아니면 조용히 확인하여 최신 위치 변동을 놓치지 않는다.
                if (IsQuietDue())
                {
                    await SyncQuietlyAsync();
                }
            }
            else
            {
                // 잴 수는 없지만 알아낸 것은 있다 — 이 사람은 이미 줬다. 표시를 남겨 한 간격 동안 되풀이하지 않는다.
                await StampSyncAsync();
            }

            return;
        }

        // ⑤ 좌표가 없다. 팝업 권유는 IsDue() 간격을 지킨다.
        if (!IsDue())
        {
            return;
        }

        if (!canAsk || _settings is not { PushAvailable: true })
        {
            // 다시 묻지 않기를 눌렀거나 서버에 VAPID 키가 없어 알림이 안 오는 경우다. 뒤엣것에서 위치를 묻는 것은 줄 것 없이 받기만 하는 일이다.
            await StampSyncAsync();
            return;
        }

        if (permission.Granted)
        {
            // 권한은 허용됐는데 좌표가 없는 사람에게도 창을 띄운다 — 그러려면 위에서 건너뛴 자격(알림을 받는가)을 여기서 본다.
            var browser = await Enroll.StatusAsync();

            if (browser is not { Supported: true, Subscribed: true })
            {
                await StampSyncAsync();
                return;
            }
        }

        // 창을 띄울 때는 찍지 않는다 — 사람이 닫거나 눌러야 끝난 것이고, 지금 찍으면 못 보고 옮긴 사람에게 한 간격 동안 안 뜬다.
        _open = true;
        StateHasChanged();
    }

    /// <summary>화면을 열었을 때 조용히 다시 잴 때인가. 너무 잦은 연속 호출(1분 미만)만 거르고, 자리를 옮겼을 가능성을 놓치지 않는다.</summary>
    private bool IsQuietDue() =>
        Boot.GeoSyncedAt is not { } last
        || DateTime.UtcNow - last >= TimeSpan.FromMinutes(1);

    /// <summary>이 브라우저에서 위치를 다룬 지 고른 간격만큼 지났나(고른 적이 없으면 GeoLocator.SyncInterval 과 같은 30분). 서버를 부르기 전에 이것으로 거른다. 읽어 온 상태가 아니라 PortalBoot 이 든 값을 본다 — 읽은 사진은 회로가 붙던 순간의 것이라 같은 회로에서 시계가 되풀이 물으면 쟀는데도 영영 잴 때가 된다.</summary>
    private bool IsDue() =>
        Boot.GeoSyncedAt is not { } last
        || DateTime.UtcNow - last >= Boot.GeoSyncInterval;

    /// <summary>저장된 좌표가 있나.</summary>
    private bool HasLocation =>
        _settings?.Preference is { WeatherLat: not null, WeatherLon: not null };

    /// <summary>「위치 확인」. 이 클릭에서 브라우저 권한 요청이 이어진다 — 절차는 GeoLocator 가 갖고 있다.</summary>
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
                // 창을 닫지 않는다 — 실패의 대부분은 권한 거절이라 주소창의 자물쇠에서 풀고 다시 누를 수 있어야 한다.
                Toasts.Show(geo.Error ?? "위치를 받지 못했습니다.", NoticeTone.Warning);
                return;
            }

            // 이 창에서 위치를 내주는 뜻이 곧 「내 위치 날씨」를 켜는 것이라 함께 켠다.
            var result = await Geo.SaveAsync(_settings.Preference, geo, enableLocalWeather: true);

            Toasts.Show(result.Message, result.Tone);

            if (!result.Ok)
            {
                return;
            }

            _open = false;
            await RememberAsync(session: true);
            await StampSyncAsync();
            await StartActiveTrackingAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>좌표를 이미 준 사람의 자리를 조용히 다시 잰다. 아무 말도 하지 않는다 — 곁다리로 도는 일이고 결과는 설정 화면의 「확인한 때」에 남는다. 좌표를 준 사람에게만 부른다 — 권한이 허용됐다고 받아 두지는 않는다.</summary>
    private async Task SyncQuietlyAsync()
    {
        if (_savingTrack)
        {
            return;
        }

        _savingTrack = true;
        try
        {
            var geo = await Geo.QuietAsync();

            if (!geo.Ok)
            {
                // 못 쟀으면 표시도 남기지 않는다 — 다음 화면이나 다음 시계에서 다시 해 본다.
                return;
            }

            var result = await Geo.SaveAsync(_settings!.Preference, geo, enableLocalWeather: false);

            if (result.Ok)
            {
                await StampSyncAsync();
            }
        }
        finally
        {
            _savingTrack = false;
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

        // 아무 말도 하지 않는다 — 끈 것이 아니라 안 묻기로 한 것이라 알릴 결과가 없고, 토스트를 띄우면 방금 치운 말이 다시 뜬다.
    }

    /// <summary>닫았다는 표시를 브라우저에 남긴다. 부품 수명에 기대지 않는다 — 업무를 옮기면 레이아웃이 다시 만들어져 firstRender 가 또 참이 된다(PushAskPopup 머리말).</summary>
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
            // 사생활 보호 모드에서는 setItem 이 던진다 — 이번 탭에서 한 번 더 뜨는 것이 전부라 말할 일이 아니다.
            Log.LogDebug(ex, "위치 권유를 닫았다는 표시를 남기지 못했습니다.");
        }
    }

    /// <summary>방금 확인했다고 적는다. 글자를 굽는 일은 PortalBoot 가 한다 — 열쇠와 꼴을 아는 자리가 둘이면 한쪽만 고치는 날이 온다. 그쪽이 「마지막으로 다룬 때」도 함께 고쳐 시계가 다음 깨어날 때를 셈할 수 있다.</summary>
    private Task StampSyncAsync() => Boot.StampGeoSyncAsync();

    // 다음에 잴 때를 기다리는 시계. 화면 전환에만 기대면 한 화면을 열어 둔 사람은 영영 안 잰다(운영 기록의 103분 틈). 1분마다 훑으면 회로가 한 시간에 왕복 예순을 내므로 그 시각에 한 번만 깬다.

    /// <summary>시계. 부품이 사라지면 걷는다.</summary>
    private CancellationTokenSource? _clock;

    /// <summary>시계가 아무리 바빠도 이보다 촘촘히는 깨지 않는 바닥이다 — 시각 셈이 어긋나(기기 시계를 되돌린 경우) 쉬지 않고 도는 것을 막고, 회로가 끊겨 못 쟀을 때 다시 해 보는 간격이기도 하다.</summary>
    private static readonly TimeSpan MinWait = TimeSpan.FromSeconds(30);

    /// <summary>다음에 깰 때까지. 마지막으로 다룬 때부터 센다 — 방금 깨어 아무것도 못 했으면 한 간격을 더 기다린다(쉬지 않고 돌지 않게).</summary>
    private TimeSpan NextWait()
    {
        var interval = Boot.GeoSyncInterval;
        var now = DateTime.UtcNow;

        var wait = Boot.GeoSyncedAt is { } last && now - last < interval
            ? last + interval - now
            : interval;

        return wait < MinWait ? MinWait : wait;
    }

    /// <summary>시계를 건다. 앞서 걸어 둔 것이 있으면 걷는다 — 겹치면 간격마다 둘씩 잰다.</summary>
    private void ScheduleNextSync()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = new CancellationTokenSource();

        _ = RunClockAsync(_clock.Token);
    }

    /// <summary>잘 때가 되면 깨어 한 번 재고 다시 잔다. 간격은 깰 때마다 다시 읽어 환경설정에서 고친 값이 다음 잠부터 듣는다. 깰 때 하는 일이 브라우저를 건드리는 것이라 회로가 끊긴 동안에는 JSDisconnectedException 이 난다 — JSException 이 아니라 아래 catch 들에 걸리지 않으니 여기서 받아 기다린다. 끊김으로 시계를 버리면 탭을 닫을 때까지 영영 안 잰다. 서비스워커로는 이 자리를 메울 수 없다(geolocation 이 없다 — docs/geolocation-background.md).</summary>
    private async Task RunClockAsync(CancellationToken token)
    {
        // 회로가 끊겨 못 재고 돌아선 참인가. 그때는 한 간격을 세지 않고 곧 다시 해 본다 — 기다리는 것이 잴 때가 아니라 회로다.
        var disconnected = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(disconnected ? MinWait : NextWait(), token);

                // 회로의 차례로 돌아와서 화면을 건드린다 — 시계는 회로 밖에서 돌아 여기서 바로 JS 를 부르면 렌더링이 엉킨다.
                await InvokeAsync(SyncOnScheduleAsync);

                disconnected = false;
            }
            catch (OperationCanceledException)
            {
                // 부품이 사라졌다. 할 일이 없다.
                return;
            }
            catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException)
            {
                // 회로가 끊겨 있다. **시계는 그대로 둔다** — 다시 이어지면 잰다.
                disconnected = true;
                Log.LogDebug(ex, "위치를 다시 재려는데 회로가 끊겨 있었다. 곧 다시 해 본다.");
            }
            catch (Exception ex)
            {
                Log.LogDebug(ex, "위치를 다시 재지 못했다.");
            }
        }
    }

    /// <summary>시계가 깨웠을 때 가는 길. 창을 띄우지 않는다 — 창을 닫은 사람에게 30분 뒤 같은 창을 다시 미는 것은 조르기다.</summary>
    private async Task SyncOnScheduleAsync()
    {
        if (_open || _busy)
        {
            return;
        }

        // 탭이 둘일 수 있다 — 표시는 탭끼리 나눠 쓰고 회로는 탭마다 따로 돈다. 옆 탭이 방금 쟀으면 여기서 물러난다.
        var last = await Boot.ReadGeoSyncedAtAsync();

        if (last is { } at && DateTime.UtcNow - at < Boot.GeoSyncInterval)
        {
            return;
        }

        var permission = await Geo.PermissionAsync();

        // 조용히 재는 길이라 허용된 경우에만 간다 — 아직 안 준 사람에게 물음창을 띄우는 것은 사람이 누른 사슬에서만 할 일이다.
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

    /// <summary>사용자가 화면을 열고 쓰는 동안 조용히 위치 변동을 쫓도록 브라우저에 등록한다.</summary>
    private async Task StartActiveTrackingAsync()
    {
        if (_selfReference is null)
        {
            return;
        }

        try
        {
            await Js.InvokeVoidAsync("jsiniGeo.startWatch", _selfReference);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            Log.LogDebug(ex, "위치 추적기를 시작하지 못했습니다.");
        }
    }

    /// <summary>화면을 닫거나 다른 업무로 옮길 때 위치 추적을 멈춘다.</summary>
    private async Task StopActiveTrackingAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("jsiniGeo.stopWatch");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            Log.LogDebug(ex, "위치 추적기를 멈추지 못했습니다.");
        }
    }

    /// <summary>
    /// 화면이 열려 있는 동안 브라우저가 위치 변동이나 화면 활성화를 감지하여 알려 온다.
    /// 사용자가 알아채지 못하게 조용히 수집하여 이동 경로(scom.location_tracks)에 기록한다.
    /// </summary>
    [JSInvokable]
    public async Task OnLocationTrackReportAsync(double lat, double lon, double? accuracy)
    {
        if (_savingTrack || _open || _busy)
        {
            return;
        }

        _savingTrack = true;
        try
        {
            if (!await LoadSettingsAsync() || !HasLocation)
            {
                return;
            }

            var geo = new GeoResult
            {
                Ok = true,
                Latitude = lat,
                Longitude = lon,
                Accuracy = accuracy,
            };

            var result = await Geo.SaveAsync(_settings!.Preference, geo, enableLocalWeather: false);

            if (result.Ok)
            {
                await StampSyncAsync();
            }
        }
        finally
        {
            _savingTrack = false;
        }
    }

    /// <summary>시계를 걷는다. 업무를 옮기면 이 부품이 통째로 사라진다.</summary>
    public void Dispose()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = null;

        _selfReference?.Dispose();
        _selfReference = null;

        _ = StopActiveTrackingAsync();
    }
}
