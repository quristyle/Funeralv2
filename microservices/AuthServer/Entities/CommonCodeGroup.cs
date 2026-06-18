using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 공통코드 그룹 엔티티
/// </summary>
[Table("common_code_groups", Schema = "scom")]
public class CommonCodeGroup : BaseEntity<string>
{
    public CommonCodeGroup()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>그룹 코드 (식별자, 예: AREA_CODE)</summary>
    [Required]
    [MaxLength(50)]
    public string GroupCode { get; set; } = string.Empty;

    /// <summary>그룹 명칭</summary>
    [Required]
    [MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>계층 구조 여부</summary>
    public bool IsHierarchical { get; set; } = false;

    /// <summary>비고</summary>
    public string? Remark { get; set; }

    /// <summary>그룹에 속한 코드 목록</summary>
    public ICollection<CommonCode>? Codes { get; set; }
}
