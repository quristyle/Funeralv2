namespace HelpDeskServer.Dtos;

/// <summary>
/// 사용자 목록 조회 시 반환되는 데이터 구조
/// </summary>
public class UserDto
{
    /// <summary>
    /// 사용자 ID (Admin.Id 또는 Customer.Id)
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 사용자 이름
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 타입 ("admin" 또는 "customer")
    /// </summary>
    public string UserType { get; set; } = string.Empty;
}
