namespace NotificationServer.DTOs;

/// <summary>계정 하나의 앱 현황을 한 봉투에 담는다. 구독·설정·발송 기록·쪽지를 함께 줘 까닭을 한 번에 찾게 한다.</summary>
public class OwnerAppStatusDto
{
    /// <summary>주인의 종류. 포털 계정은 <c>jsini</c> 다.</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 포털 계정이면 로그인 아이디다.</summary>
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>집계 기간(일). 아래 <see cref="Channels"/> · <see cref="Deliveries"/> 가 이 기간의 것이다.</summary>
    public int Days { get; set; }

    /// <summary>이 아이디가 실제 계정인가. 거짓이면 지워졌거나 없는 아이디다 — 구독·기록만 남은 경우가 있다.</summary>
    public bool AccountFound { get; set; }

    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DeptName { get; set; }

    /// <summary>서버가 푸시를 보낼 수 있는 상태인가(VAPID 키). 거짓이면 기기와 스위치를 아무리 맞춰도 푸시가 안 온다.</summary>
    public bool PushAvailable { get; set; }

    /// <summary>이 사람의 수신 설정. Saved 가 거짓이면 저장한 적이 없어 기본값이다.</summary>
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

/// <summary>길 하나 × 방향 하나의 집계. 줄 수는 사람·기기 단위다 — 몇 번 눌렀나가 아니라 몇 군데에 닿으려 했나를 센다.</summary>
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

    /// <summary>상대. 수신이면 보낸 사람, 발신이면 받은 사람이다. 시스템이 보낸 것은 비어 있다.</summary>
    public string? Counterpart { get; set; }

    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Url { get; set; }

    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>쪽지 현황. 쪽지는 글 자체가 본체라 발송 기록에 얹지 않고 따로 세므로 칸을 갈라 둔다.</summary>
public class AppNoteStatDto
{
    /// <summary>기간 안에 받은 쪽지.</summary>
    public int Received { get; set; }

    /// <summary>기간 안에 보낸 쪽지.</summary>
    public int Sent { get; set; }

    /// <summary>기간 안에 받은 것 중 메일로도 전달된 것. 본인이 쪽지 메일받기를 켰을 때만 늘어난다.</summary>
    public int MailForwarded { get; set; }

    /// <summary>아직 안 읽은 쪽지. 기간과 무관한 전체다 — 기간으로 자르면 가장 중요한 것이 먼저 사라진다.</summary>
    public int UnreadTotal { get; set; }

    public DateTime? LastReceivedAt { get; set; }
    public DateTime? LastSentAt { get; set; }
}
