using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// F.A.Q 엔티티
/// </summary>
/// <remarks>
/// F.A.Q 는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
/// 각 MSA 가 자기 F.A.Q 를 따로 두지 않는다(공지와 같은 방침).
///
/// 쓰는 사람과 읽는 사람이 갈린다 — 관리자만 등록·수정·삭제하고
/// 나머지 사용자는 읽는다. 판정은 `scom.role_menus` 의 `/help/faq` 권한으로 한다.
/// </remarks>
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
