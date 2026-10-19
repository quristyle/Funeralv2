namespace NotificationServer.DTOs;

// 쪽지 DTO — 주고받은 글의 모양이다. 알림 DTO(NotificationDtos.cs)는 따로 둔다.

/// <summary>쪽지를 보낸다.</summary>
public class SendNoteDto
{
    /// <summary>받는 사람. 로그인 아이디와 이메일을 섞어 쉼표나 세미콜론으로 잇는다. 서버가 푸는데 못 푼 것은 빼지 않고 이름을 짚어 돌려준다.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>제목. 비우면 서버가 「누가 언제 보냈다」로 지어 넣는다.</summary>
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}

/// <summary>쪽지를 받을 수 있는 사람 하나. 보내는 쪽은 이 결과를 믿지 않고 서버가 같은 규칙으로 다시 푼다.</summary>
public class NoteRecipientDto
{
    /// <summary>포털 로그인 아이디. 쪽지도 푸시도 이 값 하나로 간다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름.</summary>
    public string? Name { get; set; }

    /// <summary>회사 · 부서. 같은 이름이 둘일 때 가르는 값이다.</summary>
    public string? Affiliation { get; set; }

    /// <summary>대표 이메일. 없으면 메일로는 두드릴 수 없다.</summary>
    public string? Email { get; set; }

    /// <summary>푸시 알림을 켜 두었나. 거짓이면 쪽지를 보낼 수 없다 — 쪽지함에만 쌓이고 보낸 쪽만 보냈다고 믿게 된다.</summary>
    public bool PushEnabled { get; set; } = true;

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다. PushEnabled 만으로는 설정 행이 없을 때 참이 되어 모른다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>쪽지를 받을 길이 있는가. 앱 푸시나 쪽지 메일 둘 중 하나면 된다. 보내기와 찾기가 같은 값으로 가른다.</summary>
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

    /// <summary>앱 푸시가 기기에 실제로 닿은 때. PushSent(넘겼다)와 다르다 — 보낸함의 전달 칸이 이 값을 본다.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>푸시가 안 닿아 <b>대신 보낸 메일</b>이 나간 때. 안 보냈으면 <c>null</c>.</summary>
    public DateTime? FallbackEmailAt { get; set; }

    /// <summary>기기에 닿았나. 보낸함이 「전달됨」으로 그린다.</summary>
    public bool IsDelivered => DeliveredAt is not null;
}

/// <summary>보낸 결과. 쪽지함에 들어갔는지와 두드림이 갔는지를 갈라 말한다 — 하나로 뭉치면 틀린 쪽을 의심하게 된다.</summary>
public class SendNoteResultDto
{
    /// <summary>쪽지함에 들어간 통 수. <b>이것이 0 일 때만 실패다.</b></summary>
    public int Sent { get; set; }

    /// <summary>푸시가 닿은 기기 수(사람 수가 아니다).</summary>
    public int PushDevices { get; set; }

    /// <summary>메일이 나간 사람 수.</summary>
    public int EmailSent { get; set; }

    /// <summary>풀지 못한 값들. 조용히 빼지 않는다 — 몇 명이 빠졌는지 그 자리에서 알려야 한다.</summary>
    public List<string> Unknown { get; set; } = [];

    /// <summary>실제로 받은 사람들(표시용).</summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>찾기는 했으나 푸시를 꺼 두어 쪽지를 받을 수 없는 사람들. Unknown 과 갈라 담는다 — 다시 칠 것인지 다른 길로 연락할 것인지가 다르다.</summary>
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

/// <summary>빠른 선택에 내놓는 사람들. 아무것도 치지 않은 순간의 화면이다. 최근은 한 줄짜리 딱지로, 전체는 접어 두었다가 펴서 나눈다.</summary>
public class NoteQuickPickDto
{
    /// <summary>최근에 쪽지를 보낸 사람들. 가장 가까운 순이고, 지금도 받을 길이 있는 사람만 남긴다.</summary>
    public List<NoteRecipientDto> Recent { get; set; } = [];

    /// <summary>쪽지를 받을 수 있는 사람 전부(이름순). 상한을 둔다 — 모두가 기기를 등록하는 날 목록이 통째로 내려오면 창이 늦게 열린다.</summary>
    public List<NoteRecipientDto> All { get; set; } = [];

    /// <summary><see cref="All"/> 이 상한에서 잘렸나. 화면이 「더 있다」를 말한다.</summary>
    public bool AllTruncated { get; set; }
}

/// <summary>앱 푸시가 기기에 닿았다고 서비스워커가 알려 온다. 이 길만 익명이고 쪽지 아이디(GUID)가 열쇠다 — 새어도 잃는 것은 전환 메일 한 통이다.</summary>
public class NoteDeliveredDto
{
    public string NoteId { get; set; } = string.Empty;
}
