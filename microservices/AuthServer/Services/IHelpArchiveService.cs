using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 자료실 서비스 인터페이스
/// </summary>
/// <remarks>
/// 관리자만 등록·수정·삭제하고 나머지 사용자는 설명을 읽고 내려받는다.
/// '관리자' 판정은 <c>scom.role_menus</c> 의 <c>/help/archive</c> 권한으로 하며
/// 화면이 아니라 **이 서비스가** 판정한다(F.A.Q 와 같은 방식).
/// </remarks>
public interface IHelpArchiveService
{
    /// <summary>이 사용자가 자료를 등록·수정·삭제할 수 있는지</summary>
    Task<bool> CanManageAsync(string userId);

    /// <summary>
    /// 목록. 관리자에게는 비활성까지, 나머지에게는 활성만 보인다.
    /// </summary>
    /// <param name="userId">게이트웨이가 넘긴 로그인 아이디</param>
    /// <param name="keyword">자료명 · 설명 · 파일명 검색어</param>
    /// <param name="category">분류 필터</param>
    Task<HelpArchiveListDto> GetListAsync(string userId, string? keyword, string? category);

    /// <summary>단건 조회. 비활성 항목은 관리자에게만 보인다.</summary>
    Task<HelpArchiveDto?> GetByIdAsync(string userId, string id);

    /// <summary>등록. 권한이 없으면 null 을 돌려준다.</summary>
    Task<HelpArchiveDto?> CreateAsync(SaveHelpArchiveDto request, string userId);

    /// <summary>수정</summary>
    Task<HelpArchiveSaveResult> UpdateAsync(string id, SaveHelpArchiveDto request, string userId);

    /// <summary>삭제 (soft delete)</summary>
    Task<HelpArchiveSaveResult> DeleteAsync(string id, string userId);

    /// <summary>
    /// 내려받기. 다운로드 수를 세고 FileServer 로 넘길 주소를 돌려준다.
    /// </summary>
    /// <remarks>
    /// 브라우저가 FileServer 를 직접 열면 셀 수가 없으므로 여기를 한 번 거친다.
    /// 읽을 수 없는 자료(비활성인데 관리자가 아닌 경우)면 null 이다.
    /// </remarks>
    Task<string?> ResolveDownloadAsync(string userId, string archiveId, string fileId);
}

/// <summary>자료실 쓰기 결과</summary>
public enum HelpArchiveSaveResult
{
    Ok,
    NotFound,
    Forbidden
}
