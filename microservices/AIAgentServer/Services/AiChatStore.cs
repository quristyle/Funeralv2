using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services;

/// <summary>
/// AI쳇 대화를 담고 꺼낸다. <b>주제 하나가 줄기 하나다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [왜 서버인가]
/// </para>
///
/// <para>
/// 여태 대화는 회로가 사는 동안만 있었다 — 새로고침하면 사라졌다.
/// 브라우저에 담으면 그것은 넘길 수 있지만 <b>책상에서 쓴 대화를 휴대폰에서
/// 못 본다.</b> AI쳇은 헤더 서랍으로 어디서나 열리는 물건이라 기기에 묶이면
/// 「분명히 물어봤는데 없다」가 난다.
/// </para>
///
/// <para>
/// [담는 일이 대화를 막지 않는다]
/// </para>
///
/// <para>
/// 사용량 기록과 같은 선이다(<see cref="AiUsageLog"/>) — 담다 실패해도 답은
/// 이미 사람에게 흘러갔고, 거기서 예외를 올리면 <b>멀쩡히 받은 답이 오류로
/// 보인다.</b> 삼키고 경고만 남긴다.
/// </para>
///
/// <para>
/// 다만 <b>목록·열기는 삼키지 않는다.</b> 그쪽은 사람이 기다리는 조회라,
/// 실패를 빈 목록으로 돌려주면 「대화가 다 사라졌다」로 읽힌다.
/// </para>
///
/// <para>
/// [남의 것은 못 연다]
/// </para>
///
/// <para>
/// 모든 길이 <c>userId</c> 를 함께 받아 <b>조건에 넣는다.</b> 찾은 뒤에
/// 주인을 견주는 방식은 한 줄만 빠뜨려도 새는데, 조건에 넣으면 빠뜨릴 수가
/// 없다 — 없는 열쇠와 남의 열쇠가 똑같이 「없음」이 된다.
/// </para>
/// </remarks>
public sealed class AiChatStore(
    IServiceScopeFactory scopes,
    ILogger<AiChatStore> logger)
{
    /// <summary>
    /// 제목으로 따올 첫 질문의 길이.
    /// </summary>
    /// <remarks>
    /// 고르개 한 줄에 들어가야 하는 값이다. 길게 두면 목록에서 어느 것이
    /// 어느 것인지 가릴 수가 없고, 짧게 두면 「공통코드 화면에서…」 처럼
    /// 앞부분이 다 같은 질문들이 구분되지 않는다.
    /// </remarks>
    private const int TitleLength = 40;

    /// <summary>
    /// 한 대화에 담아 두는 최대 마디 수.
    /// </summary>
    /// <remarks>
    /// <b>넘으면 오래된 것부터 버린다.</b> 한 줄기가 끝없이 길어지면 여는 것도
    /// 느려지고, 애초에 문맥으로 다 올라가지도 않는다(공급자마다 글자 예산이
    /// 있어 <c>LLMService</c> 가 앞을 잘라 보낸다). 주제가 바뀌면 새 대화를
    /// 여는 것이 맞고, 이 상한은 그 권유가 아니라 <b>마지막 안전판</b>이다.
    /// </remarks>
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

    /// <summary>
    /// 그 대화의 마디들. <b>남의 것이거나 없으면 <c>null</c></b> —
    /// 둘을 가르지 않는다(머리말).
    /// </summary>
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

    /// <summary>
    /// 빈 대화를 하나 연다. 제목은 <b>첫 질문이 들어올 때</b> 붙는다.
    /// </summary>
    /// <remarks>
    /// 사람에게 제목을 먼저 묻지 않는다 — 물어보려고 연 창에서 제목부터
    /// 지으라고 하면 그 자리에서 막힌다.
    /// </remarks>
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

        // 빈 글자는 **지우는 것**이다. 그때는 첫 질문에서 다시 따온 제목이
        // 아니라 「새 대화」로 돌아간다 — 화면이 비어 있는 제목을 그렇게 그린다.
        var trimmed = title?.Trim();
        row.Title = string.IsNullOrEmpty(trimmed) ? null : Shorten(trimmed);

        // **`updated_at` 은 건드리지 않는다.** 이름을 고친 것이 「말이 오간
        // 것」은 아니라서, 바꾸면 목록 차례가 손댈 때마다 뒤집힌다.
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// 대화를 지운다. <b>마디들도 함께 사라진다</b>(외래키 CASCADE).
    /// 내 것이 아니면 거짓.
    /// </summary>
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

    /// <summary>
    /// 오간 한 턴(질문과 답)을 담는다. <b>기다리지 않는다</b> —
    /// 답은 이미 사람에게 흘러갔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>질문과 답을 함께 담는다.</b> 질문만 먼저 담아 두면 답이 실패했을 때
    /// 한쪽만 남은 대화가 되고, 그것을 다음 턴 문맥으로 올리면 AI 가 자기가
    /// 답을 안 한 줄 모른 채 이어 말한다.
    /// </para>
    /// <para>
    /// 답이 비어 있으면(한 글자도 못 받았다) <b>아무것도 담지 않는다.</b>
    /// 물어본 적이 없던 것으로 두는 편이, 답 없는 질문이 쌓이는 것보다 낫다 —
    /// 그 실패는 사용량 기록이 따로 남긴다.
    /// </para>
    /// </remarks>
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
            // 요청의 수명과 무관하게 돈다 — 스트리밍이 끝나 범위가 닫힌 뒤에
            // 쓰면 ObjectDisposedException 이 난다(AiUsageLog 와 같은 자리).
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

            // 첫 질문이 제목이 된다. **이미 제목이 있으면 건드리지 않는다** —
            // 사람이 고쳐 둔 이름을 다음 질문이 덮으면 안 된다.
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

    /// <summary>
    /// 상한을 넘으면 오래된 마디부터 버린다. <b>짝을 맞춰 버리지 않는다</b> —
    /// 질문 하나가 혼자 남아도 화면은 그대로 그리고, 문맥은 어차피 공급자
    /// 예산에 맞춰 다시 잘린다.
    /// </summary>
    private static async Task TrimAsync(AiUsageDbContext db, string sessionId, int lastSeq)
    {
        if (lastSeq <= MaxMessages) return;

        await db.AiChatMessages
            .Where(m => m.SessionId == sessionId && m.Seq <= lastSeq - MaxMessages)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// 제목으로 쓸 만큼 자른다. 줄바꿈은 한 칸으로 눕힌다 — 고르개는 한 줄이다.
    /// </summary>
    private static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();

        return line.Length <= TitleLength ? line : line[..TitleLength] + "…";
    }
}
