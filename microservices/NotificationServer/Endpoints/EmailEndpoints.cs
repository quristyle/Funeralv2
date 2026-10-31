using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>이메일 직발송 엔드포인트(/api/notification/emails/*). 큐 방식과 달리 SMTP 로 즉시 보내 성공·실패를 바로 안다.
/// 받는 사람은 부르는 쪽이 정하되 toUser·toRole 은 여기서 scom 을 보고 푼다.</summary>
public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/emails").WithTags("Emails");

        // ── SMTP 직발송 ─────────────────────────────────────
        // 게이트웨이를 거친 호출은 X-User-Id 를 게이트웨이가 채우고, 서비스 간 직접 호출은 자기 이름을 적는다.
        group.MapPost("/send", async (
            [FromBody] SendEmailDto request,
            UserContext? user,
            [FromServices] IEmailSender sender,
            [FromServices] AppDbContext db,
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] INotificationPolicyService policies,
            [FromServices] IAccountEmailResolver addresses,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            // ── 받는 사람을 **안 적어도 되는 부름이 있다** ──────
            // 받는 역할을 알림관리가 정하는 이벤트는 역할을 안 적어도 된다. 정책으로 푼 뒤에도 0명이면 400 으로 까닭을 적는다.
            var addressed = !string.IsNullOrWhiteSpace(request.To)
                            || !string.IsNullOrWhiteSpace(request.ToRole)
                            || !string.IsNullOrWhiteSpace(request.ToUser);

            // 정책이 받는 사람을 채울 수 있는 부름인가 — 코드도 구분도 없으면 바로 막는다.
            var policyCanFill = !string.IsNullOrWhiteSpace(request.EventCode)
                                || !string.IsNullOrWhiteSpace(request.Category);

            if ((!addressed && !policyCanFill) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "받는 사람(to · toUser · toRole 중 하나, 또는 알림관리가 역할을 정하는 eventCode) · "
                             + "제목 · 본문이 모두 필요합니다.",
                    code: "INVALID"));
            }

            // ── 첨부를 먼저 본다 ─────────────────────────────
            // 보내기 전에 막는다(상한·판정은 포털 메일 보내기와 EmailAttachmentGuard 한 곳).
            if (!EmailAttachmentGuard.TryCheck(
                    request.Attachments, out _, out var fileError, out var fileCode))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: fileError!, code: fileCode!));
            }

            var logger = loggerFactory.CreateLogger("EmailEndpoints");

            // ── 알림관리(회사의 규칙) ───────────────────────
            // 푸시와 같은 문으로 정책을 묻는다. 주소를 직접 적은 몫(to)은 묻지 않는다(갈래 스위치가 이미 판단했다).
            var decision = await policies.ResolveAsync(
                request.EventCode, request.Category, NotificationChannel.Email, ct);

            if (decision.Blocked)
            {
                // 실패가 아니다(400 이면 설정 존중이 고장으로 읽힌다). 「보냈다」고도 하지 않는다.
                logger.LogInformation(
                    "알림관리에서 이벤트를 꺼 두어 메일을 보내지 않았습니다. "
                    + "event={Event} category={Category} by={By}",
                    request.EventCode, request.Category, user.UserId);

                return Results.Ok(ApiResponse<bool>.Ok(false,
                    "알림관리에서 이 이벤트를 꺼 두어 보내지 않았습니다."));
            }

            // 받는 사람을 모은다 — 직접 지정(to) + 역할(toRole) 해석
            var recipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.To))
            {
                recipients.AddRange(request.To
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            var unknownUsers = new List<string>();

                // 갈래를 끈 사람은 「이메일이 없다」와 갈라 센다(받는 사람이 0 일 때 할 말이 다르다).
            var optedOutUsers = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.ToUser))
            {
                var ids = request.ToUser
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                // 갈래를 끈 사람은 여기서 던진다(댓글 알림은 설정 스위치를 따른다).
                if (string.Equals(
                        PushCategories.Normalize(request.Category),
                        PushCategories.HelpDeskComment,
                        StringComparison.Ordinal))
                {
                    var off = await prefs.GetCommentEmailDisabledLoginIdsAsync(ids, ct);
                    if (off.Count > 0)
                    {
                        optedOutUsers.AddRange(ids.Where(off.Contains));
                        ids = ids.Where(i => !off.Contains(i)).ToList();
                    }
                }

                // 「내 알림 설정」에서 이 알림의 메일만 끈 사람(정책이 걸리는 이벤트에만 열쇠가 있다).
                if (decision.EventCode is { } eventCode && ids.Count > 0)
                {
                    var eventOff = await prefs.GetEventEmailDisabledLoginIdsAsync(eventCode, ids, ct);
                    if (eventOff.Count > 0)
                    {
                        optedOutUsers.AddRange(ids.Where(eventOff.Contains));
                        ids = ids.Where(i => !eventOff.Contains(i)).ToList();
                    }
                }

                // 정책 거름막은 당사자에게 가는 알림에만 건다(역할 대상은 PushSender.ExpandOwnersAsync 가 본다).
                if (decision is { Unrestricted: false, TargetsRoles: false })
                {
                    var allowed = await policies.GetLoginIdsInRolesAsync(decision.RoleIds, ct);

                    var blocked = ids.Where(i => !allowed.Contains(i)).ToList();

                    if (blocked.Count > 0)
                    {
                        logger.LogInformation(
                            "알림관리 정책에 안 들어 메일에서 뺐습니다: {Users} (event={Event})",
                            string.Join(", ", blocked), request.EventCode ?? request.Category);

                        // 「이메일이 없다」와 갈라 센다(본인이 끈 것과 함께 세지만 고칠 자리는 알림관리 하나다).
                        optedOutUsers.AddRange(blocked);
                        ids = ids.Where(allowed.Contains).ToList();
                    }
                }

                var found = await addresses.ByLoginIdsAsync(ids, ct);

                recipients.AddRange(found.Values);
                unknownUsers.AddRange(ids.Where(i => !found.ContainsKey(i)));
            }

            // 역할로 보내는 몫은 정책이 정한다(설정 파일의 역할을 대신한다).
            var toRoles = (request.ToRole ?? string.Empty)
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!decision.Unrestricted)
            {
                toRoles = decision.TargetsRoles
                    ? [.. decision.RoleIds]
                    : [.. toRoles.Where(r => decision.RoleIds.Contains(r, StringComparer.OrdinalIgnoreCase))];
            }

            if (toRoles.Count > 0)
            {
                // 역할을 여럿 받는다. 쉼표로 모아 한 번 추리므로 두 역할에 걸친 사람도 한 통만 받는다.
                recipients.AddRange(await ResolveRoleEmailsAsync(db, prefs, toRoles, decision.EventCode, ct));
            }

            // 주소 꼴이 아닌 것은 빠지고, 빠진 것은 들고 가서 부르는 쪽이 알게 한다.
            var malformed = recipients
                .Where(r => !System.Net.Mail.MailAddress.TryCreate(r, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            recipients = recipients
                .Where(r => System.Net.Mail.MailAddress.TryCreate(r, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 본인이 껐을 뿐이면 실패가 아니다(400 이면 고장으로 읽힌다). 「보냈다」고도 하지 않는다(false).
            if (recipients.Count == 0 && optedOutUsers.Count > 0)
            {
                logger.LogInformation(
                    "받는 사람이 모두 해당 갈래의 메일을 끄고 있어 보내지 않았습니다. "
                    + "category={Category} toUser={User} by={By}",
                    request.Category, request.ToUser, user.UserId);

                return Results.Ok(ApiResponse<bool>.Ok(false,
                    $"받는 사람이 알림을 끄고 있어 보내지 않았습니다: {string.Join(", ", optedOutUsers)}"));
            }

            if (recipients.Count == 0)
            {
                // 역할·아이디에 메일 가진 사용자가 없을 수 있다(조용히 성공으로 말하지 않는다).
                logger.LogWarning(
                    "이메일 받는 사람이 없습니다. to={To} toUser={User} toRole={Role} by={By}",
                    request.To, request.ToUser, request.ToRole, user.UserId);

                var why = unknownUsers.Count > 0
                    ? $"이메일이 등록되지 않은 아이디입니다: {string.Join(", ", unknownUsers)}"
                    : malformed.Count > 0
                        ? $"주소 꼴이 아닙니다: {string.Join(", ", malformed)}"

                        // 고칠 자리는 역할에 사람이 없는 것이 아니라 알림관리 화면이다.
                        : !addressed && decision.Unrestricted
                            ? "알림관리에 받는 역할이 없습니다 — 포털관리 › 알림관리에서 "
                              + "이 이벤트의 「이메일」을 켠 역할이 하나라도 있어야 보냅니다."
                            : "역할에 이메일이 등록된 사용자가 없습니다.";

                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: $"받는 사람이 없습니다 ({why}).", code: "NO_RECIPIENT"));
            }

            // ── 평문이면 회사 메일 꼴을 입힌다 ─────────────────
            // 평문이면 회사 메일 틀을 입힌다(NoticeEmailTemplate). HTML 은 이미 완성된 문서라 손대지 않는다.
            var body = request.Html
                ? request.Body
                : NoticeEmailTemplate.Render(request.Subject, request.Body, request.SenderName);

            // 평문 갈래도 함께 싣는다(IEmailSender.SendAsync 의 textBody).
            var textBody = request.Html
                ? null
                : NoticeEmailTemplate.PlainAlternative(request.Subject, request.Body, request.SenderName);

            try
            {
                await sender.SendAsync(
                    string.Join(",", recipients), request.Subject, body,
                    html: true, request.Attachments, textBody);

                logger.LogInformation("이메일 직발송 완료. to={To} role={Role} files={Files} by={By}",
                    string.Join(",", recipients), request.ToRole, request.Attachments.Count, user.UserId);

                // 보낸 것을 표에도 남긴다(로그 파일만으로는 무엇이 나갔는지 답할 수 없다).
                await EmailSendLog.WriteAsync(
                    db, logger, recipients, request.Subject, request.Body, request.Html,
                    user.UserId, success: true, failureReason: null, ct);

                return Results.Ok(ApiResponse<bool>.Ok(true, "메일을 보냈습니다."));
            }
            catch (Exception ex)
            {
                // 실패를 성공으로 말하지 않는다(재시도 여부는 부르는 쪽이 정한다).
                logger.LogError(ex, "이메일 직발송 실패. to={To} by={By}",
                    string.Join(",", recipients), user.UserId);

                // 못 보낸 것도 남긴다(안 남기면 「아무 일도 없었다」와 구분되지 않는다).
                await EmailSendLog.WriteAsync(
                    db, logger, recipients, request.Subject, request.Body, request.Html,
                    user.UserId, success: false, failureReason: "메일 서버가 받지 않음", ct);

                return Results.Json(
                    ApiResponse<bool>.Fail("메일 발송에 실패했습니다.", "EMAIL_SEND_FAILED"),
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("SendEmailDirect");
    }

    // 아이디 → 대표 메일 질의는 IAccountEmailResolver 에 둔다. 아이디를 짚은 업무 메일은 본인 설정으로 막지 않는다.

/// <summary>역할 사용자들의 이메일을 푼다(계정마다 하나, 대표 이메일 우선). 본인이 이메일을 끈 사람은 뺀다.
/// 주인 키는 accounts.user_id 다(role_accounts 는 accounts.id 를 가리켜 맞춰 든다).</summary>
    private static async Task<List<string>> ResolveRoleEmailsAsync(
        AppDbContext db, INotificationPreferenceService prefs,
        IReadOnlyCollection<string> roleIds, string? eventCode, CancellationToken ct)
    {
        if (roleIds.Count == 0) return [];

        var rows = await (
            from ra in db.RoleAccounts
            where roleIds.Contains(ra.RoleId) && !ra.IsDeleted
            join a in db.Accounts on ra.AccountId equals a.Id
            where !a.IsDeleted
            join d in db.AccountProfileDetails on a.Id equals d.AccountId
            where d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
            select new { AccountId = a.Id, a.UserId, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        var picked = rows
            .GroupBy(r => r.AccountId)
            .Select(g => g.OrderByDescending(r => r.IsPrimary).First())
            .ToList();

        var optedOut = await prefs.GetEmailDisabledLoginIdsAsync(
            picked.Select(p => p.UserId), ct);

        if (eventCode is not null)
        {
            optedOut.UnionWith(await prefs.GetEventEmailDisabledLoginIdsAsync(
                eventCode, picked.Select(p => p.UserId), ct));
        }

        return picked
            .Where(p => !optedOut.Contains(p.UserId))
            .Select(p => p.Content.Trim())
            .ToList();
    }
}
