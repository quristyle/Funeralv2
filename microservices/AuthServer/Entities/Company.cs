using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 회사 엔티티
/// </summary>
[Table("companies", Schema = "scom")]
public class Company : BaseEntity<string>
{
    public Company()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("business_number")]
    public string? BusinessNumber { get; set; } // 사업자 번호

    [Column("representative")]
    public string? Representative { get; set; } // 대표자명

    [Column("status")]
    public int Status { get; set; } = 1; // 1: 활성, 0: 비활성

    [Column("remark")]
    public string? Remark { get; set; }

    // 관계 설정: 1(Company) : N(Department)
    public ICollection<Department>? Departments { get; set; }
}
