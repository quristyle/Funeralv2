using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 고객사 생성을 위한 DTO
/// </summary>
/// <param name="Name">고객사 이름</param>
/// <param name="ModifiedBy">수정한 사용자</param>
/// <param name="MenuContext">작업이 수행된 메뉴/화면 정보</param>
public record CompanyCreateDto([Required] string Name, string? ModifiedBy, string? MenuContext);