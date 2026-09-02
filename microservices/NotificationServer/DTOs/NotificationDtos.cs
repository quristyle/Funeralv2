namespace NotificationServer.DTOs;

/// <summary>구독 등록 요청. 브라우저의 <c>PushSubscription</c> 을 그대로 옮긴 모양이다.</summary>
public class SubscribeDto
{
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>브라우저는 이 둘을 <c>keys</c> 안에 담아 준다. 화면이 펼쳐서 보낸다.</summary>
    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// 주인의 종류. 비우면 서버가 게이트웨이 헤더를 보고 <c>jsini</c> 로 정한다.
    /// </summary>
    public string? OwnerType { get; set; }

    /// <summary>주인 식별자. 비우면 로그인한 계정 아이디로 정한다.</summary>
    public string? OwnerKey { get; set; }

    /// <summary>어느 시스템에서 구독했나 (참고용).</summary>
    public string? Source { get; set; }
}

/// <summary>주인 한 명을 가리키는 값.</summary>
public class OwnerRefDto
{
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;
}

/// <summary>보낼 알림 내용.</summary>
public class PushMessageDto
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>클릭했을 때 열 주소.</summary>
    public string? Url { get; set; }

    /// <summary>아이콘 주소.</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 같은 태그의 알림은 브라우저가 하나로 합친다. 같은 건에 대한 갱신을 보낼 때 쓴다.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>화면이 알아서 쓰는 부가 값.</summary>
    public Dictionary<string, string>? Data { get; set; }
}

/// <summary>
/// 푸시 발송 요청.
/// </summary>
/// <remarks>
/// <b>누구에게 보낼지는 부르는 쪽이 정한다.</b> 이 서비스는 팀도 회사도 모른다 —
/// 헬프데스크가 자기 DB 에서 대상을 고른 뒤 그 주인 키 목록을 넘긴다.
/// </remarks>
public class SendPushDto
{
    public List<OwnerRefDto> Owners { get; set; } = new();
    public PushMessageDto Message { get; set; } = new();
}

/// <summary>발송 결과.</summary>
public class SendPushResultDto
{
    /// <summary>보낸 구독 수 (사람 수가 아니라 기기 수다).</summary>
    public int Sent { get; set; }

    /// <summary>실패한 구독 수.</summary>
    public int Failed { get; set; }

    /// <summary>죽어서 지운 구독 수 (푸시 서비스가 404/410 을 준 것).</summary>
    public int Removed { get; set; }

    /// <summary>대상 주인 중 구독이 하나도 없던 수. "왜 안 왔나" 를 설명해 준다.</summary>
    public int OwnersWithoutSubscription { get; set; }

    /// <summary>본인이 푸시를 꺼 두어 제외한 주인 수. 이것도 "왜 안 왔나" 의 답이다.</summary>
    public int OptedOut { get; set; }

    public string? Message { get; set; }
}

/// <summary>이메일 발송 요청.</summary>
public class SendEmailDto
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다 (기존 스크립트 규약이 그렇다).</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 받는 역할 (예: <c>SYSTEM_ADMINISTRATOR</c>). 지정하면 그 역할 사용자들의
    /// 대표 이메일(scom)로 보낸다. <see cref="To"/> 와 함께 주면 합쳐진다.
    /// 직발송 <c>/emails/send</c> 만 본다.
    /// </summary>
    public string? ToRole { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문 (HTML 허용).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 참이면 본문을 HTML 로 보낸다 (직발송 <c>/emails/send</c> 만 본다 —
    /// 큐 방식은 배포 장비 스크립트가 형식을 정한다).
    /// </summary>
    public bool Html { get; set; }
}

/// <summary>이메일 발송 결과.</summary>
/// <remarks>
/// <b>큐에 넣은 것까지가 이 서비스의 일이다.</b> 실제 발송은 배포 장비의 스크립트가 한다
/// (SMTP 설정이 이 시스템에 없다). 그래서 "보냈다" 가 아니라 "넣었다" 로 말한다.
/// </remarks>
public class SendEmailResultDto
{
    public bool Queued { get; set; }

    /// <summary>떨어뜨린 JSON 파일 경로. 문제를 쫓을 때 쓴다.</summary>
    public string? SpoolFile { get; set; }

    public string? Message { get; set; }
}

/// <summary>화면이 구독을 만들 때 필요한 값.</summary>
public class VapidPublicKeyDto
{
    /// <summary>공개 키. 비밀이 아니다.</summary>
    public string? PublicKey { get; set; }

    /// <summary>푸시를 쓸 수 있는 상태인가. 거짓이면 화면이 구독 버튼을 숨기면 된다.</summary>
    public bool Enabled { get; set; }
}

/// <summary>내 알림 설정 (스위치 셋).</summary>
public class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool WeatherEnabled { get; set; }

    /// <summary>
    /// 저장한 적이 있나. 거짓이면 아래 값은 <b>기본값</b>이고 표에는 행이 없다.
    /// 화면이 "아직 설정하지 않았습니다" 를 말할 수 있게 내려 준다.
    /// </summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 알림 설정 변경 요청. <b>비운 항목은 건드리지 않는다</b> — 스위치 하나만 눌러도
/// 나머지를 덮어쓰지 않도록 세 값을 모두 nullable 로 둔다.
/// </summary>
public class UpdateNotificationPreferenceDto
{
    public bool? PushEnabled { get; set; }
    public bool? EmailEnabled { get; set; }
    public bool? WeatherEnabled { get; set; }
}

/// <summary>내 기기(구독) 한 대.</summary>
public class PushDeviceDto
{
    /// <summary>푸시 서비스 주소. 화면은 이것으로 "지금 이 브라우저" 를 알아본다.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string? Source { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>연달아 실패한 횟수. 0 이 아니면 그 기기는 못 받고 있을 수 있다.</summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// 알림 설정 화면이 한 번에 받는 상태.
/// </summary>
/// <remarks>
/// 화면 하나가 API 넷(공개키 · 설정 · 기기목록 · 계정)을 따로 부르면 순서에 따라
/// 스위치가 깜빡인다. 한 번에 내려 준다.
/// </remarks>
public class MyNotificationStateDto
{
    /// <summary>이 설정의 주인 (표시·확인용).</summary>
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;

    public NotificationPreferenceDto Preference { get; set; } = new();

    /// <summary>서버가 푸시를 보낼 수 있는 상태인가 (VAPID 설정 여부).</summary>
    public bool PushAvailable { get; set; }

    /// <summary>브라우저가 구독을 만들 때 쓰는 공개 키.</summary>
    public string? VapidPublicKey { get; set; }

    public List<PushDeviceDto> Devices { get; set; } = new();
}
