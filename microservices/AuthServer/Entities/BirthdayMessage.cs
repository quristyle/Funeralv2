using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 생일 축하 메시지 엔티티 클래스 (scom.birthday_messages)
/// </summary>
/// <remarks>
/// 보낸 이(<see cref="SenderId"/>)·받는 이(<see cref="RecipientId"/>)는 모두
/// 포털 계정(scom.accounts)의 <c>user_id</c> 값이다. FK 는 걸지 않는다 —
/// accounts 의 PK 가 id(GUID)라 user_id 로는 걸 수 없고(유니크 제약 없음),
/// 계정이 지워져도 받은 축하의 기록은 남긴다 (docs/sql/birthday_messages.sql).
/// </remarks>
[Table("birthday_messages", Schema = "scom")]
public class BirthdayMessage : BaseEntity
{
    /// <summary>
    /// 받는 이 (accounts.user_id)
    /// </summary>
    [Required]
    [Column("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// 보낸 이 (accounts.user_id)
    /// </summary>
    [Required]
    [Column("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// 메시지 내용
    /// </summary>
    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 읽음 여부
    /// </summary>
    [Column("is_read")]
    public bool IsRead { get; set; }
}
