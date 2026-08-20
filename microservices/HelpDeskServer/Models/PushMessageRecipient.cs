namespace HelpDeskServer.Models;

/// <summary>
/// 특정 사용자에게 발송된 푸시 메시지의 수신 상태를 추적하는 엔티티
/// </summary>
public class PushMessageRecipient : BaseEntity {
  /// <summary>
  /// PushMessage 외래 키
  /// </summary>
  public int PushMessageId { get; set; }

  /// <summary>
  /// 참조하는 PushMessage
  /// </summary>
  public virtual PushMessage PushMessage { get; set; } = null!;

  /// <summary>
  /// 수신자 사용자 ID
  /// </summary>
  public int UserId { get; set; }

  /// <summary>
  /// 수신자 사용자 타입 ("admin" 또는 "customer")
  /// </summary>
  public string UserType { get; set; } = string.Empty;

  /// <summary>
  /// 메시지가 발송된 구독 Endpoint
  /// </summary>
  public string Endpoint { get; set; } = string.Empty;

  /// <summary>
  /// 푸시 서비스에 성공적으로 전달되었는지 여부
  /// </summary>
  public bool IsDelivered { get; set; } = false;

  /// <summary>
  /// 푸시 서비스에 전달된 시간
  /// </summary>
  public DateTime? DeliveredAt { get; set; }

  /// <summary>
  /// 사용자가 알림을 확인했는지 여부
  /// </summary>
  public bool IsRead { get; set; } = false;

  /// <summary>
  /// 사용자가 알림을 확인한 시간
  /// </summary>
  public DateTime? ReadAt { get; set; }

}
