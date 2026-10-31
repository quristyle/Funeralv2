namespace JSini.Web.Models;

// 알림 설정과 웹푸시 구독. 포털관리 「알림 설정」과 장례식장 「환경설정」이 같은 자료를 쓰므로 공유 모델에 둔다.
// 칸 이름은 NotificationServer 의 DTO 와 글자까지 맞춘다. 어긋나도 예외가 나지 않고 값만 조용히 사라진다.

/// <summary>내 알림 수신 설정.</summary>
public sealed class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; }
    public bool EmailEnabled { get; set; }

    /// <summary>기상 특보 알림을 받는가. 생활과환경이 이 값을 본다.</summary>
    public bool WeatherEnabled { get; set; }

    /// <summary>쪽지를 메일로도 받는가(기본 꺼짐). 쪽지는 쪽지함과 앱 푸시로 이미 닿으므로 받는 사람이 정한다.</summary>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 앱 푸시로 받는가(기본 켜짐). <see cref="PushEnabled"/> 아래의 갈래 스위치이고, 판정은 알림 서비스가 발송 직전에 한다.</summary>
    public bool CommentPushEnabled { get; set; } = true;

    /// <summary>내 글에 달린 댓글을 이메일로 받는가(기본 켜짐). 댓글은 요청글을 열어야 보이므로 쪽지 메일과 달리 켜 둔다.</summary>
    public bool CommentEmailEnabled { get; set; } = true;

    /// <summary>내 위치 날씨를 받는가. 기상 특보(<see cref="WeatherEnabled"/>)와 달리 고른 시각마다 온다.</summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>저장해 둔 좌표. 없으면 스위치를 켜도 보낼 곳이 없다(웹은 뒤에서 위치를 못 읽는다).</summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    /// <summary>보여 줄 지역 이름(예: <c>울산광역시 남구 삼산동</c>).</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각들(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c> 이다.</summary>
    public string? WeatherHours { get; set; }

    /// <summary>위치를 마지막으로 잡은 때. 좌표가 실제로 달라졌을 때만 찍힌다.</summary>
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>위치를 마지막으로 확인한 때(그대로였을 때도 찍힌다). 이사한 사람과 한자리에 사는 사람을 가르는 값이다.</summary>
    public DateTime? WeatherSyncedAt { get; set; }

    /// <summary>저장된 설정인가. 거짓이면 서버가 준 기본값이다. 「정한 적 없음」과 「전부 꺼 둠」을 가르려고 있다.</summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>내 알림 설정 응답. 설정과 함께 푸시 가능 여부·공개 키·등록 기기를 한 번에 받는다.</summary>
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

/// <summary>브라우저가 만든 구독을 서버에 올릴 때 보내는 것. 이름은 NotificationServer 의 <c>SubscribeDto</c> 와 맞추고, ownerType·ownerKey 는 서버가 로그인 계정으로 정한다.</summary>
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

/// <summary><c>jsiniPwa.subscribe</c> / <c>jsiniPwa.status</c> 가 돌려주는 것. 실패를 false 하나로 뭉개지 않고 이유를 담는다(iOS 는 홈 화면 추가가 필요하다).</summary>
public sealed class PushBrowserResult
{
    public bool Ok { get; set; }

    /// <summary>이 브라우저가 웹푸시를 지원하는가.</summary>
    public bool Supported { get; set; }

    /// <summary><c>granted</c> · <c>denied</c> · <c>default</c> · <c>unsupported</c>.</summary>
    public string? Permission { get; set; }

    /// <summary>이미 구독 중인가.</summary>
    public bool Subscribed { get; set; }

    /// <summary>설치된 앱으로 열려 있는가. iOS 는 홈 화면 앱에서만 알림을 주므로 거짓인 동안 구독 자체가 불가능하다.</summary>
    public bool Standalone { get; set; }

    /// <summary>서비스워커가 이 브라우저에 자리 잡았는가. 없으면 구독을 만들 수 없다.</summary>
    public bool ServiceWorker { get; set; }

    /// <summary>브라우저가 설치할 수 있다고 알려 왔는가. 거짓이라고 설치가 안 되는 것은 아니다(이미 설치했거나 신호를 주지 않는 브라우저). 화면은 참일 때만 단추를 둔다.</summary>
    public bool Installable { get; set; }

    /// <summary>이 탭에서 설치를 마쳤는가. 설치 뒤에도 보던 탭은 Standalone 이 거짓이라, 방금 설치한 사람이 아직 안 한 것으로 읽히지 않게 따로 찍는다.</summary>
    public bool Installed { get; set; }

    /// <summary>설치를 어떻게 해야 하는가. prompt(단추) · ios(공유 → 홈 화면에 추가) · menu·none. menu·none 에서 화면은 아무 말도 하지 않는다. 설치한 사람에게 권하면 고장이 된다.</summary>
    public string? InstallHint { get; set; }

    /// <summary>휴대폰·태블릿인가. 창 폭이 아니라 기기로 본다(좁힌 데스크톱까지 걸리면 되풀이 권유가 거기서도 뜬다).</summary>
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
    /// <summary>서비스워커가 준 기기 주소. 구독의 열쇠다. 「지금 이 브라우저」를 알아보는 것도 기기를 빼는 것도 이 값으로 한다.</summary>
    public string? Endpoint { get; set; }

    /// <summary>어디서 구독했는지(<c>portal</c> …).</summary>
    public string? Source { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>마지막으로 보낸 때. 받은 때가 아니다(서버는 보낸 것까지만 안다).</summary>
    public DateTime? LastSentAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>마지막으로 그 기기가 받았다고 되알려 온 때. 아직이면 null. LastSentAt 과 짝이 갈라지면 그 기기는 지금 못 받고 있다.</summary>
    public DateTime? LastDeliveredAt { get; set; }

    /// <summary>최근 30일에 이 기기로 <b>보낸</b> 알림 수(성공한 것만).</summary>
    public int RecentSentCount { get; set; }

    /// <summary>그중 이 기기에 <b>닿은</b> 수. 「넷 중 하나」를 말하는 짝이다.</summary>
    public int RecentDeliveredCount { get; set; }

    /// <summary>연달아 실패한 횟수. 0 이 아니면 그 기기는 못 받고 있을 수 있다.</summary>
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

/// <summary>발송 결과. 보낼 것이 없어도 실패가 아니다(구독한 기기가 없거나 스위치가 꺼져 있다).</summary>
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


// 내 위치 날씨. 좌표를 브라우저에서 받고, 그 좌표의 날씨를 서버에서 받는다. 화면이 「여기가 맞나」를 되묻는 데 쓴다.

/// <summary>
/// <c>jsiniGeo.locate</c> 가 돌려주는 것.
/// </summary>
/// <remarks>실패를 false 하나로 뭉개지 않는다. 권한 거절·기기가 못 잡음·HTTPS 아님은 사람이 할 일이 다르다.</remarks>
public sealed class GeoResult
{
    public bool Ok { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>오차 반경(m). 브라우저가 주는 값이고 참고용이다.</summary>
    public double? Accuracy { get; set; }

    /// <summary>실패했을 때 사람이 읽을 이유.</summary>
    public string? Error { get; set; }

    /// <summary>재어 보지도 않았다. 조용한 확인에서만 참이 되며 권한이 granted 가 아니라는 뜻이다. 실패와 달리 사람에게 말하면 안 되는 것이다(아무도 시키지 않았다).</summary>
    public bool Skipped { get; set; }
}

/// <summary><c>jsiniGeo.permission</c> 이 돌려주는 브라우저 사정. State 는 granted·denied·prompt·unknown·unsupported·insecure 중 하나이며, unknown 은 「안 된다」가 아니라 「모른다」다(Permissions API 가 없는 옛 iOS 사파리).</summary>
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

/// <summary>좌표만 따로 저장할 때 보내는 것. 설정 전체를 보내면 뒤에서 도는 저장이 다른 탭에서 방금 바꾼 스위치를 되돌린다. WeatherLocated 가 참일 때만 서버가 「확인한 때」를 찍는다.</summary>
public sealed class LocationUpdateDto
{
    public double WeatherLat { get; set; }
    public double WeatherLon { get; set; }

    /// <summary>알아낸 지역 이름. 아직 모르면 <c>null</c> 이다.</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>브라우저가 말한 오차 반지름(m). 설정에는 안 들어가고, 이동 경로 기록에서 믿을 수 없는 점을 가려내는 단서로 쓴다.</summary>
    public double? WeatherAccuracy { get; set; }

    /// <summary>「내 위치 날씨」를 함께 켤지. 권유 창에서 위치를 처음 잡을 때만 참이다.</summary>
    public bool? WeatherLocalEnabled { get; set; }

    /// <summary>브라우저가 <b>방금 재어</b> 보낸 것이라는 표시. 늘 참이다.</summary>
    public bool WeatherLocated { get; set; } = true;
}

/// <summary>한 지점의 날씨(생활과환경 <c>GET /life/weather/point</c>). 칸 이름은 서버 <c>PointWeatherDto</c> 와 글자까지 맞춘다. 어긋나면 예외 없이 값만 사라진다.</summary>
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

    /// <summary>주간 예보(내일부터 열흘). <c>weekly=true</c> 로 물었을 때만 온다. 설정 화면은 쓰지 않고 기상 현황판의 주간 예보만 쓴다.</summary>
    public List<PointWeekDayDto> Weekly { get; set; } = [];

    /// <summary>알림 본문으로 나갈 글. 화면은 이것을 그대로 보여 준다(미리 본 것과 실제 알림이 다르면 안 된다).</summary>
    public string? Summary { get; set; }
}

/// <summary>한 지점의 주간 예보 하루치(mid-term 과 같은 모양). 칸 이름은 서버 <c>MidTermForecastDto</c> 와 맞춘다.</summary>
public sealed class PointWeekDayDto
{
    /// <summary><c>yyyy-MM-dd</c>.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary><c>내일</c> · <c>모레</c> · <c>3일후</c>.</summary>
    public string DayDisplay { get; set; } = string.Empty;

    public int MinTemp { get; set; }
    public int MaxTemp { get; set; }

    /// <summary>오전·오후 하늘 상태. 8~10일치는 둘이 같다(기상청이 안 가른다).</summary>
    public string AmSky { get; set; } = string.Empty;
    public string PmSky { get; set; } = string.Empty;

    public int AmPop { get; set; }
    public int PmPop { get; set; }
}

/// <summary>좌표가 어느 동네인가(생활과환경 <c>GET /life/weather/place</c>). 기상청을 부르지 않으므로 날씨가 느려도 이름은 뜬다. 좌표만 저장된 사람의 이름을 메우는 길이기도 하다.</summary>
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

/// <summary>내가 받을 수 있는 알림 하나와 길마다의 본인 설정. 받을 수 있는지는 알림관리가 정한다(Allowed 가 거짓이면 그 길은 켜도 안 온다).</summary>
public sealed class MyNotificationEventDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>ROLE(역할로 오는 업무 알림) · USER(나를 짚어 오는 알림).</summary>
    public string TargetKind { get; set; } = string.Empty;

    public bool PushAllowed { get; set; }
    public bool EmailAllowed { get; set; }
    public bool PushEnabled { get; set; }
    public bool EmailEnabled { get; set; }
}
