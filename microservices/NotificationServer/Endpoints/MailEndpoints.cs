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

/// <summary>포털 「메일 보내기」(/api/notification/mails/*). 사람이 쓴 편지라 주소를 그대로 받고 보낸 글을 전문으로 남긴다.
/// 목록·상세·지우기는 X-User-Id 로 제 것만 본다. 메뉴 권한은 화면을 여는 것만 정한다.</summary>
public static class MailEndpoints
{
    /// <summary>목록이 한 번에 주는 최대 줄 수. 기간을 넓게 잡은 사람이 표를 통째로 끌어오지 않게 막는 빗장이다.</summary>
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

            // 주소 꼴을 가른다(성한 것만 보내고 나머지는 들고 간다 — 조용히 빼면 오타가 사라진다).
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

            // 참조에만 있는 주소는 받는 사람에서 뺀다(같은 메일을 두 통 받는 것을 막는다).
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
                // 본문에 틀을 덧입히지 않는다(서식 편집기 글은 이미 완성된 문서다).
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
                // 실패도 보관한다(고쳐 다시 보낼 글이 바로 이것이라 버리면 사람이 쓴 글이 사라진다).
                mail.IsSuccess = false;
                mail.FailureReason = "메일 서버가 받지 않음";

                logger.LogError(ex, "메일 발송 실패. to={To} by={By}",
                    mail.ToAddresses, user.UserId);
            }

            // 발송 기록은 성공·실패 모두 남기고, 참조도 받는 사람으로 센다.
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
                // 메일은 이미 나갔으므로 던지지 않는다(보관을 못 했다는 사실만 말한다).
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
                // 실패는 실패로 말한다(보관함에는 이미 들어갔으므로 아이디를 함께 준다).
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

            // 기간은 한국 달력으로 센다(값은 UTC 라 그냥 견주면 자정부터 아홉 시간이 어긋난다 — docs/utc-time.md).
            if (from is { } f)
            {
                var start = AppTime.StartOfDayUtc(DateOnly.FromDateTime(f));
                query = query.Where(m => m.SentAt >= start);
            }

            // 끝 날짜는 그날을 포함한다(다음 한국 날짜 0시 앞까지).
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
                // 남의 것과 없는 것을 가르지 않는다(가르면 아이디로 있고 없음을 알 수 있다).
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

            // 표시만 한다(발송 기록 push_send_logs 는 그대로 남긴다 — 보관함 치우기와 발송 기록 지우기는 다르다).
            mail.IsDeleted = true;
            mail.UpdatedAt = DateTime.UtcNow;
            mail.UpdatedBy = user.UserId;

            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<bool>.Ok(true, "보낸 메일함에서 치웠습니다."));
        })
        .WithName("DeletePortalMail");

        // ── 최근에 쓴 주소 ─────────────────────────────────
        // 계정 목록과 달리 「내가 실제로 보낸 곳」이다(계정 없는 거래처 주소가 여기에만 있다).
        group.MapGet("/recent-addresses", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromQuery] int? take,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // 최근 몇 통만 본다(전체를 훑으면 오래 쓸수록 느려진다).
            var recent = await db.PortalMails.AsNoTracking()
                .Where(m => m.SenderKey == user.UserId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Take(200)
                .Select(m => new { m.ToAddresses, m.CcAddresses, m.SentAt })
                .ToListAsync(ct);

            var seen = new Dictionary<string, MailAddressUseDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in recent)
            {
                // 참조도 함께 센다(다음에 보낼 때 받는 사람이 될 주소다).
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

            // 최근 순으로 세운다 — 많이 쓴 순이면 옛날에 자주 보낸 주소가 맨 위에 남는다.
            var result = seen.Values
                .OrderByDescending(a => a.LastUsedAt)
                .ThenByDescending(a => a.UseCount)
                .Take(Math.Clamp(take ?? 20, 1, 100))
                .ToList();

            return Results.Ok(ApiResponse<List<MailAddressUseDto>>.Ok(result));
        })
        .WithName("RecentMailAddresses");
    }

    /// <summary>주소 칸을 하나씩으로 가르고 성한 것과 아닌 것을 갈라 돌려준다. 쉼표·세미콜론·줄바꿈을 받는다.</summary>
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
