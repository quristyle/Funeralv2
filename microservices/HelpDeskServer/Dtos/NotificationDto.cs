namespace HelpDeskServer.Dtos;

/// <summary>
/// 알림 내역 조회 시 프론트엔드로 반환되는 데이터 구조
/// </summary>
public class NotificationDto {
  /// <summary>
  /// 알림 수신 ID
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// 알림 내용
  /// </summary>
  public string Message { get; set; } = string.Empty;

  public string? Url { get; set; }



  /// <summary>
  /// 수신 시간
  /// </summary>
  public DateTime ReceivedAt { get; set; }

  /// <summary>
  /// 확인 여부
  /// </summary>
  public bool IsRead { get; set; }

  /// <summary>
  /// 구독 Endpoint URL
  /// </summary>
  public string Endpoint { get; set; } = string.Empty;
}
