using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models;

/// <summary>
/// 일정 관리 테이블 모델
/// </summary>
[Table("Schedules")]
public class Schedule
{
    [Key]
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// 공통 일정 여부 (true면 모든 회사에 표시)
    /// </summary>
    public bool IsCommon { get; set; } = false;

    /// <summary>
    /// 특정 회사 ID (IsCommon이 false일 때 사용)
    /// </summary>
    public int? CompanyId { get; set; }

    /// <summary>
    /// 완료 여부
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// 완료일 (null이면 미완료 상태)
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// 작성자 ID
    /// </summary>
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
