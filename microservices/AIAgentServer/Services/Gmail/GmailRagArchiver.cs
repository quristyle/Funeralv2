using System.Text;

using AIAgentServer.Data;
using AIAgentServer.Services.Rag;

using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Gmail;

/// <summary>오래된 메일을 달 단위로 묶어 RAG 문서함에 올린다.
///
/// <para><b>왜 둘로 가르나.</b> 표를 그대로 두고도 <c>search_gmail</c> 이 날짜·낱말로 찾을 수 있다.
/// 하지만 「작년에 계약 관련해서 뭐라고 했더라」처럼 <b>낱말이 안 떠오르는</b> 질문은 낱말 검색으로
/// 안 걸린다 — 그런 질문에 답하는 것이 RAG 다. 그래서 오래된 것은 뜻으로도 찾을 수 있게 문서로 만든다.
/// 표에서 지우지는 않는다(<c>rag_doc_id</c> 만 찍는다) — 두 길이 서로를 대신하는 것이 아니라 겹쳐 있다.</para>
///
/// <para>한 번 부를 때 <paramref name="maxMessages"/> 만큼만 처리하고 끝난다. 수십만 통을 한 요청에
/// 담지 않으려는 것이다 — 0 이 돌아올 때까지 다시 부르면 된다.</para></summary>
public sealed class GmailRagArchiver(
    AiUsageDbContext db,
    RagLibrary library,
    RagBackgroundIndexer indexer,
    IConfiguration config,
    ILogger<GmailRagArchiver> logger)
{
    /// <summary>한 문서에 담을 메일 수. 너무 키우면 토막내기가 한 문서에 쏠리고, 너무 줄이면 문서가 흩어진다.</summary>
    private const int MessagesPerDocument = 200;

    /// <summary>한 통에서 가져갈 본문 길이. 넘는 것은 자른다 — 긴 메일 하나가 문서를 통째로 먹지 않게.</summary>
    private const int BodyCap = 4000;

    public sealed record Result(int Documents, int Messages, string? Note);

    private int ArchiveAfterMonths => config.GetValue("Gmail:ArchiveAfterMonths", 6);

    private string? DefaultCategory => config["Gmail:RagCategoryId"];

    public async Task<Result> RunAsync(string? categoryId, int maxMessages, string? who, CancellationToken ct)
    {
        var target = string.IsNullOrWhiteSpace(categoryId) ? DefaultCategory : categoryId;
        if (string.IsNullOrWhiteSpace(target))
            return new Result(0, 0, "올릴 RAG 카테고리가 정해지지 않았습니다(Gmail:RagCategoryId).");

        if (await library.GetCategoryAsync(target, ct) is null)
            return new Result(0, 0, $"그런 RAG 카테고리가 없습니다: {target}");

        var cutoff = DateTime.UtcNow.AddMonths(-ArchiveAfterMonths);

        // 날짜가 없는 메일은 「오래됐다」고 볼 근거가 없으므로 건드리지 않는다.
        var pending = await db.GmailMessages.AsNoTracking()
            .Where(m => m.RagDocId == null && m.SentAt != null && m.SentAt < cutoff)
            .OrderBy(m => m.SentAt)
            .Take(maxMessages)
            .Select(m => new
            {
                m.Id, m.AccountId, m.SentAt, m.FromAddr, m.FromName,
                m.ToAddrs, m.Subject, m.BodyText,
            })
            .ToListAsync(ct);

        if (pending.Count == 0)
            return new Result(0, 0, $"{ArchiveAfterMonths}개월이 지난 메일 중 아직 안 올린 것이 없습니다.");

        var documents = 0;
        var messages = 0;

        // 계정과 달로 묶는다 — 「2024년 3월 메일」처럼 사람이 떠올리는 덩어리와 같다.
        var groups = pending
            .GroupBy(m => new { m.AccountId, Month = new DateTime(m.SentAt!.Value.Year, m.SentAt!.Value.Month, 1) })
            .OrderBy(g => g.Key.Month);

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(m => m.SentAt).ToList();

            for (var part = 0; part * MessagesPerDocument < ordered.Count; part++)
            {
                ct.ThrowIfCancellationRequested();

                var slice = ordered.Skip(part * MessagesPerDocument).Take(MessagesPerDocument).ToList();
                var name = DocumentName(group.Key.AccountId, group.Key.Month, part, ordered.Count);

                var text = Compose(group.Key.AccountId, group.Key.Month, slice.Select(m => (
                    m.SentAt, m.FromAddr, m.FromName, m.ToAddrs, m.Subject, m.BodyText)).ToList());

                await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
                var row = await library.UploadAsync(
                    target!, folderId: null, name, "text/markdown", stream, who ?? "gmail-archive", ct);

                var ids = slice.Select(m => m.Id).ToList();
                await db.GmailMessages
                    .Where(m => ids.Contains(m.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.RagDocId, row.Id), ct);

                documents++;
                messages += slice.Count;

                logger.LogInformation(
                    "메일 {Count}통을 RAG 문서 {Doc} 로 올렸습니다: {Name}", slice.Count, row.Id, name);
            }
        }

        // 올리기만 하면 문서는 「pending」으로 앉아 있을 뿐이다 — 색인을 걸어야 찾아진다.
        // RAG 문서함 화면의 업로드가 하는 것과 같은 일이다.
        if (documents > 0) indexer.QueueReindex(target!, "gmail-archive", who);

        return new Result(documents, messages, null);
    }

    private static string DocumentName(string accountId, DateTime month, int part, int totalInMonth)
    {
        var stem = $"메일_{accountId}_{month:yyyy-MM}";
        return totalInMonth > MessagesPerDocument ? $"{stem}_{part + 1}.md" : $"{stem}.md";
    }

    /// <summary>문서 한 장을 짓는다. 머리에 무엇을 담은 묶음인지 적는다 — 토막으로 잘려 나가도
    /// 모델이 「이건 메일이다」를 알아야 엉뚱한 자료로 오해하지 않는다.</summary>
    private static string Compose(
        string accountId,
        DateTime month,
        IReadOnlyList<(DateTime? SentAt, string? FromAddr, string? FromName, string ToAddrs, string? Subject, string? BodyText)> mails)
    {
        var sb = new StringBuilder();
        sb.Append("# 메일 보관 — ").Append(accountId).Append(' ').Append(month.ToString("yyyy년 M월")).AppendLine();
        sb.AppendLine();
        sb.Append("메일함 `").Append(accountId).Append("` 에서 받은 ")
          .Append(month.ToString("yyyy년 M월")).Append(" 메일 ").Append(mails.Count).AppendLine("통입니다.");
        sb.AppendLine();

        foreach (var m in mails)
        {
            var sender = string.IsNullOrWhiteSpace(m.FromName)
                ? m.FromAddr ?? "(보낸이 모름)"
                : $"{m.FromName} <{m.FromAddr}>";

            sb.Append("## ").AppendLine(string.IsNullOrWhiteSpace(m.Subject) ? "(제목 없음)" : m.Subject);
            sb.AppendLine();
            sb.Append("- 날짜: ").AppendLine(m.SentAt is null ? "모름" : Portal.PortalDay.ToKorean(m.SentAt.Value));
            sb.Append("- 보낸이: ").AppendLine(sender);
            sb.Append("- 받는이: ").AppendLine(ReadAddresses(m.ToAddrs));
            sb.AppendLine();

            var body = (m.BodyText ?? string.Empty).Trim();
            if (body.Length > BodyCap) body = body[..BodyCap] + "\n\n…(본문이 길어 줄였습니다)";
            sb.AppendLine(body.Length == 0 ? "(본문 없음)" : body);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ReadAddresses(string json)
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return list is { Count: > 0 } ? string.Join(", ", list) : "(없음)";
        }
        catch
        {
            return "(없음)";
        }
    }
}
