using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>F.A.Q 엔티티</summary>
/// 공지와 같이 JSini 관리 포털이 관리하고 모든 MSA 가 공통으로 본다. 쓰기는 관리자만, 읽기는 전원이다.
[Table("faqs", Schema = "scom")]
public class Faq : BaseEntity<string>
{
    public Faq()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>분류. 비우면 화면이 '기타' 로 묶어 보여준다.</summary>
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>질문</summary>
    [Required]
    [Column("question")]
    public string Question { get; set; } = string.Empty;

    /// <summary>답변 (HTML)</summary>
    [Column("answer")]
    public string? Answer { get; set; }

    /// <summary>노출 순서 (작을수록 먼저)</summary>
    [Column("order_no")]
    public int OrderNo { get; set; }

    /// <summary>사용 상태 (0: 비활성, 1: 활성). 비활성은 관리자에게만 보인다.</summary>
    [Column("status")]
    public int Status { get; set; } = 1;
}
