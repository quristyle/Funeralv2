using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 알림을 누가 언제 읽었는지.
/// </summary>
/// <remarks>
/// 옛 <c>t_notification</c> 은 읽음 여부(<c>isread</c> · <c>readtime</c>)를 알림 행에
/// 함께 두었다. 전체 공지를 여러 사람이 볼 수 있게 하려면 사람 수만큼 행이 필요하므로
/// 여기로 뺐다.
/// </remarks>
[Table("funeral_notice_reads")]
public class FuneralNoticeRead
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>읽은 알림</summary>
    [Required]
    [Column("notice_id")]
    [MaxLength(50)]
    public string NoticeId { get; set; } = string.Empty;

    /// <summary>읽은 사람</summary>
    [Required]
    [Column("user_id")]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>읽은 시각</summary>
    [Column("read_at")]
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
