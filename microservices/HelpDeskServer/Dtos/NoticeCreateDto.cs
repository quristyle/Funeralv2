using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 공지사항 생성을 위한 DTO
/// </summary>
/// <param name="Id">공지사항 ID (수정 시 사용)</param>
/// <param name="Title">제목</param>
/// <param name="Content">내용</param>
/// <param name="CreatedBy">생성한 사용자</param>
/// <param name="CreatedAt">생성 일시</param>
public record NoticeCreateDto(
    int? Id,
    [Required] string Title,
    [Required] string Content,
    string? CreatedBy = null,
    DateTime? CreatedAt = null
);
