using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// Contact Us DTO
/// </summary>
public class ContactUsDto {
  /// <summary>
  /// 이름
  /// </summary>
  [Required(ErrorMessage = "이름을 입력해주세요.")]
  [StringLength(50, ErrorMessage = "이름은 50자를 초과할 수 없습니다.")]
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// 이메일
  /// </summary>
  [Required(ErrorMessage = "이메일을 입력해주세요.")]
  [EmailAddress(ErrorMessage = "유효한 이메일 주소를 입력해주세요.")]
  [StringLength(100, ErrorMessage = "이메일은 100자를 초과할 수 없습니다.")]
  public string Email { get; set; } = string.Empty;

  /// <summary>
  /// 제목
  /// </summary>
  [StringLength(100, ErrorMessage = "제목은 100자를 초과할 수 없습니다.")]
  public string? Subject { get; set; }

  /// <summary>
  /// 내용
  /// </summary>
  [Required(ErrorMessage = "내용을 입력해주세요.")]
  [StringLength(5000, ErrorMessage = "내용은 5000자를 초과할 수 없습니다.")]
  public string Message { get; set; } = string.Empty;
}
