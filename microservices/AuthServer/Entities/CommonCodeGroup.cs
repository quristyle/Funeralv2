using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 공통코드 그룹 엔티티 클래스
/// </summary>
[Table("common_code_groups", Schema = "scom")]
public class CommonCodeGroup : BaseEntity<string>
{
    /// <summary>
    /// CommonCodeGroup 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public CommonCodeGroup()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 그룹 코드 (예: AREA_CODE)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string GroupCode { get; set; } = string.Empty;

    /// <summary>
    /// 그룹 명칭
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 계층 구조 여부
    /// </summary>
    public bool IsHierarchical { get; set; } = false;

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 그룹에 속한 공통코드 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<CommonCode>? Codes { get; set; }
}
