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

/// <summary>
/// 배포 알림 (<c>POST /deploy-event</c>) — 새 이미지가 운영에 반영되면 슈퍼관리자에게 푸시한다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 파이프라인이 알려 주는가.</b> "새 이미지가 반영된 시각" 을 확실히 아는 것은
/// <c>docker compose up -d</c> 를 부른 쪽뿐이다. 서버가 스스로 알아내려면 도커 소켓의
/// 컨테이너 태그를 주기적으로 훑어 이전 값과 비교해야 하는데, 그러면 (1) 폴링 간격만큼
/// 늦고 (2) 알림을 보내는 서비스 자신이 방금 재시작된 참이라 <b>이전 값을 잊은 채</b>
/// 깨어난다 — 매 배포마다 「전부 바뀌었다」로 보이거나, 기준점을 DB 에 따로 들고
/// 있어야 한다. 부르는 쪽이 한 줄 알려 주는 편이 정확하고 싸다.
/// </para>
///
/// <para>
/// <b>부르는 쪽</b>: <c>.github/workflows/deploy.yml</c> 의 <c>deploy</c> 잡. 운영 서버에서
/// 도는 self-hosted 러너라 게이트웨이의 루프백(<c>127.0.0.1:5265</c>)으로 부른다 —
/// 관통 검증 단계가 이미 같은 주소를 쓰고 있다.
/// </para>
///
/// <para>
/// <b>인증</b>: 러너에게는 계정이 없다. AuthServer 의 배포 보고(<c>X-Release-Token</c>)와
/// 같은 방식으로 공유 비밀 하나(<c>X-Deploy-Token</c>)로 인증한다. 게이트웨이에서
/// 이 경로만 Anonymous 로 열되 <c>public-write</c> 로 조인다.
/// </para>
///
/// <para>
/// <b>대상</b>: 슈퍼관리자(<c>scom.role_accounts.role_id = SYSTEM_ADMINISTRATOR</c>) 전원.
/// 푸시를 끈 사람은 <see cref="PushSender"/> 가 알아서 거르므로 여기서 보지 않는다.
/// </para>
///
/// <para>
/// <b>실패한 배포도 보낸다.</b> 성공만 알리면 알림이 안 온 것이 "배포가 없었다" 인지
/// "배포가 깨졌다" 인지 구분되지 않는다. 둘 중 알아야 할 쪽은 후자다.
/// </para>
/// </remarks>
public static class DeployEventEndpoints
{
    /// <summary>배포 파이프라인이 공유 비밀을 담는 헤더.</summary>
    private const string TokenHeader = "X-Deploy-Token";

    /// <summary>
    /// 같은 태그의 알림은 브라우저가 하나로 합친다. 배포는 「가장 최근 것」만 뜻이 있으므로
    /// 고정값을 쓴다 — 연달아 배포해도 알림이 쌓이지 않고 마지막 것으로 갈린다.
    /// </summary>
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

            // 설정이 없으면 열지 않는다. "토큰이 비었으면 통과" 는 설정을 잊은 장비를
            // 그대로 공개 발송구로 만든다 (DeployNotifyOptions 머리말).
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

            // 슈퍼관리자 전원. 계정·역할 표는 읽기 전용 매핑이다 (ScomIdentityRows 머리말).
            var owners = await (
                from ra in db.RoleAccounts
                where ra.RoleId == settings.RoleId && !ra.IsDeleted
                join a in db.Accounts on ra.AccountId equals a.Id
                where !a.IsDeleted
                select a.UserId
            ).Distinct().ToListAsync(ct);

            if (owners.Count == 0)
            {
                // 오류가 아니다 — 보낼 사람이 없는 것뿐이다. 다만 배포마다 조용히
                // 0 건이 되는 상황은 알아챌 수 있어야 하므로 로그에 남긴다.
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
                "배포 알림 발송: {Status} {Sha} · 대상 {Targets}명 · 푸시 {Sent}건",
                request.Status, ShortSha(request.Sha), owners.Count, result.Sent);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                targets = owners.Count,
                sent = result.Sent,
                failed = result.Failed,
                optedOut = result.OptedOut,
                withoutSubscription = result.OwnersWithoutSubscription,
                detail = result.Message,
            }));
        })
        .WithName("SendDeployEvent")
        .WithTags("Deploy");
    }

    /// <summary>
    /// 토큰 대조. <b>길이가 달라도 같은 시간이 걸리게</b> 해시를 거쳐 비교한다 —
    /// <c>FixedTimeEquals</c> 는 길이가 다르면 즉시 false 라 길이가 새어 나간다.
    /// </summary>
    private static bool TokenMatches(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
    }

    /// <summary>
    /// 알림 문구를 만든다.
    /// </summary>
    /// <remarks>
    /// 잠금화면에서는 <b>두 줄이 전부</b>다. 그래서 제목에 성패를, 본문에 커밋 한 줄과
    /// 짧은 SHA 를 담는다 — 「무엇이 올라갔나」에 답하는 것이 그 둘이다.
    /// 누가 올렸는지와 워크플로 주소는 눌러서 들어가면 배포 현황 화면에 있다.
    /// </remarks>
    private static PushMessageDto BuildMessage(DeployEventDto request, string clickUrl)
    {
        var ok = string.Equals(request.Status, "success", StringComparison.OrdinalIgnoreCase);
        var sha = ShortSha(request.Sha);

        var title = ok ? "[배포] 새 버전이 반영되었습니다" : "[배포] 실패했습니다";

        // 커밋 제목이 없을 수 있다(수동 실행 등). 그때는 SHA 만으로도 말이 되게 둔다.
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
            // **한 시간 지난 배포 소식은 배달하지 않는다.** 하루에 여러 번 올라가는
            // 날이면 브라우저를 안 켠 사람의 줄에 그만큼 쌓이고, 나중에 켤 때
            // 한꺼번에 내려온다. 지난 배포는 배포 현황 화면에 다 있다.
            TtlSeconds = 3600,
        };
    }

    /// <summary>커밋 SHA 앞 7자리. 사람이 이력에서 찾을 때 쓰는 길이다.</summary>
    private static string ShortSha(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "" : sha.Length <= 7 ? sha : sha[..7];
}

/// <summary>
/// 배포 파이프라인이 보내는 배포 한 건.
/// </summary>
/// <remarks>
/// 모든 값이 없어도 동작한다 — 워크플로가 채우지 못한 칸 때문에 알림이 통째로
/// 안 가는 쪽이 더 나쁘다. 성패만 <see cref="Status"/> 로 분명히 한다.
/// </remarks>
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

    /// <summary>
    /// 알림을 눌렀을 때 열 주소. 비우면 설정의 <c>DeployNotify:ClickUrl</c>(배포 현황)로 간다.
    /// </summary>
    public string? Url { get; set; }
}
