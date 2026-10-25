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
/// <para><b>만든 글의 실물은 파일 서버가 맡는다</b>(RagLibrary.UploadAsync) — <c>Rag:UploadRoot</c>
/// 가 아니다. 그 폴더는 2026-10-17 이전의 자리이고 지금은 이사 도구가 거기 있던 것을 파일
/// 서버로 옮긴다. 화면에서 손으로 올린 문서와 같은 길이라 여기서 따로 할 일이 없다.</para>
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

    /// <param name="folderId">올릴 분류(RAG 문서함의 폴더). <c>null</c> 이면 카테고리 바로 아래.</param>
    /// <param name="afterMonths">이만큼 지난 것만 올린다. <c>0</c> 이면 <b>기간을 안 따진다</b> —
    /// 설정값(기본 6개월)이 화면에 안 보이면 「왜 아무것도 안 올라가나」의 답을 찾을 길이 없다.</param>
    public async Task<Result> RunAsync(
        string? categoryId, long? folderId, int maxMessages, int? afterMonths, string? who, CancellationToken ct)
    {
        var target = string.IsNullOrWhiteSpace(categoryId) ? DefaultCategory : categoryId;
        if (string.IsNullOrWhiteSpace(target))
            return new Result(0, 0, "올릴 RAG 카테고리가 정해지지 않았습니다(Gmail:RagCategoryId).");

        if (await library.GetCategoryAsync(target, ct) is null)
            return new Result(0, 0, $"그런 RAG 카테고리가 없습니다: {target}");

        // 분류가 그 카테고리의 것인지 본다. 안 보면 남의 카테고리 폴더에 메일을 넣을 수
        // 있고, 그 문서는 고른 적 없는 카테고리의 색인에 들어가 거기서 검색된다.
        if (folderId is { } fid)
        {
            var ok = await db.RagFolders.AsNoTracking()
                .AnyAsync(f => f.Id == fid && f.CategoryId == target && !f.IsDeleted, ct);

            if (!ok) return new Result(0, 0, "고른 분류가 그 카테고리의 것이 아닙니다.");
        }

        var months = afterMonths ?? ArchiveAfterMonths;
        DateTime? cutoff = months > 0 ? DateTime.UtcNow.AddMonths(-months) : null;

        // 날짜가 없는 메일은 어느 달로 묶을지 정할 수 없어 건드리지 않는다.
        var pending = await db.GmailMessages.AsNoTracking()
            .Where(m => m.RagDocId == null && m.SentAt != null && (cutoff == null || m.SentAt < cutoff))
            .OrderBy(m => m.SentAt)
            .Take(maxMessages)
            .Select(m => new
            {
                m.Id, m.AccountId, m.SentAt, m.FromAddr, m.FromName,
                m.ToAddrs, m.Subject, m.BodyText,
            })
            .ToListAsync(ct);

        if (pending.Count == 0)
            return new Result(0, 0, await WhyNothingAsync(months, ct));

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
                    target!, folderId, name, "text/markdown", stream, who ?? "gmail-archive", ct);

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

    /// <summary>올릴 것이 없을 때 그 까닭을 센다. 한 문장으로 뭉치면 「적재를 아직 안 했다」와
    /// 「기준 밖이라 안 걸린다」가 구별되지 않아, 사람이 할 일을 못 찾는다 —
    /// 앞엣것은 적재 단추를 눌러야 하고 뒤엣것은 기준을 넓혀야 한다.</summary>
    private async Task<string> WhyNothingAsync(int months, CancellationToken ct)
    {
        var total = await db.GmailMessages.CountAsync(ct);
        if (total == 0)
            return "표에 메일이 한 통도 없습니다. 먼저 mbox 를 적재해 주세요.";

        var left = await db.GmailMessages.CountAsync(m => m.RagDocId == null, ct);
        if (left == 0)
            return $"메일 {total:N0}통을 이미 다 올렸습니다.";

        var noDate = await db.GmailMessages.CountAsync(m => m.RagDocId == null && m.SentAt == null, ct);
        if (noDate == left)
            return $"안 올린 {left:N0}통이 모두 날짜를 모르는 메일이라 달로 묶을 수 없습니다.";

        return months > 0
            ? $"안 올린 메일 {left:N0}통이 모두 {months}개월 안의 것입니다. 기준을 「전체」로 바꾸면 올라갑니다."
            : $"안 올린 메일 {left:N0}통이 모두 날짜를 모르는 것이라 달로 묶을 수 없습니다.";
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
