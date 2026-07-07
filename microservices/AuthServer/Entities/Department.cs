using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 조직/부서 정보 엔티티 클래스
/// </summary>
[Table("departments", Schema = "scom")]
public class Department : BaseEntity<string>
{
    /// <summary>
    /// Department 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Department()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 부서 명칭
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 상위 부서 식별자 (ID) (트리 구조 지원)
    /// </summary>
    [Column("parent_id")]
    public string? ParentId { get; set; }

    /// <summary>
    /// 소속 회사 식별자 (ID)
    /// </summary>
    [Column("company_id")]
    public string? CompanyId { get; set; }

    /// <summary>
    /// 소속 회사 엔티티 탐색 속성
    /// </summary>
    [ForeignKey("CompanyId")]
    public Company? Company { get; set; }

    /// <summary>
    /// 부서 사용 상태 (0: 비활성, 1: 활성)
    /// </summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>
    /// 부서 설명 및 비고
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 부서 소속 사용자 계정 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<Account>? Accounts { get; set; }
}
