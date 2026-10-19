using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationServer.Entities;

/// <summary>푸시 발송 기록 한 줄. <b>대상 한 명 × 기기 하나 = 한 줄</b>이다. 못 보낸 줄도 남긴다(사유를 묶어 보려고).</summary>
[Table("push_send_logs", Schema = "scom")]
public class PushSendLog
{
    /// <summary>웹푸시로 간 줄.</summary>
    public const string ChannelPush = "push";

    /// <summary>메일로 간 줄. <b>받는 사람 하나가 한 줄</b>이다.</summary>
    public const string ChannelEmail = "email";

    public PushSendLog()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Key]
    [Column("id")]
    public string Id { get; set; }

    /// <summary>보낸 때(UTC). 목록의 기본 정렬이고 기간 조건이 이 값을 본다.</summary>
    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>보낸 길. 지금은 push 뿐이다. 이메일·문자·카카오도 같은 표를 쓰려고 칸을 미리 둔다.</summary>
    [Column("channel")]
    public string Channel { get; set; } = "push";

    /// <summary>알림구분(공통코드 NOTI_CATEGORY 의 값). 제목으로는 거르지 않는다. 구분 전에 쌓인 줄은 null 이다.</summary>
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>받는 이의 종류(<c>jsini</c> · <c>helpdesk-admin</c> …).</summary>
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>받는 이(포털이면 로그인 아이디).</summary>
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>보낸 기기(푸시 구독 endpoint). 구독이 없거나 수신을 꺼서 시도조차 안 한 줄에는 없다.</summary>
    [Column("endpoint")]
    public string? Endpoint { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    /// <summary>눌렀을 때 열리는 주소. 무엇을 보냈는지 되짚을 때 제목보다 정확하다.</summary>
    [Column("url")]
    public string? Url { get; set; }

    [Column("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>못 보낸 까닭. 사람이 읽는 짧은 말로 적는다(PushSender 의 Reason* 상수).</summary>
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    /// <summary>보낸 사람(포털 로그인 아이디). 시스템이 보낸 것은 비어 있다.</summary>
    [Column("sent_by")]
    public string? SentBy { get; set; }

    /// <summary>한 번 보낸 것을 묶는 열쇠. 발송 한 번에 하나 만들어 같은 값을 넣는다. 옛 줄은 null 이다.</summary>
    [Column("batch_id")]
    public string? BatchId { get; set; }

    /// <summary>받은 사람이 읽은 때(UTC). 안 읽었으면 null. 사람의 상태라 같은 묶음의 그 사람 줄을 전부 찍는다.</summary>
    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    /// <summary>받는 사람이 알림함에서 치운 때(UTC). 읽음과 다르다. 줄은 지우지 않는다 — 발송 통계가 함께 줄기 때문이다.</summary>
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    /// <summary>이 알림이 실제로 띄운 아이콘 주소. 열쇠(?t=)는 떼고 담아야 알림함에서 보는 사람의 신원으로 열린다.</summary>
    [Column("icon")]
    public string? Icon { get; set; }
    /// <summary>기기에 실제로 닿은 때(UTC). 서비스워커가 되알려 줄 때만 찍힌다. 푸시 서비스가 받았다는 것과는 다르다.</summary>
    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }
}
