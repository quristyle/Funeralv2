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
    /// <summary>웹푸시로 간 줄.</summary>
    public const string ChannelPush = "push";

    /// <summary>
    /// 메일로 간 줄. <b>받는 사람 하나가 한 줄</b>이다 — 한 통을 셋에게 보내면 셋이다.
    /// </summary>
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

    /// <summary>
    /// <b>알림구분</b>. 공통코드 묶음 <c>NOTI_CATEGORY</c> 의 코드값이다
    /// (<c>DEPLOY</c> · <c>HELPDESK</c> · <c>NOTICE</c> …).
    /// </summary>
    /// <remarks>
    /// <para>
    /// [제목으로는 못 거른다]
    /// </para>
    ///
    /// <para>
    /// 「배포 알림만 보자」를 제목 부분일치로 하고 있으면, 제목 문구를 한 번
    /// 다듬는 순간 옛 줄과 새 줄이 갈라진다. <b>보낸 쪽이 자기 갈래를
    /// 적어 두는 것</b>이 조회의 유일한 단단한 근거다.
    /// </para>
    ///
    /// <para>
    /// 값의 목록은 코드가 아니라 <b>공통코드가 갖는다</b>
    /// (<see cref="JSini.Shared.DTOs.PushCategories"/> 머리말). 여기 적힌 값이
    /// 공통코드에 없어도 저장은 되고, 화면에는 코드값 그대로 뜬다.
    /// </para>
    ///
    /// <para>
    /// <b>구분을 붙이기 전에 쌓인 줄은 비어 있다</b>(<c>null</c>). 채워 넣을
    /// 길이 없다 — 그 줄에는 어디서 보냈는지가 아무 데도 안 남아 있다.
    /// 화면은 그것을 「구분 없음」으로 모아 본다.
    /// </para>
    /// </remarks>
    [Column("category")]
    public string? Category { get; set; }

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

    /// <summary>
    /// <b>한 번 보낸 것</b>을 묶는 열쇠. 발송 한 번에 하나 만들어 그때 생긴
    /// 모든 줄에 같은 값을 넣는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 표의 줄은 <b>기기 단위</b>다. 그런데 「내 알림함」은 사람이 보는
    /// 화면이라 <b>메시지 단위</b>여야 한다 — 없으면 기기 둘을 쓰는 사람에게
    /// 같은 알림이 두 줄로 보이고, 하나만 읽음 처리하면 나머지가 남는다.
    /// </para>
    ///
    /// <para>
    /// 옛 줄에는 없다(<c>null</c>). 그때는 줄 자체를 열쇠로 삼는다 —
    /// 알림함이 그 갈래를 살핀다.
    /// </para>
    /// </remarks>
    [Column("batch_id")]
    public string? BatchId { get; set; }

    /// <summary>
    /// 받은 사람이 읽은 때(UTC). 안 읽었으면 <c>null</c>.
    ///
    /// <para>
    /// <b>기기가 아니라 사람의 상태다.</b> 그래서 읽음 처리는 그 묶음
    /// (<see cref="BatchId"/>)에 딸린 그 사람의 줄을 <b>전부</b> 찍는다 —
    /// 한 줄만 찍으면 다른 기기 줄이 안 읽은 채로 남는다.
    /// </para>
    /// </summary>
    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 이 알림이 <b>실제로 띄운 아이콘 주소</b>. 대개 시킨 사람의 프로필 사진이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [왜 남겨야 했나 — 알림함에서는 되짚을 수가 없다]
    /// </para>
    ///
    /// <para>
    /// 아이콘은 부르는 쪽이 실은 사람의 아이디(<c>IconOwnerKey</c>)로 지목하고
    /// 발송 직전에 얼굴로 풀린다(<c>PushSender.FillIconAsync</c>). 그 아이디도
    /// 풀린 주소도 어디에도 안 남아서, <b>알림함은 그 얼굴을 다시 만들 길이
    /// 없었다.</b>
    /// </para>
    ///
    /// <para>
    /// <see cref="SentBy"/> 로 대신할 수 없다. 둘은 다른 것을 가리킨다 —
    /// 「보낸 주체」와 「누가 시킨 일인가」다. 가장 잦은 AI 작업 알림이 바로
    /// 그 경우로, <c>sent_by</c> 에는 사람이 아니라 <c>AI_TASK</c> 라는 글자가
    /// 들어 있고 얼굴의 주인은 작업을 지시한 사람이다.
    /// </para>
    ///
    /// <para>
    /// [열쇠(<c>?t=</c>)는 떼고 담는다]
    /// </para>
    ///
    /// <para>
    /// 알림으로 나가는 주소에는 그 사진 한 장을 여는 <b>한 시간짜리 열쇠</b>가
    /// 붙어 있다(<c>AvatarIconTokenFactory</c>) — 로그인해 있지 않은 기기로도
    /// 알림이 가기 때문이다. 그것을 그대로 담으면 <b>알림함의 얼굴이 한 시간
    /// 뒤에 조용히 그림자로 바뀐다.</b> 셸의 중계가 열쇠를 <c>Bearer</c> 로
    /// 올려 보내면서 <b>지금 보는 사람의 신원을 대신하기</b> 때문이고, 시효가
    /// 지난 열쇠는 401 이다. 로그인해서 보고 있어도 그렇다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <c>PushSender.IconForLog</c> 가 떼고 담는다. 열쇠 없는 그 주소는
    /// <b>보는 사람의 신원</b>으로 열리고, 알림함을 보는 사람은 언제나 로그인해
    /// 있다. 그러니 이 칸은 「푸시가 쓴 그림」이지 「푸시가 쓴 글자 그대로」는
    /// 아니다.
    /// </para>
    ///
    /// <para>
    /// 지목하지 않은 알림(배포 알림 등)은 <c>null</c> 이다. 그때 무엇을 그릴지는
    /// 보는 쪽이 정한다 — 서비스워커는 앱 아이콘을, 알림함은 갈래 그림을 쓴다.
    /// </para>
    /// </remarks>
    [Column("icon")]
    public string? Icon { get; set; }
}
