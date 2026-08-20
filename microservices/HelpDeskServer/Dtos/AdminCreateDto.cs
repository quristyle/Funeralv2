using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 관리자 생성을 위한 DTO
/// </summary>
/// <param name="LoginId">로그인 ID</param>
/// <param name="UserName">사용자 이름</param>
/// <param name="Email">이메일 주소</param>
/// <param name="TeamIds">소속 팀 ID 목록</param>
/// <param name="CreatedBy">생성한 사용자</param>
/// <param name="MenuContext">작업이 수행된 메뉴/화면 정보</param>
public record AdminCreateDto([Required] string LoginId, [Required] string UserName, [Required] string Email, List<int>? TeamIds, string? CreatedBy, string? MenuContext);