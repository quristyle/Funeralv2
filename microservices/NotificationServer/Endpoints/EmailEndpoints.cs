using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
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
/// 받는 사람은 부르는 쪽이 정한다 (결정 D8-A — 이 서비스는 보내는 일만 한다).
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
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            if (user is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.To) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    message: "받는 사람 · 제목 · 본문이 모두 필요합니다.", code: "INVALID"));
            }

            var logger = loggerFactory.CreateLogger("EmailEndpoints");
            try
            {
                await sender.SendAsync(request.To, request.Subject, request.Body, request.Html);
                logger.LogInformation("이메일 직발송 완료. to={To} by={By}", request.To, user.UserId);
                return Results.Ok(ApiResponse<bool>.Ok(true, "메일을 보냈습니다."));
            }
            catch (Exception ex)
            {
                // 실패를 성공으로 말하지 않는다 — 부르는 쪽이 재시도 여부를 정한다.
                logger.LogError(ex, "이메일 직발송 실패. to={To} by={By}", request.To, user.UserId);
                return Results.Json(
                    ApiResponse<bool>.Fail("메일 발송에 실패했습니다.", "EMAIL_SEND_FAILED"),
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("SendEmailDirect")
        .WithOpenApi();
    }
}
