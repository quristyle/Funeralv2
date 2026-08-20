using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 관리자 비밀번호 변경을 위한 DTO
/// </summary>
/// <param name="OldPassword">기존 비밀번호</param>
/// <param name="NewPassword">새 비밀번호</param>
public record AdminChangePasswordDto(
    [Required] string OldPassword,
    [Required] [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")] [RegularExpression(@"^(?=.*[0-9])(?=.*[!@#$%^&*(),.?"":{}|<>]).*$", ErrorMessage = "Password must contain at least one number and one special character.")] string NewPassword
);
