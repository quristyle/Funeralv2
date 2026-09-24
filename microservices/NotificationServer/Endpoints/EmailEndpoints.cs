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
/// 받는 사람은 부르는 쪽이 정한다 (결정 D8-A). 다만 <b>아이디·역할로 받는 것</b>
/// (<c>toUser</c> · <c>toRole</c>)은 예외로 여기서 푼다 — 아이디·역할 → 이메일
/// 명단은 scom(이 서비스의 DB)에 있고, 다른 서비스(SiteServer·ProjMngServer 등)는
/// 그 DB 를 볼 수 없기 때문이다.
/// </para>
/// </remarks>
public static class EmailEndpoints
{
    /// <summary>
    /// 첨부 개수 상한. 메일 서버가 세는 값은 아니고 <b>화면과 맞춘 값</b>이다 —
    /// 고르는 자리에서 이미 막으므로 여기 걸리면 다른 경로로 들어온 것이다.
    /// </summary>
    private const int MaxAttachmentCount = 5;

    /// <summary>
    /// 첨부 총량 상한. <b>메일 서버가 거절하기 전에 우리가 말한다.</b>
    /// 대개 20~25MB 에서 거절당하는데, 그때는 어느 파일 탓인지 알 수 없는
    /// SMTP 오류만 남는다.
    /// </summary>
    private const long MaxAttachmentBytes = 15L * 1024 * 1024;

    /// <summary>
    /// base64 를 <b>길이만 확인하고 버린다.</b> 여기서 바이트를 들고 있어 봐야
    /// 보내는 쪽이 다시 디코딩하므로, 큰 파일을 두 벌 메모리에 올리게 된다.
    /// </summary>
    private static bool TryDecode(string? content, out long bytes)
    {
        bytes = 0;

        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        var buffer = new byte[((content.Length * 3) + 3) / 4];

        if (!Convert.TryFromBase64String(content, buffer, out var written))
        {
            return false;
        }

        bytes = written;
        return written > 0;
    }

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
            [FromServices] INotificationPreferenceService prefs,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            if ((string.IsNullOrWhiteSpace(request.To)
                 && string.IsNullOrWhiteSpace(request.ToRole)
                 && string.IsNullOrWhiteSpace(request.ToUser)) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "받는 사람(to · toUser · toRole 중 하나) · 제목 · 본문이 모두 필요합니다.",
                    code: "INVALID"));
            }

            // ── 첨부를 먼저 본다 ─────────────────────────────
            //
            // **보내기 전에 막는다.** SMTP 에 붙인 뒤 거절당하면 어느 파일이
            // 문제인지 알 수 없고, 메일 서버마다 문구가 달라 화면이 옮길 말이
            // 없다. 여기서 걸면 파일 이름을 짚어 말할 수 있다.
            if (request.Attachments.Count > MaxAttachmentCount)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: $"첨부는 {MaxAttachmentCount}개까지입니다.", code: "TOO_MANY_FILES"));
            }

            long totalBytes = 0;

            foreach (var file in request.Attachments)
            {
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    return Results.BadRequest(ApiResponse<bool>.Fail(
                        message: "첨부 파일 이름이 비어 있습니다.", code: "INVALID"));
                }

                // base64 가 깨져 있으면 여기서 잡는다. 보내다 던지면 502 가 되어
                // 「메일 서버가 거절했다」로 읽힌다 — 사실은 우리가 보낸 값이 틀렸다.
                if (!TryDecode(file.Content, out var bytes))
                {
                    return Results.BadRequest(ApiResponse<bool>.Fail(
                        message: $"「{file.FileName}」 을 읽지 못했습니다.", code: "BAD_ATTACHMENT"));
                }

                totalBytes += bytes;
            }

            if (totalBytes > MaxAttachmentBytes)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: $"첨부가 모두 합쳐 {MaxAttachmentBytes / 1024 / 1024}MB 를 넘습니다 "
                             + $"(지금 {totalBytes / 1024 / 1024}MB). 메일 서버가 거절합니다.",
                    code: "ATTACHMENT_TOO_LARGE"));
            }

            var logger = loggerFactory.CreateLogger("EmailEndpoints");

            // 받는 사람을 모은다 — 직접 지정(to) + 역할(toRole) 해석
            var recipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.To))
            {
                recipients.AddRange(request.To
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            var unknownUsers = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.ToUser))
            {
                var ids = request.ToUser
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var found = await ResolveUserEmailsAsync(db, ids, ct);

                recipients.AddRange(found.Values);
                unknownUsers.AddRange(ids.Where(i => !found.ContainsKey(i)));
            }

            if (!string.IsNullOrWhiteSpace(request.ToRole))
            {
                recipients.AddRange(await ResolveRoleEmailsAsync(db, prefs, request.ToRole.Trim(), ct));
            }

            // 주소 꼴이 아닌 것은 여기서 빠진다. **무엇이 빠졌는지 들고 간다** —
            // 그냥 「받는 사람이 없습니다」로만 답하면 부르는 쪽이 자기가 보낸
            // 값의 어디가 틀렸는지 알 수 없다.
            var malformed = recipients
                .Where(r => !System.Net.Mail.MailAddress.TryCreate(r, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            recipients = recipients
                .Where(r => System.Net.Mail.MailAddress.TryCreate(r, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
            {
                // 역할·아이디에 이메일 가진 사용자가 없을 수도 있다 — 조용히 성공으로 말하지 않는다.
                logger.LogWarning(
                    "이메일 받는 사람이 없습니다. to={To} toUser={User} toRole={Role} by={By}",
                    request.To, request.ToUser, request.ToRole, user.UserId);

                var why = unknownUsers.Count > 0
                    ? $"이메일이 등록되지 않은 아이디입니다: {string.Join(", ", unknownUsers)}"
                    : malformed.Count > 0
                        ? $"주소 꼴이 아닙니다: {string.Join(", ", malformed)}"
                        : "역할에 이메일이 등록된 사용자가 없습니다.";

                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: $"받는 사람이 없습니다 ({why}).", code: "NO_RECIPIENT"));
            }

            // ── 평문이면 회사 메일 꼴을 입힌다 ─────────────────
            //
            // **글자만 덩그러니 나가는 길을 남겨 두지 않는다.** 업무 화면의
            // 「알림 보내기」(생일 축하 등)는 사람이 친 글을 그대로 넘기는데,
            // 그때까지 그 글은 아무 틀 없이 도착했다 — 같은 포털이 보낸
            // 비밀번호 찾기·문의 접수 메일과 나란히 놓으면 회사 메일로 보이지
            // 않는다. 틀은 여기 한 곳에만 둔다(`NoticeEmailTemplate` 머리말).
            //
            // HTML 로 온 것은 부르는 쪽이 이미 완성된 문서를 보낸 것이라
            // 손대지 않는다 — 틀을 두 겹으로 씌우면 클라이언트마다 다르게 무너진다.
            var body = request.Html
                ? request.Body
                : NoticeEmailTemplate.Render(request.Subject, request.Body, request.SenderName);

            // 평문 갈래도 같이 싣는다 — 원본이 이미 평문이라 만드는 값이 거의
            // 들지 않는다(`IEmailSender.SendAsync` 의 `textBody`). HTML 로 온
            // 것은 원본이 평문이 아니므로 만들 것이 없다.
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

                // **보낸 것을 표에도 남긴다.** 로그 파일만으로는 「이 사람에게
                // 무엇이 나갔나」에 답할 수 없다 — 그것을 묻는 자리가 계정
                // 앱 현황 화면이다(EmailSendLog 머리말).
                await EmailSendLog.WriteAsync(
                    db, logger, recipients, request.Subject, request.Body, request.Html,
                    user.UserId, success: true, failureReason: null, ct);

                return Results.Ok(ApiResponse<bool>.Ok(true, "메일을 보냈습니다."));
            }
            catch (Exception ex)
            {
                // 실패를 성공으로 말하지 않는다 — 부르는 쪽이 재시도 여부를 정한다.
                logger.LogError(ex, "이메일 직발송 실패. to={To} by={By}",
                    string.Join(",", recipients), user.UserId);

                // **못 보낸 것도 남긴다.** 푸시 기록이 그러는 까닭과 같다 —
                // 안 남기면 「그 시각에 아무 일도 없었다」로 보이고, 그것이
                // 「보낸 적 없다」와 구분되지 않는다.
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

    /// <summary>
    /// 로그인 아이디들의 이메일을 푼다 — 아이디마다 하나(대표 이메일 우선).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>본인이 이메일 알림을 껐는지는 보지 않는다.</b> 역할로 가는 메일은
    /// 「그 역할인 사람 아무나」에게 가는 알림이라 본인의 뜻을 지킬 수 있지만,
    /// 이쪽은 <b>부르는 쪽이 사람을 하나 짚어</b> 보내는 것이다 — AI 작업의
    /// 「끝나면 메일로 받기」처럼 그 건마다 본인이 켠 업무 메일이 여기로 온다.
    /// 알림 설정으로 그것을 막으면 켠 사람이 왜 안 오는지 알 길이 없다.
    /// </para>
    /// <para>
    /// 찾지 못한 아이디는 <b>돌려주지 않는다</b> — 부르는 쪽이 키를 보고
    /// 무엇이 빠졌는지 말할 수 있게 사전으로 준다.
    /// </para>
    /// </remarks>
    private static async Task<Dictionary<string, string>> ResolveUserEmailsAsync(
        AppDbContext db, IReadOnlyList<string> loginIds, CancellationToken ct)
    {
        var keys = loginIds
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await (
            from a in db.Accounts
            where keys.Contains(a.UserId) && !a.IsDeleted
            join d in db.AccountProfileDetails on a.Id equals d.AccountId
            where d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
            select new { a.UserId, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.IsPrimary).First().Content.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 역할 사용자들의 이메일을 푼다 — 계정마다 하나(대표 이메일 우선).
    /// scom 은 이 서비스의 DB 라 조회만 한다 (Entities/ScomIdentityRows.cs 머리말).
    /// </summary>
    /// <remarks>
    /// <b>본인이 이메일 알림을 끈 사람은 빠진다.</b> 역할로 보내는 메일은 "그 역할인
    /// 사람 아무나" 에게 가는 알림이라 본인의 뜻을 지킬 수 있다.
    ///
    /// <para>
    /// 반대로 <c>to</c> 에 주소를 직접 적어 보내는 메일은 걸러내지 않는다 — 문의 회신
    /// 처럼 "이 주소로 보내야 하는" 업무 메일이고, 주소만으로는 어느 계정인지도 확실치
    /// 않다. 알림 설정으로 업무 메일을 막으면 조용히 일이 끊긴다.
    /// </para>
    ///
    /// <para>
    /// 설정의 주인 키는 <c>accounts.user_id</c>(로그인 아이디)다 — 게이트웨이가 주는
    /// <c>X-User-Id</c> 가 그 값이라 구독도 같은 키로 저장된다. <c>role_accounts</c> 는
    /// <c>accounts.id</c> 를 가리키므로 둘을 함께 들고 와서 맞춰야 한다.
    /// </para>
    /// </remarks>
    private static async Task<List<string>> ResolveRoleEmailsAsync(
        AppDbContext db, INotificationPreferenceService prefs, string roleId, CancellationToken ct)
    {
        var rows = await (
            from ra in db.RoleAccounts
            where ra.RoleId == roleId && !ra.IsDeleted
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

        return picked
            .Where(p => !optedOut.Contains(p.UserId))
            .Select(p => p.Content.Trim())
            .ToList();
    }
}
