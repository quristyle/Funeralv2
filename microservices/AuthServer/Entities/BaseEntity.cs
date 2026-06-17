using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 모든 엔티티의 기본이 되는 베이스 엔티티
/// </summary>
public abstract class BaseEntity
{
    /// <summary>생성 일시</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>생성자 ID</summary>
    public string? CreatedBy { get; set; }

    /// <summary>수정 일시</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>수정자 ID</summary>
    public string? UpdatedBy { get; set; }
}
