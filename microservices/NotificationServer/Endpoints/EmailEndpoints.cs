using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 이메일 직발송 엔드포인트 (<c>/api/notification/emails/*</c>)
/// </summary>
/// <remarks>
/// <para>
/// 이메일이 나가는 길이 둘이다:
/// <list type="bullet">
///   <item><description><c>POST /notifications/email</c> — <b>큐 방식.</b> 배포 장비의
///   스크립트가 실제로 보낸다. 결과는 "큐에 넣었다" 까지만 안다.</description></item>
///   <item><description><c>POST /emails/send</c> (이 파일) — <b>SMTP 직발송.</b>
///   <c>EmailSettings</c>(appsettings.Local.json)의 SMTP 서버로 즉시 보내고
///   성공·실패를 바로 안다. 문의 접수 알림처럼 "지금 갔는지" 가 중요한 곳에 쓴다.</description></item>
/// </list>
/// </para>
///
/// <para>
/// 받는 사람은 부르는 쪽이 정한다 (결정 D8-A). 다만 <b>역할로 받는 것</b>(<c>toRole</c>)은
/// 예외로 여기서 푼다 — 역할 → 이메일 명단은 scom(이 서비스의 DB)에 있고,
/// 다른 서비스(SiteServer 등)는 그 DB 를 볼 수 없기 때문이다.
/// </para>
/// </remarks>
public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/emails").WithTags("Emails");

        // ── SMTP 직발송 ─────────────────────────────────────
        //
        // 다른 엔드포인트처럼 부른 이(X-User-Id)를 요구한다. 게이트웨이를 거친
        // 호출은 게이트웨이가 채우고, 서비스 간 직접 호출은 자기 이름을 적어 보낸다
        // (예: SiteServer 가 "SITE_INQUIRY" 로 부른다).
        group.MapPost("/send", async (
            [FromBody] SendEmailDto request,
            UserContext? user,
            [FromServices] IEmailSender sender,
            [FromServices] AppDbContext db,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            if ((string.IsNullOrWhiteSpace(request.To) && string.IsNullOrWhiteSpace(request.ToRole)) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "받는 사람(to 또는 toRole) · 제목 · 본문이 모두 필요합니다.", code: "INVALID"));
            }

            var logger = loggerFactory.CreateLogger("EmailEndpoints");

            // 받는 사람을 모은다 — 직접 지정(to) + 역할(toRole) 해석
            var recipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.To))
            {
                recipients.AddRange(request.To
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            if (!string.IsNullOrWhiteSpace(request.ToRole))
            {
                recipients.AddRange(await ResolveRoleEmailsAsync(db, request.ToRole.Trim(), ct));
            }

            recipients = recipients
                .Where(r => System.Net.Mail.MailAddress.TryCreate(r, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                // 역할에 이메일 가진 사용자가 없을 수도 있다 — 조용히 성공으로 말하지 않는다.
                logger.LogWarning("이메일 받는 사람이 없습니다. toRole={Role} by={By}", request.ToRole, user.UserId);
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "받는 사람이 없습니다 (역할에 이메일이 등록된 사용자가 없습니다).",
                    code: "NO_RECIPIENT"));
            }

            try
            {
                await sender.SendAsync(string.Join(",", recipients), request.Subject, request.Body, request.Html);
                logger.LogInformation("이메일 직발송 완료. to={To} role={Role} by={By}",
                    string.Join(",", recipients), request.ToRole, user.UserId);
                return Results.Ok(ApiResponse<bool>.Ok(true, "메일을 보냈습니다."));
            }
            catch (Exception ex)
            {
                // 실패를 성공으로 말하지 않는다 — 부르는 쪽이 재시도 여부를 정한다.
                logger.LogError(ex, "이메일 직발송 실패. to={To} by={By}",
                    string.Join(",", recipients), user.UserId);
                return Results.Json(
                    ApiResponse<bool>.Fail("메일 발송에 실패했습니다.", "EMAIL_SEND_FAILED"),
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("SendEmailDirect")
        .WithOpenApi();
    }

    /// <summary>
    /// 역할 사용자들의 이메일을 푼다 — 계정마다 하나(대표 이메일 우선).
    /// scom 은 이 서비스의 DB 라 조회만 한다 (Entities/ScomIdentityRows.cs 머리말).
    /// </summary>
    private static async Task<List<string>> ResolveRoleEmailsAsync(
        AppDbContext db, string roleId, CancellationToken ct)
    {
        var rows = await (
            from ra in db.RoleAccounts
            where ra.RoleId == roleId && !ra.IsDeleted
            join a in db.Accounts on ra.AccountId equals a.Id
            where !a.IsDeleted
            join d in db.AccountProfileDetails on a.Id equals d.AccountId
            where d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
            select new { AccountId = a.Id, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.AccountId)
            .Select(g => g.OrderByDescending(r => r.IsPrimary).First().Content.Trim())
            .ToList();
    }
}
