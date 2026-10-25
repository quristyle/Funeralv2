namespace AIAgentServer.Services.Gmail;

/// <summary>메일을 받아 오는 계정 하나. 길(<see cref="Kind"/>)에 따라 증분을 이어받는 표식이 다르다 —
/// Gmail API 는 <see cref="LastHistoryId"/>, IMAP 은 <see cref="LastUid"/> 를 쓴다.</summary>
public class GmailAccountRow
{
    /// <summary>'imrbiz' · 'personal' 처럼 사람이 읽는 짧은 열쇠.</summary>
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>이 메일함의 주인(포털 로그인 아이디). 업무 비서가 남의 메일을 못 보게 좁히는 열쇠다.</summary>
    public string OwnerLoginId { get; set; } = string.Empty;

    /// <summary><c>api</c>(Gmail API) 또는 <c>imap</c>.</summary>
    public string Kind { get; set; } = "api";

    public bool Enabled { get; set; } = true;

    public string? LastHistoryId { get; set; }

    public long? LastUid { get; set; }

    /// <summary>IMAP 서버가 이 값을 바꾸면 <see cref="LastUid"/> 는 쓸 수 없다 — 전체를 다시 받아야 한다.</summary>
    public long? UidValidity { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>받아 온 메일 한 통. 길 셋(<c>takeout</c> · <c>api</c> · <c>imap</c>)이 같은 표에 쌓이고,
/// 겹침은 <see cref="Rfc822MessageId"/> 로 지운다 — 제공자 번호는 길마다 달라서 못 쓴다.</summary>
public class GmailMessageRow
{
    public long Id { get; set; }

    public string AccountId { get; set; } = string.Empty;

    /// <summary>어느 길로 들어왔나. <c>takeout</c> · <c>api</c> · <c>imap</c>.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gmail 메시지 id 또는 IMAP UID. <b>길 안에서만 고유하다</b> — 겹침 판정에 쓰면 안 된다.</summary>
    public string? ProviderMsgId { get; set; }

    public string? ThreadId { get; set; }

    /// <summary>헤더의 <c>Message-ID</c>. 보낸 서버가 붙이고 길이 달라져도 안 바뀐다. 드물게 없는 메일이 있다.</summary>
    public string? Rfc822MessageId { get; set; }

    public string? Subject { get; set; }

    public string? FromAddr { get; set; }

    public string? FromName { get; set; }

    /// <summary>JSON 배열 글.</summary>
    public string ToAddrs { get; set; } = "[]";

    /// <summary>JSON 배열 글.</summary>
    public string CcAddrs { get; set; } = "[]";

    /// <summary>헤더의 <c>Date</c>. <b>UTC 다.</b> 헤더가 깨졌으면 <c>null</c>.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>JSON 배열 글. Takeout 은 <c>X-Gmail-Labels</c>, API 는 labelIds 에서 온다.</summary>
    public string Labels { get; set; } = "[]";

    public string? Snippet { get; set; }

    public string? BodyText { get; set; }

    public string? BodyHtml { get; set; }

    public bool HasAttachments { get; set; }

    /// <summary>JSON 배열 글. 이름·형식·크기만 담고 실물 바이트는 받지 않는다.</summary>
    public string Attachments { get; set; } = "[]";

    /// <summary>RAG 문서함으로 옮긴 뒤의 문서 번호. 찬 줄은 <see cref="BodyHtml"/> 을 비워 자리를 아낄 수 있다.</summary>
    public long? RagDocId { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>첨부 한 개의 겉모습. 실물은 받지 않는다.</summary>
public sealed record GmailAttachmentInfo(string? Name, string? MimeType, long? Size);
