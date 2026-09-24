namespace NotificationServer.DTOs;

/// <summary>
/// 계정 <b>하나</b>의 앱 관련 현황을 한 봉투에 담은 것.
/// </summary>
/// <remarks>
/// <para>
/// [왜 한 번에 주나]
/// </para>
///
/// <para>
/// 「저 사람만 알림이 안 온다」를 알아보려면 지금까지 표 넷을 따로 뒤져야 했다 —
/// 구독(<c>push_subscriptions</c>) · 설정(<c>notification_preferences</c>) ·
/// 발송 기록(<c>push_send_logs</c>) · 쪽지(<c>notes</c>). 그런데 까닭은 거의 언제나
/// <b>그 넷의 관계</b>에 있다. 기기가 0 대인가, 스위치를 껐는가, 보냈는데 실패했는가,
/// 아예 보낸 적이 없는가. 따로 부르면 화면이 그 넷을 다시 맞춰야 하고, 왕복도 넷이다.
/// </para>
///
/// <para>
/// [계정 관리 화면의 칸 넷과 무엇이 다른가]
/// </para>
///
/// <para>
/// 그쪽(<see cref="OwnerNotificationStateDto"/>)은 <b>여러 사람</b>을 훑는 자리라
/// 기기를 수 하나로 줄이고 스위치 넷만 낸다. 이쪽은 <b>한 사람</b>을 파는 자리라
/// 기기 목록과 실제 주고받은 기록까지 싣는다.
/// </para>
/// </remarks>
public class OwnerAppStatusDto
{
    /// <summary>주인의 종류. 포털 계정은 <c>jsini</c> 다.</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 포털 계정이면 로그인 아이디다.</summary>
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>집계 기간(일). 아래 <see cref="Channels"/> · <see cref="Deliveries"/> 가 이 기간의 것이다.</summary>
    public int Days { get; set; }

    /// <summary>
    /// 이 아이디가 실제 계정인가. 거짓이면 <b>지워졌거나 없는 아이디</b>다 —
    /// 구독·기록만 남아 있는 경우가 있어 굳이 가려 말한다.
    /// </summary>
    public bool AccountFound { get; set; }

    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DeptName { get; set; }

    /// <summary>
    /// 서버가 푸시를 보낼 수 있는 상태인가(VAPID 키). <b>거짓이면 이 사람이
    /// 무엇을 해도 푸시가 안 온다</b> — 기기와 스위치만 보면 알 수 없는 갈래다.
    /// </summary>
    public bool PushAvailable { get; set; }

    /// <summary>
    /// 이 사람의 수신 설정. <see cref="NotificationPreferenceDto.Saved"/> 가 거짓이면
    /// 한 번도 저장한 적이 없어 <b>기본값</b>이라는 뜻이다.
    /// </summary>
    public NotificationPreferenceDto Preference { get; set; } = new();

    /// <summary>구독한 기기들. 비어 있으면 푸시를 켜 두어도 도착할 곳이 없다.</summary>
    public List<PushDeviceDto> Devices { get; set; } = new();

    /// <summary>길(푸시·메일) × 방향(수신·발신)마다 한 줄.</summary>
    public List<AppChannelStatDto> Channels { get; set; } = new();

    /// <summary>쪽지 주고받은 현황.</summary>
    public AppNoteStatDto Notes { get; set; } = new();

    /// <summary>최근 주고받은 기록(수신·발신을 한 줄기로 섞어 시각 역순).</summary>
    public List<AppDeliveryRowDto> Deliveries { get; set; } = new();
}

/// <summary>
/// 길 하나 × 방향 하나의 집계.
/// </summary>
/// <remarks>
/// <b>줄 수는 사람·기기 단위다.</b> 한 번 보낸 알림이 기기 두 대로 가면 두 줄이고,
/// 메일 한 통을 셋에게 보내면 세 줄이다 — 「몇 번 눌렀나」가 아니라 <b>「몇 군데에
/// 닿으려 했나」</b>를 센다. 안 닿은 까닭을 찾는 자리라 그쪽이 맞다.
/// </remarks>
public class AppChannelStatDto
{
    /// <summary><c>push</c> · <c>email</c>.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary><c>received</c>(이 사람에게 간 것) · <c>sent</c>(이 사람이 보낸 것).</summary>
    public string Direction { get; set; } = string.Empty;

    public int Total { get; set; }
    public int Success { get; set; }

    /// <summary>못 간 줄. <see cref="Total"/> − <see cref="Success"/> 다.</summary>
    public int Failure { get; set; }

    /// <summary>읽음이 찍힌 줄. 메일은 읽음을 알 길이 없어 늘 0 이다.</summary>
    public int Read { get; set; }

    public DateTime? LastAt { get; set; }
}

/// <summary>주고받은 기록 한 줄.</summary>
public class AppDeliveryRowDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }

    /// <summary><c>push</c> · <c>email</c>.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary><c>received</c> · <c>sent</c>.</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// 상대. 수신이면 <b>보낸 사람</b>, 발신이면 <b>받은 사람</b>이다.
    /// 시스템이 보낸 것은 비어 있다.
    /// </summary>
    public string? Counterpart { get; set; }

    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Url { get; set; }

    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// 쪽지 현황.
/// </summary>
/// <remarks>
/// <b>푸시·메일과 표가 다르다.</b> 쪽지는 글 자체가 본체라 발송 기록에 얹지 않고
/// 따로 산다(<c>scom.notes</c> 머리말). 그래서 여기서도 칸을 갈라 둔다.
/// </remarks>
public class AppNoteStatDto
{
    /// <summary>기간 안에 받은 쪽지.</summary>
    public int Received { get; set; }

    /// <summary>기간 안에 보낸 쪽지.</summary>
    public int Sent { get; set; }

    /// <summary>
    /// 기간 안에 받은 것 중 <b>메일로도 전달된</b> 것. 본인이 「쪽지를 메일로도
    /// 받기」를 켜 두었을 때만 늘어난다.
    /// </summary>
    public int MailForwarded { get; set; }

    /// <summary>
    /// 아직 안 읽은 쪽지. <b>기간과 무관한 전체</b>다 — 안 읽은 것은 오래될수록
    /// 문제라, 기간으로 자르면 가장 중요한 것이 먼저 사라진다.
    /// </summary>
    public int UnreadTotal { get; set; }

    public DateTime? LastReceivedAt { get; set; }
    public DateTime? LastSentAt { get; set; }
}
