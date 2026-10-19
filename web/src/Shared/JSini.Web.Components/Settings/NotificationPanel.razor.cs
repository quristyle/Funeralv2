using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Settings;

public partial class NotificationPanel
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private PushEnroll Enroll { get; set; } = default!;
    [Inject] private GeoLocator Geo { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // 보일 것을 고르는 매개변수가 없다 — 끄는 쪽을 쓰는 화면이 없어졌고(판 전체를 환경설정 하나에서 연다), 남겨 두면 어느 화면이 끄는지 매번 되짚게 된다.

    private NotificationSettingsDto? _settings;
    private PushSubscriptionListDto? _subscriptions;
    private ConfirmDialog? _confirm;

    /// <summary>기기 목록. 설정 응답의 devices 와 구독 목록 두 곳에서 오므로 더 많이 담긴 쪽을 쓴다.</summary>
    private IReadOnlyList<PushDeviceDto> Devices
    {
        get
        {
            var fromSettings = _settings?.Devices ?? [];
            var fromSubs = _subscriptions?.Items ?? [];

            return fromSubs.Count >= fromSettings.Count ? fromSubs : fromSettings;
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    // 알림 서버에 못 닿았을 때는 이 판만 자기 자리에서 접는다 — 스위치·공개 키·기기 목록이 전부 알림 서버에 있다. 같은 화면의 다른 구역은 브라우저 값이라 멀쩡하므로 구역 하나만 접는다.
    // 로그인이 풀린 것과 권한이 막힌 것은 접지 않는다(`when` 절) — 셸이 따로 받는 길이라 여기서 삼키면 그 길이 끊긴다.

    /// <summary>알림 서버에 닿지 못한 까닭. 닿았으면 <c>null</c> 이다.</summary>
    private string? _loadError;

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _loadError = null;

        try
        {
            var settings = Api.GetMyPreferencesAsync();
            var subs = Api.GetMySubscriptionsAsync();

            await Task.WhenAll(settings, subs);

            _settings = settings.Result ?? new NotificationSettingsDto();
            _subscriptions = subs.Result;

            // 좌표를 알았으니 그 자리의 이름도 묻는다 — 저장된 이름이 비어 있는 사람이 실제로 있다. 실패해도 이 조회를 실패로 만들지 않는다.
            await LoadPlaceAsync();
        }
        catch (ApiException ex) when (!ex.IsUnauthorized && !ex.IsForbidden)
        {
            _loadError = ex.Message;
            _settings = null;
            _subscriptions = null;
            _place = null;
        }

        // 설정을 읽었으면 화면은 채워진 것이다. 기기가 없다는 말은 접힌 때도 1 이다 — 접힌 쪽이 이미 까닭을 말하므로 토스트까지 뜨면 같은 말이 두 번 난다.
        return 1;
    }, "설정을 받지 못했습니다.", "설정을 읽지 못했습니다");

    /// <summary>스위치를 누르는 즉시 저장한다. 저장 단추를 따로 두면 사용자가 무엇을 바꿨는지 기억해야 하고, 저장 없이 나가면 조용히 사라진다.</summary>
    private async Task ToggleAsync(Action<NotificationPreferenceDto> change)
    {
        if (_settings is null)
        {
            return;
        }

        change(_settings.Preference);

        if (await RunAsync(() => Api.SaveMyPreferencesAsync(_settings.Preference),
                           "저장했습니다.", "저장하지 못했습니다"))
        {
            // 저장했으니 「아직 정한 적 없음」 안내를 내린다.
            _settings.Preference.Saved = true;
            _settings.Preference.UpdatedAt = DateTime.UtcNow;
        }
    }

    // 내 위치 날씨: 웹은 뒤에서 위치를 못 읽으므로 여기서 한 번 받아 서버에 저장하고, 알림은 서버가 그 좌표로 만든다. 자리가 바뀌면 사람이 다시 눌러야 하고 「잡은 때」를 보여 주는 것이 그 때문이다.

    /// <summary>위치를 잡거나 미리보기를 받아 오는 동안 단추를 잠근다.</summary>
    private bool _locating;

    /// <summary>미리 보여 줄 날씨. 아직 안 받았으면 <c>null</c> 이다.</summary>
    private PointWeatherDto? _preview;

    /// <summary>좌표를 저장해 두었나. 스위치를 켤 수 있는지가 여기 달려 있다 — 좌표가 없으면 켜졌는데 알림이 안 오는 상태가 된다.</summary>
    private bool HasSavedLocation =>
        _settings?.Preference is { WeatherLat: not null, WeatherLon: not null };

    /// <summary>저장된 좌표. <b>이름을 찾았어도 함께 보여 준다</b>.</summary>
    private string CoordText =>
        _settings?.Preference is { WeatherLat: { } lat, WeatherLon: { } lon }
            ? FormattableString.Invariant($"{lat:0.####}, {lon:0.####}")
            : "-";

    // 여기가 어디인가: 좌표만 저장되고 이름은 날씨와 함께 채우는 순서라 기상청이 답하지 않은 날에는 이름이 비어 남는다. 그래서 화면이 열릴 때 이름을 따로 묻는다(날씨를 거치지 않는 길).

    /// <summary>물어서 받아 둔 행정구역. 아직 못 받았으면 <c>null</c>.</summary>
    private PointPlaceDto? _place;

    /// <summary>머리에 한 줄로 적을 이름. 물어서 받은 것이 먼저다 — 저장된 이름은 자리가 바뀌면 옛 동네일 수 있다.</summary>
    private string? PlaceText =>
        string.IsNullOrWhiteSpace(_place?.Place)
            ? _settings?.Preference.WeatherPlace
            : _place!.Place;

    /// <summary>단계별 이름. 못 찾았으면 -. 물어서 받은 것이 정본이고, 못 받았을 때만 저장된 이름을 공백으로 가른다 — 정확하지 않을 수 있어도 세 칸을 비워 두는 것보다 낫다.</summary>
    private string RegionText(int level)
    {
        if (_place is { } found)
        {
            var name = level switch
            {
                1 => found.Region1,
                2 => found.Region2,
                _ => found.Region3,
            };

            return string.IsNullOrWhiteSpace(name) ? "-" : name!;
        }

        var saved = _settings?.Preference.WeatherPlace;

        if (string.IsNullOrWhiteSpace(saved))
        {
            return "-";
        }

        var parts = saved.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length >= level ? parts[level - 1] : "-";
    }

    /// <summary>저장된 좌표가 어디인지 물어 온다. 실패해도 말하지 않는다 — 곁들이는 값이고 못 받으면 저장된 이름으로 돌아간다.</summary>
    private async Task LoadPlaceAsync()
    {
        if (_settings?.Preference is not { WeatherLat: { } lat, WeatherLon: { } lon })
        {
            _place = null;
            return;
        }

        // 같은 자리를 스위치마다 다시 묻지 않는다 — 부를 때마다 표를 훑으면 스위치 수만큼 는다.
        if (_place is { } had
            && Math.Abs(had.Lat - lat) < 1e-9
            && Math.Abs(had.Lon - lon) < 1e-9)
        {
            return;
        }

        try
        {
            _place = await Api.GetPointPlaceAsync(lat, lon);
        }
        catch (Exception)
        {
            _place = null;
        }
    }

    // 위치 확인 간격은 이 브라우저의 값이다(PortalBoot.GeoSyncIntervalKey) — 계정에 두면 큰 모니터에서 고른 촘촘한 간격이 휴대폰까지 따라가 배터리를 쓴다.

    /// <summary>고르개에 놓을 한 칸.</summary>
    private sealed record IntervalChoice(int Minutes, string Label);

    private static readonly IntervalChoice[] IntervalChoices =
        [.. PortalBoot.GeoSyncMinuteChoices.Select(m => new IntervalChoice(m, IntervalLabel(m)))];

    /// <summary>분을 사람이 읽는 말로. 60분 미만은 분으로, 그 위는 시간으로.</summary>
    private static string IntervalLabel(int minutes) =>
        minutes < 60 ? $"{minutes}분마다" : $"{minutes / 60}시간마다";

    /// <summary>지금 고른 간격. 브라우저에서 읽어 둔 값이다.</summary>
    private int _intervalMinutes = PortalBoot.DefaultGeoSyncMinutes;

    private IntervalChoice? CurrentInterval =>
        IntervalChoices.FirstOrDefault(c => c.Minutes == _intervalMinutes);

    private async Task ChangeIntervalAsync(IntervalChoice? choice)
    {
        if (choice is null) return;

        _intervalMinutes = choice.Minutes;
        await Boot.SetGeoSyncIntervalAsync(choice.Minutes);
    }

    /// <summary>받는 시각을 고르는 칸. 시각을 낱낱이 고르게 하지 않는다 — 사람이 원하는 것은 「아침에」·「아침저녁으로」 정도다. 값은 7,18 꼴이고 서버가 범위·중복을 버린다.</summary>
    private sealed record HourPreset(string Label, string Hours);

    private static readonly HourPreset[] HourPresets =
    [
        new("아침 7시", "7"),
        new("아침 7시 · 저녁 6시", "7,18"),
        new("아침 7시 · 낮 12시 · 저녁 6시", "7,12,18"),
        new("세 시간마다 (6~21시)", "6,9,12,15,18,21"),

        // 가장 촘촘한 칸이다. 날씨에 따라 움직이는 일을 하는 사람에게만 맞다 — 위치 확인 간격이 30분이라 발송마다 좌표가 한두 번은 확인된 뒤에 나간다.
        new("한 시간마다 (6~21시)", "6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21"),
    ];

    /// <summary>지금 고른 시각 묶음. 저장된 값이 없으면 기본 7,18 로 본다 — 서버 발송기가 같은 기본값을 쓴다.</summary>
    private HourPreset? CurrentHourPreset
    {
        get
        {
            var hours = string.IsNullOrWhiteSpace(_settings?.Preference.WeatherHours)
                ? "7,18"
                : _settings!.Preference.WeatherHours!;

            return HourPresets.FirstOrDefault(p => p.Hours == hours);
        }
    }

    private async Task ChangeHoursAsync(HourPreset? preset)
    {
        if (preset is null || _settings is null) return;

        await ToggleAsync(p => p.WeatherHours = preset.Hours);
    }

    /// <summary>브라우저에 위치를 묻고 받은 좌표를 저장한 뒤 그 지점의 날씨를 보여 준다. 좌표를 먼저 저장하고 날씨는 나중에 받는다 — 반대면 기상청이 느릴 때 위치까지 날아간다. 지역 이름은 날씨와 함께 와서 한 번 더 저장한다.</summary>
    private async Task LocateAsync()
    {
        if (_locating || _settings is null) return;

        _locating = true;
        try
        {
            var geo = await Geo.LocateAsync();

            if (!geo.Ok)
            {
                Say(geo.Error ?? "위치를 받지 못했습니다.", NoticeTone.Warning);
                return;
            }

            // 스위치는 건드리지 않는다 — 좌표만 갈아 끼운다. 「내 위치 날씨」를 켜고 끄는 것은 위의 체크의 몫이다.
            var result = await Geo.SaveAsync(_settings.Preference, geo, enableLocalWeather: false);

            Say(result.Message, result.Tone);

            if (!result.Ok) return;

            // 자리가 그대로면 날씨를 안 받아 오므로 저장된 좌표로 미리보기만 따로 받는다 — 사람이 단추를 눌렀으므로 화면에 뭔가는 나와야 한다.
            if (result.Point is { } point)
            {
                _preview = point;
            }
            else
            {
                await LoadPreviewAsync(geo.Latitude, geo.Longitude);
            }

            // 좌표가 갈렸으면 지역 이름도 다시 묻는다 — 안 물으면 새 좌표에 옛 동네 이름이 붙는다.
            await LoadPlaceAsync();
        }
        finally
        {
            _locating = false;
        }
    }

    /// <summary>저장해 둔 좌표의 날씨를 다시 받아 본다. 좌표는 건드리지 않는다.</summary>
    private async Task PreviewSavedAsync()
    {
        if (_locating || _settings?.Preference is not { WeatherLat: { } lat, WeatherLon: { } lon }) return;

        _locating = true;
        try
        {
            await LoadPreviewAsync(lat, lon);
        }
        finally
        {
            _locating = false;
        }
    }

    /// <summary>한 지점의 날씨를 받아 미리보기에 담는다. 좌표도 이름도 건드리지 않는다 — 저장은 GeoLocator 가 한다.</summary>
    /// <param name="lat">위도(10진 도)</param>
    /// <param name="lon">경도(10진 도)</param>
    private async Task LoadPreviewAsync(double lat, double lon)
    {
        PointWeatherDto? point = null;

        var ok = await RunAsync(
            async () => point = await Api.GetPointWeatherAsync(lat, lon),
            // 여기서는 말하지 않는다 — 좌표 저장이 이미 말했고 날씨는 아래 그림으로 보인다.
            okMessage: string.Empty, "날씨를 받지 못했습니다");

        if (!ok || point is null) return;

        _preview = point;
    }

    /// <summary>시험 발송. 보낸 것이 없으면 성공이라 말하지 않고 서버가 준 까닭(기기가 없다 · 스위치가 꺼졌다)을 옮긴다 — 「보냈습니다」만 하면 알림이 안 오는 사람이 자기 기기를 의심한다.</summary>
    private async Task TestAsync()
    {
        if (_busy) return;

        _busy = true;
        try
        {
            PushSendResultDto? result = null;

            var ok = await RunAsync(
                async () => result = await Api.SendTestPushAsync(),
                // 여기서는 말하지 않는다 — 보낸 대수를 보고 아래에서 가려 말한다.
                okMessage: string.Empty, "시험 발송을 하지 못했습니다");

            if (!ok) return;

            if (result is { Sent: > 0 })
            {
                Say($"시험 알림을 보냈습니다 (기기 {result.Sent}대). 잠시 뒤 도착합니다.");
            }
            else
            {
                Say(result?.Message ?? "보낸 알림이 없습니다. 이 브라우저를 먼저 등록하십시오.",
                    NoticeTone.Warning);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    // ── 이 브라우저의 구독 ──────────────────────────────────────

    /// <summary>브라우저 쪽 사정. 아직 못 읽었으면 <c>null</c> 이다.</summary>
    private PushBrowserResult? _browser;

    /// <summary>구독·해제가 도는 동안 단추를 잠근다. 두 번 누르면 기기가 둘 생긴다.</summary>
    private bool _busy;

    /// <summary>브라우저 상태를 읽는다. OnInitializedAsync 에서 부를 수 없다 — 프리렌더 중에는 브라우저가 없어 JS interop 이 던진다. 첫 렌더 뒤에 읽고 화면을 다시 그린다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await RefreshBrowserAsync();

        // 고른 위치 확인 간격도 여기서 읽는다 — 레이아웃이 이미 내는 왕복에 실려 온다(PortalBoot).
        _intervalMinutes = (await Boot.ReadAsync()).GeoSyncMinutes;

        StateHasChanged();
    }

    /// <summary>jsiniPwa.status 를 읽어 _browser 에 담는다. 스크립트가 아직 안 실렸거나 막혔으면 예외를 삼키고 지원하지 않음으로 떨어뜨린다 — 판의 나머지는 그래도 쓸 수 있어야 한다.</summary>
    private async Task RefreshBrowserAsync()
    {
        try
        {
            _browser = await JS.InvokeAsync<PushBrowserResult>("jsiniPwa.status");
        }
        catch (Exception)
        {
            _browser = new PushBrowserResult { Supported = false, Permission = "unsupported" };
        }
    }

    /// <summary>브라우저의 설치 창을 띄운다. 단추 클릭에서 이어져야 한다 — 저절로 부르면 조용히 거절된다. 설치하고 나면 실행 방식이 바뀌므로 상태를 다시 읽는다.</summary>
    private async Task InstallAsync()
    {
        if (_busy) return;

        _busy = true;
        try
        {
            PushBrowserResult? result = null;

            try
            {
                result = await JS.InvokeAsync<PushBrowserResult>("jsiniPwa.install");
            }
            catch (Exception ex)
            {
                Say($"설치 창을 띄우지 못했습니다: {ex.Message}", NoticeTone.Warning);
            }

            if (result is { Ok: false })
            {
                Say(result.Error ?? "설치 창을 띄우지 못했습니다.", NoticeTone.Warning);
            }

            await RefreshBrowserAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>이 브라우저를 등록한다. 절차는 PushEnroll 에 있다 — 순서를 틀리면 조용히 깨지는 대목이 셋이고 부르는 자리가 둘이라 한 벌만 둔다. 여기서는 결과를 말하고 화면을 다시 읽는다.</summary>
    private async Task SubscribeAsync()
    {
        if (_busy) return;

        _busy = true;
        try
        {
            // 절차는 PushEnroll 하나뿐이다 — 로그인 뒤 권유 창(PushAskPopup)도 같은 것을 부른다.
            var result = await Enroll.SubscribeAsync(_settings ?? new NotificationSettingsDto());

            Say(result.Message, result.Tone);

            await RefreshBrowserAsync();
            await ReloadAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>이 브라우저의 등록을 푼다. 서버를 먼저 지운다 — 브라우저에서 먼저 끊으면 endpoint 를 잃어 서버 것을 지울 열쇠가 없어진다.</summary>
    private async Task UnsubscribeAsync()
    {
        if (_busy) return;

        var endpoint = _browser?.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            await RefreshBrowserAsync();
            return;
        }

        _busy = true;
        try
        {
            var ok = await RunAsync(
                () => Api.RemovePushSubscriptionAsync(endpoint),
                "이 브라우저의 등록을 해제했습니다.", "해제하지 못했습니다");

            if (!ok) return;

            try { await JS.InvokeAsync<PushBrowserResult>("jsiniPwa.unsubscribe"); }
            catch (Exception ex)
            {
                Say($"서버에서는 지웠지만 브라우저 구독을 끊지 못했습니다: {ex.Message}",
                    NoticeTone.Warning);
            }

            await RefreshBrowserAsync();
            await ReloadAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>목록에 있는 기기를 서버에서 떼어 낸다. 지금 이 브라우저이면 브라우저 구독도 함께 끊는다 — 서버에서만 지우면 「등록」이 이미 있는 구독을 돌려보내 아무 일도 안 일어난 것처럼 보인다. 되돌릴 수 없으므로 한 번 묻는다.</summary>
    private async Task RemoveDeviceAsync(PushDeviceDto device)
    {
        if (_busy || string.IsNullOrWhiteSpace(device.Endpoint)) return;

        var name = DeviceName(device);

        if (_confirm is not null
            && !await _confirm.AskAsync($"「{name}」 을(를) 목록에서 뺍니다.\n그 기기로는 알림이 오지 않습니다.",
                                        confirmText: "빼기"))
        {
            return;
        }

        _busy = true;
        try
        {
            var ok = await RunAsync(
                () => Api.RemovePushSubscriptionAsync(device.Endpoint!),
                $"「{name}」 을(를) 뺐습니다.", "기기를 빼지 못했습니다");

            if (!ok) return;

            if (IsThisBrowser(device))
            {
                try { await JS.InvokeAsync<PushBrowserResult>("jsiniPwa.unsubscribe"); }
                catch (Exception) { /* 서버에서는 이미 지웠다. 여기서 더 말할 것이 없다 */ }
            }

            await RefreshBrowserAsync();
            await ReloadAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>지금 보고 있는 브라우저의 구독인가. 열쇠는 <c>endpoint</c> 다.</summary>
    private bool IsThisBrowser(PushDeviceDto device)
        => !string.IsNullOrWhiteSpace(device.Endpoint)
           && string.Equals(device.Endpoint, _browser?.Endpoint, StringComparison.Ordinal);

    /// <summary>브라우저에 구독이 있고 서버 목록에도 이 브라우저가 있는지 확인한다.</summary>
    private bool IsThisBrowserSubscribed => _browser?.Subscribed == true && Devices.Any(IsThisBrowser);

    // 기기 이름은 PushDeviceLabel 이 만든다 — 포털관리의 남의 기기 화면과 같은 글자를 쓰므로 복제하지 않는다(어긋나면 「내 기기가 목록에 없다」가 된다).
    private static string DeviceName(PushDeviceDto device) => PushDeviceLabel.Name(device);

    private static string DeviceDetails(PushDeviceDto device) => PushDeviceLabel.Details(device);
}
