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

    public string? Message { get; set; }
}

/// <summary>이메일 발송 요청.</summary>
public class SendEmailDto
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다 (기존 스크립트 규약이 그렇다).</summary>
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문 (HTML 허용).</summary>
    public string Body { get; set; } = string.Empty;
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
