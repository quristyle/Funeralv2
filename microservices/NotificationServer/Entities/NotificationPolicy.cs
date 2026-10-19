using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>이 역할은 이 이벤트를 이 길로 받는다(한 줄). 회사의 규칙이라 본인 설정보다 먼저 걸린다.
/// (이벤트, 역할) 쌍은 하나만 있어야 한다 — 두 줄이면 껐는데 가는 쪽으로 틀린다.</summary>
[Table("notification_policies", Schema = "scom")]
public class NotificationPolicy : BaseEntity<string>
{
    public NotificationPolicy()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>이벤트 코드. <see cref="NotificationEventRow"/> 의 식별자다.</summary>
    [Required]
    [MaxLength(64)]
    [Column("event_code")]
    public string EventCode { get; set; } = string.Empty;

    /// <summary>역할 식별자(<c>scom.roles.id</c>). 예: <c>SYSTEM_ADMINISTRATOR</c>.</summary>
    [Required]
    [MaxLength(64)]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>앱 푸시로 받나.</summary>
    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    /// <summary>이메일로 받나.</summary>
    [Column("email_enabled")]
    public bool EmailEnabled { get; set; }

    /// <summary>이 줄을 쓰나. 끄면 「당분간 안 받는다」, 지우면 「상관없다」다. 이벤트에 켜진 줄이 하나도 없으면 제한이 없는 것으로 본다.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
