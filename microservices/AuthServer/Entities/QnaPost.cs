using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// Q&amp;A 글 엔티티 — 질문과 답글을 한 테이블에 담는다.
/// </summary>
/// 질문과 답글을 한 테이블에 담고 자기 참조로 잇는다 — 구조가 같고 깊이 제한이 없어서다. ParentId 가 null 이면 질문(스레드 뿌리)이다.
/// 관리자가 정한 공개 여부(IsPublic)가 꺼진 글은 작성자와 관리자에게만 보인다. 일반 사용자 글은 꺼진 채 들어가고, 관리자 답변은 켜진 채 들어간다.
[Table("qna_posts", Schema = "scom")]
public class QnaPost : BaseEntity<string>
{
    public QnaPost()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>부모 글 아이디. 질문(뿌리)이면 null</summary>
    [Column("parent_id")]
    public string? ParentId { get; set; }

    [ForeignKey(nameof(ParentId))]
    public virtual QnaPost? Parent { get; set; }

    /// <summary>스레드 뿌리 아이디. 뿌리 글은 자기 아이디가 들어간다.</summary>
    [Required]
    [Column("root_id")]
    public string RootId { get; set; } = string.Empty;

    /// <summary>뿌리에서의 깊이. 뿌리 = 0, 답글 = 부모 + 1</summary>
    [Column("depth")]
    public int Depth { get; set; }

    /// <summary>제목. 질문(뿌리)만 쓴다.</summary>
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>본문 (HTML)</summary>
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 공개 여부. 관리자가 정한다.
    /// 끄면 작성자 본인과 관리자에게만 보인다.
    /// </summary>
    [Column("is_public")]
    public bool IsPublic { get; set; }

    /// <summary>관리자가 쓴 답변인지. 화면에서 '답변' 표시를 붙이는 데 쓴다.</summary>
    [Column("is_answer")]
    public bool IsAnswer { get; set; }

    /// <summary>
    /// 작성자 로그인 아이디 (accounts.user_id).
    /// </summary>
    /// created_by 를 쓰지 않는다 — 감사 로직이 저장 때 그 칸을 자기 값으로 덮어써서 본인 글 판정에 쓸 수 없다.
    [Column("author_id")]
    public string? AuthorId { get; set; }

    /// <summary>작성 당시의 표시 이름. 계정 이름이 나중에 바뀌어도 글은 그대로 남는다.</summary>
    [Column("author_name")]
    public string? AuthorName { get; set; }

    /// <summary>답글 목록</summary>
    public virtual ICollection<QnaPost> Children { get; set; } = new List<QnaPost>();
}
