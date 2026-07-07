using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 다단계 공통코드 엔티티 클래스 (자가 참조 구조)
/// </summary>
[Table("common_codes", Schema = "scom")]
public class CommonCode : BaseEntity<string>
{
    /// <summary>
    /// CommonCode 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public CommonCode()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 소속 공통코드 그룹 식별자 (ID)
    /// </summary>
    [Required]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// 소속 공통코드 그룹 엔티티 탐색 속성
    /// </summary>
    [ForeignKey("GroupId")]
    public CommonCodeGroup? Group { get; set; }

    /// <summary>
    /// 부모 코드 식별자 (ID) (다단계 구조용 자가 참조)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 부모 코드 엔티티 탐색 속성
    /// </summary>
    [ForeignKey("ParentId")]
    public CommonCode? Parent { get; set; }

    /// <summary>
    /// 코드 값
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>
    /// 코드 명칭
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string CodeName { get; set; } = string.Empty;

    /// <summary>
    /// 다국어 리소스 키 (선택 사항)
    /// </summary>
    [MaxLength(200)]
    public string? I18nKey { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 계층 레벨 (1, 2, 3...)
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// 최하위 자식 노드 여부
    /// </summary>
    public bool IsLeaf { get; set; } = true;

    /// <summary>
    /// 사용 상태 (1: 사용, 0: 미사용)
    /// </summary>
    public int Status { get; set; } = 1;

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 하위 공통코드 목록 탐색 속성 (1:N 관계 자가 참조)
    /// </summary>
    public ICollection<CommonCode>? Children { get; set; }
}
