using System.Text;
using System.Text.Json;

using HtmlAgilityPack;

using MimeKit;

namespace AIAgentServer.Services.Gmail;

/// <summary>MIME 메일 한 통을 표에 들어갈 모양으로 바꾼다. 길 셋(Takeout·API·IMAP)이 모두 이것을 거치므로
/// <b>어느 길로 들어왔든 같은 모양</b>이 된다 — 그래야 겹침을 지우고 도구가 한 가지로 물을 수 있다.</summary>
public static class MailNormalizer
{
    private const int SnippetLength = 200;

    /// <summary>글자 본문이 없고 HTML 만 있는 메일이 흔하다. 그런 메일은 본문 검색에서 통째로 빠지므로
    /// 태그를 걷어 글자를 만들어 둔다.</summary>
    public static string? TextFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            foreach (var tag in new[] { "script", "style" })
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes is null) continue;
                foreach (var n in nodes.ToList()) n.Remove();
            }

            return HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        }
        catch
        {
            // 깨진 HTML 하나 때문에 메일을 통째로 버리지 않는다. 본문 글자만 포기한다.
            return null;
        }
    }

    /// <summary>여러 줄·연속 공백을 한 칸으로 눌러 미리보기 글을 만든다.</summary>
    public static string? Snippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sb = new StringBuilder(SnippetLength + 4);
        var space = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                space = true;
                continue;
            }

            if (space && sb.Length > 0) sb.Append(' ');
            space = false;
            sb.Append(ch);
            if (sb.Length >= SnippetLength) { sb.Append('…'); break; }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Takeout 의 <c>X-Gmail-Labels</c> 는 쉼표로 잇는데, 라벨 이름에 쉼표가 있으면 따옴표로 감싼다.
    /// 그래서 그냥 <c>Split(',')</c> 하면 라벨 하나가 둘로 쪼개진다.</summary>
    public static List<string> SplitLabels(string? header)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(header)) return result;

        var sb = new StringBuilder();
        var quoted = false;
        foreach (var ch in header)
        {
            if (ch == '"') { quoted = !quoted; continue; }

            if (ch == ',' && !quoted)
            {
                Flush(sb, result);
                continue;
            }

            sb.Append(ch);
        }

        Flush(sb, result);
        return result;

        static void Flush(StringBuilder sb, List<string> into)
        {
            var v = sb.ToString().Trim();
            sb.Clear();
            if (v.Length > 0) into.Add(v);
        }
    }

    /// <summary>헤더의 날짜가 믿을 만한지 본다. 아주 옛 메일과 Takeout 에는 <c>Date</c> 가 없거나 깨진 것이
    /// 섞여 있어, 그대로 넣으면 「1년 안에 온 메일」 같은 질의가 엉뚱한 것을 집는다.</summary>
    private static DateTime? SaneDate(DateTimeOffset value)
    {
        if (value == default) return null;

        var utc = value.UtcDateTime;
        if (utc.Year < 1990) return null;
        if (utc > DateTime.UtcNow.AddDays(2)) return null;   // 시계가 틀린 보낸 쪽
        return utc;
    }

    private static List<string> Addresses(InternetAddressList list) =>
        list.Mailboxes
            .Select(m => m.Address)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

    private static List<GmailAttachmentInfo> Attachments(MimeMessage msg)
    {
        var result = new List<GmailAttachmentInfo>();

        foreach (var entity in msg.Attachments)
        {
            long? size = null;
            if (entity is MimePart part)
            {
                try
                {
                    // 스트림이 되감기를 받아 주지 않는 경우가 있다 — 그때는 크기를 포기한다.
                    if (part.Content?.Stream is { CanSeek: true } s) size = s.Length;
                }
                catch
                {
                    size = null;
                }
            }

            result.Add(new GmailAttachmentInfo(
                entity.ContentDisposition?.FileName ?? entity.ContentType?.Name,
                entity.ContentType?.MimeType,
                size));
        }

        return result;
    }

    /// <summary>겹침을 가를 열쇠가 하나도 없는 메일(Message-ID 도 X-GM-MSGID 도 없는 것)에 붙일 지문.
    /// 이것이 없으면 같은 파일을 두 번 적재했을 때 그런 메일만 줄이 불어난다 — 고유 색인이 NULL 에는
    /// 안 걸리기 때문이다. 보낸이·날짜·제목·본문 머리를 섞어 만들므로 같은 메일이면 같은 값이 나온다.</summary>
    private static string ContentFingerprint(MimeMessage msg, string? text)
    {
        var seed = string.Join(
            '\u001F',   // 칸 경계. 제목 끝과 본문 머리가 붙어 같은 지문이 되는 것을 막는다
            msg.From?.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
            msg.Date == default ? string.Empty : msg.Date.UtcDateTime.ToString("O"),
            msg.Subject ?? string.Empty,
            text is null ? string.Empty : text[..Math.Min(text.Length, 4096)]);

        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "sha256:" + Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>MIME 한 통을 표 줄로 바꾼다. <paramref name="labels"/> 가 <c>null</c> 이면
    /// Takeout 헤더(<c>X-Gmail-Labels</c>)에서 읽는다.</summary>
    public static GmailMessageRow FromMime(
        MimeMessage msg,
        string accountId,
        string source,
        string? providerMsgId = null,
        string? threadId = null,
        IReadOnlyList<string>? labels = null)
    {
        var from = msg.From?.Mailboxes.FirstOrDefault();

        var html = msg.HtmlBody;
        var text = msg.TextBody ?? TextFromHtml(html);

        var attachments = Attachments(msg);
        var labelList = labels?.ToList() ?? SplitLabels(msg.Headers["X-Gmail-Labels"]);

        return new GmailMessageRow
        {
            AccountId = accountId,
            Source = source,

            // Takeout 메일은 Gmail 번호를 헤더로 들고 온다 — API 가 뒤에 같은 메일을 가져와도
            // 겹침은 Message-ID 로 가르므로 이 값은 되짚기용이다.
            ProviderMsgId = providerMsgId
                            ?? NullIfBlank(msg.Headers["X-GM-MSGID"])
                            // 열쇠가 둘 다 없으면 겹침을 가를 길이 사라진다 — 내용으로 지문을 만들어 채운다.
                            ?? (NullIfBlank(msg.MessageId) is null ? ContentFingerprint(msg, text) : null),
            ThreadId = threadId ?? NullIfBlank(msg.Headers["X-GM-THRID"]),
            Rfc822MessageId = NullIfBlank(msg.MessageId),

            Subject = NullIfBlank(msg.Subject),
            FromAddr = NullIfBlank(from?.Address),
            FromName = NullIfBlank(from?.Name),
            ToAddrs = JsonSerializer.Serialize(msg.To is null ? [] : Addresses(msg.To)),
            CcAddrs = JsonSerializer.Serialize(msg.Cc is null ? [] : Addresses(msg.Cc)),
            SentAt = SaneDate(msg.Date),

            Labels = JsonSerializer.Serialize(labelList),
            Snippet = Snippet(text),
            BodyText = text,
            BodyHtml = html,
            HasAttachments = attachments.Count > 0,
            Attachments = JsonSerializer.Serialize(attachments),

            CreatedAt = DateTime.UtcNow,
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
