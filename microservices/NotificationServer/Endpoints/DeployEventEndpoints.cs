using System.Security.Cryptography;
using System.Text;

using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Options;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>배포 알림(POST /deploy-event). 새 이미지가 운영에 반영되면 슈퍼관리자에게 푸시한다. 서버가 태그를 훑으면 재시작 뒤 이전 값을 잊으므로 부르는 쪽이 알린다.
/// 인증은 AuthServer 배포 보고와 같은 공유 비밀(X-Deploy-Token)이고, 실패한 배포도 보낸다.</summary>
public static class DeployEventEndpoints
{
    /// <summary>배포 파이프라인이 공유 비밀을 담는 헤더.</summary>
    private const string TokenHeader = "X-Deploy-Token";

    /// <summary>같은 태그로 묶여 브라우저가 하나로 합친다. 배포는 가장 최근 것만 뜻이 있으므로 고정값을 쓴다.</summary>
    private const string PushTag = "deploy";

    public static void MapDeployEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/deploy-event", async (
            [FromBody] DeployEventDto request,
            HttpRequest http,
            [FromServices] AppDbContext db,
            [FromServices] IPushSender push,
            [FromServices] IOptions<DeployNotifyOptions> options,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("DeployEvent");
            var settings = options.Value;

            // 설정이 없으면 열지 않는다(토큰이 비어 통과하면 공개 발송구가 된다).
            if (!settings.IsConfigured)
            {
                logger.LogWarning("DeployNotify:Token 이 없어 배포 알림 요청을 거절했습니다.");
                return Results.Json(
                    ApiResponse<object>.Fail(message: "배포 알림이 설정되지 않았습니다.", code: "NOT_CONFIGURED"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var token = http.Headers[TokenHeader].FirstOrDefault();
            if (!TokenMatches(settings.Token!, token))
            {
                logger.LogWarning("배포 알림 토큰이 맞지 않습니다.");
                return Results.Json(
                    ApiResponse<object>.Fail(message: "토큰이 맞지 않습니다.", code: "FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // 슈퍼관리자 전원(scom 역할표는 읽기 전용 매핑이다).
            var owners = await (
                from ra in db.RoleAccounts
                where ra.RoleId == settings.RoleId && !ra.IsDeleted
                join a in db.Accounts on ra.AccountId equals a.Id
                where !a.IsDeleted
                select a.UserId
            ).Distinct().ToListAsync(ct);

            if (owners.Count == 0)
            {
                // 보낼 사람이 없는 것은 오류가 아니다(배포마다 0건이면 로그로 알 수 있게 남긴다).
                logger.LogWarning("{Role} 역할인 계정이 없어 배포 알림을 보내지 않았습니다.", settings.RoleId);
                return Results.Ok(ApiResponse<object>.Ok(new { targets = 0, sent = 0 }));
            }

            var message = BuildMessage(request, settings.ClickUrl);

            var result = await push.SendAsync(new SendPushDto
            {
                Owners = owners
                    .Select(id => new OwnerRefDto { OwnerType = "jsini", OwnerKey = id })
                    .ToList(),
                Message = message
            }, sentBy: "system:deploy", ct);

            logger.LogInformation(
                "배포 알림 발송: {Status} {Sha} · 대상 {Targets}명 · 푸시 {Sent}건 · 메일 {Mailed}통",
                request.Status, ShortSha(request.Sha), owners.Count, result.Sent, result.Mailed);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                targets = owners.Count,
                sent = result.Sent,

                // 이메일을 켠 역할에는 메일로도 나간다.
                mailed = result.Mailed,
                failed = result.Failed,
                optedOut = result.OptedOut,
                withoutSubscription = result.OwnersWithoutSubscription,
                detail = result.Message,
            }));
        })
        .WithName("SendDeployEvent")
        .WithTags("Deploy");
    }

    /// <summary>토큰 대조. 길이가 달라도 같은 시간이 걸리도록 해시를 거쳐 비교한다(FixedTimeEquals 는 길이가 다르면 즉시 false 라 길이가 샌다).</summary>
    private static bool TokenMatches(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
    }

    /// <summary>알림 문구를 만든다. 잠금화면은 두 줄이 전부라 제목에 성패를, 본문에 커밋 한 줄과 짧은 SHA 를 담는다.</summary>
    private static PushMessageDto BuildMessage(DeployEventDto request, string clickUrl)
    {
        var ok = string.Equals(request.Status, "success", StringComparison.OrdinalIgnoreCase);
        var sha = ShortSha(request.Sha);

        var title = ok ? "[배포] 새 버전이 반영되었습니다" : "[배포] 실패했습니다";

        // 커밋 제목이 없을 수 있다(수동 실행). 그때는 SHA 만으로 말이 되게 한다.
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Title)) parts.Add(request.Title!.Trim());
        if (!string.IsNullOrWhiteSpace(sha)) parts.Add(sha);
        if (!string.IsNullOrWhiteSpace(request.Actor)) parts.Add($"{request.Actor}");

        var body = parts.Count > 0 ? string.Join(" · ", parts) : "배포가 끝났습니다.";

        return new PushMessageDto
        {
            Title = title,
            Body = body,
            Url = string.IsNullOrWhiteSpace(request.Url) ? clickUrl : request.Url,
            Tag = PushTag,
            // 한 시간 지난 배포 소식은 배달하지 않는다(안 켠 사람에게 쌓였다가 한꺼번에 온다).
            TtlSeconds = 3600,
            Category = PushCategories.Deploy,
        };
    }

    /// <summary>커밋 SHA 앞 7자리. 사람이 이력에서 찾을 때 쓰는 길이다.</summary>
    private static string ShortSha(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "" : sha.Length <= 7 ? sha : sha[..7];
}

/// <summary>배포 파이프라인이 보내는 배포 한 건. 모든 값이 없어도 동작한다(칸이 비었다고 알림이 통째로 빠지는 것이 더 나쁘다).</summary>
public class DeployEventDto
{
    /// <summary><c>success</c> 면 성공, 그 밖의 값은 실패로 본다.</summary>
    public string Status { get; set; } = "success";

    /// <summary>배포한 커밋 SHA. 이미지 태그와 같은 값이다(<c>TAG</c>).</summary>
    public string? Sha { get; set; }

    /// <summary>커밋 제목 한 줄.</summary>
    public string? Title { get; set; }

    /// <summary>워크플로를 돌린 사람.</summary>
    public string? Actor { get; set; }

    /// <summary>알림을 눌렀을 때 열 주소. 비우면 설정의 DeployNotify:ClickUrl(배포 현황)로 간다.</summary>
    public string? Url { get; set; }
}
