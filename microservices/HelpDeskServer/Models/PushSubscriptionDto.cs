namespace HelpDeskServer.Models;

/// <summary>
/// 푸시 구독 정보 DTO
/// </summary>
public sealed class PushSubscriptionDto
{
    /// <summary>푸시 서비스 엔드포인트</summary>
    public string? Endpoint { get; set; }
    /// <summary>암호화 키</summary>
    public KeysDto? Keys { get; set; }

    /// <summary>
    /// 푸시 구독 암호화 키 DTO
    /// </summary>
    public sealed class KeysDto
    {
        /// <summary>P256DH 키</summary>
        public string? P256dh { get; set; }
        /// <summary>인증 키</summary>
        public string? Auth { get; set; }
    }
}

/// <summary>
/// 푸시 메시지 내용 DTO
/// </summary>
public sealed class PushMessageDto
{
    /// <summary>알림 제목</summary>
    public string? Title { get; set; }
    /// <summary>알림 본문</summary>
    public string? Body { get; set; }
    /// <summary>알림 아이콘 URL</summary>
    public string? Icon { get; set; }
    /// <summary>알림 클릭 시 이동할 URL</summary>
    public string? Url { get; set; }
}
