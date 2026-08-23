using System.ComponentModel.DataAnnotations;

namespace HelpDeskServer.Dtos;

/// <summary>
/// 고객(사용자) 생성을 위한 DTO
///
/// 비밀번호 항목은 없앴다(결정 Q4). 계정과 인증은 JSini 관리 포털이 단독으로 맡고,
/// 여기서 만드는 것은 헬프데스크 조직 데이터(고객 레코드)다.
/// </summary>
/// <param name="LoginId">로그인 ID</param>
/// <param name="UserName">사용자 이름</param>
/// <param name="Email">이메일 주소</param>
/// <param name="CompanyId">소속 고객사 ID</param>
/// <param name="Sex">성별</param>
/// <param name="Photo">사진 URL</param>
/// <param name="CreatedBy">생성한 사용자</param>
/// <param name="MenuContext">작업이 수행된 메뉴/화면 정보</param>
public record CustomerCreateDto(
    [Required] string LoginId,
    [Required] string UserName,
    [Required] string Email,
    int CompanyId,
    string? Sex,
    string? Photo,
    string? CreatedBy,
    string? MenuContext);