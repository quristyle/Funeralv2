using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// Q&amp;A 글 엔티티 — 질문과 답글을 한 테이블에 담는다.
/// </summary>
/// <remarks>
/// 질문과 답글의 구조가 같고(제목만 뿌리에서 쓴다) 깊이 제한이 없어야 하므로
/// 테이블을 나누지 않고 자기 자신을 가리키는 부모 관계로 표현한다.
///
///   ParentId == null   질문(스레드 뿌리)
///   ParentId != null   답글. 답글의 답글도 같은 방식으로 계속 이어진다
///
/// <see cref="RootId"/> 를 따로 두는 이유는 스레드 하나를 한 번의 조회로
/// 가져오기 위해서다. 부모를 재귀로 따라 올라가지 않는다.
///
/// [공개 여부]
/// <see cref="IsPublic"/> 는 관리자가 정한다. 꺼진 글은 작성자 본인과 관리자에게만 보인다.
/// 일반 사용자가 쓴 글은 꺼진 상태로 들어가고(관리자 공개 대기),
/// 관리자가 쓴 답변은 켜진 상태로 들어간다 — 질문자가 바로 볼 수 있어야 한다.
/// </remarks>
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
    /// <remarks>
    /// `created_by` 를 쓰지 않는다. AppDbContext 의 감사 로직이 저장할 때
    /// `created_by` 를 자기 값으로 덮어써서 본인 글 판정에 쓸 수 없다.
    /// </remarks>
    [Column("author_id")]
    public string? AuthorId { get; set; }

    /// <summary>작성 당시의 표시 이름. 계정 이름이 나중에 바뀌어도 글은 그대로 남는다.</summary>
    [Column("author_name")]
    public string? AuthorName { get; set; }

    /// <summary>답글 목록</summary>
    public virtual ICollection<QnaPost> Children { get; set; } = new List<QnaPost>();
}
