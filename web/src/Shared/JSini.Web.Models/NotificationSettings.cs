namespace JSini.Web.Models;

// ============================================================
// 알림 설정과 웹푸시 구독.
//
// **모듈이 아니라 여기 있다.** 이 설정을 다루는 화면이 둘이다 —
// 포털관리의 「알림 설정」과 장례식장의 「환경설정」. 둘 다 **로그인한 본인의
// 설정**을 다루므로 자료 모양이 같아야 하고, 한쪽 모듈에 두면 다른 쪽이
// 쓸 수 없다(업무 모듈끼리는 참조하지 않는다).
//
// 화면 자체도 한 벌이다 — `JSini.Web.Components/Settings/NotificationPanel`.
//
// **칸 이름은 NotificationServer 의 DTO 와 글자까지 맞춘다.** 한동안 기기
// 칸이 `Id`·`LastUsedAt` 이었는데 서버가 보내는 것은 `endpoint`·`lastSentAt`
// 이라, 「최근 사용」 칸이 **늘 비어 있었고** 기기를 뺄 열쇠(`endpoint`)가
// 아예 없었다. 어긋나도 예외가 나지 않고 값만 조용히 사라진다.
// ============================================================

/// <summary>내 알림 수신 설정.</summary>
public sealed class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; }
    public bool EmailEnabled { get; set; }

    /// <summary>기상 특보 알림을 받는가. 생활과환경이 이 값을 본다.</summary>
    public bool WeatherEnabled { get; set; }

    /// <summary>
    /// <b>쪽지를 메일로도 받는가.</b> 기본은 꺼짐이다.
    /// </summary>
    /// <remarks>
    /// <see cref="EmailEnabled"/> 와 갈래가 다르다. 그쪽은 업무 알림(역할로 오는
    /// 메일)이고 이쪽은 <b>사람이 나에게 쓴 글</b>이다. 쪽지는 이미 쪽지함에 남고
    /// 앱 푸시로 두드리므로, 메일까지 받을지는 <b>받는 사람이 정한다</b> —
    /// 보내는 화면에는 그 체크가 없다.
    /// </remarks>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>
    /// <b>내 위치 날씨</b>를 받는가. 위 <see cref="WeatherEnabled"/>(기상 특보)와
    /// 다른 스위치다 — 그쪽은 특보가 떴을 때만, 이쪽은 <b>고른 시각마다</b> 온다.
    /// </summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>
    /// 저장해 둔 좌표. <b>없으면 스위치를 켜도 보낼 곳이 없다</b> — 웹은 뒤에서
    /// 위치를 못 읽으므로 화면이 한 번 받아 저장해 둬야 한다.
    /// </summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    /// <summary>보여 줄 지역 이름(예: <c>울산광역시 남구 삼산동</c>).</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각들(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c> 이다.</summary>
    public string? WeatherHours { get; set; }

    /// <summary>
    /// 위치를 마지막으로 <b>잡은</b> 때 — <b>좌표가 실제로 달라진 때</b>다.
    /// 자리가 안 바뀐 사람은 이 값이 오래 그대로다.
    /// </summary>
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>
    /// 위치를 마지막으로 <b>확인한</b> 때. 브라우저가 저절로 다시 재어 보고
    /// <b>그대로였을 때도</b> 찍힌다(<see cref="JSini.Web.Models.GeoPermissionResult"/> 가
    /// <c>granted</c> 인 브라우저에서만 돈다).
    /// </summary>
    /// <remarks>
    /// 화면이 둘을 갈라 보여 준다. 위의 「잡은 때」만 있으면 <b>한자리에 사는
    /// 사람</b>의 위치가 늘 묵어 보이고, 이것만 있으면 <b>이사한 사람</b>이
    /// 옛 동네 날씨를 받는 것을 눈치채지 못한다.
    /// </remarks>
    public DateTime? WeatherSyncedAt { get; set; }

    /// <summary>
    /// 저장된 설정인가. 거짓이면 서버가 준 <b>기본값</b>이라는 뜻이다.
    ///
    /// 화면이 이것을 구별해야 「아직 정한 적 없음」과 「전부 꺼 둠」이
    /// 같아 보이지 않는다.
    /// </summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 내 알림 설정 응답 전체.
///
/// 설정만 오는 것이 아니라 <b>푸시를 쓸 수 있는 환경인지</b>와 등록된 기기까지
/// 함께 온다. 셋이 한 화면에서 같이 쓰이므로 통째로 받는다.
/// </summary>
public sealed class NotificationSettingsDto
{
    /// <summary>설정 주인의 종류(<c>jsini</c> · <c>helpdesk</c>).</summary>
    public string? OwnerType { get; set; }

    public string? OwnerKey { get; set; }

    public NotificationPreferenceDto Preference { get; set; } = new();

    /// <summary>
    /// 서버가 푸시를 보낼 수 있는 상태인가(VAPID 키가 설정돼 있는가).
    /// 거짓이면 스위치를 켜도 아무 일이 일어나지 않는다.
    /// </summary>
    public bool PushAvailable { get; set; }

    /// <summary>브라우저가 구독을 만들 때 쓰는 공개 키.</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>이 계정으로 등록된 기기들.</summary>
    public List<PushDeviceDto> Devices { get; set; } = [];
}

/// <summary>
/// 브라우저가 만든 구독을 서버에 올릴 때 보내는 것.
///
/// 이름은 NotificationServer 의 <c>SubscribeDto</c> 와 맞춘 것이다.
/// <c>ownerType</c>·<c>ownerKey</c> 는 **일부러 없다** — 서버가 로그인한
/// 계정으로 정한다(AdminClient.RegisterPushSubscriptionAsync 참조).
/// </summary>
public sealed class PushSubscribeRequest
{
    /// <summary>푸시 서비스가 준 이 기기의 주소. 구독의 열쇠다.</summary>
    public string? Endpoint { get; set; }

    /// <summary>본문을 암호화하는 공개 키 (base64url).</summary>
    public string? P256dh { get; set; }

    /// <summary>인증 비밀 (base64url).</summary>
    public string? Auth { get; set; }

    /// <summary>어디서 구독했는지. 기기 목록에서 갈래를 구분하는 데 쓴다.</summary>
    public string? Source { get; set; } = "portal";

    public PushDeviceMetadataDto Metadata { get; set; } = new();
}

/// <summary>
/// <c>jsiniPwa.subscribe</c> / <c>jsiniPwa.status</c> 가 돌려주는 것.
///
/// 브라우저 쪽 사정을 그대로 담는다 — 실패를 <c>false</c> 하나로 뭉개면
/// 화면이 "안 됐습니다" 말고는 할 말이 없다. iOS 사파리처럼 **홈 화면에
/// 추가해야 비로소 되는** 경우를 구분해 말해 주려면 이유가 필요하다.
/// </summary>
public sealed class PushBrowserResult
{
    public bool Ok { get; set; }

    /// <summary>이 브라우저가 웹푸시를 지원하는가.</summary>
    public bool Supported { get; set; }

    /// <summary><c>granted</c> · <c>denied</c> · <c>default</c> · <c>unsupported</c>.</summary>
    public string? Permission { get; set; }

    /// <summary>이미 구독 중인가.</summary>
    public bool Subscribed { get; set; }

    /// <summary>
    /// <b>설치된 앱으로 열려 있는가</b>(홈 화면·작업 표시줄의 아이콘으로 연 것).
    ///
    /// <para>
    /// 브라우저 탭으로 열어도 데스크톱에서는 알림이 오지만, <b>iOS 는 홈 화면
    /// 앱에서만 준다</b> — 거기서는 이 값이 거짓인 동안 구독 자체가 불가능하다.
    /// 화면이 「왜 안 되는지」를 말하려면 이 구분이 필요하다.
    /// </para>
    /// </summary>
    public bool Standalone { get; set; }

    /// <summary>
    /// 서비스워커가 이 브라우저에 자리 잡았는가. 없으면 구독을 만들 수 없다 —
    /// 알림을 실제로 받아 주는 것이 그 워커다.
    /// </summary>
    public bool ServiceWorker { get; set; }

    /// <summary>
    /// 브라우저가 <b>설치할 수 있다고 알려 왔는가.</b>
    ///
    /// <para>
    /// <b>거짓이라고 설치가 안 되는 것은 아니다</b> — 이미 설치했거나, 그
    /// 신호를 주지 않는 브라우저(파이어폭스·사파리)일 수 있다. 그래서 화면은
    /// 참일 때만 단추를 두고 거짓일 때는 아무 말도 하지 않는다.
    /// </para>
    /// </summary>
    public bool Installable { get; set; }

    /// <summary>
    /// <b>이 탭에서 설치를 마쳤는가.</b>
    ///
    /// <para>
    /// 설치가 끝나도 보고 있던 탭은 탭 그대로라 <see cref="Standalone"/> 은
    /// 거짓으로 남고, 한 번 쓴 설치 신호는 사라져 <see cref="Installable"/> 도
    /// 거짓이 된다. 그 둘만 보면 <b>방금 설치한 사람이 「아직 안 했다」로
    /// 읽힌다</b> — 권유 창이 설치를 또 권하지 않게 하려고 있는 값이다.
    /// </para>
    /// </summary>
    public bool Installed { get; set; }

    /// <summary>
    /// 설치를 <b>어떻게</b> 해야 하는가 — <c>prompt</c>(단추 하나로 된다) ·
    /// <c>ios</c>(공유 → 홈 화면에 추가) · <c>menu</c>(길이 없다) ·
    /// <c>none</c>(이미 앱이다).
    /// </summary>
    /// <remarks>
    /// <c>menu</c> 와 <c>none</c> 에서 화면은 <b>아무 말도 하지 않는다</b> —
    /// 신호를 안 주는 브라우저와 이미 설치해 둔 브라우저가 거기서 구분되지
    /// 않아서, 말을 얹으면 설치한 사람에게 설치를 권하게 된다(<c>pwa.js</c>).
    /// </remarks>
    public string? InstallHint { get; set; }

    /// <summary>
    /// <b>휴대폰·태블릿인가.</b> 창 폭이 아니라 기기로 본다 — 좁게 줄여 놓은
    /// 데스크톱까지 걸리면 되풀이 권유가 거기서도 뜬다(<c>PushAskPopup</c>).
    /// </summary>
    public bool Mobile { get; set; }

    public string? Endpoint { get; set; }
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
    public PushDeviceMetadataDto? Metadata { get; set; }

    /// <summary>실패했을 때 사람이 읽을 이유.</summary>
    public string? Error { get; set; }
}

/// <summary>푸시를 받도록 등록된 기기 하나.</summary>
public sealed class PushDeviceDto
{
    /// <summary>
    /// 푸시 서비스가 준 이 기기의 주소. <b>구독의 열쇠다</b> — 「지금 이
    /// 브라우저」를 알아보는 것도, 기기를 목록에서 빼는 것도 이 값으로 한다.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>어디서 구독했는지(<c>portal</c> …).</summary>
    public string? Source { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>
    /// 마지막으로 <b>보낸</b> 때. 받은 때가 아니다 — 서버는 보낸 것까지만 안다.
    /// </summary>
    public DateTime? LastSentAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 연달아 실패한 횟수. 0 이 아니면 그 기기는 <b>못 받고 있을 수 있다</b> —
    /// 화면이 그것을 말해 주지 않으면 「왜 저 기기만 안 오지」가 된다.
    /// </summary>
    public int FailureCount { get; set; }

    public PushDeviceMetadataDto Metadata { get; set; } = new();
}

/// <summary>구독을 만든 브라우저가 제공하는 기기 식별 정보.</summary>
public sealed class PushDeviceMetadataDto
{
    public string? DeviceType { get; set; }
    public string? Platform { get; set; }
    public string? PlatformVersion { get; set; }
    public string? DeviceVendor { get; set; }
    public string? DeviceModel { get; set; }
    public string? Browser { get; set; }
    public string? BrowserVersion { get; set; }
    public string? BrowserEngine { get; set; }
    public bool? IsMobile { get; set; }
    public bool? IsStandalone { get; set; }
    public string? DisplayMode { get; set; }
    public int? ScreenWidth { get; set; }
    public int? ScreenHeight { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public double? DevicePixelRatio { get; set; }
    public int? ColorDepth { get; set; }
    public int? HardwareConcurrency { get; set; }
    public double? DeviceMemoryGb { get; set; }
    public int? MaxTouchPoints { get; set; }
    public string? Language { get; set; }
    public string? Languages { get; set; }
    public string? TimeZone { get; set; }
    public string? ConnectionType { get; set; }
    public string? EffectiveConnectionType { get; set; }
    public string? UserAgentDataJson { get; set; }
}

/// <summary>
/// 구독 목록 응답. <b>배열이 아니라 <c>{ items, count }</c> 객체다.</b>
/// </summary>
public sealed class PushSubscriptionListDto
{
    public List<PushDeviceDto> Items { get; set; } = [];
    public int Count { get; set; }
}

/// <summary>
/// 발송 결과. <b>보낸 것이 없어도 실패가 아니다</b> — 구독한 기기가 없거나
/// 스위치가 꺼져 있으면 서버는 「보낼 것이 없었다」로 답한다.
/// </summary>
public sealed class PushSendResultDto
{
    /// <summary>실제로 보낸 기기 수. <b>사람 수가 아니다</b> — 한 사람이 여럿일 수 있다.</summary>
    public int Sent { get; set; }

    /// <summary>보내려다 실패한 기기 수.</summary>
    public int Failed { get; set; }

    /// <summary>죽어서 지운 구독 수(푸시 서비스가 404·410 을 준 것).</summary>
    public int Removed { get; set; }

    /// <summary>
    /// 대상 중 <b>구독한 기기가 하나도 없던</b> 사람 수. 「왜 안 왔나」의 답이다.
    /// </summary>
    public int OwnersWithoutSubscription { get; set; }

    /// <summary>본인이 푸시를 꺼 두어 제외한 사람 수. 이것도 「왜 안 왔나」의 답이다.</summary>
    public int OptedOut { get; set; }

    /// <summary>보낸 것이 없을 때 그 까닭. 화면이 그대로 옮긴다.</summary>
    public string? Message { get; set; }
}


// ============================================================
// 내 위치 날씨.
//
// 브라우저에게 좌표를 묻고(GeoResult), 그 좌표의 날씨를 서버에서
// 받아 온다(PointWeatherDto). 둘 다 **설정 화면이 「여기가 맞나」를
// 되묻기 위해** 있다 — 좌표만 저장해 두면 사람은 자기가 무엇을
// 저장했는지 알 수 없고, 엉뚱한 동네 날씨가 와도 까닭을 못 찾는다.
// ============================================================

/// <summary>
/// <c>jsiniGeo.locate</c> 가 돌려주는 것.
/// </summary>
/// <remarks>
/// 실패를 <c>false</c> 하나로 뭉개지 않는다 — 권한을 거절한 것과 기기가 못 잡은
/// 것과 HTTPS 가 아니라 막힌 것은 사람이 할 일이 전혀 다르다.
/// </remarks>
public sealed class GeoResult
{
    public bool Ok { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>오차 반경(m). 브라우저가 주는 값이고 참고용이다.</summary>
    public double? Accuracy { get; set; }

    /// <summary>실패했을 때 사람이 읽을 이유.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// <b>재어 보지도 않았다.</b> 조용한 확인(<c>jsiniGeo.quiet</c>)에서만 참이
    /// 되고, 권한이 아직 <c>granted</c> 가 아니라는 뜻이다.
    /// </summary>
    /// <remarks>
    /// 실패와 가른다 — 실패는 <b>말해야 하는 것</b>이고(사람이 단추를 눌렀다)
    /// 이것은 <b>말하면 안 되는 것</b>이다(아무도 시키지 않았다).
    /// </remarks>
    public bool Skipped { get; set; }
}

/// <summary>
/// <c>jsiniGeo.permission</c> 이 돌려주는 것 — 브라우저가 위치를 내줄 사정인가.
/// </summary>
/// <remarks>
/// <see cref="State"/> 는 <c>granted</c> · <c>denied</c> · <c>prompt</c> ·
/// <c>unknown</c> · <c>unsupported</c> · <c>insecure</c> 중 하나다.
/// <b><c>unknown</c> 은 「모른다」이지 「안 된다」가 아니다</b> — Permissions API
/// 가 없는 브라우저(옛 iOS 사파리)라, 물어보면 될 수도 있다.
/// </remarks>
public sealed class GeoPermissionResult
{
    public bool Supported { get; set; }

    /// <summary>보안 컨텍스트(HTTPS · localhost)인가. 아니면 브라우저가 아예 거절한다.</summary>
    public bool Secure { get; set; }

    public string State { get; set; } = "unknown";

    /// <summary>물음창 없이 지금 잴 수 있는가.</summary>
    public bool Granted => Supported && Secure && State == "granted";

    /// <summary>물어볼 수는 있는가. 거절로 굳었으면 창을 띄워도 소용이 없다.</summary>
    public bool CanAsk => Supported && Secure && State is "prompt" or "unknown";
}

/// <summary>
/// 좌표만 따로 저장할 때 보내는 것.
/// </summary>
/// <remarks>
/// <para>
/// <b>설정 전체를 보내지 않는다.</b> 설정 화면은 스위치 하나에도 전체를 보내지만
/// (사람이 보고 있는 한 벌이다) 이쪽은 <b>뒤에서 도는 저장</b>이라, 전체를 보내면
/// 다른 탭에서 방금 바꾼 스위치를 조용히 되돌린다.
/// </para>
/// <para>
/// <see cref="WeatherLocated"/> 가 <c>true</c> 인 것이 이 모양의 요점이다 —
/// 서버는 그것을 보고 「확인한 때」를 찍는다.
/// </para>
/// </remarks>
public sealed class LocationUpdateDto
{
    public double WeatherLat { get; set; }
    public double WeatherLon { get; set; }

    /// <summary>알아낸 지역 이름. 아직 모르면 <c>null</c> 이다.</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>
    /// 「내 위치 날씨」를 함께 켤지. <b>권유 창에서 위치를 처음 잡을 때만</b>
    /// 참이다 — 설정 화면에서 다시 잡는 것은 스위치를 만지는 일이 아니다.
    /// </summary>
    public bool? WeatherLocalEnabled { get; set; }

    /// <summary>브라우저가 <b>방금 재어</b> 보낸 것이라는 표시. 늘 참이다.</summary>
    public bool WeatherLocated { get; set; } = true;
}

/// <summary>
/// 한 지점의 날씨 — 생활과환경의 <c>GET /life/weather/point</c> 응답.
/// </summary>
/// <remarks>
/// <b>칸 이름을 서버의 <c>PointWeatherDto</c> 와 글자까지 맞춘다.</b> 어긋나도
/// 예외가 나지 않고 값만 조용히 사라진다 — 기기 목록에서 이미 겪은 일이다.
/// </remarks>
public sealed class PointWeatherDto
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>기상청 격자. 서버가 위경도에서 계산한다.</summary>
    public int Nx { get; set; }
    public int Ny { get; set; }

    /// <summary>가장 가까운 행정구역 이름. 못 찾으면 비어 있다.</summary>
    public string? Place { get; set; }

    /// <summary>시·도 (예: <c>울산광역시</c>).</summary>
    public string? Region1 { get; set; }

    /// <summary>시·군·구 (예: <c>남구</c>).</summary>
    public string? Region2 { get; set; }

    /// <summary>읍·면·동 (예: <c>삼산동</c>).</summary>
    public string? Region3 { get; set; }

    public PointWeatherNowDto? Now { get; set; }

    public List<PointWeatherDayDto> Days { get; set; } = [];

    /// <summary>
    /// 알림 본문으로 나갈 글. <b>화면은 이것을 그대로 보여 준다</b> — 미리 본 것과
    /// 실제로 오는 알림의 말이 다르면 사람은 둘 중 하나를 믿지 못한다.
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// 좌표 하나가 <b>어느 동네인가</b> — 생활과환경의 <c>GET /life/weather/place</c> 응답.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PointWeatherDto"/> 와 갈라 둔 까닭은 <b>기상청을 안 부른다</b>는 것이다.
/// 설정 화면은 열릴 때마다 저장된 좌표의 시·도·동을 보여 주는데, 날씨까지 받아 오면
/// 기상청이 느린 날에 <b>이름조차 안 뜬다</b>.
/// </para>
/// <para>
/// <b>좌표만 저장된 사람이 실제로 있다.</b> 좌표는 저장됐는데 그 뒤 날씨 조회가
/// 실패하면 이름이 비는데(<c>GeoLocator.SaveAsync</c> 의 순서), 그때 화면에는
/// 숫자 두 개만 남았다. 이 길이 그것을 메운다.
/// </para>
/// </remarks>
public sealed class PointPlaceDto
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    public int Nx { get; set; }
    public int Ny { get; set; }

    /// <summary>세 단계를 공백으로 이은 이름. 못 찾으면 비어 있다.</summary>
    public string? Place { get; set; }

    /// <summary>시·도.</summary>
    public string? Region1 { get; set; }

    /// <summary>시·군·구.</summary>
    public string? Region2 { get; set; }

    /// <summary>읍·면·동.</summary>
    public string? Region3 { get; set; }
}

/// <summary>지금 실황.</summary>
public sealed class PointWeatherNowDto
{
    public double TemperatureC { get; set; }
    public double? SensibleTemp { get; set; }
    public string? Condition { get; set; }
    public int? Humidity { get; set; }
    public double? WindSpeed { get; set; }
    public double? Rainfall { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}

/// <summary>하루치 예보 요약.</summary>
public sealed class PointWeatherDayDto
{
    public string Date { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름 — <c>오늘</c> · <c>내일</c> · <c>9/26</c></summary>
    public string Label { get; set; } = string.Empty;

    public double? MinC { get; set; }
    public double? MaxC { get; set; }

    /// <summary>그 날 시간별 강수확률 중 가장 큰 값.</summary>
    public int? RainProbability { get; set; }

    public string? Condition { get; set; }
}
