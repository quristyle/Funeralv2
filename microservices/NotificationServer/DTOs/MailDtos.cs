namespace NotificationServer.DTOs;

/// <summary>포털 「메일 보내기」가 보내는 한 통. 시스템 알림 정책을 걸지 않는다 — 사람이 주소를 쳐 보낸 편지에 그 정책을 걸면 보낸 사람은 갔다고 믿고 받는 사람은 못 받는다.</summary>
public class MailComposeDto
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>참조(CC). 여럿이면 쉼표로 잇는다. 숨은참조가 아니라 받는 사람 모두에게 보인다.</summary>
    public string? Cc { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문. 화면이 서식 편집기로 쓰므로 언제나 HTML 이다.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>붙일 파일들. 바이트를 그대로 싣는다(<see cref="EmailAttachmentDto"/>).</summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];

    /// <summary>AI 요약을 본문 머리에 넣고 보냈는가. 보관함이 그 사실을 적어 둔다.</summary>
    public bool HasAiSummary { get; set; }
}

/// <summary>메일 한 통을 보낸 결과. 참·거짓 하나로 말하지 않고 주소 몇 곳, 참조, 첨부 수를 함께 준다.</summary>
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

/// <summary>내가 최근에 쓴 주소 하나. 계정 목록과 달리 내가 실제로 보낸 곳이라 계정 없는 거래처가 여기에만 있다.</summary>
public class MailAddressUseDto
{
    public string Address { get; set; } = string.Empty;

    /// <summary>그 주소를 쓴 횟수. 자주 쓰는 것이 위로 온다.</summary>
    public int UseCount { get; set; }

    public DateTime LastUsedAt { get; set; }
}
