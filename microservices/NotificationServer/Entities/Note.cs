using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>쪽지 한 통. 사람 하나 → 사람 하나가 한 줄이다. 글 자체가 본체라 발송 기록(PushSendLog)에 얹지 않고 따로 둔다 — 푸시와 메일이 실패해도 쪽지함에는 남는다.
/// 주인은 로그인 아이디 하나이고(포털 계정끼리만 주고받는다), 지우기는 보낸 쪽과 받은 쪽이 따로다.</summary>
[Table("notes", Schema = "scom")]
public class Note : BaseEntity<string>
{
    public Note()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>보낸 사람의 포털 로그인 아이디(<c>scom.accounts.user_id</c>).</summary>
    [Required]
    [Column("sender_key")]
    public string SenderKey { get; set; } = string.Empty;

    /// <summary>보낸 사람의 이름을 그때 모습 그대로 적어 둔다 — 계정이 지워지거나 이름이 바뀌어도 옛 쪽지의 이름은 그대로다.</summary>
    [Column("sender_name")]
    public string? SenderName { get; set; }

    /// <summary>받는 사람의 포털 로그인 아이디.</summary>
    [Required]
    [Column("receiver_key")]
    public string ReceiverKey { get; set; } = string.Empty;

    /// <summary>받는 사람의 이름. 적어 두는 까닭은 <see cref="SenderName"/> 과 같다.</summary>
    [Column("receiver_name")]
    public string? ReceiverName { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    /// <summary>보낸 때(UTC). 목록의 기본 정렬이고 기간 조건이 이 값을 본다.</summary>
    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>받은 사람이 읽은 때(UTC). 안 읽었으면 <c>null</c>.</summary>
    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    /// <summary>앱 푸시가 받는 사람의 기기에 실제로 닿은 때(UTC). 기기 서비스워커가 되알려 준 때이며, 이 칸이 비면 메일 전환 판정의 근거가 된다(PushSent 는 넘긴 때).</summary>
    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>전환 메일이 나간 때(UTC). 이 칸을 따로 둬야 배치가 같은 쪽지를 5분마다 다시 메일로 보내지 않는다.</summary>
    [Column("fallback_email_at")]
    public DateTime? FallbackEmailAt { get; set; }

    /// <summary>앱 푸시가 기기 한 대에라도 갔는가. 못 받은 까닭의 절반이 여기 있다.</summary>
    [Column("push_sent")]
    public bool PushSent { get; set; }

    /// <summary>메일이 나갔는가.</summary>
    [Column("email_sent")]
    public bool EmailSent { get; set; }

    /// <summary>두드림이 막힌 까닭(사람이 읽는 짧은 말). 쪽지 자체는 성공이고 알림만 막힌 경우가 대부분이다.</summary>
    [Column("notify_note")]
    public string? NotifyNote { get; set; }

    /// <summary>보낸 사람이 자기 보낸함에서 치웠나.</summary>
    [Column("sender_deleted")]
    public bool SenderDeleted { get; set; }

    /// <summary>받은 사람이 자기 쪽지함에서 치웠나.</summary>
    [Column("receiver_deleted")]
    public bool ReceiverDeleted { get; set; }
}
