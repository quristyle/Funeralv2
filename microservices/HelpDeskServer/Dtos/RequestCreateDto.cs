using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 개선 요청 생성을 위한 DTO
/// </summary>
/// <param name="Title">제목</param>
/// <param name="Description">내용</param>
/// <param name="CustomerId">요청 고객 ID</param>
/// <param name="CreatedBy">생성한 사용자</param>
/// <param name="MenuContext">작업이 수행된 메뉴/화면 정보</param>
/// <param name="MainPhoto">본문의 대표 사진 URL</param>
public record RequestCreateDto(
    [Required] string Title,
    string? Description,
    [Required] int CustomerId,
    string? CreatedBy,
    string? MenuContext,
    string? MainPhoto
);