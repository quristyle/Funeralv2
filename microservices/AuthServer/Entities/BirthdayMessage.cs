using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>생일 축하 메시지(<c>scom.birthday_messages</c>). 보낸 이·받는 이는 계정의 user_id 값이고 FK 는 걸지 않는다.</summary>
[Table("birthday_messages", Schema = "scom")]
public class BirthdayMessage : BaseEntity
{
    /// <summary>받는 이 (accounts.user_id)</summary>
    [Required]
    [Column("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>보낸 이 (accounts.user_id)</summary>
    [Required]
    [Column("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>메시지 내용</summary>
    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>읽음 여부</summary>
    [Column("is_read")]
    public bool IsRead { get; set; }
}
