namespace NotificationServer.DTOs;

/// <summary>
/// 포털 「메일 보내기」가 보내는 한 통.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="SendEmailDto"/> 와 왜 갈랐나.</b> 저쪽은 <b>시스템이 사람에게</b>
/// 보내는 알림의 통로다 — 역할·아이디로 받는 사람을 풀고, 알림관리 정책과
/// 본인의 수신 설정을 거치고, 평문이면 회사 메일 틀을 입힌다. 이쪽은
/// <b>사람이 사람에게</b> 쓰는 편지라 그 셋이 전부 쓸모없거나 해롭다.
/// </para>
/// <para>
/// 특히 정책이 그렇다. 「이 이벤트는 이 역할만 받는다」는 규칙은 <b>자동으로
/// 나가는 알림</b>을 조이려고 있는 것인데, 사람이 주소를 쳐 보내는 편지에
/// 그것을 걸면 보낸 사람은 갔다고 믿고 받는 사람은 영영 못 받는다. 저쪽도
/// 주소를 직접 적은 몫(<c>to</c>)은 안 거른다 — 이 통로는 그 몫만 있다.
/// </para>
/// </remarks>
public class MailComposeDto
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 참조(CC). 여럿이면 쉼표로 잇는다. <b>숨은참조가 아니다</b> — 받는
    /// 사람 모두에게 보인다.
    /// </summary>
    public string? Cc { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문. 화면이 서식 편집기로 쓰므로 언제나 HTML 이다.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>붙일 파일들. 바이트를 그대로 싣는다(<see cref="EmailAttachmentDto"/>).</summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];

    /// <summary>AI 요약을 본문 머리에 넣고 보냈는가. 보관함이 그 사실을 적어 둔다.</summary>
    public bool HasAiSummary { get; set; }
}

/// <summary>메일 한 통을 보낸 결과.</summary>
/// <remarks>
/// <b>「보냈다」를 참·거짓 하나로 말하지 않는다.</b> 주소 몇 곳으로 갔는지,
/// 참조가 몇인지, 첨부가 몇인지를 함께 준다 — 보낸 사람이 가장 먼저 세어 보는
/// 것이 그 수이고, 토스트 한 줄에 그것이 없으면 보낸함을 열어 확인하게 된다.
/// </remarks>
public class MailSendResultDto
{
    /// <summary>보관함에 들어간 줄의 아이디. 실패해도 줄은 남으므로 늘 채워진다.</summary>
    public string Id { get; set; } = string.Empty;

    public bool Sent { get; set; }

    public int ToCount { get; set; }

    public int CcCount { get; set; }

    public int AttachmentCount { get; set; }

    public string? Message { get; set; }
}

/// <summary>보낸 메일함의 한 줄. <b>본문이 없다</b> — 목록은 훑는 자리다.</summary>
public class MailListItemDto
{
    public string Id { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public string To { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Subject { get; set; }

    /// <summary>본문에서 태그를 걷어낸 앞머리. 목록에서 글을 알아보는 데 쓴다.</summary>
    public string? Preview { get; set; }

    public int AttachmentCount { get; set; }

    public bool IsSuccess { get; set; }

    public string? FailureReason { get; set; }

    public bool HasAiSummary { get; set; }
}

/// <summary>보낸 메일 한 통을 다시 펼친 것. 본문 <b>전문</b>이 들어 있다.</summary>
public class MailDetailDto : MailListItemDto
{
    public string? Body { get; set; }

    public bool IsHtml { get; set; } = true;

    /// <summary>붙였던 파일 이름들. 파일 자체는 보관하지 않는다(머리말).</summary>
    public IReadOnlyList<string> AttachmentNames { get; set; } = [];

    public long AttachmentBytes { get; set; }

    public string? SenderName { get; set; }
}

/// <summary>
/// 내가 최근에 쓴 주소 하나. 새 메일을 쓸 때 바로 고를 수 있게 준다.
/// </summary>
/// <remarks>
/// <b>계정 목록과 갈래가 다르다.</b> 그쪽은 「우리 사람 전부」이고 이쪽은
/// 「내가 실제로 보낸 곳」이다 — 거래처처럼 계정이 없는 상대가 여기에만 있다.
/// </remarks>
public class MailAddressUseDto
{
    public string Address { get; set; } = string.Empty;

    /// <summary>그 주소를 쓴 횟수. 자주 쓰는 것이 위로 온다.</summary>
    public int UseCount { get; set; }

    public DateTime LastUsedAt { get; set; }
}
