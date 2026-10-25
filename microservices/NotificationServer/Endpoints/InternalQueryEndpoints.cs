using JSini.Shared.Infrastructure.Time;
using JSini.Shared.Internal;

using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.Entities;

namespace NotificationServer.Endpoints;

/// <summary>
/// 업무 비서(AI)가 쓰는 <b>내부 전용</b> 조회. 게이트웨이의 사람 인증을 거치지 않고,
/// 서버 간 머리글(<see cref="InternalCallGuard"/>)만 본다.
/// </summary>
public static class InternalQueryEndpoints
{
    /// <summary>한 통의 공통 모양으로 맞춘 줄 — 보낸 메일함과 발송 기록을 한 목록으로 합치려고 쓴다.</summary>
    private sealed record MailRow(string Id, string? Subject, string? To, DateTime SentAt, string Kind);

    public static void MapInternalQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/internal");

        // 보낸 메일 — 포털 「메일 보내기」로 사람이 손으로 쓴 글(scom.portal_mails)과
        // 알림 직발송의 메일 줄(scom.push_send_logs, channel=email)을 합쳐서 낸다.
        // **받은 메일함은 없다** — 포털에 수신함이 없으므로 보낸 사람 기준만 본다.
        group.MapGet("/mails", async (
            HttpContext http,
            AppDbContext db,
            IConfiguration config,
            string? userId,
            DateTime? from,
            DateTime? to,
            string? keyword,
            int? take) =>
        {
            if (!InternalCallGuard.IsAllowed(http.Request.Headers["X-Internal-Token"], config["Assistant:InternalToken"]))
                return Results.NotFound();

            var ct = http.RequestAborted;
            var takeCount = Math.Clamp(take ?? 30, 1, 100);

            var mailQuery = db.PortalMails.AsNoTracking().Where(m => !m.IsDeleted).AsQueryable();
            var logQuery = db.PushSendLogs.AsNoTracking()
                .Where(l => l.Channel == PushSendLog.ChannelEmail)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                mailQuery = mailQuery.Where(m => m.SenderKey == userId);
                logQuery = logQuery.Where(l => l.SentBy == userId);
            }

            // 기간은 한국 달력으로 센다(값은 UTC 라 그냥 견주면 자정부터 아홉 시간이 어긋난다 — docs/utc-time.md).
            if (from is { } f)
            {
                var start = AppTime.StartOfDayUtc(DateOnly.FromDateTime(f));
                mailQuery = mailQuery.Where(m => m.SentAt >= start);
                logQuery = logQuery.Where(l => l.SentAt >= start);
            }

            // 끝 날짜는 그날을 포함한다(다음 한국 날짜 0시 앞까지).
            if (to is { } t)
            {
                var end = AppTime.StartOfDayUtc(DateOnly.FromDateTime(t).AddDays(1));
                mailQuery = mailQuery.Where(m => m.SentAt < end);
                logQuery = logQuery.Where(l => l.SentAt < end);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";

                mailQuery = mailQuery.Where(m =>
                    EF.Functions.ILike(m.Subject ?? "", like)
                    || EF.Functions.ILike(m.ToAddresses, like)
                    || EF.Functions.ILike(m.Preview ?? "", like));

                logQuery = logQuery.Where(l =>
                    EF.Functions.ILike(l.Title ?? "", like)
                    || EF.Functions.ILike(l.Endpoint ?? "", like)
                    || EF.Functions.ILike(l.Body ?? "", like));
            }

            // 전체 건수는 자르기 전 두 표를 각각 센 합이다.
            var total = await mailQuery.CountAsync(ct) + await logQuery.CountAsync(ct);

            // 합친 목록의 상위 take 건은 각 표의 상위 take 건 안에서만 나온다(병합 정렬과 같은 이치) —
            // 그래서 두 표를 각각 take 만큼만 가져와도 결과가 정확하다.
            var mails = await mailQuery
                .OrderByDescending(m => m.SentAt)
                .Take(takeCount)
                .Select(m => new MailRow(m.Id, m.Subject, m.ToAddresses, m.SentAt, "보낸 메일"))
                .ToListAsync(ct);

            var logs = await logQuery
                .OrderByDescending(l => l.SentAt)
                .Take(takeCount)
                .Select(l => new MailRow(l.Id, l.Title, l.Endpoint, l.SentAt, "발송 기록"))
                .ToListAsync(ct);

            var items = mails.Concat(logs)
                .OrderByDescending(r => r.SentAt)
                .Take(takeCount)
                .Select(r => new
                {
                    id = r.Id,
                    subject = r.Subject,
                    to = r.To,
                    sentAt = r.SentAt,
                    kind = r.Kind,
                });

            return Results.Ok(new { total, items });
        });
    }
}
