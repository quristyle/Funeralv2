using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 고객(사용자) 생성을 위한 DTO
/// </summary>
/// <param name="LoginId">로그인 ID</param>
/// <param name="UserName">사용자 이름</param>
/// <param name="Email">이메일 주소</param>
/// <param name="Password">비밀번호</param>
/// <param name="CompanyId">소속 고객사 ID</param>
/// <param name="Sex">성별</param>
/// <param name="Photo">사진 URL</param>
/// <param name="CreatedBy">생성한 사용자</param>
/// <param name="MenuContext">작업이 수행된 메뉴/화면 정보</param>
public record CustomerCreateDto(
    [Required] string LoginId,
    [Required] string UserName,
    [Required] string Email,
    [Required][MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")][RegularExpression(@"^(?=.*[0-9])(?=.*[!@#$%^&*(),.?"":{}|<>]).*$", ErrorMessage = "Password must contain at least one number and one special character.")] string Password,
    int CompanyId,
    string? Sex,
    string? Photo,
    string? CreatedBy,
    string? MenuContext);