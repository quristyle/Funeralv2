namespace HelpDeskServer.Options;

/// <summary>
/// VAPID (Voluntary Application Server Identification) 키 설정을 위한 옵션 클래스입니다.
/// Web Push 프로토콜에서 애플리케이션 서버를 식별하는 데 사용됩니다.
/// </summary>
public sealed class VapidOptions
{
    /// <summary>
    /// VAPID 주체(Subject)입니다. 일반적으로 'mailto:' 스킴을 사용하는 이메일 주소 또는 도메인 URL입니다.
    /// </summary>
    public string? Subject { get; set; }  // mailto:admin@example.com 또는 도메인 URL
    /// <summary>
    /// VAPID 공개 키(Public Key)입니다. 클라이언트(브라우저)가 푸시 구독을 생성할 때 사용됩니다.
    /// </summary>
    public string? PublicKey { get; set; }
    /// <summary>
    /// VAPID 개인 키(Private Key)입니다. 서버가 푸시 알림을 보낼 때 요청에 서명하는 데 사용됩니다.
    /// </summary>
    public string? PrivateKey { get; set; }
}
