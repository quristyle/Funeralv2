using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// F.A.Q 서비스 인터페이스
/// </summary>
/// <remarks>
/// 관리자만 등록·수정·삭제하고 나머지 사용자는 읽는다.
/// '관리자' 판정은 `scom.role_menus` 의 <c>/help/faq</c> 권한으로 하며
/// 화면이 아니라 **이 서비스가** 판정한다.
/// </remarks>
public interface IFaqService
{
    /// <summary>이 사용자가 F.A.Q 를 등록·수정·삭제할 수 있는지</summary>
    Task<bool> CanManageAsync(string userId);

    /// <summary>
    /// 목록. 관리자에게는 비활성까지, 나머지에게는 활성만 보인다.
    /// </summary>
    /// <param name="userId">게이트웨이가 넘긴 로그인 아이디</param>
    /// <param name="keyword">질문·답변 본문 검색어</param>
    /// <param name="category">분류 필터</param>
    Task<FaqListDto> GetListAsync(string userId, string? keyword, string? category);

    /// <summary>단건 조회. 비활성 항목은 관리자에게만 보인다.</summary>
    Task<FaqDto?> GetByIdAsync(string userId, string id);

    /// <summary>등록. 권한이 없으면 null 을 돌려준다.</summary>
    Task<FaqDto?> CreateAsync(SaveFaqDto request, string userId);

    /// <summary>
    /// 수정.
    /// </summary>
    /// <returns>없으면 <c>NotFound</c>, 권한이 없으면 <c>Forbidden</c></returns>
    Task<FaqSaveResult> UpdateAsync(string id, SaveFaqDto request, string userId);

    /// <summary>삭제 (soft delete)</summary>
    Task<FaqSaveResult> DeleteAsync(string id, string userId);
}

/// <summary>F.A.Q 쓰기 결과</summary>
public enum FaqSaveResult
{
    Ok,
    NotFound,
    Forbidden
}
