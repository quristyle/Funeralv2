namespace JSini.Web.Models;

// 쪽지. 사람에게서 사람에게로 가는 짧은 글. NotifySend 와 달리 글이 본체라 두드림이 막혀도 쪽지함에 남고, 받는 사람을 아이디나 이메일로 정한다.
// 칸 이름은 NotificationServer 의 DTO 와 맞춘다.

/// <summary>쪽지를 받을 수 있는 사람 하나. 찾기 결과의 한 줄이다.</summary>
public sealed class NoteRecipientDto
{
    /// <summary>포털 로그인 아이디. 쪽지도 푸시도 이 값 하나로 간다.</summary>
    public string LoginId { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>부서. 같은 이름이 둘일 때 가르는 값이다.</summary>
    public string? Affiliation { get; set; }

    /// <summary>
    /// 대표 이메일. <b>비어 있으면 메일로는 두드릴 수 없다</b> — 화면이 그것을
    /// 미리 말해 준다.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 푸시 알림을 켜 두었나. <b>거짓이면 이 사람에게는 쪽지를 보낼 수 없다.</b>
    /// </summary>
    /// <remarks>쪽지는 앱 푸시로 닿아야 성립한다. 찾기 목록에서 지우지 않고 못 보낸다고 미리 말해 준다(지우면 아이디가 틀렸나 하고 의심하게 된다).</remarks>
    public bool PushEnabled { get; set; } = true;

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>쪽지를 받을 길이 있는가(둘 중 하나면 된다). 찾기 목록은 서버가 이 값으로 걸러 준다.</summary>
    public bool CanReceive => PushReachable || EmailReachable;

    /// <summary>목록에 적는 한 줄. 이름이 없으면 아이디를 쓴다.</summary>
    public string Display => string.IsNullOrWhiteSpace(Name) ? LoginId : $"{Name} ({LoginId})";
}

/// <summary>쪽지 한 통. 받은함과 보낸함이 같은 모양을 쓴다.</summary>
public sealed class NoteDto
{
    public string Id { get; set; } = string.Empty;

    public string SenderKey { get; set; } = string.Empty;
    public string? SenderName { get; set; }

    public string ReceiverKey { get; set; } = string.Empty;
    public string? ReceiverName { get; set; }

    public string? Title { get; set; }
    public string? Body { get; set; }

    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 받는 사람이 읽었나. <b>보낸함에서도 이 값을 본다</b> — 「보냈는데 봤나」가
    /// 보낸 사람이 가장 먼저 묻는 것이다.
    /// </summary>
    public bool IsRead { get; set; }

    public bool PushSent { get; set; }
    public bool EmailSent { get; set; }

    /// <summary>두드림이 막힌 까닭. 다 갔으면 <c>null</c>.</summary>
    public string? NotifyNote { get; set; }

    /// <summary>앱 푸시가 받는 사람의 기기에 실제로 닿은 때(UTC). 서비스워커가 되알려 준 값이다. PushSent(서버가 푸시 서비스에 넘긴 때)와 다르다. 기기가 꺼져 있으면 수명까지 들고 있다가 버린다.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>푸시가 안 닿아 대신 나간 메일의 때(UTC). EmailSent(보낼 때 함께 나간 메일)와 다르고, 배치가 나중에 보낸 것이다.</summary>
    public DateTime? FallbackEmailAt { get; set; }

    /// <summary>기기에 닿았나. 보낸함의 「전달」 칸이 이 값을 그린다.</summary>
    public bool IsDelivered { get; set; }
}

    /// <summary><b>빠른 선택</b>에 내놓는 사람들. 최근 보낸 사람(지금도 받을 수 있는 사람만)과 전체를 나눠 담는다. 최근은 딱지로, 전체는 접어 두었다가 펼친다.</summary>
public sealed class NoteQuickPickDto
{
    /// <summary>최근에 쪽지를 보낸 사람들(가까운 순). <b>지금도 받을 수 있는 사람만</b>.</summary>
    public List<NoteRecipientDto> Recent { get; set; } = [];

    /// <summary>쪽지를 받을 수 있는 사람 전부(이름순).</summary>
    public List<NoteRecipientDto> All { get; set; } = [];

    /// <summary><see cref="All"/> 이 상한에서 잘렸나. 참이면 화면이 「더 있다」를 말한다.</summary>
    public bool AllTruncated { get; set; }
}

    /// <summary>쪽지 한 번 보낸 결과. 쪽지함 저장과 두드림을 갈라 담는다. 쪽지함에는 들어갔는데 푸시만 막히는 일이 흔하므로 하나로 뭉치면 같은 쪽지를 또 보내게 된다.</summary>
public sealed class NoteSendResultDto
{
    /// <summary>쪽지함에 들어간 통 수. <b>이것이 0 일 때만 실패다.</b></summary>
    public int Sent { get; set; }

    /// <summary>푸시가 닿은 기기 수(사람 수가 아니다).</summary>
    public int PushDevices { get; set; }

    /// <summary>메일이 나간 사람 수.</summary>
    public int EmailSent { get; set; }

    /// <summary>누구인지 풀지 못한 값들. <b>조용히 빠지지 않는다.</b></summary>
    public List<string> Unknown { get; set; } = [];

    /// <summary>실제로 받은 사람들(표시용).</summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>찾기는 했으나 푸시를 꺼 두어 쪽지를 받을 수 없는 사람들. Unknown(아이디 없음)과 갈라 담는다. 보내는 쪽이 할 일이 다르다.</summary>
    public List<string> Blocked { get; set; } = [];

    /// <summary>두드림이 막힌 까닭. 다 갔으면 <c>null</c>.</summary>
    public string? NotifyNote { get; set; }
}

/// <summary>안 읽은 쪽지 수. 상단 띠의 봉투에 붙는 숫자다.</summary>
public sealed class NoteUnreadDto
{
    public int Unread { get; set; }
}
