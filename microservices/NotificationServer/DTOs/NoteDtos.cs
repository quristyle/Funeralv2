namespace NotificationServer.DTOs;

// ============================================================
// 쪽지 — 사람에게서 사람에게로 가는 짧은 글.
//
// **알림 DTO 와 갈래가 다르다.** 위쪽(`NotificationDtos.cs`)은 「누구에게 무엇을
// 두드릴까」의 모양이고, 여기는 「무엇을 주고받았나」의 모양이다. 쪽지는 두드림이
// 전부 막혀도 남아야 하는 글이라(Entities/Note.cs 머리말) 한 파일에 섞지 않는다.
// ============================================================

/// <summary>
/// 쪽지를 보낸다.
/// </summary>
public class SendNoteDto
{
    /// <summary>
    /// 받는 사람. <b>로그인 아이디와 이메일 주소를 섞어 적을 수 있다</b> —
    /// 여럿이면 쉼표(또는 세미콜론)로 잇는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 아이디만 받게 하면 「그 사람 아이디가 뭐였지」에서 막히고, 주소만 받게 하면
    /// 메일을 등록하지 않은 계정에게 못 보낸다. 둘 다 받아 서버가 푼다
    /// (<c>NoteRecipientResolver</c>) — <b>못 푼 것은 조용히 빼지 않고 이름을
    /// 짚어 돌려준다.</b>
    /// </para>
    /// </remarks>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 제목. <b>비워도 된다</b> — 그러면 서버가 「누가 언제 보냈다」로 지어 넣는다
    /// (<c>NoteEndpoints.DefaultTitle</c>).
    /// </summary>
    /// <remarks>
    /// 대부분의 쪽지는 한두 줄이라 제목이 내용과 같은 말이 된다. 제목을 받아야만
    /// 보낼 수 있게 두면 <b>같은 글자를 두 번 치게</b> 만드는 셈이라, 쓰는 화면도
    /// 이 칸을 접어 두고 필요한 사람만 펼친다.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}

/// <summary>쪽지를 받을 수 있는 사람 하나.</summary>
/// <remarks>
/// 보내기 전에 화면이 「이 사람이 맞나」를 확인하는 자리에 쓴다. 쪽지를 실제로
/// 보낼 때도 서버가 같은 규칙으로 다시 푼다 — <b>화면이 푼 결과를 믿지 않는다.</b>
/// </remarks>
public class NoteRecipientDto
{
    /// <summary>포털 로그인 아이디. 쪽지도 푸시도 이 값 하나로 간다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름.</summary>
    public string? Name { get; set; }

    /// <summary>회사 · 부서. 같은 이름이 둘일 때 가르는 값이다.</summary>
    public string? Affiliation { get; set; }

    /// <summary>
    /// 대표 이메일. <b>없으면 메일로는 두드릴 수 없다</b> — 화면이 그것을 미리
    /// 말해 주라고 내려 준다.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 푸시 알림을 켜 두었나. <b>거짓이면 이 사람에게는 쪽지를 보낼 수 없다.</b>
    /// </summary>
    /// <remarks>
    /// 쪽지는 <b>적어도 앱 푸시로는 닿아야</b> 성립한다. 푸시를 꺼 둔 사람에게
    /// 넣어 두면 쪽지함에만 쌓이고 본인은 왔다는 것조차 모른다 — 보낸 쪽은
    /// 보냈다고 믿는다. 그래서 목록에서 지우지 않고 <b>못 보낸다고 미리</b>
    /// 말해 준다(지워 버리면 「아이디가 틀렸나」를 한참 의심하게 된다).
    /// </remarks>
    public bool PushEnabled { get; set; } = true;

    /// <summary>
    /// 앱 푸시가 <b>실제로 닿는가</b> — 끄지 않았고(<see cref="PushEnabled"/>)
    /// 등록한 기기가 있다.
    /// </summary>
    /// <remarks>
    /// <see cref="PushEnabled"/> 만으로는 모른다. 설정 행이 없으면 「켜짐」이라
    /// 기기를 한 번도 등록하지 않은 사람도 참이 된다.
    /// </remarks>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>
    /// 쪽지를 받을 길이 있는가. <b>둘 중 하나면 된다</b> — 보내기와 찾기가 같은
    /// 값으로 가른다(2026-09-24).
    /// </summary>
    public bool CanReceive => PushReachable || EmailReachable;
}

/// <summary>쪽지 한 통(목록·읽기가 함께 쓴다).</summary>
public class NoteRowDto
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

    /// <summary>받는 사람이 읽었나. 보낸함에서도 이 값을 본다.</summary>
    public bool IsRead => ReadAt is not null;

    /// <summary>앱 푸시가 기기 한 대에라도 갔나.</summary>
    public bool PushSent { get; set; }

    /// <summary>메일이 나갔나.</summary>
    public bool EmailSent { get; set; }

    /// <summary>두드림이 막힌 까닭. 다 갔으면 <c>null</c>.</summary>
    public string? NotifyNote { get; set; }
}

/// <summary>
/// 보낸 결과.
/// </summary>
/// <remarks>
/// <b>쪽지가 간 것과 두드림이 간 것을 갈라 말한다.</b> 쪽지는 쪽지함에 들어갔는데
/// 푸시만 막히는 일이 흔하다(구독한 기기가 없다 · 본인이 껐다). 하나로 뭉쳐
/// 「보냈습니다」라고만 말하면 받는 사람이 자기 기기를 의심하고, 「실패했습니다」로
/// 말하면 이미 도착한 쪽지를 보낸 사람이 한 번 더 보낸다.
/// </remarks>
public class SendNoteResultDto
{
    /// <summary>쪽지함에 들어간 통 수. <b>이것이 0 일 때만 실패다.</b></summary>
    public int Sent { get; set; }

    /// <summary>푸시가 닿은 기기 수(사람 수가 아니다).</summary>
    public int PushDevices { get; set; }

    /// <summary>메일이 나간 사람 수.</summary>
    public int EmailSent { get; set; }

    /// <summary>
    /// 누구인지 풀지 못한 값들. <b>조용히 빼지 않는다</b> — 열 명에게 보낸 줄 알았는데
    /// 여덟만 받았다는 것을 그 자리에서 알려면 이 목록이 있어야 한다.
    /// </summary>
    public List<string> Unknown { get; set; } = [];

    /// <summary>실제로 받은 사람들(표시용).</summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>
    /// 찾기는 했으나 <b>푸시를 꺼 두어 쪽지를 받을 수 없는</b> 사람들.
    /// </summary>
    /// <remarks>
    /// <see cref="Unknown"/> 과 갈라 담는다. 「그런 아이디가 없다」면 보내는 쪽이
    /// 다시 칠 일이고, 「그 사람이 푸시를 껐다」면 <b>다른 길로 연락할 일</b>이다 —
    /// 한 자루에 담으면 둘을 구분할 수 없다.
    /// </remarks>
    public List<string> Blocked { get; set; } = [];

    /// <summary>두드림이 막힌 까닭. 다 갔으면 <c>null</c>.</summary>
    public string? NotifyNote { get; set; }

    public string? Message { get; set; }
}

/// <summary>안 읽은 쪽지 수. 상단 띠의 봉투에 붙는 숫자다.</summary>
public class NoteUnreadDto
{
    public int Unread { get; set; }
}
