using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models;

/// <summary>
/// Web Push 구독 정보를 저장하는 엔티티
/// </summary>
public class PushSubscription {
  /// <summary>
  /// Push Service Endpoint URL (PK)
  /// </summary>
  [Key]
  public string Endpoint { get; set; } = string.Empty;
  /// <summary>
  /// P256DH 키
  /// </summary>
  public string P256dh { get; set; } = string.Empty;
  /// <summary>
  /// 인증 키
  /// </summary>
  public string Auth { get; set; } = string.Empty;

  /// <summary>
  /// 사용자 ID (Admin.Id 또는 Customer.Id)
  /// </summary>
  public int UserId { get; set; }

  /// <summary>
  /// 사용자 타입 ("Admin" 또는 "Customer")
  /// </summary>
  public string UserType { get; set; } = string.Empty;

  /// <summary>
  /// 참조하는 관리자 (UserType이 "Admin"일 경우)
  /// </summary>
  [ForeignKey("UserId")]
  public Admin? Admin { get; set; }

  /// <summary>
  /// 참조하는 고객 (UserType이 "Customer"일 경우)
  /// </summary>
  [ForeignKey("UserId")]
  public Customer? Customer { get; set; }
}