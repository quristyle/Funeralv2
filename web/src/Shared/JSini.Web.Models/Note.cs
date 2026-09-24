namespace JSini.Web.Models;

// ============================================================
// 쪽지 — 사람에게서 사람에게로 가는 짧은 글.
//
// **`NotifySend.cs` 와 갈래가 다르다.** 그쪽은 「고른 사람들에게 두드림 한 번」이고
// 남는 것이 발송 기록뿐이다. 이쪽은 **글이 본체**라 두드림이 다 막혀도 쪽지함에
// 남고, 받는 사람을 목록에서 고르는 것이 아니라 **아이디나 이메일을 적어** 정한다.
//
// 칸 이름은 NotificationServer 의 DTO 와 맞춘다 — 봉투를 그대로 주고받는다.
// ============================================================

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
    /// <remarks>
    /// 쪽지는 적어도 앱 푸시로는 닿아야 성립한다 — 쪽지함에만 쌓이면 본인은 왔다는
    /// 것조차 모르는데 보낸 쪽은 보냈다고 믿는다. 찾기 목록에서 지우지 않고
    /// <b>못 보낸다고 미리</b> 말해 준다(지워 버리면 「아이디가 틀렸나」를 한참
    /// 의심하게 된다).
    /// </remarks>
    public bool PushEnabled { get; set; } = true;

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>
    /// 쪽지를 받을 길이 있는가. 둘 중 하나면 된다. <b>찾기 목록은 서버가 이미 이
    /// 값으로 걸러서 준다</b>(2026-09-24) — 화면은 한 번 더 거를 뿐이다.
    /// </summary>
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
}

/// <summary>
/// 쪽지 한 번 보낸 결과.
/// </summary>
/// <remarks>
/// <b>쪽지가 간 것과 두드림이 간 것을 갈라 담는다.</b> 쪽지함에는 들어갔는데
/// 푸시만 막히는 일이 흔해서(구독한 기기가 없다 · 본인이 껐다), 하나로 뭉쳐
/// 말하면 보낸 사람이 같은 쪽지를 한 번 더 보낸다.
/// </remarks>
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

    /// <summary>
    /// 찾기는 했으나 <b>푸시를 꺼 두어 쪽지를 받을 수 없는</b> 사람들.
    /// <see cref="Unknown"/> 과 갈라 담는다 — 「아이디가 틀렸다」와 「그 사람이
    /// 껐다」는 보내는 쪽이 할 일이 서로 다르다.
    /// </summary>
    public List<string> Blocked { get; set; } = [];

    /// <summary>두드림이 막힌 까닭. 다 갔으면 <c>null</c>.</summary>
    public string? NotifyNote { get; set; }
}

/// <summary>안 읽은 쪽지 수. 상단 띠의 봉투에 붙는 숫자다.</summary>
public sealed class NoteUnreadDto
{
    public int Unread { get; set; }
}
