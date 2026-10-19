using JSini.Web.Components.Layout;
using JSini.Web.Models;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Settings;

/// <summary>브라우저에 위치를 묻고 서버에 저장하는 절차 한 벌. 저장 자리가 넷이라 절차를 한 곳에 둔다 — 순서를 틀리면 조용히 어긋나는 대목이 셋이다.
/// (1) 좌표를 먼저 저장하고 날씨는 나중에 받는다 — 반대면 기상청이 느린 날 좌표까지 날아간다. (2) 지역 이름은 날씨와 함께 와서 한 번 더 저장한다. (3) 설정 전체를 보내지 않고 좌표만 보낸다 — 뒤에서 도는 조용한 확인이 전체를 보내면 다른 탭의 스위치를 되돌린다.</summary>
public sealed class GeoLocator(
    NotificationClient api,
    IJSRuntime js,
    ILogger<GeoLocator> logger)
{
    /// <summary>자리가 바뀌었다고 볼 이동 거리(m). 기상청 격자는 5km 칸이라 칸 안의 움직임은 날씨를 바꾸지 않지만, 칸 경계에 사는 사람 때문에 300m 로 잡는다. 문턱이 없으면 GPS 흔들림마다 「잡은 때」가 갱신돼 이사를 가리는 단서가 못 된다.</summary>
    public const double MoveThresholdMeters = 300;

    /// <summary>조용한 확인 사이의 기본 간격(30분). 알림 시각과 같은 간격이면 낮에 옮긴 좌표가 한 칸 늦게 따라오고, 이동 경로가 쌓이면서는 한 시간이면 점심 나들이가 빠진다. 사람이 환경설정에서 고른 값(PortalBoot.GeoSyncInterval)을 따르며, 여기 값은 고른 적이 없을 때의 것이라 PortalBoot.DefaultGeoSyncMinutes 와 같아야 한다.</summary>
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(30);

    /// <summary>브라우저가 위치를 내줄 사정인가. 예외는 삼킨다 — 스크립트가 아직 안 실렸거나 막힌 경우이며 그때는 못 잡은 것으로 본다.</summary>
    public async Task<GeoPermissionResult> PermissionAsync()
    {
        try
        {
            return await js.InvokeAsync<GeoPermissionResult>("jsiniGeo.permission")
                ?? Unknown;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogDebug(ex, "위치 권한 상태를 읽지 못했다. 모르는 것으로 본다.");
            return Unknown;
        }
    }

    private static GeoPermissionResult Unknown =>
        new() { Supported = false, Secure = false, State = "unsupported" };

    /// <summary>지금 위치를 묻는다. 물음창이 뜰 수 있으므로 단추의 클릭에서만 부른다.</summary>
    public async Task<GeoResult> LocateAsync()
    {
        try
        {
            return await js.InvokeAsync<GeoResult>("jsiniGeo.locate")
                ?? new GeoResult { Ok = false, Error = "위치를 받지 못했습니다." };
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogDebug(ex, "위치를 묻지 못했다.");
            return new GeoResult { Ok = false, Error = $"위치를 받지 못했습니다: {ex.Message}" };
        }
    }

    /// <summary>물음창 없이 지금 위치. 권한이 이미 허용된 경우에만 잰다 — 아니면 GeoResult.Skipped 가 참으로 돌아온다.</summary>
    public async Task<GeoResult> QuietAsync()
    {
        try
        {
            return await js.InvokeAsync<GeoResult>("jsiniGeo.quiet")
                ?? new GeoResult { Ok = false, Skipped = true };
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogDebug(ex, "위치를 조용히 재지 못했다.");
            return new GeoResult { Ok = false, Skipped = true };
        }
    }

    /// <summary>지금 위치를 재어 이동 경로에 기록하고 저장한다(내 이동경로의 「지금위치 기록」).</summary>
    public async Task<GeoSaveResult> RecordCurrentAsync(CancellationToken ct = default)
    {
        var geo = await LocateAsync();
        if (!geo.Ok)
        {
            return new GeoSaveResult(false, geo.Error ?? "위치를 받지 못했습니다.", NoticeTone.Warning, null, false);
        }

        NotificationSettingsDto? settings = null;
        try
        {
            settings = await api.GetMyPreferencesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "알림 설정을 읽지 못했다. 빈 설정으로 기록한다.");
        }

        var pref = settings?.Preference ?? new NotificationPreferenceDto();
        return await SaveAsync(pref, geo, enableLocalWeather: false);
    }

    /// <summary>잡은 좌표를 저장한다. 머리말의 순서 셋을 지킨다.</summary>
    /// <param name="pref">지금 화면이 든 설정. 여기 담긴 값을 함께 고쳐 화면이 방금 저장한 것을 보여 준다.</param>
    /// <param name="geo">브라우저가 준 좌표.</param>
    /// <param name="enableLocalWeather">내 위치 날씨를 함께 켤지. 권유 창에서만 참이다 — 설정 화면에서 다시 잡는 것은 스위치를 만지는 일이 아니므로 거짓이다.</param>
    public async Task<GeoSaveResult> SaveAsync(
        NotificationPreferenceDto pref, GeoResult geo, bool enableLocalWeather)
    {
        // 자리가 바뀌었나. 저장된 좌표가 없으면 바뀐 것이고, 문턱보다 적게 움직였으면 날씨와 이름을 다시 받지 않는다.
        var moved = pref is not { WeatherLat: { } lat, WeatherLon: { } lon }
                    || DistanceMeters(lat, lon, geo.Latitude, geo.Longitude) >= MoveThresholdMeters;

        try
        {
            await api.SaveMyLocationAsync(new LocationUpdateDto
            {
                WeatherLat = geo.Latitude,
                WeatherLon = geo.Longitude,

                // 오차는 이 첫 저장에만 싣는다 — 서버가 기록 한 줄을 만드는 것이 이때이고, 두 번째 저장은 같은 줄에 이름만 채운다.
                WeatherAccuracy = geo.Accuracy,

                WeatherPlace = !moved ? pref.WeatherPlace : null,

                WeatherLocalEnabled = enableLocalWeather ? true : null,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "위치를 저장하지 못했다.");
            return new GeoSaveResult(false, "위치를 저장하지 못했습니다.", NoticeTone.Warning, null, moved);
        }

        var now = DateTime.UtcNow;

        pref.WeatherLat = geo.Latitude;
        pref.WeatherLon = geo.Longitude;
        pref.WeatherSyncedAt = now;
        pref.Saved = true;

        if (enableLocalWeather)
        {
            pref.WeatherLocalEnabled = true;
        }

        if (moved)
        {
            // 옛 동네 이름이 새 좌표에 붙어 남지 않게 지운다(서버도 같은 판단을 한다).
            pref.WeatherPlace = null;
            pref.WeatherLocatedAt = now;
        }

        // 자리가 그대로면 여기서 끝이다. 이름도 날씨도 이미 아는 것과 같다.
        if (!moved && !string.IsNullOrWhiteSpace(pref.WeatherPlace))
        {
            return new GeoSaveResult(true, "위치를 확인했습니다.", NoticeTone.Info, null, false);
        }

        // 이름을 알아 온다. 여기서 실패해도 저장은 끝났다 — 알림은 좌표만 있으면 가고 이름은 발송기가 채운다.
        PointWeatherDto? point = null;

        try
        {
            point = await api.GetPointWeatherAsync(geo.Latitude, geo.Longitude);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "저장한 좌표의 날씨를 받지 못했다.");
        }

        var place = point?.Place;

        // 날씨가 안 와도 이름은 따로 묻는다 — 기상청이 답하지 않은 날 잡으면 이름 칸이 빈 채 남아 알림 제목도 빈약해진다. 이 길은 우리 표만 훑으므로 기상청과 무관하게 답한다.
        if (string.IsNullOrWhiteSpace(place))
        {
            try
            {
                var found = await api.GetPointPlaceAsync(geo.Latitude, geo.Longitude);
                place = found?.Place;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "저장한 좌표의 지역 이름을 받지 못했다.");
            }
        }

        if (!string.IsNullOrWhiteSpace(place))
        {
            pref.WeatherPlace = place;

            try
            {
                await api.SaveMyLocationAsync(new LocationUpdateDto
                {
                    WeatherLat = geo.Latitude,
                    WeatherLon = geo.Longitude,
                    WeatherPlace = place,
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "지역 이름을 저장하지 못했다.");
            }
        }

        var where = string.IsNullOrWhiteSpace(pref.WeatherPlace)
            ? "위치를 저장했습니다."
            : $"위치를 저장했습니다 — {pref.WeatherPlace}";

        return new GeoSaveResult(true, where, NoticeTone.Info, point, moved);
    }

    /// <summary>두 지점 사이의 거리(m). 하버사인. 위경도 차를 그대로 재면 경도 1도의 길이가 위도에 따라 달라 동서 움직임을 실제보다 크게 본다.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6_371_000d;

        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * earthRadius * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }
}

/// <summary>위치를 저장한 결과.</summary>
/// <param name="Ok">저장됐나.</param>
/// <param name="Message">사람에게 보일 말. 부르는 쪽이 토스트로 옮긴다(조용한 확인은 옮기지 않는다).</param>
/// <param name="Tone">그 말의 결.</param>
/// <param name="Point">알아 온 날씨. 자리가 안 바뀌었으면 null — 부를 까닭이 없어 안 불렀다.</param>
/// <param name="Moved">자리가 바뀌었나(GeoLocator.MoveThresholdMeters 이상).</param>
public sealed record GeoSaveResult(
    bool Ok, string Message, NoticeTone Tone, PointWeatherDto? Point, bool Moved);
