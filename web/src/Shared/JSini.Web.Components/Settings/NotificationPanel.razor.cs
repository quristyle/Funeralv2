using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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

    // **보일 것을 고르는 매개변수가 없다.** `ShowTest` · `ShowDevices` 가
    // 있었고 둘 다 기본이 「둔다」였는데, 끄는 쪽을 쓰는 화면이 없어졌다 —
    // 알림 설정 화면을 걷어내고 판 전체를 환경설정 하나에서 열기 때문이다.
    // 매개변수만 남겨 두면 「어느 화면이 끄고 있나」를 매번 되짚게 된다.

    private NotificationSettingsDto? _settings;
    private PushSubscriptionListDto? _subscriptions;
    private ConfirmDialog? _confirm;

    /// <summary>
    /// 기기 목록.
    ///
    /// 두 곳에서 온다 — 설정 응답의 <c>devices</c> 와 구독 목록. 보통 같지만
    /// 한쪽이 비는 경우가 있어 <b>더 많이 담긴 쪽</b>을 쓴다.
    /// </summary>
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

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var settings = Api.GetMyPreferencesAsync();
        var subs = Api.GetMySubscriptionsAsync();

        await Task.WhenAll(settings, subs);

        _settings = settings.Result ?? new NotificationSettingsDto();
        _subscriptions = subs.Result;

        // **좌표를 알았으니 그것이 어디인지도 묻는다.** 저장된 이름이 비어 있는
        // 사람이 실제로 있어(위 「여기가 어디인가」), 안 물으면 화면에 숫자
        // 두 개만 남는다. 실패해도 이 조회를 실패로 만들지 않는다.
        await LoadPlaceAsync();

        // 설정을 읽었으면 화면은 채워진 것이다. 기기가 없다고 「없습니다」를
        // 띄우면 설정을 못 읽은 것처럼 보인다.
        return 1;
    }, "설정을 받지 못했습니다.", "설정을 읽지 못했습니다");

    /// <summary>
    /// 스위치를 누르는 즉시 저장한다. 저장 단추를 따로 두면 무엇을 바꿨는지
    /// 사용자가 기억해야 하고, 저장 없이 나가면 조용히 사라진다.
    /// </summary>
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

    // ── 내 위치 날씨 ────────────────────────────────────────────
    //
    // **웹은 뒤에서 위치를 못 읽는다.** 서비스워커에는 geolocation 이 없고,
    // 탭이 닫힌 뒤에 묻는 길도 없다. 그래서 여기서 한 번 받아 **서버에
    // 저장해 두고**, 알림은 서버가 그 저장된 좌표로 만들어 보낸다. 자리가
    // 바뀌면 사람이 다시 눌러야 하고, 「잡은 때」를 보여 주는 것이 그 때문이다.

    /// <summary>위치를 잡거나 미리보기를 받아 오는 동안 단추를 잠근다.</summary>
    private bool _locating;

    /// <summary>미리 보여 줄 날씨. 아직 안 받았으면 <c>null</c> 이다.</summary>
    private PointWeatherDto? _preview;

    /// <summary>
    /// 좌표를 저장해 두었나. <b>스위치를 켤 수 있는지가 여기 달려 있다</b> —
    /// 좌표가 없으면 켜도 보낼 곳이 없고, 그러면 켜졌는데 아무 알림도 안 오는
    /// 상태가 된다.
    /// </summary>
    private bool HasSavedLocation =>
        _settings?.Preference is { WeatherLat: not null, WeatherLon: not null };

    /// <summary>저장된 좌표. <b>이름을 찾았어도 함께 보여 준다</b>.</summary>
    private string CoordText =>
        _settings?.Preference is { WeatherLat: { } lat, WeatherLon: { } lon }
            ? FormattableString.Invariant($"{lat:0.####}, {lon:0.####}")
            : "-";

    // ── 여기가 어디인가 ─────────────────────────────────────────
    //
    // **좌표만 저장된 사람이 실제로 있다.** 좌표를 먼저 저장하고 이름은 날씨와
    // 함께 받아 채우는 순서라(`GeoLocator.SaveAsync`), 기상청이 답하지 않은
    // 날에 잡으면 이름 칸이 빈 채로 남는다. 그때 화면에는 숫자 두 개뿐이었다.
    //
    // 그래서 **화면이 열릴 때 이름을 따로 물어 본다.** 날씨를 거치지 않는
    // 길이라(`life/weather/place`) 기상청이 느려도 이름은 뜬다.

    /// <summary>물어서 받아 둔 행정구역. 아직 못 받았으면 <c>null</c>.</summary>
    private PointPlaceDto? _place;

    /// <summary>
    /// 머리에 한 줄로 적을 이름. <b>물어서 받은 것이 먼저</b>다 — 저장된 이름은
    /// 자리가 바뀌면 지워지므로(그쪽이 옛 동네일 수 있다) 새로 받은 쪽을 믿는다.
    /// </summary>
    private string? PlaceText =>
        string.IsNullOrWhiteSpace(_place?.Place)
            ? _settings?.Preference.WeatherPlace
            : _place!.Place;

    /// <summary>
    /// 단계별 이름. 못 찾았으면 <c>-</c> 다.
    /// </summary>
    /// <remarks>
    /// <b>물어서 받은 것이 정본이다.</b> 그것을 못 받았을 때만 저장된 이름을
    /// 공백으로 가른다 — 정확하지 않을 수 있지만(이름에 공백이 든 행정구역),
    /// 세 칸을 통째로 비워 두는 것보다 낫다. 서버가 같은 공백으로 이어 붙인다.
    /// </remarks>
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

    /// <summary>
    /// 저장된 좌표가 어디인지 물어 온다. <b>실패해도 아무 말도 하지 않는다</b> —
    /// 곁들이는 값이고, 못 받으면 저장된 이름으로 되돌아간다.
    /// </summary>
    private async Task LoadPlaceAsync()
    {
        if (_settings?.Preference is not { WeatherLat: { } lat, WeatherLon: { } lon })
        {
            _place = null;
            return;
        }

        // 같은 자리를 다시 묻지 않는다. 스위치를 누를 때마다 부르면 표를
        // 훑는 일이 스위치 수만큼 는다.
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

    // ── 위치 확인 간격 ──────────────────────────────────────────
    //
    // **이 브라우저의 값이다**(`PortalBoot.GeoSyncIntervalKey`). 계정에 두면
    // 큰 모니터 앞에서 고른 촘촘한 간격이 휴대폰까지 따라가 배터리를 쓴다.

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

    /// <summary>
    /// 받는 시각을 고르는 칸. <b>시각을 낱낱이 고르게 하지 않는다</b> —
    /// 스물네 개의 체크는 고르는 일이 되고, 사람이 실제로 원하는 것은
    /// 「아침에」·「아침저녁으로」 정도다.
    /// </summary>
    /// <remarks>
    /// 값은 <c>7,18</c> 꼴의 문자열로 저장한다. 서버가 그것을 다듬어(범위를
    /// 벗어난 값·중복을 버린다) 두므로, 여기서 고르지 않은 값이 들어와도
    /// 아래 <see cref="CurrentHourPreset"/> 이 목록에서 못 찾고 빈 칸이 될 뿐이다.
    /// </remarks>
    private sealed record HourPreset(string Label, string Hours);

    private static readonly HourPreset[] HourPresets =
    [
        new("아침 7시", "7"),
        new("아침 7시 · 저녁 6시", "7,18"),
        new("아침 7시 · 낮 12시 · 저녁 6시", "7,12,18"),
        new("세 시간마다 (6~21시)", "6,9,12,15,18,21"),

        // **가장 촘촘한 칸이다.** 하루 열여섯 통이라 모두에게 권할 것은
        // 아니지만, 날씨를 따라 움직이는 일(현장·운행)을 하는 사람에게는
        // 세 시간이 너무 성글다. 위치 확인 간격을 한 시간으로 두면 이 칸의
        // 발송마다 좌표가 한 번은 확인된 뒤에 나간다.
        new("한 시간마다 (6~21시)", "6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21"),
    ];

    /// <summary>
    /// 지금 고른 시각 묶음. 저장된 값이 없으면 기본값(<c>7,18</c>)으로 본다 —
    /// 서버의 발송기가 같은 기본값을 쓴다.
    /// </summary>
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

    /// <summary>
    /// 브라우저에게 위치를 묻고, 받은 좌표를 저장한 뒤 그 지점의 날씨를 보여 준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>순서가 중요하다.</b> 좌표를 먼저 서버에 저장하고 그 다음에 날씨를
    /// 받는다 — 반대로 하면 날씨 조회가 실패했을 때(기상청이 느릴 때가 실제로
    /// 있다) 위치도 함께 날아가고, 사람은 단추를 다시 눌러야 한다.
    /// </para>
    /// <para>
    /// <b>지역 이름은 날씨와 함께 온다.</b> 좌표를 저장하는 시점에는 아직
    /// 모르므로 이름 없이 저장하고, 날씨를 받아 이름을 알게 되면 한 번 더
    /// 저장한다. 서버 쪽 발송기도 같은 이름을 채워 넣는다.
    /// </para>
    /// </remarks>
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

            // **스위치는 건드리지 않는다.** 여기서 하는 일은 좌표를 갈아 끼우는
            // 것뿐이고, 「내 위치 날씨」를 켜고 끄는 것은 바로 위 체크의 몫이다
            // (로그인 뒤에 뜨는 권유 창은 반대로 함께 켠다 — 거기서는 위치를
            // 내주는 뜻이 곧 그것이기 때문이다).
            var result = await Geo.SaveAsync(_settings.Preference, geo, enableLocalWeather: false);

            Say(result.Message, result.Tone);

            if (!result.Ok) return;

            // 자리가 그대로면 절차가 날씨를 안 받아 온다(기상청을 부를 까닭이
            // 없다). 그때는 저장된 좌표로 미리보기만 따로 받아 보여 준다 —
            // 사람이 단추를 눌렀으므로 화면에 뭔가는 나와야 한다.
            if (result.Point is { } point)
            {
                _preview = point;
            }
            else
            {
                await LoadPreviewAsync(geo.Latitude, geo.Longitude);
            }

            // 좌표가 갈렸으면 지역 이름도 갈린다. 여기서 안 물으면 화면이
            // 새 좌표에 옛 동네 이름을 붙여 보여 준다.
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

    /// <summary>
    /// 한 지점의 날씨를 받아 미리보기에 담는다. <b>좌표도 이름도 건드리지 않는다</b> —
    /// 저장하는 일은 절차(<see cref="GeoLocator" />)가 한다.
    /// </summary>
    /// <param name="lat">위도(10진 도)</param>
    /// <param name="lon">경도(10진 도)</param>
    private async Task LoadPreviewAsync(double lat, double lon)
    {
        PointWeatherDto? point = null;

        var ok = await RunAsync(
            async () => point = await Api.GetPointWeatherAsync(lat, lon),
            // 여기서는 아무 말도 하지 않는다 — 좌표 저장이 이미 말했고,
            // 날씨는 아래에서 그림으로 보인다.
            okMessage: string.Empty, "날씨를 받지 못했습니다");

        if (!ok || point is null) return;

        _preview = point;
    }

    /// <summary>
    /// 시험 발송. <b>보낸 것이 없으면 성공이라고 말하지 않는다</b> — 그때는
    /// 서버가 준 까닭을 그대로 옮긴다(구독한 기기가 없다 · 스위치가 꺼져 있다).
    /// 「보냈습니다」라고만 하면 알림이 안 오는 사람이 자기 기기를 의심한다.
    /// </summary>
    private async Task TestAsync()
    {
        if (_busy) return;

        _busy = true;
        try
        {
            PushSendResultDto? result = null;

            var ok = await RunAsync(
                async () => result = await Api.SendTestPushAsync(),
                // **여기서는 아무 말도 하지 않는다.** 보낸 대수를 보고 아래에서
                // 가려 말한다 — 빈 문구는 토스트가 알아서 넘긴다.
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

    /// <summary>
    /// 브라우저 상태를 읽는다.
    ///
    /// <b>OnInitializedAsync 에서 부를 수 없다.</b> 그 시점은 프리렌더 중이라
    /// 브라우저가 아직 없고, JS interop 은 거기서 던진다. 첫 렌더 뒤에 읽고
    /// 화면을 다시 그린다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await RefreshBrowserAsync();

        // 고른 위치 확인 간격도 여기서 읽는다 — 브라우저 저장소라 같은 사정이다.
        // 레이아웃이 이미 내는 왕복에 실려 온다(`PortalBoot` 머리말).
        _intervalMinutes = (await Boot.ReadAsync()).GeoSyncMinutes;

        StateHasChanged();
    }

    /// <summary>
    /// <c>jsiniPwa.status</c> 를 읽어 <see cref="_browser"/> 에 담는다.
    ///
    /// 스크립트가 아직 안 실렸거나(느린 망) 브라우저가 막아 둔 경우를 위해
    /// 예외를 삼키고 「지원하지 않음」으로 떨어뜨린다 — 이 판의 나머지
    /// (설정 스위치 · 기기 목록 · 시험 발송)는 그래도 쓸 수 있어야 한다.
    /// </summary>
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

    /// <summary>
    /// 브라우저의 설치 창을 띄운다.
    ///
    /// <para>
    /// <b>단추 클릭에서 이어져야 한다</b> — 화면이 저절로 부르면 브라우저가
    /// 조용히 거절한다(알림 권한과 같다). 설치하고 나면 실행 방식이 바뀌므로
    /// 상태를 다시 읽는다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 이 브라우저를 등록한다. <b>절차 자체는 <see cref="PushEnroll"/> 에 있다</b> —
    /// 순서를 틀리면 조용히 깨지는 대목이 셋이고, 그것을 부르는 자리가 둘이라
    /// (이 판 · 로그인 뒤 권유 창) 한 벌만 둔다. 여기서는 결과를 말하고 화면을
    /// 다시 읽을 뿐이다.
    /// </summary>
    private async Task SubscribeAsync()
    {
        if (_busy) return;

        _busy = true;
        try
        {
            // **절차는 `PushEnroll` 하나뿐이다.** 로그인 뒤에 스스로 뜨는
            // 권유 창(`PushAskPopup`)도 같은 것을 부른다 — 순서를 틀리면
            // 조용히 깨지는 대목이 셋이라 한 벌만 둔다(그쪽 머리말).
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

    /// <summary>
    /// 이 브라우저의 등록을 푼다.
    ///
    /// <b>서버를 먼저 지운다.</b> 브라우저에서 먼저 끊으면 endpoint 를 잃어
    /// 서버 것을 지울 열쇠가 없어진다 — pwa.js 의 <c>unsubscribe</c> 가
    /// 끊기 전에 그 값을 꺼내 돌려주지만, 서버 호출이 실패했을 때 되돌릴
    /// 수 있는 쪽은 이 순서뿐이다.
    /// </summary>
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

    /// <summary>
    /// 목록에 있는 기기를 서버에서 떼어 낸다.
    ///
    /// <para>
    /// <b>그것이 지금 이 브라우저이면 브라우저 구독도 함께 끊는다.</b>
    /// 서버에서만 지우면 브라우저에는 구독이 남아, 「등록」을 다시 눌러도
    /// 이미 있는 구독이 돌아와 아무 일도 안 일어난 것처럼 보인다.
    /// </para>
    ///
    /// <para>
    /// 되돌릴 수 없으므로 한 번 묻는다 — 그 기기로는 그때부터 알림이 오지
    /// 않고, 다시 받으려면 그 기기에서 직접 등록해야 한다.
    /// </para>
    /// </summary>
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

    // 기기 이름·자잘한 정보는 `PushDeviceLabel` 이 만든다. 한동안 여기
    // private 메서드로 있었는데, 포털관리에 **남의 기기**를 보는 화면이
    // 생기면서 같은 글자가 두 곳에 필요해졌다 — 복제하면 새 브라우저가 나올
    // 때마다 한쪽만 고치게 되고, 그 어긋남은 「내 기기가 목록에 없다」로
    // 신고가 들어온다(그 클래스 머리말).
    private static string DeviceName(PushDeviceDto device) => PushDeviceLabel.Name(device);

    private static string DeviceDetails(PushDeviceDto device) => PushDeviceLabel.Details(device);
}
