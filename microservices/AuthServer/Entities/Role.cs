using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 사용자 권한/역할 엔티티 클래스
/// </summary>
[Table("roles", Schema = "scom")]
public class Role : BaseEntity<string>
{
    /// <summary>
    /// Role 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Role()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 역할 명칭
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 역할 상태 (0: 비활성, 1: 활성)
    /// </summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>
    /// 역할 설명 및 비고
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }
}
