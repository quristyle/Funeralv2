namespace AuthServer.DTOs;

public class UpdateProfileDto
{
    public string? RealName { get; set; }
    public string? Introduction { get; set; }
    public string? Email { get; set; }
    public List<string>? Emails { get; set; }
    public string? Phone { get; set; }
    public List<string>? Phones { get; set; }
    public string? Avatar { get; set; }
    public string? AvatarGroupId { get; set; }

    /// <summary>생년월일('yyyy-MM-dd'). 빈 문자열이면 지운다. null 이면 건드리지 않는다.</summary>
    public string? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지. null 이면 건드리지 않는다.</summary>
    public bool? BirthDateIsLunar { get; set; }

    /// <summary>이메일 변경 시 인증 완료 토큰</summary>
    public string? EmailVerificationToken { get; set; }
}
