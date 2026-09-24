using JSini.Web.Components.Layout;
using JSini.Web.Models;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 브라우저에게 위치를 묻고 <b>서버에 저장하는 절차 한 벌</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 절차를 따로 떼어 두나 — <see cref="PushEnroll"/> 와 같은 까닭]
/// </para>
///
/// <para>
/// 위치를 잡아 저장하는 자리가 셋이다 — 알림 판의 「내 위치 잡기」
/// (<see cref="NotificationPanel"/>), 로그인 뒤에 뜨는 권유 창
/// (<c>LocationAskPopup</c>), 그리고 그 창이 뒤에서 도는 <b>조용한 확인</b>.
/// 그런데 이 절차에는 <b>순서를 틀리면 조용히 어긋나는 대목이 셋</b> 있다.
/// </para>
///
/// <list type="number">
///   <item>
///     <b>좌표를 먼저 저장하고 날씨를 나중에 받는다.</b> 반대로 하면 기상청이
///     느린 날(실제로 있다) 날씨 조회와 함께 좌표까지 날아가고, 사람은 단추를
///     다시 눌러야 한다.
///   </item>
///   <item>
///     <b>지역 이름은 날씨와 함께 온다.</b> 좌표를 저장하는 시점에는 아직
///     모르므로 이름 없이 저장하고, 알게 되면 한 번 더 저장한다.
///   </item>
///   <item>
///     <b>설정 전체를 보내지 않는다.</b> 이 절차는 뒤에서도 돌기 때문에
///     (조용한 확인) 전체를 보내면 다른 탭에서 방금 바꾼 스위치를 되돌린다.
///     좌표만 싣는 길이 따로 있다(<see cref="NotificationClient.SaveMyLocationAsync"/>).
///   </item>
/// </list>
///
/// <para>
/// [한 번 잡은 뒤에는 저절로 이어진다 — 다만 탭이 열려 있을 때만]
/// </para>
///
/// <para>
/// 서비스워커에는 <c>geolocation</c> 이 없어 <b>탭이 닫힌 뒤에는 어떤 길로도</b>
/// 위치를 못 읽는다. 대신 <b>포털이 열려 있는 동안</b>에는 사람이 단추를 누르지
/// 않아도 다시 잴 수 있다 — 권한이 이미 <c>granted</c> 인 브라우저는
/// <c>getCurrentPosition</c> 을 물음창 없이 돌려주기 때문이다. 그래서 자리가
/// 바뀐 사람의 좌표는 <b>다음번에 포털을 열 때</b> 따라온다.
/// </para>
///
/// <para>
/// 그 확인을 <b>화면을 옮길 때마다 하지는 않는다</b>. 포털은 업무를 넘나들 때마다
/// 레이아웃을 새로 만들므로(<see cref="PortalBoot"/> 머리말) 그대로 두면 하루에
/// 수십 번 잰다. <see cref="SyncInterval"/> 만큼 지난 뒤에만 한 번 잰다.
/// </para>
///
/// <para>
/// [권한 요청은 사용자 클릭에서 이어져야 한다]
/// </para>
///
/// <para>
/// <see cref="LocateAsync"/> 는 <b>물음창을 띄울 수 있다.</b> 화면이 뜨자마자
/// 부르면 아무도 부르지 않은 창이 튀어나오므로 <b>단추의 <c>Click</c> 에서만</b>
/// 부른다. 뒤에서 도는 쪽은 <see cref="QuietAsync"/> 이고, 그쪽은 권한이 이미
/// 허용된 경우가 아니면 재어 보지도 않는다.
/// </para>
/// </remarks>
public sealed class GeoLocator(
    NotificationClient api,
    IJSRuntime js,
    ILogger<GeoLocator> logger)
{
    /// <summary>
    /// 이만큼은 움직여야 <b>자리가 바뀐 것</b>으로 본다(m).
    /// </summary>
    /// <remarks>
    /// 기상청 격자가 <b>5km 칸</b>이라 그 안에서의 움직임은 날씨를 바꾸지 않는다.
    /// 그런데도 300m 로 잡은 것은 <b>칸의 경계에 사는 사람</b> 때문이다 — 문턱을
    /// 5km 로 두면 옆 칸으로 넘어간 사람의 좌표가 영영 안 따라온다.
    /// <para>
    /// 반대로 문턱이 아예 없으면 GPS 가 가만히 있어도 흔드는 몇십 미터마다
    /// 「잡은 때」가 갱신되어, 그 값이 <b>이사를 가리는 단서</b> 구실을 못 한다.
    /// </para>
    /// </remarks>
    public const double MoveThresholdMeters = 300;

    /// <summary>
    /// 조용한 확인 사이의 간격. <b>이보다 자주 재지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <b>한 시간이다.</b> 처음에는 세 시간이었다 — 알림이 가는 시각이 보통
    /// 아침·저녁 둘(기본값 <c>7,18</c>)이라 그 안에 따라오기만 하면 된다고 봤다.
    /// 그런데 알림 시각은 사람이 고른다. 「세 시간마다 (6~21시)」를 고른 사람은
    /// <b>발송 간격과 확인 간격이 같아져</b>, 낮에 자리를 옮기면 그 좌표가
    /// 다음 발송에 못 대고 한 칸 늦게 따라왔다.
    /// <para>
    /// 더 짧게 두지 않는 까닭은 값이다. 이 간격마다 <b>게이트웨이 왕복 한 번과
    /// GPS 한 번</b>이 붙고, 휴대폰에서는 잴 때마다 배터리를 쓴다. 사람이 고를 수
    /// 있는 가장 촘촘한 발송 간격이 세 시간이므로 한 시간이면 어느 쪽이든
    /// <b>발송 전에 적어도 두 번</b>은 확인한다.
    /// </para>
    /// <para>
    /// 실제로 재는 것은 이보다 드물다 — 300m 을 안 움직였으면
    /// (<see cref="MoveThresholdMeters"/>) 기상청을 부르지 않고 돌아선다.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan SyncInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// 브라우저가 위치를 내줄 사정인가. <b>예외를 삼킨다</b> — 스크립트가 아직
    /// 안 실렸거나 막아 둔 경우인데, 그때 올바른 동작은 「못 잡는 것으로 본다」다.
    /// </summary>
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

    /// <summary>
    /// 지금 위치를 묻는다. <b>물음창이 뜰 수 있으므로 단추의 클릭에서만</b> 부른다.
    /// </summary>
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

    /// <summary>
    /// <b>물음창 없이</b> 지금 위치. 권한이 이미 허용된 경우에만 실제로 잰다.
    /// 아니면 <see cref="GeoResult.Skipped"/> 가 참인 채로 돌아온다.
    /// </summary>
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

    /// <summary>
    /// 잡은 좌표를 저장한다. 머리말의 순서 셋을 지킨다.
    /// </summary>
    /// <param name="pref">
    /// 지금 화면이 들고 있는 설정. <b>여기 담긴 값을 함께 고친다</b> — 부르는
    /// 쪽이 다시 읽지 않아도 화면이 방금 저장한 것을 보여 주도록.
    /// </param>
    /// <param name="geo">브라우저가 준 좌표.</param>
    /// <param name="enableLocalWeather">
    /// 「내 위치 날씨」를 함께 켤지. <b>권유 창에서만 참이다</b> — 거기서 위치를
    /// 내주는 뜻이 곧 「날씨를 받겠다」이기 때문이다. 설정 화면에서 다시 잡는
    /// 것은 스위치를 만지는 일이 아니라 거짓이다.
    /// </param>
    public async Task<GeoSaveResult> SaveAsync(
        NotificationPreferenceDto pref, GeoResult geo, bool enableLocalWeather)
    {
        // **자리가 바뀌었나.** 저장된 좌표가 아예 없으면 당연히 바뀐 것이고,
        // 문턱보다 적게 움직였으면 그대로 본다(그때는 지역 이름도 날씨도
        // 다시 받지 않는다 — 기상청을 부를 까닭이 없다).
        var moved = pref is not { WeatherLat: { } lat, WeatherLon: { } lon }
                    || DistanceMeters(lat, lon, geo.Latitude, geo.Longitude) >= MoveThresholdMeters;

        try
        {
            await api.SaveMyLocationAsync(new LocationUpdateDto
            {
                WeatherLat = geo.Latitude,
                WeatherLon = geo.Longitude,
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
            // 옛 동네 이름이 새 좌표에 붙어 남지 않게 지운다. 서버도 같은
            // 판단을 한다(`NotificationPreferenceService.SaveAsync`).
            pref.WeatherPlace = null;
            pref.WeatherLocatedAt = now;
        }

        // 자리가 그대로면 여기서 끝이다. 이름도 날씨도 이미 아는 것과 같다.
        if (!moved && !string.IsNullOrWhiteSpace(pref.WeatherPlace))
        {
            return new GeoSaveResult(true, "위치를 확인했습니다.", NoticeTone.Info, null, false);
        }

        // 이름을 알아 온다. **여기서 실패해도 저장은 이미 끝났다** — 알림은
        // 좌표만 있으면 가고, 이름은 발송기가 다음 발송에서 채워 넣는다.
        PointWeatherDto? point = null;

        try
        {
            point = await api.GetPointWeatherAsync(geo.Latitude, geo.Longitude);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "저장한 좌표의 날씨를 받지 못했다.");
        }

        if (point is { Place: { Length: > 0 } place })
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

    /// <summary>
    /// 두 지점 사이의 거리(m). 하버사인이다.
    /// </summary>
    /// <remarks>
    /// <b>위경도 차를 그대로 재면 안 된다.</b> 경도 1도의 길이는 위도에 따라
    /// 달라서(적도에서 111km, 우리 위도에서 약 89km) 남북과 동서를 같은 자로
    /// 재면 동서 방향 움직임을 실제보다 크게 본다.
    /// </remarks>
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

/// <summary>
/// 위치를 저장한 결과.
/// </summary>
/// <param name="Ok">저장됐나.</param>
/// <param name="Message">사람에게 보일 말. 부르는 쪽이 토스트로 옮긴다(조용한 확인은 옮기지 않는다).</param>
/// <param name="Tone">그 말의 결.</param>
/// <param name="Point">알아 온 날씨. <b>자리가 안 바뀌었으면 <c>null</c></b> 이다 — 부를 까닭이 없어 안 불렀다.</param>
/// <param name="Moved">자리가 바뀌었나(<see cref="GeoLocator.MoveThresholdMeters"/> 이상).</param>
public sealed record GeoSaveResult(
    bool Ok, string Message, NoticeTone Tone, PointWeatherDto? Point, bool Moved);
