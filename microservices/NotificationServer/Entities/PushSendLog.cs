using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationServer.Entities;

/// <summary>
/// 푸시 발송 기록 한 줄. <b>대상 한 명 × 기기 하나 = 한 줄</b>이다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 만들었나 — 보낸 것이 아무 데도 안 남았다]
/// </para>
///
/// <para>
/// 이 서비스는 구독 표만 만졌다(<c>last_sent_at</c> · <c>failure_count</c>).
/// 그래서 포털관리의 「메시지 발송」으로 보낸 알림이 <b>현황·발송 이력 화면에
/// 한 줄도 안 보였다.</b> 그 화면들은 헬프데스크 DB 의 <c>push_notification_logs</c>
/// 를 읽는데, 그 표에 쓰는 것은 헬프데스크 자신의 발송 코드뿐이다.
/// </para>
///
/// <para>
/// 남의 서비스 표에 끼어 쓰지 않는다 — DB 가 다르고(헬프데스크는 <c>helpdesk</c>),
/// 그렇게 하면 발송하는 서비스가 헬프데스크의 스키마 변경에 묶인다.
/// <b>보낸 쪽이 자기 기록을 갖는다.</b>
/// </para>
///
/// <para>
/// [못 보낸 것도 남긴다]
/// </para>
///
/// <para>
/// 성공·실패만 남기면 <b>「왜 안 갔나」에 답할 수 없다.</b> 실제로 가장 흔한 것이
/// 「그 사람은 구독한 기기가 없다」와 「본인이 알림을 껐다」인데, 그 둘은 발송
/// 시도조차 일어나지 않아 아무 흔적이 없다. 그래서 그 경우에도 줄을 남기고
/// <see cref="FailureReason"/> 에 까닭을 적는다 — 화면에서 사유로 거르면
/// 곧바로 보인다.
/// </para>
///
/// <para>
/// [지우는 규칙은 아직 없다]
/// </para>
///
/// <para>
/// 한 번 보낼 때마다 대상 수만큼 줄이 쌓인다. 옛 헬프데스크 DB 에 31,814 줄이
/// 있었으니 이 속도라면 몇 해는 괜찮지만, <b>정리 규칙이 없다는 것은 적어 둔다</b> —
/// 필요해지면 기간으로 지우는 것이 맞다(대상·본문까지 들고 있어 개인정보 성격이 있다).
/// </para>
/// </remarks>
[Table("push_send_logs", Schema = "scom")]
public class PushSendLog
{
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

    /// <summary>
    /// 보낸 길. 지금은 <c>push</c> 뿐이다.
    ///
    /// <para>
    /// 칸을 미리 둔 이유는 <b>같은 화면에서 이메일·문자·카카오도 보내기 때문</b>이다
    /// (「메시지 발송」의 탭 넷). 그쪽이 붙을 때 표를 새로 만들면 「보낸 기록」이
    /// 다시 갈라진다.
    /// </para>
    /// </summary>
    [Column("channel")]
    public string Channel { get; set; } = "push";

    /// <summary>받는 이의 종류(<c>jsini</c> · <c>helpdesk-admin</c> …).</summary>
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>받는 이(포털이면 로그인 아이디).</summary>
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>
    /// 보낸 기기(푸시 구독의 endpoint). <b>못 보낸 줄에는 없다</b> —
    /// 구독이 없거나 수신을 꺼서 시도 자체를 안 한 경우다.
    /// </summary>
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

    /// <summary>
    /// 못 보낸 까닭. <b>사람이 읽는 짧은 말</b>로 적는다 — 화면에서 이 값으로
    /// 거르고 묶어 세므로, 예외 메시지를 그대로 넣으면 같은 원인이 열 가지로
    /// 흩어진다(<see cref="Services.PushSender"/> 의 <c>Reason*</c> 상수).
    /// </summary>
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    /// <summary>보낸 사람(포털 로그인 아이디). 시스템이 보낸 것은 비어 있다.</summary>
    [Column("sent_by")]
    public string? SentBy { get; set; }
}
