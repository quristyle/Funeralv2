namespace HelpDeskServer.Models;

/// <summary>
/// 푸시 알림 발송 기록을 저장하는 엔티티
/// </summary>
public class PushNotificationLog : BaseEntity
{
    /// <summary>
    /// 대상 구독 Endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 발송 성공 여부
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 실패 시 이유 (예외 메시지)
    /// </summary>
    public string? FailureReason { get; set; }
}