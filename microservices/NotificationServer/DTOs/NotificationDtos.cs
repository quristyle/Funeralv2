namespace NotificationServer.DTOs;

/// <summary>구독 등록 요청. 브라우저의 <c>PushSubscription</c> 을 그대로 옮긴 모양이다.</summary>
public class SubscribeDto
{
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>브라우저가 <c>keys</c> 안에 담는 값. 화면이 펼쳐서 보낸다.</summary>
    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    /// <summary>주인 종류. 비우면 게이트웨이 헤더로 jsini 를 정한다.</summary>
    public string? OwnerType { get; set; }

    /// <summary>주인 식별자. 비우면 로그인 계정 아이디.</summary>
    public string? OwnerKey { get; set; }

    /// <summary>구독한 시스템(참고용).</summary>
    public string? Source { get; set; }

    public DeviceMetadataDto Metadata { get; set; } = new();
}

public class DeviceMetadataDto
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

    /// <summary>아이콘에 쓸 사람의 포털 아이디(받는 사람이 아니라 시킨 사람). Icon 을 채웠으면 무시.</summary>
    public string? IconOwnerKey { get; set; }

    /// <summary>같은 태그끼리 브라우저가 하나로 합친다(같은 건의 갱신용).</summary>
    public string? Tag { get; set; }

    /// <summary>RFC 8030 Topic. base64url 32자 이내, 맞지 않으면 PushSender 가 해시로 접는다.</summary>
    public string? Topic { get; set; }

    /// <summary>수명(초). 비우면 Push:DefaultTtlSeconds.</summary>
    public int? TtlSeconds { get; set; }

    /// <summary>알림구분(NOTI_CATEGORY). 기록·이력 거르기에만 쓰며 비워도 보낸다.</summary>
    public string? Category { get; set; }

    /// <summary>알림 이벤트 코드(NotificationEvents). 정책(역할·채널)의 열쇠. 비우면 Category. 표에 없으면 정책 없이 나간다.</summary>
    public string? EventCode { get; set; }

    /// <summary>화면이 쓰는 부가 값.</summary>
    public Dictionary<string, string>? Data { get; set; }
}

/// <summary>푸시 발송 요청. 대상은 부르는 쪽이 정하고, Roles 만 이 서비스가 역할표로 푼다.</summary>
public class SendPushDto
{
    public List<OwnerRefDto> Owners { get; set; } = new();

    /// <summary>받는 역할. 그 역할 계정 전원을 Owners 에 더한다(중복은 한 번).</summary>
    public List<string>? Roles { get; set; }

    /// <summary>이 주인 키는 뺀다(내가 한 일을 나에게 알리지 않음).</summary>
    public List<string>? ExcludeOwnerKeys { get; set; }

    public PushMessageDto Message { get; set; } = new();
}

/// <summary>발송 결과.</summary>
public class SendPushResultDto
{
    /// <summary>보낸 구독 수(기기 수, 사람 수 아님).</summary>
    public int Sent { get; set; }

    /// <summary>실패한 구독 수.</summary>
    public int Failed { get; set; }

    /// <summary>죽어서 지운 구독 수 (푸시 서비스가 404/410 을 준 것).</summary>
    public int Removed { get; set; }

    /// <summary>구독이 하나도 없던 대상 주인 수.</summary>
    public int OwnersWithoutSubscription { get; set; }

    /// <summary>본인 설정으로 제외한 주인 수.</summary>
    public int OptedOut { get; set; }

    /// <summary>알림관리 정책으로 제외한 주인 수(OptedOut 과 따로 센다).</summary>
    public int PolicyExcluded { get; set; }

    /// <summary>메일로 받은 주소 수. 기기 단위인 Sent 와 더하지 않는다.</summary>
    public int Mailed { get; set; }

    public string? Message { get; set; }
}

/// <summary>이메일 발송 요청.</summary>
public class SendEmailDto
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>받는 역할의 대표 메일. To 와 합쳐지며 겹치면 한 번. 직발송 /emails/send 만 본다.</summary>
    public string? ToRole { get; set; }

    /// <summary>포털 로그인 아이디(scom.accounts.user_id). 아이디를 To 에 실으면 NO_RECIPIENT. 직발송만 본다.</summary>
    public string? ToUser { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문 (HTML 허용).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>참이면 HTML 로 보낸다(직발송만). 거짓이면 평문을 회사 메일 틀에 입힌다.</summary>
    public bool Html { get; set; }

    /// <summary>보낸 사람 이름(평문 메일의 서명 줄). 보내는 계정이 하나라 부르는 쪽이 적는다.</summary>
    public string? SenderName { get; set; }

    /// <summary>알림구분. 받는 사람이 그 갈래를 껐는지 본다(현재 HELPDESK_COMMENT). To 에 직접 적은 주소는 거르지 않는다.</summary>
    public string? Category { get; set; }

    /// <summary>알림 이벤트 코드. 비우면 Category, 그것도 없으면 정책 없이 나간다. REPORT_MAIL·SITE_INQUIRY 는 제 이름을 적는다.</summary>
    public string? EventCode { get; set; }

    /// <summary>붙일 파일. 직발송(/emails/send)만 본다.</summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];
}

/// <summary>메일 첨부 한 개. 바이트를 싣는다(파일 서버 아이디로 넘기면 보낸 뒤 지울 곳이 없다). base64 라 약 1.33배가 오간다.</summary>
public class EmailAttachmentDto
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>비어 있으면 <c>application/octet-stream</c> 으로 붙인다.</summary>
    public string? ContentType { get; set; }

    /// <summary>파일 내용(base64).</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>이메일 발송 결과. 큐에 넣은 것까지가 이 서비스 일이다(실제 발송은 배포 장비 스크립트, 그래서 Queued).</summary>
public class SendEmailResultDto
{
    public bool Queued { get; set; }

    /// <summary>떨어뜨린 JSON 파일 경로(문제 추적용).</summary>
    public string? SpoolFile { get; set; }

    public string? Message { get; set; }
}

/// <summary>화면이 구독을 만들 때 필요한 값.</summary>
public class VapidPublicKeyDto
{
    /// <summary>공개 키(비밀 아님).</summary>
    public string? PublicKey { get; set; }

    /// <summary>거짓이면 화면이 구독 버튼을 숨긴다.</summary>
    public bool Enabled { get; set; }
}

/// <summary>내 알림 설정(스위치 넷).</summary>
public class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool WeatherEnabled { get; set; }

    /// <summary>쪽지를 메일로도 받을지. 기본 꺼짐.</summary>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 앱 푸시로 받는가. 기본은 켜짐이다.</summary>
    public bool CommentPushEnabled { get; set; } = true;

    /// <summary>내 글에 달린 댓글을 이메일로 받는가. 기본은 켜짐이다.</summary>
    public bool CommentEmailEnabled { get; set; } = true;

    /// <summary>내 위치 날씨. 기상 특보와 달리 사건 없이 시각마다 온다.</summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>저장된 위경도. 없으면 <see cref="WeatherLocalEnabled"/> 를 켜도 보낼 곳이 없다.</summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    /// <summary>표시용 지역 이름.</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c>.</summary>
    public string? WeatherHours { get; set; }

    /// <summary>위치를 마지막으로 잡은 때(좌표가 실제로 바뀐 때).</summary>
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>위치를 마지막으로 확인한 때. 좌표가 같아도 재수집마다 찍힌다.</summary>
    public DateTime? WeatherSyncedAt { get; set; }

    /// <summary>저장한 적이 있나. 거짓이면 값은 기본값이고 표에 행이 없다.</summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>알림 설정 변경 요청. 비운 항목은 건드리지 않도록 전부 nullable.</summary>
public class UpdateNotificationPreferenceDto
{
    public bool? PushEnabled { get; set; }
    public bool? EmailEnabled { get; set; }
    public bool? WeatherEnabled { get; set; }
    public bool? NoteEmailEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 앱 푸시로 받을지.</summary>
    public bool? CommentPushEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 이메일로 받을지.</summary>
    public bool? CommentEmailEnabled { get; set; }

    public bool? WeatherLocalEnabled { get; set; }

    /// <summary>새 위치. 위도·경도는 함께 줘야 바뀐다(한쪽만 바꾸면 엉뚱한 곳의 날씨).</summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    public string? WeatherPlace { get; set; }

    public string? WeatherHours { get; set; }

    /// <summary>방금 잰 위치를 보낸 저장인가. 토글 저장에 좌표가 실려도 거짓 — 재어 보낸 GeoLocator 만 참.</summary>
    public bool? WeatherLocated { get; set; }

    /// <summary>오차 반지름(m). 설정이 아니라 scom.location_tracks 한 줄에만 간다.</summary>
    public double? WeatherAccuracy { get; set; }
}

/// <summary>「내 위치 날씨」를 켠 사람 하나. 생활과환경(LifeEnvServer)이 가져가 만든 문장을 POST /weather-local 로 되돌린다(방향을 뒤집으면 알림 서비스가 기상청을 알게 된다).</summary>
public class LocalWeatherSubscriberDto
{
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;

    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>표시용 지역 이름. 비면 저쪽이 격자표에서 채운다.</summary>
    public string? Place { get; set; }

    /// <summary>받을 시각(KST). 비면 저쪽 기본값 <c>7,18</c>.</summary>
    public string? Hours { get; set; }

    /// <summary>마지막 발송 때(UTC). 같은 시각 칸을 두 번 울리지 않게 함께 보낸다.</summary>
    public DateTime? LastSentAt { get; set; }
}

/// <summary>「내 위치 날씨」 한 통. 본문은 저쪽이 만든다(기상청 코드값을 여기서 알지 않으려고).</summary>
public class SendLocalWeatherDto
{
    public string OwnerType { get; set; } = "jsini";
    public string OwnerKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>날씨 상태 아이콘 분류.</summary>
    public string? WeatherKind { get; set; }

    /// <summary>찾아낸 지역 이름. 주면 설정에 적는다(처음 잡을 때는 이름을 모른 채 저장될 수 있다).</summary>
    public string? Place { get; set; }
}

/// <summary>내 기기(구독) 한 대.</summary>
public class PushDeviceDto
{
    /// <summary>푸시 서비스 주소. 화면이 이것으로 지금 브라우저를 알아본다.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string? Source { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>기기가 받았다고 되알려 준 때(UTC). LastSentAt 과 갈라지면 그 기기는 못 받고 있다.</summary>
    public DateTime? LastDeliveredAt { get; set; }

    /// <summary>최근 발송 가운데 닿은 수.</summary>
    public int RecentDeliveredCount { get; set; }

    /// <summary>최근 기간 이 기기로 <b>보낸</b> 성공 발송 수.</summary>
    public int RecentSentCount { get; set; }

    /// <summary>연속 실패 횟수. 0 이 아니면 못 받고 있을 수 있다.</summary>
    public int FailureCount { get; set; }
    public DeviceMetadataDto Metadata { get; set; } = new();
}

/// <summary>사람 한 명의 알림 상태(관리 화면용). Saved 가 거짓이면 스위치는 기본값.</summary>
public class OwnerNotificationStateDto
{
    /// <summary>주인 식별자. 포털 계정이면 로그인 아이디.</summary>
    public string OwnerKey { get; set; } = string.Empty;

    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool WeatherEnabled { get; set; }

    /// <summary>쪽지를 메일로도 받는가. 기본 꺼짐.</summary>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 앱 푸시로 받는가. 기본은 켜짐이다.</summary>
    public bool CommentPushEnabled { get; set; } = true;

    /// <summary>내 글에 달린 댓글을 이메일로 받는가. 기본은 켜짐이다.</summary>
    public bool CommentEmailEnabled { get; set; } = true;

    /// <summary>저장한 적이 있나. 거짓이면 스위치는 기본값.</summary>
    public bool Saved { get; set; }

    /// <summary>구독 기기 수. 0 이면 푸시를 켜도 갈 곳이 없다.</summary>
    public int DeviceCount { get; set; }

    /// <summary>기기 중 가장 최근 발송 성공 시각.</summary>
    public DateTime? LastSentAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>알림 설정 화면이 한 번에 받는 상태(API 넷을 따로 부르면 스위치가 깜빡인다).</summary>
public class MyNotificationStateDto
{
    /// <summary>이 설정의 주인.</summary>
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;

    public NotificationPreferenceDto Preference { get; set; } = new();

    /// <summary>서버가 푸시를 보낼 수 있는 상태(VAPID 설정 여부).</summary>
    public bool PushAvailable { get; set; }

    /// <summary>브라우저가 구독을 만들 때 쓰는 공개 키.</summary>
    public string? VapidPublicKey { get; set; }

    public List<PushDeviceDto> Devices { get; set; } = new();
}

/// <summary>발송 이력 한 줄. 엔티티를 그대로 내보내지 않는다(endpoint 가 들어 있다).</summary>
public class PushLogRowDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }

    /// <summary>받는 이(포털이면 로그인 아이디).</summary>
    public string? TargetUser { get; set; }

    public string? OwnerType { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>알림구분 코드값(<c>NOTI_CATEGORY</c>). 옛 줄과 구분 없이 보낸 것은 비어 있다.</summary>
    public string? Category { get; set; }

    /// <summary>보낸 사람. 시스템이 보낸 것은 비어 있다.</summary>
    public string? SentBy { get; set; }

    /// <summary>기기가 실제로 띄운 때(UTC). Success 는 푸시 서비스 접수까지다.</summary>
    public DateTime? DeliveredAt { get; set; }
}

/// <summary>서비스워커의 「닿았다」 보고. 익명이라 토큰이 없고 묶음 아이디와 구독 주소로 찾는다.</summary>
public class PushDeliveredDto
{
    /// <summary>묶음 아이디(페이로드 nid, push_send_logs.batch_id).</summary>
    public string? Nid { get; set; }

    /// <summary>받은 기기의 구독 주소. 없으면 아무것도 찍지 않는다(묶음만 찍으면 한 대만 받아도 넷 다 닿은 것이 된다).</summary>
    public string? Endpoint { get; set; }
}

/// <summary>알림함 한 줄. 발송 한 번이 한 줄(기기 수와 무관).</summary>
public class NotificationRowDto
{
    /// <summary>묶음 열쇠. 옛 줄은 줄 아이디.</summary>
    public string Id { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }

    /// <summary>알림구분 코드값(NOTI_CATEGORY). 화면이 공통코드로 이름을 읽는다.</summary>
    public string? Category { get; set; }

    /// <summary>기기 한 대에라도 갔는가.</summary>
    public bool Delivered { get; set; }

    /// <summary>한 대도 못 갔을 때의 까닭. 갔으면 <c>null</c>.</summary>
    public string? FailureReason { get; set; }

    /// <summary>앱 알림 아이콘 주소(대개 시킨 사람의 프로필 사진). 칸을 만들기 전 줄은 null.</summary>
    public string? Icon { get; set; }
}

/// <summary>알림함에서 고른 여럿에 거는 요청(읽음 · 치우기). 한 번의 저장으로 묶어 중간 실패 때 부분 처리를 막는다.</summary>
public class InboxBulkDto
{
    /// <summary>묶음 열쇠들(<c>batch_id</c> 또는 옛 줄의 아이디).</summary>
    public List<string> Ids { get; set; } = [];
}
