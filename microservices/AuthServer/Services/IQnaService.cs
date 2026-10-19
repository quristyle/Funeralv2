using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>Q&amp;A 서비스 인터페이스. 답글에 답글을 다는 깊이 제한은 없다. 부모가 안 보이면 그 아래 답글도 안 보인다.</summary>
public interface IQnaService
{
    /// <summary>이 사용자가 남의 글에 답하고 공개 여부를 정할 수 있는지</summary>
    Task<bool> CanManageAsync(string userId);

    /// <summary>질문(스레드) 목록. 각 항목의 <c>Children</c> 에 보이는 답글이 트리로 담긴다.</summary>
    /// <param name="userId">게이트웨이가 넘긴 로그인 아이디</param>
    /// <param name="keyword">제목·본문 검색어. 볼 수 있는 글만 검색한다.</param>
    /// <param name="filter">
    /// <c>mine</c> 내가 쓴 질문 · <c>pending</c> 공개 대기(관리자만) ·
    /// <c>unanswered</c> 관리자 답변이 없는 질문 · 그 외는 전체
    /// </param>
    /// <param name="page">1부터</param>
    /// <param name="pageSize">한 쪽에 담을 질문 수</param>
    Task<QnaListDto> GetListAsync(
        string userId, string? keyword, string? filter, int page, int pageSize);

    /// <summary>글 하나가 속한 스레드를 뿌리부터 돌려준다. 답글을 달거나 고친 뒤 그 스레드만 다시 그릴 때 쓴다.</summary>
    Task<QnaPostDto?> GetThreadAsync(string userId, string id);

    /// <summary>질문·답글 등록</summary>
    Task<(QnaResult Result, QnaPostDto? Post)> CreateAsync(string userId, CreateQnaPostDto request);

    /// <summary>수정 (본인 글 또는 관리자)</summary>
    Task<QnaResult> UpdateAsync(string userId, string id, UpdateQnaPostDto request);

    /// <summary>삭제. 답글까지 함께 지운다 (본인 글 또는 관리자)</summary>
    Task<QnaResult> DeleteAsync(string userId, string id);

    /// <summary>공개 여부 변경 (관리자 전용)</summary>
    Task<QnaResult> SetVisibilityAsync(string userId, string id, QnaVisibilityDto request);
}

/// <summary>Q&amp;A 쓰기 결과</summary>
public enum QnaResult
{
    Ok,
    NotFound,
    Forbidden,
    Invalid
}
