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

    public string? Endpoint { get; set; }
    public string? P256dh { get; set; }
    public string? Auth { get; set; }

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
    /// <summary>실제로 보낸 기기 수.</summary>
    public int Sent { get; set; }

    /// <summary>보내려다 실패한 기기 수.</summary>
    public int Failed { get; set; }

    /// <summary>보낸 것이 없을 때 그 까닭. 화면이 그대로 옮긴다.</summary>
    public string? Message { get; set; }
}
