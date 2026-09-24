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

    /// <summary>
    /// 아이콘에 <b>얼굴을 쓸 사람</b>의 포털 로그인 아이디
    /// (<c>scom.accounts.user_id</c>). 주면 그 사람의 프로필 사진이 아이콘이 되고,
    /// 사진이 없으면 사람 형상 그림자가 된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>받는 사람이 아니라 「시킨 사람」이다.</b> AI 작업 결과 알림은 지시한
    /// 본인에게 가므로 지금은 둘이 같지만, 남에게 보내는 알림에서도 아이콘이
    /// 답해야 하는 물음은 <b>「누가 시킨 일인가」</b> 하나다.
    /// </para>
    /// <para>
    /// <see cref="Icon"/> 을 직접 채웠으면 이 값은 보지 않는다 — 주소를 손에 든
    /// 부르는 쪽의 뜻이 먼저다.
    /// </para>
    /// </remarks>
    public string? IconOwnerKey { get; set; }

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
///
/// <para>
/// 다만 <b>역할(<see cref="Roles"/>)만은 예외</b>다. 역할표(<c>scom.role_accounts</c>)는
/// 이 서비스가 원래 접속하는 DB 에 있고 다른 서비스에는 없다 — 메일이 이미 같은
/// 까닭으로 <see cref="SendEmailDto.ToRole"/> 을 받는다.
/// </para>
/// </remarks>
public class SendPushDto
{
    public List<OwnerRefDto> Owners { get; set; } = new();

    /// <summary>
    /// 받는 <b>역할</b>(예: <c>SYSTEM_ADMINISTRATOR</c>). 그 역할인 포털 계정
    /// 전원이 <see cref="Owners"/> 에 더해진다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>주소를 모르는 부르는 쪽을 위해 있다.</b> 다른 서비스는 자기 DB 만 보므로
    /// 「관리자에게 알려라」를 사람 목록으로 풀지 못한다 — 실제로 프로젝트관리 DB
    /// (<c>projmng</c>)와 포털 계정 DB(<c>jsiniportal</c>)는 아예 다른 데이터베이스다.
    /// </para>
    /// <para>
    /// <see cref="Owners"/> 와 겹쳐도, 한 사람이 역할 여럿에 걸려도 <b>한 번만</b> 간다.
    /// </para>
    /// </remarks>
    public List<string>? Roles { get; set; }

    /// <summary>
    /// 이 주인 키는 <b>빼고</b> 보낸다. 역할로 편 목록에서 특정 사람을 덜어 낼 때 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>「내가 한 일을 나에게 알리지 않는다」가 이 칸의 쓰임이다.</b> 관리자가
    /// 스스로 올린 요청까지 자기 휴대폰을 울리면, 받는 사람은 알림을 끄게 된다.
    /// 부르는 쪽이 대상에서 미리 빼려면 역할이 누구를 뜻하는지 알아야 하는데
    /// (<see cref="Roles"/> 머리말) 그것을 아는 곳이 여기뿐이다.
    /// </remarks>
    public List<string>? ExcludeOwnerKeys { get; set; }

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

    /// <summary>
    /// 받는 <b>사람</b>의 포털 로그인 아이디 (<c>scom.accounts.user_id</c>).
    /// 여럿이면 쉼표로 잇는다. 직발송 <c>/emails/send</c> 만 본다.
    /// </summary>
    /// <remarks>
    /// <b>주소를 모르는 부르는 쪽을 위해 있다.</b> 다른 서비스는 자기 DB 만
    /// 보므로 「이 작업을 요청한 사람」까지는 알아도 그 사람의 메일 주소는
    /// 모른다. 아이디를 그대로 <see cref="To"/> 에 실으면 주소가 아니라서
    /// 걸러지고 <c>NO_RECIPIENT</c> 로 돌아온다 — 실제로 AI 작업 결과 메일이
    /// 그렇게 한 통도 못 나갔다.
    /// </remarks>
    public string? ToUser { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문 (HTML 허용).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 참이면 본문을 HTML 로 보낸다 (직발송 <c>/emails/send</c> 만 본다 —
    /// 큐 방식은 배포 장비 스크립트가 형식을 정한다).
    /// </summary>
    /// <remarks>
    /// <b>거짓이라고 글자만 나가지는 않는다.</b> 평문으로 온 본문은 서버가
    /// 회사 메일 틀에 입혀 보낸다(<c>NoticeEmailTemplate</c>) — 평문 갈래도
    /// 함께 실으므로 글자로만 읽는 클라이언트에서도 그대로 보인다.
    /// 부르는 쪽이 완성된 문서를 들고 있을 때만 참으로 준다.
    /// </remarks>
    public bool Html { get; set; }

    /// <summary>
    /// 보낸 사람으로 적을 이름. <b>평문 알림에만 쓴다</b> — 서버가 입히는 틀의
    /// 서명 줄이 된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>주소가 아니라 이름이다.</b> 보내는 계정(<c>EmailSettings:User</c>)은
    /// 하나뿐이라 받는 쪽 메일함에는 언제나 「JSini 포털」로 뜬다. 그런데
    /// 사람이 짚어 보낸 알림에서 먼저 묻는 것은 <b>「누가 보냈나」</b>다.
    /// </para>
    /// <para>
    /// 서버가 <c>X-User-Id</c> 로 풀지 않고 부르는 쪽이 적어 보낸다 — 그 헤더에는
    /// 서비스 이름(<c>AI_TASK</c> · <c>SITE_INQUIRY</c>)이 오는 길도 있어서,
    /// 사람 이름이 아닌 것이 서명 줄에 그대로 실릴 수 있다.
    /// </para>
    /// </remarks>
    public string? SenderName { get; set; }

    /// <summary>
    /// 붙일 파일들. <b>직발송(<c>/emails/send</c>)만 본다</b> — 큐 방식은
    /// 스풀 JSON 에 제목·본문·받는이 셋만 담는 규약이라 실을 자리가 없다.
    /// </summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];
}

/// <summary>메일에 붙일 파일 한 개.</summary>
/// <remarks>
/// <para>
/// <b>바이트를 그대로 싣는다.</b> 파일 서버에 올리고 아이디만 넘기는 길도
/// 있었지만 쓰지 않았다 — 메일 첨부는 보내고 나면 쓸 일이 없는데, 그 길로 가면
/// <b>아무도 지우지 않는 파일이 보낼 때마다 쌓인다.</b> 알림 서비스가 파일
/// 서비스를 알아야 하는 것도 이 서비스가 「보내는 일만 한다」는 규칙에 어긋난다.
/// </para>
///
/// <para>
/// 값을 base64 로 담으므로 <b>실제 크기의 약 1.33배</b>가 오간다. 그래서 상한을
/// 넉넉히 두지 않는다(<c>EmailEndpoints</c>) — 메일 서버가 어차피 20~25MB 에서
/// 거절하고, 그 전에 우리가 이유를 말해 주는 편이 낫다.
/// </para>
/// </remarks>
public class EmailAttachmentDto
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>비어 있으면 <c>application/octet-stream</c> 으로 붙인다.</summary>
    public string? ContentType { get; set; }

    /// <summary>파일 내용(base64).</summary>
    public string Content { get; set; } = string.Empty;
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

/// <summary>내 알림 설정 (스위치 넷).</summary>
public class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool WeatherEnabled { get; set; }

    /// <summary>
    /// 쪽지를 메일로도 받을지. <b>기본은 꺼짐이다</b>
    /// (<c>Entities/NotificationPreference.NoteEmailEnabled</c> 머리말).
    /// </summary>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>
    /// <b>내 위치 날씨</b>를 받는가. 위 <see cref="WeatherEnabled"/>(기상 특보)와
    /// 다른 스위치다 — 그쪽은 사건이 있을 때만, 이쪽은 시각마다 온다.
    /// </summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>저장된 위경도. 없으면 <see cref="WeatherLocalEnabled"/> 를 켜도 보낼 곳이 없다.</summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    /// <summary>보여 줄 지역 이름. 표시 전용이다.</summary>
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각들(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c> 이다.</summary>
    public string? WeatherHours { get; set; }

    /// <summary>위치를 마지막으로 잡은 때.</summary>
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>
    /// 저장한 적이 있나. 거짓이면 아래 값은 <b>기본값</b>이고 표에는 행이 없다.
    /// 화면이 "아직 설정하지 않았습니다" 를 말할 수 있게 내려 준다.
    /// </summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 알림 설정 변경 요청. <b>비운 항목은 건드리지 않는다</b> — 스위치 하나만 눌러도
/// 나머지를 덮어쓰지 않도록 모든 값을 nullable 로 둔다.
/// </summary>
public class UpdateNotificationPreferenceDto
{
    public bool? PushEnabled { get; set; }
    public bool? EmailEnabled { get; set; }
    public bool? WeatherEnabled { get; set; }
    public bool? NoteEmailEnabled { get; set; }
    public bool? WeatherLocalEnabled { get; set; }

    /// <summary>
    /// 새로 잡은 위치. <b>둘을 함께 줘야 바뀐다</b> — 위도만 바꾸면 경도와
    /// 짝이 맞지 않는 지점이 되어 엉뚱한 곳의 날씨가 간다.
    /// </summary>
    public double? WeatherLat { get; set; }

    public double? WeatherLon { get; set; }

    public string? WeatherPlace { get; set; }

    public string? WeatherHours { get; set; }
}

/// <summary>
/// 「내 위치 날씨」를 켠 사람 하나 — <b>생활과환경(LifeEnvServer)이 가져간다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 보내는 쪽이 아니라 받아 가는 모양인가.</b> 날씨를 아는 것은 저쪽이다
/// (기상청 인증키도 격자 변환도 예보 해석도 전부 거기 있다). 이쪽은 누가 켰는지만
/// 안다. 그래서 저쪽이 시각마다 이 목록을 받아 가 자기 일을 하고, 만든 문장을
/// <c>POST /weather-local</c> 로 되돌려 준다 — 기존 기상 이벤트와 같은 방향이다
/// (LifeEnv → Notification). 방향을 뒤집으면 알림 서비스가 기상청을 알게 된다.
/// </para>
/// </remarks>
public class LocalWeatherSubscriberDto
{
    public string OwnerType { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;

    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>표시용 지역 이름. 비어 있으면 저쪽이 격자표에서 찾아 채워 보낸다.</summary>
    public string? Place { get; set; }

    /// <summary>받기로 한 시각들(KST). 비었으면 저쪽이 기본값 <c>7,18</c> 로 읽는다.</summary>
    public string? Hours { get; set; }

    /// <summary>마지막으로 보낸 때(UTC). 같은 시각 칸을 두 번 울리지 않으려고 함께 보낸다.</summary>
    public DateTime? LastSentAt { get; set; }
}

/// <summary>
/// 생활과환경이 만들어 보내는 「내 위치 날씨」 한 통.
/// </summary>
/// <remarks>
/// 본문을 <b>저쪽이 만든다.</b> 기온·하늘상태·강수확률을 어떻게 한 줄로 줄일지는
/// 날씨를 아는 쪽의 판단이고, 여기서 하면 알림 서비스가 기상청 코드값
/// (<c>SKY</c>·<c>PTY</c>)을 알아야 한다.
/// </remarks>
public class SendLocalWeatherDto
{
    public string OwnerType { get; set; } = "jsini";
    public string OwnerKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>
    /// 찾아낸 지역 이름. 주면 설정에 적어 둔다 — 사람이 처음 위치를 잡을 때는
    /// 이름을 모르는 채로 저장될 수 있고, 그때 화면에 좌표만 남는다.
    /// </summary>
    public string? Place { get; set; }
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
    public DeviceMetadataDto Metadata { get; set; } = new();
}

/// <summary>
/// 사람 <b>한 명</b>의 알림 상태 — 관리 화면이 여러 명을 한 번에 볼 때 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MyNotificationStateDto"/> 와 무엇이 다른가 — 그쪽은 <b>자기</b> 것
/// 하나를 자세히(공개 키 · 기기 목록까지) 받는다. 이쪽은 <b>남의</b> 것을 여럿
/// 훑는 자리라 기기는 <see cref="DeviceCount"/> 하나로 줄인다. 계정이 예순이
/// 넘는데 기기 목록을 다 실으면 표 한 줄에 쓰지도 않을 자료가 붙는다.
/// </para>
///
/// <para>
/// <b><see cref="Saved"/> 를 함께 본다.</b> 거짓이면 아래 스위치 셋은 그 사람이
/// 고른 값이 아니라 <b>기본값</b>이다(표에 행이 없다). 둘을 구분하지 않으면
/// 「전부 켜 두었다」와 「한 번도 안 건드렸다」가 같은 그림이 된다.
/// </para>
/// </remarks>
public class OwnerNotificationStateDto
{
    /// <summary>주인 식별자. 포털 계정이면 로그인 아이디다.</summary>
    public string OwnerKey { get; set; } = string.Empty;

    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool WeatherEnabled { get; set; }

    /// <summary>쪽지를 메일로도 받는가. 기본은 꺼짐이다.</summary>
    public bool NoteEmailEnabled { get; set; }

    /// <summary>저장한 적이 있나. 거짓이면 위 넷은 기본값이다.</summary>
    public bool Saved { get; set; }

    /// <summary>
    /// 구독한 기기 수. <b>0 이면 푸시를 켜 두어도 도착할 곳이 없다</b> —
    /// 스위치만 보면 그것을 알 수 없어 따로 싣는다.
    /// </summary>
    public int DeviceCount { get; set; }

    /// <summary>그 사람의 기기 중 가장 최근에 발송이 성공한 시각.</summary>
    public DateTime? LastSentAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
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

/// <summary>
/// 발송 이력 한 줄(화면이 받는 모양).
/// </summary>
/// <remarks>
/// <b>엔티티를 그대로 내보내지 않는다.</b> 그 표에는 endpoint 가 들어 있고
/// 그것은 기기를 특정하는 값이라 화면에 보낼 것이 아니다. 그리고 화면이
/// 쓰는 이름(<c>targetUser</c>)과 표의 이름(<c>owner_key</c>)이 다르다 —
/// 표 이름을 화면에 맞추면 이번에는 서버 코드가 어색해진다.
/// </remarks>
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

    /// <summary>보낸 사람. 시스템이 보낸 것은 비어 있다.</summary>
    public string? SentBy { get; set; }
}

/// <summary>
/// 알림함 한 줄. <b>발송 한 번이 한 줄</b>이다(기기 수와 무관).
/// </summary>
public class NotificationRowDto
{
    /// <summary>묶음 열쇠. 옛 줄에는 묶음이 없어 줄 아이디가 그대로 온다.</summary>
    public string Id { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }

    /// <summary>기기 한 대에라도 갔는가.</summary>
    public bool Delivered { get; set; }

    /// <summary>한 대도 못 갔을 때의 까닭. 갔으면 <c>null</c>.</summary>
    public string? FailureReason { get; set; }
}
