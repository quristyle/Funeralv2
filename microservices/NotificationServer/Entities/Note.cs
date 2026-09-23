using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 쪽지 한 통. <b>사람 하나 → 사람 하나</b>가 한 줄이다.
/// </summary>
/// <remarks>
/// <para>
/// [<see cref="PushSendLog"/> 와 무엇이 다른가]
/// </para>
///
/// <para>
/// 발송 기록은 <b>보낸 흔적</b>이다 — 기기마다 한 줄이고, 못 보낸 까닭을 적어 두는
/// 자리이며, 알림함은 그것을 <c>batch_id</c> 로 묶어 「내게 온 소식」처럼 보여 준다.
/// 그런데 그것은 어디까지나 <b>알림이 남긴 자국</b>이라, 알림을 보내지 않으면
/// (푸시를 꺼 두었거나 VAPID 가 없거나) 글도 함께 사라진다.
/// </para>
///
/// <para>
/// 쪽지는 <b>글 자체가 본체다.</b> 푸시와 메일은 「왔다」를 알리는 두드림일 뿐이고,
/// 둘 다 실패해도 쪽지는 쪽지함에 그대로 있어야 한다. 그래서 발송 기록에 얹지 않고
/// 표를 따로 둔다 — 얹었다면 알림 정리 규칙이 생기는 날 남의 편지가 함께 지워진다.
/// </para>
///
/// <para>
/// [주인이 <c>(ownerType, ownerKey)</c> 쌍이 아니라 로그인 아이디 하나다]
/// </para>
///
/// <para>
/// 구독·설정 표는 헬프데스크 계정(숫자)까지 담아야 해서 종류를 함께 들지만
/// (<see cref="PushSubscription"/> 머리말), 쪽지는 <b>포털 계정끼리만</b> 주고받는다.
/// 헬프데스크 고객에게 쪽지를 보내는 일이 생기면 그때 종류 칸을 더한다 — 지금 넣어
/// 두면 모든 질의에 <c>= 'jsini'</c> 가 따라다니면서 아무것도 가르지 않는다.
/// </para>
///
/// <para>
/// [지우기는 <b>보낸 쪽·받은 쪽이 따로</b>다]
/// </para>
///
/// <para>
/// <see cref="BaseEntity{TKey}.IsDeleted"/> 하나로 두면 보낸 사람이 자기 보낸함에서
/// 치우는 순간 <b>받은 사람의 쪽지까지 사라진다.</b> 편지를 부친 사람이 남의 우편함을
/// 비울 수는 없으므로 칸을 둘로 나눈다. 둘 다 치운 줄만 실제로 지울 값어치가 있다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 보낸 사람의 이름을 <b>그때 모습 그대로</b> 적어 둔다.
    /// </summary>
    /// <remarks>
    /// 아이디로 매번 계정을 다시 읽지 않는 것은 속도 때문만이 아니다. 계정이
    /// 지워지거나 이름이 바뀌면 <b>옛 쪽지의 보낸 이가 빈칸이 되거나 딴 이름이
    /// 된다.</b> 받은 편지에 적힌 이름은 받은 그때의 것이어야 한다.
    /// </remarks>
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

    /// <summary>
    /// 앱 푸시가 기기 한 대에라도 갔는가. <b>「왜 못 받았나」의 절반이 여기 있다.</b>
    /// </summary>
    [Column("push_sent")]
    public bool PushSent { get; set; }

    /// <summary>메일이 나갔는가.</summary>
    [Column("email_sent")]
    public bool EmailSent { get; set; }

    /// <summary>
    /// 두드림이 막힌 까닭. <b>사람이 읽는 짧은 말</b>로 적는다 — 보낸 사람이
    /// 보낸함에서 그대로 읽는다.
    /// </summary>
    /// <remarks>
    /// 쪽지 자체는 성공이고 알림만 막힌 경우가 대부분이다. 그것을 적어 두지 않으면
    /// 「보냈는데 상대가 못 봤다」의 원인을 누구도 되짚을 수 없다.
    /// </remarks>
    [Column("notify_note")]
    public string? NotifyNote { get; set; }

    /// <summary>보낸 사람이 자기 보낸함에서 치웠나.</summary>
    [Column("sender_deleted")]
    public bool SenderDeleted { get; set; }

    /// <summary>받은 사람이 자기 쪽지함에서 치웠나.</summary>
    [Column("receiver_deleted")]
    public bool ReceiverDeleted { get; set; }
}
