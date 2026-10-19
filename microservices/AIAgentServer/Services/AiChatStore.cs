using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services;

/// <summary>AI쳇 대화를 서버에 담고 꺼낸다(브라우저에만 두면 휴대폰에서 안 보인다). 담는 일은 답을 막지 않으며 실패는 삼키고 경고만 남긴다.
/// 목록·열기는 삼키지 않는다 — 실패를 빈 목록으로 돌리면 「대화가 다 사라졌다」로 읽힌다. 모든 길이 userId 를 조건에 넣어 남의 대화는 못 연다.</summary>
public sealed class AiChatStore(
    IServiceScopeFactory scopes,
    ILogger<AiChatStore> logger)
{
    /// <summary>제목으로 따올 첫 질문의 길이. 고르개 한 줄에 들어갈 만큼이다.</summary>
    private const int TitleLength = 40;

    /// <summary>한 대화에 담아 두는 최대 마디 수. 넘으면 오래된 것부터 버린다 — 주제가 바뀌면 새 대화를 여는 것이 맞고, 이것은 마지막 안전판이다.</summary>
    private const int MaxMessages = 400;

    /// <summary>내 대화 목록. 최근에 말이 오간 순이다.</summary>
    public async Task<List<AiChatSessionRow>> ListAsync(
        string userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        return await db.AiChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);
    }

    /// <summary>그 대화의 마디들. 남의 것이거나 없으면 null(둘을 가르지 않는다).</summary>
    public async Task<List<AiChatMessageRow>?> MessagesAsync(
        string userId, string sessionId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var mine = await db.AiChatSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        if (!mine) return null;

        return await db.AiChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Seq)
            .ToListAsync(ct);
    }

    /// <summary>빈 대화를 하나 연다. 제목은 첫 질문이 들어올 때 붙는다 — 연 창에서 제목부터 지으라고 하면 막힌다.</summary>
    public async Task<AiChatSessionRow> CreateAsync(
        string userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var now = DateTime.UtcNow;
        var row = new AiChatSessionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Title = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AiChatSessions.Add(row);
        await db.SaveChangesAsync(ct);

        return row;
    }

    /// <summary>제목을 고친다. 내 것이 아니면 거짓.</summary>
    public async Task<bool> RenameAsync(
        string userId, string sessionId, string? title, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var row = await db.AiChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        if (row is null) return false;

        // 빈 글자는 지우는 것이다. 그때는 「새 대화」로 돌아가 화면이 비어 있는 제목을 그렇게 그린다.
        var trimmed = title?.Trim();
        row.Title = string.IsNullOrEmpty(trimmed) ? null : Shorten(trimmed);

        // updated_at 은 건드리지 않는다 — 이름을 고친 것은 말이 오간 것이 아니라서 목록 차례가 뒤집힌다.
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>대화를 지운다. 마디들도 함께 사라진다(외래키 CASCADE). 내 것이 아니면 거짓.</summary>
    public async Task<bool> DeleteAsync(
        string userId, string sessionId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var deleted = await db.AiChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }

    /// <summary>오간 한 턴(질문과 답)을 담는다. 기다리지 않는다 — 답은 이미 사람에게 흘러갔다.
    /// 질문과 답을 함께 담는다(질문만 남으면 다음 문맥에서 AI 가 답하지 않은 줄 모르고 이어 말한다). 답이 한 글자도 없으면 아무것도 담지 않는다.</summary>
    public void RecordTurn(string userId, string sessionId, string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        _ = SaveTurnAsync(userId, sessionId, question.Trim(), answer.Trim());
    }

    private async Task SaveTurnAsync(
        string userId, string sessionId, string question, string answer)
    {
        try
        {
            // 요청의 수명과 무관하게 돈다 — 스트리밍이 끝나 범위가 닫힌 뒤에 쓰면 ObjectDisposedException 이 난다.
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

            var session = await db.AiChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            // 그 사이에 지웠거나 남의 것이다. 되살리지 않는다 — 지운 것은 지운 것이다.
            if (session is null) return;

            var nextSeq = await db.AiChatMessages
                .Where(m => m.SessionId == sessionId)
                .MaxAsync(m => (int?)m.Seq) ?? 0;

            var now = DateTime.UtcNow;

            db.AiChatMessages.Add(new AiChatMessageRow
            {
                SessionId = sessionId,
                Role = "user",
                Content = question,
                Seq = ++nextSeq,
                CreatedAt = now,
            });

            db.AiChatMessages.Add(new AiChatMessageRow
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = answer,
                Seq = ++nextSeq,
                CreatedAt = now,
            });

            // 첫 질문이 제목이 된다. 이미 제목이 있으면 건드리지 않는다 — 사람이 고쳐 둔 이름을 덮으면 안 된다.
            if (string.IsNullOrWhiteSpace(session.Title))
            {
                session.Title = Shorten(question);
            }

            session.UpdatedAt = now;

            await db.SaveChangesAsync();
            await TrimAsync(db, sessionId, nextSeq);
        }
        catch (Exception ex)
        {
            // 삼킨다(머리말). 답은 이미 사람이 받았다.
            logger.LogWarning(ex, "AI 대화를 담지 못했습니다: {Session}", sessionId);
        }
    }

    /// <summary>상한을 넘으면 오래된 마디부터 버린다. 짝을 맞춰 버리지 않는다 — 문맥은 어차피 공급자 예산에 맞춰 다시 잘린다.</summary>
    private static async Task TrimAsync(AiUsageDbContext db, string sessionId, int lastSeq)
    {
        if (lastSeq <= MaxMessages) return;

        await db.AiChatMessages
            .Where(m => m.SessionId == sessionId && m.Seq <= lastSeq - MaxMessages)
            .ExecuteDeleteAsync();
    }

    /// <summary>제목으로 쓸 만큼 자른다. 줄바꿈은 한 칸으로 눕힌다(고르개는 한 줄이다).</summary>
    private static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();

        return line.Length <= TitleLength ? line : line[..TitleLength] + "…";
    }
}
