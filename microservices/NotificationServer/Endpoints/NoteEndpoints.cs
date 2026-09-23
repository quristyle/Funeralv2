using JSini.Shared.DTOs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 쪽지 엔드포인트 (<c>/api/notification/notes/*</c>)
/// </summary>
/// <remarks>
/// <para>
/// [왜 알림 서비스에 있나]
/// </para>
///
/// <para>
/// 쪽지 한 통은 <b>글 하나 + 두드림 둘</b>이다. 그 두드림(앱 푸시 · 메일)은 이미
/// 전부 이 서비스에 있고, 보내는 사람의 얼굴을 아이콘으로 다는 것도
/// (<c>IconOwnerKey</c> → <c>IAvatarIconResolver</c>) 여기 있다. 받는 사람을
/// 아이디·이메일로 푸는 자리도 마찬가지다(<c>EmailEndpoints</c> 의 <c>toUser</c>).
/// 글을 담을 표 하나만 더하면 되는데 그것 때문에 서비스를 새로 세우면, <b>두드림을
/// 부르려고 서비스를 건너뛰는 길</b>이 하나 더 생긴다.
/// </para>
///
/// <para>
/// [이 서비스는 「보내는 일만 한다」는 규칙을 어기는가]
/// </para>
///
/// <para>
/// 어기지 않는다. 그 규칙이 막는 것은 <b>「누구에게 보낼지」를 이 서비스가 아는
/// 것</b>이다(팀·회사·담당자 같은 업무 판정). 쪽지의 받는 사람은 업무 판정이 아니라
/// <b>사람이 손으로 적은 글자</b>이고, 그것을 계정으로 푸는 표는 이 서비스가 원래
/// 읽는 <c>scom</c> 에 있다.
/// </para>
///
/// <para>
/// [두드림이 막혀도 쪽지는 성공이다]
/// </para>
///
/// <para>
/// 푸시가 0 대에 갔다고 실패로 답하면 보낸 사람이 같은 쪽지를 한 번 더 보낸다 —
/// 그 사이 상대의 쪽지함에는 이미 두 통이 쌓인다. <b>쪽지함에 들어갔으면 성공</b>이고,
/// 두드림이 어디까지 갔는지는 결과 안에 따로 담는다(<see cref="SendNoteResultDto"/>).
/// </para>
/// </remarks>
public static class NoteEndpoints
{
    /// <summary>제목 · 본문의 길이 상한. 화면이 아니라 여기가 정본이다.</summary>
    private const int MaxTitle = 200;

    private const int MaxBody = 4000;

    /// <summary>
    /// 쪽지를 눌렀을 때 열 화면. <b>알림과 메일이 같은 곳을 가리킨다.</b>
    /// </summary>
    private const string InboxUrl = "/admin/note/box";

    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes").WithTags("Notes");

        // ── 받는 사람 찾기 ──────────────────────────────────
        //
        // 아이디를 정확히 아는 사람은 그냥 치면 되지만, 대개는 이름만 안다.
        // **보내기는 이 결과를 믿지 않는다** — 같은 규칙으로 다시 푼다
        // (`NoteRecipientResolver` 머리말).
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

        // ── 쪽지 보내기 ─────────────────────────────────────
        group.MapPost("", async (
            [FromBody] SendNoteDto request,
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INoteRecipientResolver resolver,
            [FromServices] IPushSender push,
            [FromServices] IEmailSender email,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var logger = loggerFactory.CreateLogger("NoteEndpoints");

            var title = (request.Title ?? string.Empty).Trim();
            var body = (request.Body ?? string.Empty).Trim();

            if (title.Length == 0)
            {
                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: "쪽지 제목이 필요합니다.", code: "INVALID"));
            }

            if (title.Length > MaxTitle || body.Length > MaxBody)
            {
                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: $"제목은 {MaxTitle}자, 내용은 {MaxBody}자를 넘을 수 없습니다.",
                    code: "TOO_LONG"));
            }

            var tokens = (request.To ?? string.Empty)
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var (found, unknown) = await resolver.ResolveAsync(tokens, ct);

            if (found.Count == 0)
            {
                // **무엇이 틀렸는지 짚어 준다.** 「받는 사람이 없습니다」로만 답하면
                // 아이디를 잘못 친 것인지 그 계정에 메일이 없는 것인지 알 수 없다.
                var why = unknown.Count > 0
                    ? $"그런 아이디·이메일이 없습니다: {string.Join(", ", unknown)}"
                    : "받는 사람을 적으십시오.";

                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: $"보낼 사람을 찾지 못했습니다 ({why}).", code: "NO_RECIPIENT"));
            }

            // 보낸 이의 이름. 게이트웨이가 헤더로 준다 — 없으면 아이디를 쓴다.
            var senderName = string.IsNullOrWhiteSpace(user.Name) ? user.UserId : user.Name;

            var now = DateTime.UtcNow;

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

            // **글을 먼저 확정한다.** 두드림을 먼저 보내면, 저장이 깨졌을 때
            // 「알림은 왔는데 쪽지함이 비어 있다」가 된다 — 그쪽이 훨씬 나쁘다.
            await db.SaveChangesAsync(ct);

            var trouble = new List<string>();

            // ── 앱 푸시 ─────────────────────────────────────
            //
            // 아이콘은 **보낸 사람의 얼굴**이다(`PushMessageDto.IconOwnerKey`).
            // 받은 쪽이 먼저 묻는 것이 「누가 보냈나」라서다.
            var pushDevices = 0;

            if (request.Push)
            {
                try
                {
                    var result = await push.SendAsync(new SendPushDto
                    {
                        Owners = [.. found.Select(r => new OwnerRefDto
                        {
                            OwnerType = "jsini",
                            OwnerKey = r.LoginId,
                        })],
                        Message = new PushMessageDto
                        {
                            Title = $"쪽지 · {senderName}",
                            Body = title,
                            Url = InboxUrl,
                            IconOwnerKey = user.UserId,
                        },
                    }, user.UserId, ct);

                    pushDevices = result.Sent;

                    if (result.Sent == 0)
                    {
                        trouble.Add(result.Message ?? "앱 알림이 가지 않았습니다");
                    }
                }
                catch (Exception ex)
                {
                    // **쪽지는 이미 들어갔다.** 두드림 하나 때문에 실패로 답하지 않는다.
                    logger.LogWarning(ex, "쪽지 앱 알림을 보내지 못했습니다. by={By}", user.UserId);
                    trouble.Add("앱 알림을 보내지 못했습니다");
                }
            }

            // ── 메일 ────────────────────────────────────────
            //
            // 주소가 없는 사람은 뺀다 — 보낼 데가 없다. **조용히 빼지 않고**
            // 몇 명이 빠졌는지 결과에 적는다.
            var emailSent = 0;

            if (request.Email)
            {
                var addresses = found
                    .Select(r => r.Email)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var without = found.Count - addresses.Count;

                if (addresses.Count == 0)
                {
                    trouble.Add("메일 주소가 등록된 사람이 없습니다");
                }
                else
                {
                    try
                    {
                        // **틀은 여기서 짜지 않는다.** 평문을 회사 메일 꼴에 입히는
                        // 일은 한 곳에만 있다(`NoticeEmailTemplate`) — 보내는 자리마다
                        // HTML 을 짜면 틀이 그 수만큼 복제된다.
                        await email.SendAsync(
                            string.Join(",", addresses),
                            title,
                            NoticeEmailTemplate.Render(title, body, senderName),
                            html: true,
                            attachments: null,
                            textBody: NoticeEmailTemplate.PlainAlternative(title, body, senderName));

                        emailSent = addresses.Count;

                        if (without > 0)
                        {
                            trouble.Add($"메일 주소가 없는 사람 {without}명");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "쪽지 메일을 보내지 못했습니다. by={By}", user.UserId);
                        trouble.Add("메일을 보내지 못했습니다");
                    }
                }
            }

            var notifyNote = trouble.Count > 0 ? string.Join(" · ", trouble) : null;

            // 두드림 결과를 쪽지에 적어 둔다 — 보낸함이 그대로 읽는다.
            foreach (var note in notes)
            {
                note.PushSent = pushDevices > 0;
                note.EmailSent = emailSent > 0;
                note.NotifyNote = notifyNote;
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "쪽지 {Count}통. by={By} push={Push} mail={Mail} 못푼값={Unknown}",
                notes.Count, user.UserId, pushDevices, emailSent, unknown.Count);

            return Results.Ok(ApiResponse<SendNoteResultDto>.Ok(new SendNoteResultDto
            {
                Sent = notes.Count,
                PushDevices = pushDevices,
                EmailSent = emailSent,
                Unknown = unknown,
                Recipients = [.. found.Select(r => string.IsNullOrWhiteSpace(r.Name) ? r.LoginId : r.Name!)],
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

            // **받아 와서 옮긴다.** `Select(n => Row(n))` 처럼 우리 메서드를 투영에
            // 넣으면 EF 가 번역하지 못해 요청이 통째로 죽는다(게이트웨이에는 502 로
            // 보인다). 상한이 걸린 목록이라 메모리에서 옮기는 편이 안전하다.
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
        //
        // 상단 띠의 봉투에 붙는 숫자다. **가장 자주 불리는 자리**라 줄을 세기만 한다.
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
        //
        // **받은 사람만 찍을 수 있다.** 열쇠만 보고 갱신하면 아이디를 아는 사람이
        // 남의 쪽지를 읽은 것으로 만들 수 있다.
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
        //
        // **보낸 쪽과 받은 쪽이 따로다.** 보낸 사람이 자기 보낸함에서 치웠다고
        // 남의 우편함을 비울 수는 없다(`Note` 머리말).
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

            // 양쪽 다 치운 줄만 실제로 지운다 — 한쪽이라도 보고 있으면 남긴다.
            note.IsDeleted = note.ReceiverDeleted && note.SenderDeleted;
            note.UpdatedAt = DateTime.UtcNow;
            note.UpdatedBy = user.UserId;

            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("DeleteNote");
    }

    /// <summary>
    /// 기간 조건. <paramref name="endDate"/> 는 <b>그 날의 끝까지</b> 넣는다 —
    /// 날짜만 받는 칸이라 그대로 비교하면 마지막 날이 통째로 빠진다.
    /// </summary>
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

    /// <summary>
    /// 엔티티를 화면이 받는 모양으로. <b>그대로 내보내지 않는다</b> — 치운 표시
    /// 둘은 남의 상태라 화면에 보낼 것이 아니다.
    /// </summary>
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
    };
}
