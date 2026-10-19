using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>쪽지 엔드포인트(/api/notification/notes/*). 두드림(앱 푸시·메일)이 실패해도 쪽지함에 들어갔으면 성공이다.
/// 메일은 받는 사람이 켠 경우에만 간다.</summary>
public static class NoteEndpoints
{
    /// <summary>제목 · 본문의 길이 상한. 화면이 아니라 여기가 정본이다.</summary>
    private const int MaxTitle = 200;

    /// <summary>「전체에서 고르기」가 한 번에 주는 받는 사람 수 상한. 목록이 통째로 내려와 창이 늦게 열리는 것을 막는다.</summary>
    private const int MaxDirectory = 200;

    private const int MaxBody = 4000;

    /// <summary>쪽지를 눌렀을 때 열 화면. 알림과 메일이 같은 곳을 가리킨다.</summary>
    private const string InboxUrl = "/admin/note/box";

    /// <summary>한국 시간대. 제목 글자에 사람이 읽는 시각을 적을 때만 쓴다. 리눅스와 윈도우의 아이디가 달라 둘 다 시도한다.</summary>
    private static readonly TimeZoneInfo KoreaZone = ResolveKoreaZone();

    private static TimeZoneInfo ResolveKoreaZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
    }

    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").WithTags("Notes");

        // ── 받는 사람 찾기 ──────────────────────────────────
        // 보내기는 이 결과를 믿지 않고 같은 규칙으로 다시 푼다(NoteRecipientResolver).
        group.MapGet("/recipients", async (
            UserContext? user,
            [FromServices] INoteRecipientResolver resolver,
            [FromQuery] string? q = null,
            [FromQuery] int take = 20,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await resolver.SearchAsync(q, take, ct);
            return Results.Ok(ApiResponse<List<NoteRecipientDto>>.Ok(rows));
        })
        .WithName("SearchNoteRecipients");

        // ── 빠른 선택 ───────────────────────────────────────
        // 최근은 내 보낸함에서, 전체는 받을 길이 있는 사람 상한 안에서 뽑는다.
        group.MapGet("/recipients/quick", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INoteRecipientResolver resolver,
            [FromQuery] int take = 8,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var want = Math.Clamp(take, 1, 20);

            // 사람 하나당 한 줄로 접는다(가장 가까운 때 순). 치운 것도 본다.
            var recentKeys = await db.Notes
                .Where(n => n.SenderKey == user.UserId && n.ReceiverKey != user.UserId)
                .GroupBy(n => n.ReceiverKey)
                .Select(g => new { Key = g.Key, Last = g.Max(n => n.SentAt) })
                .OrderByDescending(x => x.Last)
                .Take(want)
                .ToListAsync(ct);

            var order = recentKeys
                .Select((x, i) => (x.Key, i))
                .ToDictionary(t => t.Key, t => t.i, StringComparer.Ordinal);

            // 지금도 받을 수 있는 사람만 남긴다(눌러 담고 나서야 막힌 것을 아는 일을 막는다).
            var recent = recentKeys.Count == 0
                ? []
                : (await resolver.LoadByLoginIdsAsync(recentKeys.Select(x => x.Key), ct))
                    .Where(r => r.CanReceive)
                    .OrderBy(r => order.GetValueOrDefault(r.LoginId, int.MaxValue))
                    .ToList();

            // 잘렸는지 알려고 상한보다 하나 더 받는다.
            var all = await resolver.ListReachableAsync(MaxDirectory + 1, ct);

            var truncated = all.Count > MaxDirectory;

            if (truncated)
            {
                all = [.. all.Take(MaxDirectory)];
            }

            // 나 자신은 뺀다(잘못 누르기 쉬운 맨 앞 자리).
            all = [.. all.Where(r => !string.Equals(r.LoginId, user.UserId, StringComparison.Ordinal))];

            return Results.Ok(ApiResponse<NoteQuickPickDto>.Ok(new NoteQuickPickDto
            {
                Recent = recent,
                All = all,
                AllTruncated = truncated,
            }));
        })
        .WithName("GetNoteQuickPick");

        // ── 기기에 닿았다 ───────────────────────────────────
        // 익명이다(서비스워커가 부르고 토큰이 없어 쪽지 아이디를 열쇠로 쓴다). 한 번만 찍어 처음 도착 시각을 남긴다.
        group.MapPost("/delivered", async (
            [FromBody] NoteDeliveredDto request,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            var id = (request.NoteId ?? string.Empty).Trim();

            // 없는 아이디에도 참을 돌려준다(404 로 가르면 아이디로 쪽지를 찾아볼 수 있다).
            if (id.Length == 0 || id.Length > 64)
            {
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }

            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct);

            if (note is not null && note.DeliveredAt is null)
            {
                note.DeliveredAt = AppTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .AllowAnonymous()
        .WithName("MarkNoteDelivered");

        // ── 쪽지 보내기 ─────────────────────────────────────
        group.MapPost("", async (
            [FromBody] SendNoteDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INoteRecipientResolver resolver,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] IPushSender push,
            [FromServices] IEmailSender email,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var logger = loggerFactory.CreateLogger("NoteEndpoints");

            var title = (request.Title ?? string.Empty).Trim();
            var body = (request.Body ?? string.Empty).Trim();

            // 제목은 비면 지어 주지만 내용은 지어 줄 것이 없다.
            if (body.Length == 0)
            {
                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: "쪽지 내용을 적으십시오.", code: "INVALID"));
            }

            if (title.Length > MaxTitle || body.Length > MaxBody)
            {
                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: $"제목은 {MaxTitle}자, 내용은 {MaxBody}자를 넘을 수 없습니다.",
                    code: "TOO_LONG"));
            }

            var tokens = (request.To ?? string.Empty)
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var (resolved, unknown) = await resolver.ResolveAsync(tokens, ct);

            // 받을 길이 없는 사람(앱 푸시도 메일도 없음)은 막는다(쪽지함에만 쌓이면 보낸 쪽이 보냈다고 믿는다).
            // 찾기 목록도 같은 값으로 거른다(NoteRecipientDto.CanReceive).
            var found = resolved.Where(r => r.CanReceive).ToList();
            var blocked = resolved.Where(r => !r.CanReceive).Select(Display).ToList();

            if (found.Count == 0)
            {
                // 어느 것이 틀렸는지 짚는다(아이디 오타인지 푸시를 꺼 둔 것인지).
                var why = new List<string>();

                if (unknown.Count > 0) why.Add($"그런 아이디·이메일이 없습니다: {string.Join(", ", unknown)}");
                if (blocked.Count > 0) why.Add($"앱 푸시도 쪽지 메일도 받지 않아 쪽지가 닿지 않습니다: {string.Join(", ", blocked)}");
                if (why.Count == 0) why.Add("받는 사람을 적으십시오");

                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: $"보낼 사람을 찾지 못했습니다 ({string.Join(" · ", why)}).", code: "NO_RECIPIENT"));
            }

            // 보낸 이의 이름. 게이트웨이가 헤더로 준다 — 없으면 아이디를 쓴다.
            var senderName = string.IsNullOrWhiteSpace(user.Name) ? user.UserId : user.Name;

            var now = DateTime.UtcNow;

            // 제목을 비우면 지어 넣는다(보낸 사람·시각이 목록에서 찾는 단서다).
            if (title.Length == 0)
            {
                title = DefaultTitle(senderName, now);
            }

            var notes = found
                .Select(r => new Note
                {
                    SenderKey = user.UserId,
                    SenderName = senderName,
                    ReceiverKey = r.LoginId,
                    ReceiverName = r.Name,
                    Title = title,
                    Body = body,
                    SentAt = now,
                    CreatedAt = now,
                    CreatedBy = user.UserId,
                })
                .ToList();

            db.Notes.AddRange(notes);

            // 글을 먼저 확정한다(두드림을 먼저 보내면 저장 실패 때 알림만 가고 쪽지함은 빈다).
            await db.SaveChangesAsync(ct);

            var trouble = new List<string>();

            // ── 앱 푸시 ─────────────────────────────────────
            // 앱 푸시는 닿는 사람에게만 사람마다 보낸다(쪽지 아이디를 실어야 되알림이 어느 쪽지인지 안다).
            // 아이콘은 한 번만 조회해 같은 객체를 돌려 쓴다(PushSender 가 채운 아이콘을 재사용).
            var pushDevices = 0;
            var pushTargets = found.Where(r => r.PushReachable).ToList();

            var noteOf = notes.ToDictionary(n => n.ReceiverKey, StringComparer.Ordinal);

            var message = new PushMessageDto
            {
                Title = $"쪽지 · {senderName}",
                Body = title,
                Url = InboxUrl,
                IconOwnerKey = user.UserId,
                Category = PushCategories.Note,
            };

            foreach (var target in pushTargets)
            {
                // 서비스워커가 닿았다고 되알려 줄 때 쥐여 보내는 열쇠(전환 메일 판정의 근거).
                message.Data = noteOf.TryGetValue(target.LoginId, out var mine)
                    ? new Dictionary<string, string> { ["noteId"] = mine.Id }
                    : null;

                try
                {
                    var result = await push.SendAsync(new SendPushDto
                    {
                        Owners = [new OwnerRefDto { OwnerType = "jsini", OwnerKey = target.LoginId }],
                        Message = message,
                    }, user.UserId, ct);

                    pushDevices += result.Sent;

                    if (result.Sent == 0)
                    {
                        trouble.Add($"{Display(target)} 에게 앱 알림이 가지 않았습니다");
                    }
                }
                catch (Exception ex)
                {
                    // 쪽지는 이미 들어갔으므로 두드림 하나의 실패로 답하지 않는다.
                    logger.LogWarning(ex, "쪽지 앱 알림을 보내지 못했습니다. by={By} to={To}",
                        user.UserId, target.LoginId);
                    trouble.Add($"{Display(target)} 에게 앱 알림을 보내지 못했습니다");
                }
            }

            // ── 메일 ────────────────────────────────────────
            // 받는 사람이 켠 경우에만 보낸다(기본 꺼짐이라 조용히 건너뛴다).
            var emailedKeys = new HashSet<string>(StringComparer.Ordinal);

            var wantMail = new List<NoteRecipientDto>();

            try
            {
                var on = await prefs.GetNoteEmailEnabledLoginIdsAsync(
                    found.Select(r => r.LoginId), ct);

                wantMail = [.. found.Where(r => on.Contains(r.LoginId))];
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "쪽지 메일 대상을 읽지 못했습니다. by={By}", user.UserId);
                trouble.Add("메일 수신 설정을 읽지 못했습니다");
            }

            if (wantMail.Count > 0)
            {
                // 주소가 없으면 보낼 데가 없으므로 빠진 사람 수를 결과에 적는다.
                var withAddress = wantMail
                    .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                    .ToList();

                var without = wantMail.Count - withAddress.Count;

                if (without > 0)
                {
                    trouble.Add($"쪽지 메일을 켜 두었으나 주소가 없는 사람 {without}명");
                }

                // 사람마다 한 통씩 보낸다(주소를 한 통에 모으면 서로의 주소가 보인다).
                foreach (var r in withAddress)
                {
                    try
                    {
                        // 틀은 NoticeEmailTemplate 에서만 입힌다. 평문 갈래도 함께 실어 평문 클라이언트에서 빈 칸이 되지 않게 한다.
                        await email.SendAsync(
                            r.Email!,
                            title,
                            NoticeEmailTemplate.Render(title, body, senderName),
                            html: true,
                            attachments: null,
                            textBody: NoticeEmailTemplate.PlainAlternative(title, body, senderName));

                        emailedKeys.Add(r.LoginId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "쪽지 메일을 보내지 못했습니다. by={By} to={To}",
                            user.UserId, r.LoginId);
                        trouble.Add($"{Display(r)} 에게 메일을 보내지 못했습니다");
                    }
                }
            }

            var notifyNote = trouble.Count > 0 ? string.Join(" · ", trouble) : null;

            // 두드림 결과를 사람마다 적는다(한 값으로 뭉치면 안 받은 사람도 보낸 것으로 보인다).
            foreach (var note in notes)
            {
                note.PushSent = pushDevices > 0;
                note.EmailSent = emailedKeys.Contains(note.ReceiverKey);
                note.NotifyNote = notifyNote;
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "쪽지 {Count}통. by={By} push={Push} mail={Mail} 못푼값={Unknown} 푸시꺼짐={Blocked}",
                notes.Count, user.UserId, pushDevices, emailedKeys.Count, unknown.Count, blocked.Count);

            return Results.Ok(ApiResponse<SendNoteResultDto>.Ok(new SendNoteResultDto
            {
                Sent = notes.Count,
                PushDevices = pushDevices,
                EmailSent = emailedKeys.Count,
                Unknown = unknown,
                Blocked = blocked,
                Recipients = [.. found.Select(Display)],
                NotifyNote = notifyNote,
            }));
        })
        .WithName("SendNote");

        // ── 받은 쪽지함 ─────────────────────────────────────
        group.MapGet("/inbox", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int take = 300,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            // 상한이 걸린 목록이라 DB 에서 받은 뒤 메모리에서 옮긴다(투영에 우리 메서드를 넣으면 EF 가 번역하지 못한다).
            var rows = await Period(
                    db.Notes.Where(n => n.ReceiverKey == user.UserId && !n.ReceiverDeleted),
                    startDate, endDate)
                .OrderByDescending(n => n.SentAt)
                .Take(Math.Clamp(take, 1, 2000))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<NoteRowDto>>.Ok([.. rows.Select(Row)]));
        })
        .WithName("GetMyNoteInbox");

        // ── 보낸 쪽지함 ─────────────────────────────────────
        group.MapGet("/sent", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int take = 300,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await Period(
                    db.Notes.Where(n => n.SenderKey == user.UserId && !n.SenderDeleted),
                    startDate, endDate)
                .OrderByDescending(n => n.SentAt)
                .Take(Math.Clamp(take, 1, 2000))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<NoteRowDto>>.Ok([.. rows.Select(Row)]));
        })
        .WithName("GetMyNoteSent");

        // ── 안 읽은 수 ──────────────────────────────────────
        // 가장 자주 불리는 자리라 줄을 세기만 한다.
        group.MapGet("/unread-count", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var unread = await db.Notes
                .CountAsync(n => n.ReceiverKey == user.UserId
                                 && !n.ReceiverDeleted && n.ReadAt == null, ct);

            return Results.Ok(ApiResponse<NoteUnreadDto>.Ok(new NoteUnreadDto { Unread = unread }));
        })
        .WithName("GetMyNoteUnreadCount");

        // ── 읽음 ────────────────────────────────────────────
        // 받은 사람만 찍는다(열쇠만으로 갱신하면 아이디만 아는 사람이 남의 쪽지를 읽음으로 만들 수 있다).
        group.MapPost("/{id}/read", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var note = await db.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.ReceiverKey == user.UserId, ct);

            if (note is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "그런 쪽지가 없습니다.", code: "NOT_FOUND"));
            }

            if (note.ReadAt is null)
            {
                note.ReadAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("MarkNoteRead");

        // ── 치우기 ──────────────────────────────────────────
        // 보낸 쪽과 받은 쪽은 따로 치운다(남의 우편함은 비우지 못한다).
        group.MapDelete("/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var note = await db.Notes.FirstOrDefaultAsync(
                n => n.Id == id && (n.ReceiverKey == user.UserId || n.SenderKey == user.UserId), ct);

            if (note is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "그런 쪽지가 없습니다.", code: "NOT_FOUND"));
            }

            if (note.ReceiverKey == user.UserId) note.ReceiverDeleted = true;
            if (note.SenderKey == user.UserId) note.SenderDeleted = true;

            // 양쪽 다 치운 줄만 지운다.
            note.IsDeleted = note.ReceiverDeleted && note.SenderDeleted;
            note.UpdatedAt = DateTime.UtcNow;
            note.UpdatedBy = user.UserId;

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("DeleteNote");
    }

    /// <summary>제목을 비우고 보냈을 때 대신 적는 「누가 언제 보냈다」 한 줄. 본문 첫 줄은 자르지 않는다.</summary>
    private static string DefaultTitle(string senderName, DateTime sentAtUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(sentAtUtc, DateTimeKind.Utc), KoreaZone);

        return $"{senderName} 님이 {local:yyyy-MM-dd HH:mm} 에 보낸 쪽지";
    }

    /// <summary>사람을 가리키는 한 마디. 이름이 없으면 아이디를 쓴다.</summary>
    private static string Display(NoteRecipientDto r) =>
        string.IsNullOrWhiteSpace(r.Name) ? r.LoginId : r.Name!;

    /// <summary>기간 조건. endDate 는 그 날의 끝까지 넣는다(날짜 칸이라 그대로 비교하면 마지막 날이 빠진다).</summary>
    private static IQueryable<Note> Period(IQueryable<Note> query, DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            query = query.Where(n => n.SentAt >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(n => n.SentAt < endDate.Value.ToUniversalTime().AddDays(1));
        }

        return query;
    }

    /// <summary>화면이 받는 모양으로 옮긴다. 치운 표시 둘은 남의 상태라 그대로 내보내지 않는다.</summary>
    private static NoteRowDto Row(Note n) => new()
    {
        Id = n.Id,
        SenderKey = n.SenderKey,
        SenderName = n.SenderName,
        ReceiverKey = n.ReceiverKey,
        ReceiverName = n.ReceiverName,
        Title = n.Title,
        Body = n.Body,
        SentAt = n.SentAt,
        ReadAt = n.ReadAt,
        PushSent = n.PushSent,
        EmailSent = n.EmailSent,
        NotifyNote = n.NotifyNote,
        DeliveredAt = n.DeliveredAt,
        FallbackEmailAt = n.FallbackEmailAt,
    };
}
