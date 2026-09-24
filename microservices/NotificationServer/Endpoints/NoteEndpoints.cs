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
///
/// <para>
/// [두드림을 <b>보내는 사람이 고르지 않는다</b>]
/// </para>
///
/// <para>
/// 한동안 쓰는 화면에 「앱 푸시」·「메일」 체크가 있었다. 그런데 그것은 <b>받는
/// 사람의 사정</b>이다 — 메일을 한 통 더 받을지, 알림을 받을지는 받는 쪽이 자기
/// 설정에서 정할 일이지 보내는 쪽이 매번 고를 일이 아니다. 지금은 둘로 갈린다.
/// </para>
///
/// <list type="bullet">
///   <item><description><b>앱 푸시는 늘 간다.</b> 그리고 <b>푸시를 꺼 둔 사람은
///   아예 받지 못한다</b> — 쪽지함에만 쌓이면 본인은 왔다는 것조차 모르는데
///   보낸 쪽은 보냈다고 믿게 되기 때문이다. 그런 사람은 결과의
///   <c>Blocked</c> 에 담아 이름을 짚어 돌려준다.</description></item>
///   <item><description><b>메일은 받는 사람이 켜 둔 경우에만</b> 간다
///   (개인설정의 「쪽지 메일받기」 — 기본은 꺼짐). 본문은 회사 메일 틀을 입힌
///   HTML 이다(<c>NoticeEmailTemplate</c>).</description></item>
/// </list>
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

    /// <summary>
    /// 한국 시간대. 지어 주는 제목에 <b>사람이 읽는 시각</b>을 적으려고 쓴다.
    /// </summary>
    /// <remarks>
    /// 저장은 UTC 로 한다(<c>Note.SentAt</c>). 제목은 저장된 값이 아니라
    /// <b>글자</b>라 나중에 화면이 고쳐 줄 수 없어서, 만들 때 이미 한국 시각이어야
    /// 한다. 리눅스와 윈도우의 아이디가 달라 둘 다 시도한다
    /// (<c>AuthServer.BirthdayEndpoints</c> 와 같은 꼴).
    /// </remarks>
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

            // **제목이 아니라 내용을 받는다.** 제목은 비면 지어 주지만
            // (`DefaultTitle`) 내용이 비면 지어 줄 것이 없다 — 빈 쪽지는
            // 받는 사람에게 알림 한 번일 뿐 아무 말도 아니다.
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

            // **푸시를 꺼 둔 사람은 받지 못한다.** 쪽지함에만 넣어 두면 본인은
            // 왔다는 것조차 모르는데 보낸 쪽은 보냈다고 믿는다 — 그 조용한
            // 어긋남이 「없는 아이디」보다 나쁘다.
            var found = resolved.Where(r => r.PushEnabled).ToList();
            var blocked = resolved.Where(r => !r.PushEnabled).Select(Display).ToList();

            if (found.Count == 0)
            {
                // **무엇이 틀렸는지 짚어 준다.** 「받는 사람이 없습니다」로만 답하면
                // 아이디를 잘못 친 것인지 그 사람이 푸시를 꺼 둔 것인지 알 수 없다.
                var why = new List<string>();

                if (unknown.Count > 0) why.Add($"그런 아이디·이메일이 없습니다: {string.Join(", ", unknown)}");
                if (blocked.Count > 0) why.Add($"푸시 알림을 꺼 두어 쪽지를 받지 못합니다: {string.Join(", ", blocked)}");
                if (why.Count == 0) why.Add("받는 사람을 적으십시오");

                return Results.BadRequest(ApiResponse<SendNoteResultDto>.Fail(
                    message: $"보낼 사람을 찾지 못했습니다 ({string.Join(" · ", why)}).", code: "NO_RECIPIENT"));
            }

            // 보낸 이의 이름. 게이트웨이가 헤더로 준다 — 없으면 아이디를 쓴다.
            var senderName = string.IsNullOrWhiteSpace(user.Name) ? user.UserId : user.Name;

            var now = DateTime.UtcNow;

            // **제목을 비우면 지어 넣는다.** 쪽지 대부분은 한두 줄이라 제목이
            // 내용과 같은 말이 되는데, 그렇다고 제목 칸을 없애면 쪽지함의 목록이
            // 통째로 빈 줄이 된다. 「누가 언제 보냈다」는 목록에서 사람이 실제로
            // 찾는 두 가지다.
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

            // **글을 먼저 확정한다.** 두드림을 먼저 보내면, 저장이 깨졌을 때
            // 「알림은 왔는데 쪽지함이 비어 있다」가 된다 — 그쪽이 훨씬 나쁘다.
            await db.SaveChangesAsync(ct);

            var trouble = new List<string>();

            // ── 앱 푸시 ─────────────────────────────────────
            //
            // **늘 보낸다.** 받을지 말지는 이미 위에서 갈렸다(푸시를 끈 사람은
            // 여기까지 오지 않는다). 아이콘은 **보낸 사람의 얼굴**이다
            // (`PushMessageDto.IconOwnerKey`) — 받은 쪽이 먼저 묻는 것이
            // 「누가 보냈나」라서다.
            var pushDevices = 0;

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

            // ── 메일 ────────────────────────────────────────
            //
            // **받는 사람이 켜 둔 경우에만 간다**(개인설정 › 쪽지 메일받기).
            // 기본이 꺼짐이므로 아무도 안 켰으면 조용히 건너뛴다 — 그것은
            // 사고가 아니라 사람들의 뜻이라 결과에 적지 않는다.
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
                // 주소가 없으면 켜 두었어도 보낼 데가 없다. **조용히 빼지 않고**
                // 몇 명이 빠졌는지 결과에 적는다.
                var withAddress = wantMail
                    .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                    .ToList();

                var without = wantMail.Count - withAddress.Count;

                if (without > 0)
                {
                    trouble.Add($"쪽지 메일을 켜 두었으나 주소가 없는 사람 {without}명");
                }

                // **한 사람에 한 통씩 보낸다.** 주소를 쉼표로 이어 한 통으로 보내면
                // 받는 사람들이 서로의 메일 주소를 보게 된다 — 쪽지는 사람 대 사람의
                // 글이라 그 명단이 새어서는 안 된다.
                foreach (var r in withAddress)
                {
                    try
                    {
                        // **틀은 여기서 짜지 않는다.** 평문을 회사 메일 꼴에 입히는
                        // 일은 한 곳에만 있다(`NoticeEmailTemplate`) — 보내는 자리마다
                        // HTML 을 짜면 틀이 그 수만큼 복제된다. 평문 갈래도 함께 실어
                        // 평문으로만 읽는 클라이언트에서 빈 칸이 되지 않게 한다.
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

            // 두드림 결과를 쪽지에 적어 둔다 — 보낸함이 그대로 읽는다.
            // **메일은 사람마다 갈린다**(켠 사람에게만 갔다). 한 값으로 뭉치면
            // 보낸함이 「메일 보냄」이라고 말하는데 정작 그 사람은 안 받은 꼴이 된다.
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
    /// 제목을 비우고 보냈을 때 대신 적는 한 줄 — <b>「누가 언제 보냈다」</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 쪽지는 대개 한두 줄이라 제목이 내용과 같은 말이 된다. 그래서 쓰는 화면이
    /// 제목 칸을 접어 두는데(<c>NoteWritePanel</c>), 그렇다고 제목을 빈 채로 두면
    /// <b>쪽지함 목록이 통째로 빈 줄</b>이 되고 앱 푸시의 본문도 비어 버린다.
    /// </para>
    ///
    /// <para>
    /// 내용의 첫 줄을 잘라 쓰는 길도 있지만 그러지 않는다 — 목록에서 본문이 한 번,
    /// 열어서 또 한 번 같은 글자를 읽게 되고, 첫 줄이 「안녕하세요」인 쪽지가
    /// 여럿이면 목록에서 서로 구별되지 않는다. <b>누가 언제</b>가 그 자리에서
    /// 사람이 실제로 찾는 두 가지다.
    /// </para>
    /// </remarks>
    private static string DefaultTitle(string senderName, DateTime sentAtUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(sentAtUtc, DateTimeKind.Utc), KoreaZone);

        return $"{senderName} 님이 {local:yyyy-MM-dd HH:mm} 에 보낸 쪽지";
    }

    /// <summary>사람을 가리키는 한 마디. 이름이 없으면 아이디를 쓴다.</summary>
    private static string Display(NoteRecipientDto r) =>
        string.IsNullOrWhiteSpace(r.Name) ? r.LoginId : r.Name!;

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
