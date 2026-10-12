using System.Net.Mail;

using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 포털 「메일 보내기」 (<c>/api/notification/mails/*</c>) — <b>사람이 쓰는 편지</b>.
/// </summary>
/// <remarks>
/// <para>
/// [<c>/emails/send</c> 와 무엇이 다른가]
/// </para>
///
/// <para>
/// 저쪽은 <b>시스템이 사람에게</b> 보내는 알림의 통로다 — 역할·아이디로 받는
/// 사람을 풀고, 알림관리 정책과 본인의 수신 설정을 거치고, 평문이면 회사
/// 메일 틀을 입힌다. 이쪽은 <b>사람이 사람에게</b> 쓰는 편지라 받는 사람이
/// 주소 그 자체이고, 서식 본문을 쓴 사람이 완성해 보낸다.
/// </para>
///
/// <para>
/// 그리고 <b>보낸 글이 남는다.</b> 그것이 이 통로를 따로 둔 가장 큰 까닭이다 —
/// 저쪽이 남기는 발송 기록은 주소마다 한 줄에 본문 미리보기뿐이라 「내가 보낸
/// 메일」 목록이 되지 못한다(<see cref="PortalMail"/> 머리말). 발송 기록은
/// <b>여기서도 그대로 남긴다</b>(<see cref="EmailSendLog"/>) — 「저 사람에게
/// 무엇이 나갔나」를 묻는 자리가 계정 앱 현황이고, 이 통로로 보낸 메일만
/// 거기서 빠지면 그 화면이 거짓말을 한다.
/// </para>
///
/// <para>
/// [남의 보낸함을 여는 길은 없다]
/// </para>
///
/// <para>
/// 목록 · 상세 · 지우기 · 최근 주소가 모두 <c>X-User-Id</c> 로 제 것만 본다.
/// 메뉴 권한(시스템관리자)은 <b>이 화면을 열 수 있나</b>를 정할 뿐이고,
/// 열고 나서 보이는 것은 언제나 자기 편지다 — 관리자라고 남의 편지를 읽게
/// 하면 그것은 다른 기능이고, 만들 거라면 그때 따로 만든다.
/// </para>
/// </remarks>
public static class MailEndpoints
{
    /// <summary>
    /// 목록이 한 번에 주는 최대 줄 수. <b>기간이 조건이라 보통 이보다 적다</b> —
    /// 이 값은 「기간을 넓게 잡은 사람」이 표를 통째로 끌어오지 않게 하는 빗장이다.
    /// </summary>
    private const int MaxTake = 500;

    public static void MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mails").WithTags("Mails");

        // ── 보내기 ──────────────────────────────────────────
        group.MapPost("/", async (
            [FromBody] MailComposeDto request,
            UserContext? user,
            [FromServices] IEmailSender sender,
            [FromServices] AppDbContext db,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var logger = loggerFactory.CreateLogger("MailEndpoints");

            if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<MailSendResultDto>.Fail(
                    message: "제목과 본문이 모두 필요합니다.", code: "INVALID"));
            }

            // **주소 꼴을 여기서 가른다.** 성한 것만 모으고 아닌 것은 들고
            // 간다 — 조용히 빼면 오타 하나가 사라지고, 나머지에게는 이미 나간
            // 뒤라 고쳐 다시 보낼 때 받은 사람이 같은 메일을 두 번 받는다.
            var (to, badTo) = SplitAddresses(request.To);
            var (cc, badCc) = SplitAddresses(request.Cc);

            var bad = badTo.Concat(badCc).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (bad.Count > 0)
            {
                return Results.BadRequest(ApiResponse<MailSendResultDto>.Fail(
                    message: $"주소 꼴이 아닙니다: {string.Join(", ", bad.Take(5))}"
                             + (bad.Count > 5 ? " 외" : ""),
                    code: "BAD_ADDRESS"));
            }

            if (to.Count == 0)
            {
                return Results.BadRequest(ApiResponse<MailSendResultDto>.Fail(
                    message: "받는 사람이 없습니다.", code: "NO_RECIPIENT"));
            }

            // 상한과 판정은 알림 직발송과 한 곳을 쓴다(`EmailAttachmentGuard`).
            if (!EmailAttachmentGuard.TryCheck(
                    request.Attachments, out var attachmentBytes, out var fileError, out var fileCode))
            {
                return Results.BadRequest(ApiResponse<MailSendResultDto>.Fail(
                    message: fileError!, code: fileCode!));
            }

            // **참조에만 있는 주소는 받는 사람에서 뺀다.** 양쪽에 같은 주소를
            // 적는 일이 흔한데(「아무개에게 보내고 아무개를 참조」) 그대로
            // 보내면 한 사람이 같은 메일을 두 통 받는다.
            cc = [.. cc.Where(c => !to.Contains(c, StringComparer.OrdinalIgnoreCase))];

            var mail = new PortalMail
            {
                SenderKey = user.UserId,
                SenderName = user.Name,
                ToAddresses = string.Join(", ", to),
                CcAddresses = cc.Count > 0 ? string.Join(", ", cc) : null,
                Subject = request.Subject.Trim(),
                Body = request.Body,
                Preview = EmailSendLog.Preview(request.Body, html: true),
                IsHtml = true,
                AttachmentCount = request.Attachments.Count,
                AttachmentNames = request.Attachments.Count > 0
                    ? string.Join(", ", request.Attachments.Select(f => f.FileName))
                    : null,
                AttachmentBytes = attachmentBytes,
                HasAiSummary = request.HasAiSummary,
                SentAt = DateTime.UtcNow,
                CreatedBy = user.UserId,
            };

            try
            {
                // **본문에 틀을 덧입히지 않는다.** 서식 편집기로 쓴 글은 이미
                // 완성된 문서라, 회사 메일 틀을 한 겹 더 씌우면 클라이언트마다
                // 다르게 무너진다(`EmailEndpoints` 의 같은 자리).
                await sender.SendAsync(
                    string.Join(",", to), mail.Subject!, request.Body,
                    html: true, request.Attachments, textBody: null,
                    cc: cc.Count > 0 ? string.Join(",", cc) : null);

                mail.IsSuccess = true;

                logger.LogInformation(
                    "메일을 보냈습니다. to={To} cc={Cc} files={Files} by={By}",
                    mail.ToAddresses, mail.CcAddresses, mail.AttachmentCount, user.UserId);
            }
            catch (Exception ex)
            {
                // **실패도 보관한다.** 다시 열어 고쳐 보내야 하는 글이 바로
                // 이것이다 — 여기서 버리면 사람이 쓴 글이 통째로 사라진다.
                mail.IsSuccess = false;
                mail.FailureReason = "메일 서버가 받지 않음";

                logger.LogError(ex, "메일 발송 실패. to={To} by={By}",
                    mail.ToAddresses, user.UserId);
            }

            // **발송 기록은 성공·실패를 가리지 않고 남긴다.** 참조도 받는
            // 사람으로 센다 — 그 주소에도 실제로 메일이 갔고, 「저 사람에게
            // 무엇이 나갔나」에 참조로 받은 메일이 빠지면 안 된다.
            mail.BatchId = await EmailSendLog.WriteAsync(
                db, logger, [.. to, .. cc], mail.Subject, request.Body, html: true,
                user.UserId, mail.IsSuccess, mail.FailureReason, ct);

            db.PortalMails.Add(mail);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // **메일은 이미 나갔다.** 여기서 던지면 보낸 사람이 실패로 읽고
                // 다시 누른다 — 받는 쪽은 같은 메일을 두 통 받는다. 보관을
                // 못 했다는 사실만 말한다.
                logger.LogError(ex, "보낸 메일을 보관하지 못했습니다. by={By}", user.UserId);

                db.Entry(mail).State = EntityState.Detached;

                return Results.Ok(ApiResponse<MailSendResultDto>.Ok(new MailSendResultDto
                {
                    Id = mail.Id,
                    Sent = mail.IsSuccess,
                    ToCount = to.Count,
                    CcCount = cc.Count,
                    AttachmentCount = mail.AttachmentCount,
                    Message = mail.IsSuccess
                        ? "메일은 보냈지만 보낸 메일함에 남기지 못했습니다."
                        : "메일을 보내지 못했습니다.",
                }));
            }

            if (!mail.IsSuccess)
            {
                // 실패는 실패로 말한다 — 부르는 쪽이 다시 보낼지 정한다.
                // 보관함에는 이미 들어갔으므로 아이디를 함께 준다.
                return Results.Json(
                    ApiResponse<MailSendResultDto>.Fail(
                        "메일 발송에 실패했습니다. 보낸 메일함에 남겨 두었으니 다시 보낼 수 있습니다.",
                        "EMAIL_SEND_FAILED"),
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(ApiResponse<MailSendResultDto>.Ok(new MailSendResultDto
            {
                Id = mail.Id,
                Sent = true,
                ToCount = to.Count,
                CcCount = cc.Count,
                AttachmentCount = mail.AttachmentCount,
                Message = "메일을 보냈습니다.",
            }, "메일을 보냈습니다."));
        })
        .WithName("SendPortalMail");

        // ── 보낸 메일함 ────────────────────────────────────
        group.MapGet("/", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? keyword,
            [FromQuery] int? take,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var query = db.PortalMails.AsNoTracking()
                .Where(m => m.SenderKey == user.UserId && !m.IsDeleted);

            // **기간은 한국 달력으로 센다.** 사람이 고른 것은 날짜이고 값은
            // UTC 시각이라, 그냥 견주면 **한국의 자정부터 오전 9시까지 아홉
            // 시간**이 하루씩 어긋난다 — 아침에 보낸 메일이 「어제」로 가고,
            // 「오늘」로 거르면 안 나온다. 컨테이너 시계가 UTC 라
            // (`TZ=Etc/UTC`) `ToUniversalTime()` 으로는 못 고친다. 개발
            // 장비에서는 멀쩡해 보이므로 더 나쁘다(docs/utc-time.md).
            if (from is { } f)
            {
                var start = AppTime.StartOfDayUtc(DateOnly.FromDateTime(f));
                query = query.Where(m => m.SentAt >= start);
            }

            // **끝 날짜는 그날을 포함한다.** 다음 한국 날짜의 0시 앞까지다 —
            // 그대로 견주면 고른 그날이 통째로 빠진다.
            if (to is { } t)
            {
                var end = AppTime.StartOfDayUtc(DateOnly.FromDateTime(t).AddDays(1));
                query = query.Where(m => m.SentAt < end);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";

                query = query.Where(m =>
                    EF.Functions.ILike(m.Subject ?? "", like)
                    || EF.Functions.ILike(m.ToAddresses, like)
                    || EF.Functions.ILike(m.CcAddresses ?? "", like)
                    || EF.Functions.ILike(m.Preview ?? "", like));
            }

            var rows = await query
                .OrderByDescending(m => m.SentAt)
                .Take(Math.Clamp(take ?? 200, 1, MaxTake))
                .Select(m => new MailListItemDto
                {
                    Id = m.Id,
                    SentAt = m.SentAt,
                    To = m.ToAddresses,
                    Cc = m.CcAddresses,
                    Subject = m.Subject,
                    Preview = m.Preview,
                    AttachmentCount = m.AttachmentCount,
                    IsSuccess = m.IsSuccess,
                    FailureReason = m.FailureReason,
                    HasAiSummary = m.HasAiSummary,
                })
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<MailListItemDto>>.Ok(rows));
        })
        .WithName("ListPortalMails");

        // ── 한 통 펼치기 ───────────────────────────────────
        group.MapGet("/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var mail = await db.PortalMails.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id && m.SenderKey == user.UserId && !m.IsDeleted, ct);

            if (mail is null)
            {
                // **「남의 것이다」와 「없다」를 가르지 않는다.** 가르면 남의
                // 메일 아이디를 하나씩 넣어 보는 것으로 있고 없음을 알 수 있다.
                return Results.NotFound(ApiResponse<MailDetailDto>.Fail(
                    message: "메일을 찾지 못했습니다.", code: "NOT_FOUND"));
            }

            return Results.Ok(ApiResponse<MailDetailDto>.Ok(new MailDetailDto
            {
                Id = mail.Id,
                SentAt = mail.SentAt,
                To = mail.ToAddresses,
                Cc = mail.CcAddresses,
                Subject = mail.Subject,
                Preview = mail.Preview,
                Body = mail.Body,
                IsHtml = mail.IsHtml,
                AttachmentCount = mail.AttachmentCount,
                AttachmentNames = Split(mail.AttachmentNames),
                AttachmentBytes = mail.AttachmentBytes,
                IsSuccess = mail.IsSuccess,
                FailureReason = mail.FailureReason,
                HasAiSummary = mail.HasAiSummary,
                SenderName = mail.SenderName,
            }));
        })
        .WithName("GetPortalMail");

        // ── 보낸함에서 치우기 ──────────────────────────────
        group.MapDelete("/{id}", async (
            string id,
            UserContext? user,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            var mail = await db.PortalMails
                .FirstOrDefaultAsync(m => m.Id == id && m.SenderKey == user.UserId && !m.IsDeleted, ct);

            if (mail is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail(
                    message: "메일을 찾지 못했습니다.", code: "NOT_FOUND"));
            }

            // **표시만 한다.** 발송 기록(`push_send_logs`)은 그대로 남는다 —
            // 보낸 사람이 자기 보관함을 치우는 것과 「무엇이 나갔나」의 기록을
            // 지우는 것은 다른 일이다. 쪽지에서 지우기를 쪽마다 가른 것과
            // 같은 까닭이다(`Note` 머리말).
            mail.IsDeleted = true;
            mail.UpdatedAt = DateTime.UtcNow;
            mail.UpdatedBy = user.UserId;

            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<bool>.Ok(true, "보낸 메일함에서 치웠습니다."));
        })
        .WithName("DeletePortalMail");

        // ── 최근에 쓴 주소 ─────────────────────────────────
        //
        // **계정 목록과 갈래가 다르다.** 그쪽은 「우리 사람 전부」이고 이쪽은
        // 「내가 실제로 보낸 곳」이다 — 거래처처럼 계정이 없는 상대는 여기에만
        // 있고, 그 주소야말로 다시 칠 일이 가장 많다.
        group.MapGet("/recent-addresses", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int? take,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // **최근 몇 통만 본다.** 보낸함 전체를 훑어 세면 오래 쓸수록
            // 느려지는데, 「최근 주소」가 답해야 하는 것은 어차피 최근이다.
            var recent = await db.PortalMails.AsNoTracking()
                .Where(m => m.SenderKey == user.UserId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Take(200)
                .Select(m => new { m.ToAddresses, m.CcAddresses, m.SentAt })
                .ToListAsync(ct);

            var seen = new Dictionary<string, MailAddressUseDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in recent)
            {
                // 참조도 함께 센다 — 다음에 보낼 때 받는 사람이 될 주소다.
                foreach (var address in Split(row.ToAddresses).Concat(Split(row.CcAddresses)))
                {
                    if (seen.TryGetValue(address, out var hit))
                    {
                        hit.UseCount++;

                        if (row.SentAt > hit.LastUsedAt) hit.LastUsedAt = row.SentAt;
                    }
                    else
                    {
                        seen[address] = new MailAddressUseDto
                        {
                            Address = address,
                            UseCount = 1,
                            LastUsedAt = row.SentAt,
                        };
                    }
                }
            }

            // **최근 순이 먼저다.** 많이 쓴 순으로 두면 한 번 자주 보낸 주소가
            // 영영 맨 위에 남아, 어제 쓴 주소를 찾으러 목록을 뒤지게 된다.
            var result = seen.Values
                .OrderByDescending(a => a.LastUsedAt)
                .ThenByDescending(a => a.UseCount)
                .Take(Math.Clamp(take ?? 20, 1, 100))
                .ToList();

            return Results.Ok(ApiResponse<List<MailAddressUseDto>>.Ok(result));
        })
        .WithName("RecentMailAddresses");
    }

    /// <summary>
    /// 주소 칸을 하나씩으로 가르고 <b>성한 것과 아닌 것을 갈라</b> 돌려준다.
    /// </summary>
    /// <remarks>
    /// 쉼표·세미콜론에 <b>줄바꿈까지</b> 받는다 — 어디선가 복사해 붙이면 줄로
    /// 오는 일이 흔하다. 화면도 같은 규칙으로 가르지만(`MailSendPage`) 다른
    /// 길로 들어온 요청이 있을 수 있어 여기서 다시 본다.
    /// </remarks>
    private static (List<string> Good, List<string> Bad) SplitAddresses(string? text)
    {
        var good = new List<string>();
        var bad = new List<string>();

        foreach (var part in (text ?? string.Empty)
                     .Split([',', ';', '\n', '\r', '\t'],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (MailAddress.TryCreate(part, out _)) good.Add(part);
            else bad.Add(part);
        }

        return (good, bad);
    }

    /// <summary>쉼표로 이어 둔 값을 되가른다. 비면 빈 목록.</summary>
    private static List<string> Split(string? text) =>
        [.. (text ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
