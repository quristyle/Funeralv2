using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>한 사람이 이벤트 하나를 길마다 받을지(본인 설정). 줄이 없으면 받는다 — 끈 것만 적힌다.
/// 회사의 규칙(<see cref="NotificationPolicy"/>)을 지난 뒤에 걸리고, 정책이 안 걸리는 이벤트(governed = false)에는 쓰지 않는다.</summary>
[Table("notification_user_event_prefs", Schema = "scom")]
public class NotificationUserEventPref : BaseEntity<string>
{
    public NotificationUserEventPref()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>이벤트 코드. <see cref="NotificationEventRow"/> 의 식별자다.</summary>
    [Required]
    [MaxLength(64)]
    [Column("event_code")]
    public string EventCode { get; set; } = string.Empty;

    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    [Column("email_enabled")]
    public bool EmailEnabled { get; set; } = true;
}
