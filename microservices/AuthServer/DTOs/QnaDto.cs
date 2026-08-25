namespace AuthServer.DTOs;

/// <summary>
/// Q&amp;A 글 (질문 · 답글 공통)
/// </summary>
/// <remarks>
/// 답글은 <see cref="Children"/> 에 다시 같은 모양으로 담긴다.
/// 답글의 답글의 답글까지 깊이 제한 없이 이어진다.
/// </remarks>
public class QnaPostDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>부모 글 아이디. 질문(뿌리)이면 null</summary>
    public string? ParentId { get; set; }

    /// <summary>스레드 뿌리 아이디</summary>
    public string RootId { get; set; } = string.Empty;

    public int Depth { get; set; }

    /// <summary>제목. 질문(뿌리)만 쓴다.</summary>
    public string? Title { get; set; }

    /// <summary>본문 (HTML)</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>공개 여부. 관리자가 정한다.</summary>
    public bool IsPublic { get; set; }

    /// <summary>관리자가 쓴 답변인지</summary>
    public bool IsAnswer { get; set; }

    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>
    /// 작성자 프로필 사진 주소. 없으면 null — 화면이 이름 첫 글자로 대신 그린다.
    /// </summary>
    /// <remarks>
    /// 이름과 달리 **글에 새겨 두지 않고 조회할 때마다 계정에서 읽는다.**
    /// 사진은 바뀌는 것이 정상이라, 글마다 그때의 사진을 붙여 두면 지난 글에
    /// 옛 사진이 남는다. 이름은 표시용 기록이라 새겨 두는 편이 맞다.
    /// </remarks>
    public string? AuthorAvatar { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>내가 쓴 글인지. 화면이 수정·삭제 버튼을 켤 때 쓴다.</summary>
    public bool IsMine { get; set; }

    /// <summary>이 글을 고칠 수 있는지 (본인 또는 관리자)</summary>
    public bool CanEdit { get; set; }

    /// <summary>답글 (같은 모양으로 계속 이어진다)</summary>
    public List<QnaPostDto> Children { get; set; } = new();

    // ── 뿌리 글에서만 채운다 ───────────────────────────────

    /// <summary>이 사용자에게 보이는 답글 수 (모든 깊이 합산)</summary>
    public int ReplyCount { get; set; }

    /// <summary>관리자 답변이 하나라도 달렸는지</summary>
    public bool IsAnswered { get; set; }

    /// <summary>스레드에서 가장 마지막에 올라온 글의 시각</summary>
    public DateTime? LastPostedAt { get; set; }
}

/// <summary>
/// Q&amp;A 질문·답글 등록 요청
/// </summary>
public class CreateQnaPostDto
{
    /// <summary>답글이면 부모 글 아이디. 질문이면 비운다.</summary>
    public string? ParentId { get; set; }

    /// <summary>제목. 질문일 때만 쓴다.</summary>
    public string? Title { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 공개 여부. **관리자만** 지정할 수 있다.
    /// 일반 사용자가 보낸 값은 무시하고 비공개로 들어간다(관리자 공개 대기).
    /// </summary>
    public bool? IsPublic { get; set; }
}

/// <summary>
/// Q&amp;A 글 수정 요청
/// </summary>
public class UpdateQnaPostDto
{
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>공개 여부. 관리자가 보낸 값만 반영한다.</summary>
    public bool? IsPublic { get; set; }
}

/// <summary>
/// Q&amp;A 공개 여부 변경 요청 (관리자 전용)
/// </summary>
public class QnaVisibilityDto
{
    public bool IsPublic { get; set; }

    /// <summary>
    /// 참이면 이 글의 답글까지 같은 값으로 함께 바꾼다.
    /// 스레드를 통째로 공개할 때 한 번에 처리한다.
    /// </summary>
    public bool IncludeReplies { get; set; }
}

/// <summary>
/// Q&amp;A 목록 응답
/// </summary>
public class QnaListDto
{
    /// <summary>질문(뿌리) 목록. 답글은 각 항목의 Children 에 담겨 있다.</summary>
    public List<QnaPostDto> Items { get; set; } = new();

    /// <summary>
    /// 남의 글에 답하고 공개 여부를 정할 수 있는 사용자인지.
    /// (`/help/qna` 의 사용자 정의 권한 1 — '답변·공개 관리')
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>질문·답글을 쓸 수 있는지 (`can_create`)</summary>
    public bool CanWrite { get; set; }

    /// <summary>현재 쪽 (1부터)</summary>
    public int Page { get; set; }

    /// <summary>한 쪽에 담은 질문 수</summary>
    public int PageSize { get; set; }

    /// <summary>조건에 맞는 질문 총 개수 (답글은 세지 않는다)</summary>
    public int Total { get; set; }
}
