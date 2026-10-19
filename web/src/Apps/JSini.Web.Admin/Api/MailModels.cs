namespace JSini.Web.Admin.Api;

// ── 메일 보내기 ─────────────────────────────────────────────
// 이름과 칸은 NotificationServer 의 `MailComposeDto`·`MailListItemDto` 와 맞춘 것이다. **어긋나도 예외가 안 난다** — 서버가 못
// 읽은 칸을 기본값으로 두고 지나가므로, 증상이 「보냈다는데 참조가 비어 있다」로만 보인다.

/// <summary>보낼 메일 한 통.</summary>
public sealed class MailComposeRequest
{
    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>참조. 숨은참조가 아니다 — 받는 사람 모두에게 보인다.</summary>
    public string? Cc { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>본문. 서식 편집기가 만든 HTML 이다.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>붙일 파일들. 비면 본문만 나간다.</summary>
    public List<EmailAttachmentDto> Attachments { get; set; } = [];

    /// <summary>AI 요약을 본문 머리에 넣고 보냈는가. 보낸함이 그 사실을 적어 둔다.</summary>
    public bool HasAiSummary { get; set; }
}

/// <summary>메일 한 통을 보낸 결과.</summary>
public sealed class MailSendResult
{
    public string Id { get; set; } = string.Empty;
    public bool Sent { get; set; }
    public int ToCount { get; set; }
    public int CcCount { get; set; }
    public int AttachmentCount { get; set; }
    public string? Message { get; set; }
}

/// <summary>보낸 메일함의 한 줄. 본문이 없다 — 목록은 훑는 자리다.</summary>
public class MailListItem
{
    public string Id { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Subject { get; set; }

    /// <summary>태그를 걷어낸 본문 앞머리.</summary>
    public string? Preview { get; set; }

    public int AttachmentCount { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
    public bool HasAiSummary { get; set; }
}

/// <summary>보낸 메일 한 통을 펼친 것. 본문 전문이 들어 있다.</summary>
public sealed class MailDetail : MailListItem
{
    public string? Body { get; set; }
    public bool IsHtml { get; set; } = true;

    /// <summary>붙였던 파일 이름들. 파일 자체는 보관하지 않는다.</summary>
    public List<string> AttachmentNames { get; set; } = [];

    public long AttachmentBytes { get; set; }
    public string? SenderName { get; set; }
}

/// <summary>내가 최근에 쓴 주소 하나.</summary>
public sealed class MailAddressUse
{
    public string Address { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public DateTime LastUsedAt { get; set; }
}
