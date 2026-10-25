using AIAgentServer.Data;
using AIAgentServer.Services.Portal;

using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Gmail;

/// <summary>받아 온 메일을 찾는다.
///
/// <para><b>묻는 사람이 주인인 메일함만 본다.</b> 업무 비서의 다른 도구들은 묻는 사람의 평소 권한을
/// 넘어서 답하지만(그것이 그 화면의 목적이다), 메일만은 그러면 안 된다 — 슈퍼관리자·임원이 함께 쓰는
/// 화면이라 범위를 안 좁히면 다른 임원이 이 사람의 개인 메일을 읽는다.
/// 그 열쇠가 <see cref="GmailAccountRow.OwnerLoginId"/> 다.</para></summary>
public sealed class GmailSearch(AiUsageDbContext db)
{
    /// <summary>찾은 메일 한 줄.</summary>
    public sealed record Hit(
        long Id,
        string AccountId,
        DateTime? SentAt,
        string? FromAddr,
        string? FromName,
        string? Subject,
        string? Snippet,
        bool HasAttachments);

    /// <summary>묻는 사람이 가진 메일함의 열쇠들. 하나도 없으면 빈 목록이다.</summary>
    public Task<List<string>> MyAccountIdsAsync(string askedBy, CancellationToken ct) =>
        db.GmailAccounts.AsNoTracking()
            .Where(a => a.OwnerLoginId.ToLower() == askedBy.ToLower())
            .Select(a => a.Id)
            .ToListAsync(ct);

    public async Task<(int Total, List<Hit> Items)> SearchAsync(
        string askedBy,
        string? fromDay,
        string? toDay,
        string? fromAddr,
        string? keyword,
        int take,
        CancellationToken ct)
    {
        var mine = await MyAccountIdsAsync(askedBy, ct);
        if (mine.Count == 0) return (0, []);

        var query = db.GmailMessages.AsNoTracking().Where(m => mine.Contains(m.AccountId));

        // 날짜는 한국 달력으로 센다 — 다른 도구들과 같은 경계다(docs/utc-time.md).
        if (!string.IsNullOrWhiteSpace(fromDay))
        {
            var startUtc = PortalDay.StartUtc(fromDay);
            query = query.Where(m => m.SentAt >= startUtc);
        }

        if (!string.IsNullOrWhiteSpace(toDay))
        {
            var endUtc = PortalDay.EndExclusiveUtc(toDay);
            query = query.Where(m => m.SentAt < endUtc);
        }

        if (!string.IsNullOrWhiteSpace(fromAddr))
        {
            var needle = $"%{fromAddr}%";
            query = query.Where(m =>
                (m.FromAddr != null && EF.Functions.ILike(m.FromAddr, needle)) ||
                (m.FromName != null && EF.Functions.ILike(m.FromName, needle)));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var needle = $"%{keyword}%";
            query = query.Where(m =>
                (m.Subject != null && EF.Functions.ILike(m.Subject, needle)) ||
                (m.Snippet != null && EF.Functions.ILike(m.Snippet, needle)) ||
                (m.BodyText != null && EF.Functions.ILike(m.BodyText, needle)));
        }

        var total = await query.CountAsync(ct);

        // 날짜가 없는 메일(헤더가 깨진 것)이 맨 앞에 몰리지 않게 뒤로 보낸다 —
        // 거짓(날짜 있음)이 먼저 오도록 오름차순으로 센다.
        var items = await query
            .OrderBy(m => m.SentAt == null)
            .ThenByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new Hit(
                m.Id, m.AccountId, m.SentAt, m.FromAddr, m.FromName, m.Subject, m.Snippet, m.HasAttachments))
            .ToListAsync(ct);

        return (total, items);
    }
}
