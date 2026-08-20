namespace HelpDeskServer.Models;

/// <summary>
/// 사용자별 확장 속성을 저장하는 엔티티 (예: 알림 설정)
/// </summary>
public class UserProperty : BaseEntity
{
    /// <summary>
    /// 사용자 ID (Admin.Id 또는 Customer.Id)
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 사용자 타입 ("Admin" 또는 "Customer")
    /// </summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>
    /// 속성 키 (예: "receiveEmailNotifications")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 속성 값 (예: "true", "false")
    /// </summary>
    public string Value { get; set; } = string.Empty;
}