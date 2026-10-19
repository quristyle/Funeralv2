using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthServer.Endpoints;

/// <summary>소셜 로그인(구글·네이버·카카오): 공급자 목록 · 인가 주소 · 로그인 · 연결 · 끊기. 익명 경로와 로그인한 사람 경로를 패스키와 같은 규칙으로 나눈다.</summary>
public static class SocialLoginEndpoints
{
    public static void MapSocialLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/social");

        // 익명 공급자 목록 — 설정이 안 된 공급자는 아예 내보내지 않는다(누르면 고장으로 신고된다). 비밀은 싣지 않는다.
        group.MapGet("/providers", (SocialLoginService social) =>
            Results.Ok(ApiResponse<IReadOnlyList<SocialProviderDto>>.Ok(social.UsableProviders())))
        .AllowAnonymous()
        .WithName("GetSocialProviders");

        // 설정 점검은 로그인한 사람만 본다 — /providers 는 쓸 수 있는 것만 내보내서 열쇠 없음·기능 꺼짐·공급자 미기재가 같은 빈 목록으로 보이기 때문이다.
        group.MapGet("/config-status", async (
            UserContext? user, AppDbContext db, SocialLoginService social,
            IOptions<SocialLoginOptions> options, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var opts = options.Value;

            // 공급자별 연결 수는 한 질의로 센다 — 따로 세면 서로 다른 시점을 본다.
            var counts = await db.AccountSocialLogins
                .Where(l => !l.IsDeleted)
                .GroupBy(l => l.Provider)
                .Select(g => new { Provider = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Provider, g => g.Count, ct);

            var providers = opts.Providers
                .Select(p => new SocialProviderConfigDto
                {
                    Provider = p.Key.ToLowerInvariant(),
                    DisplayName = string.IsNullOrWhiteSpace(p.Value.DisplayName)
                        ? p.Key
                        : p.Value.DisplayName,
                    Usable = p.Value.IsUsable,
                    HasClientId = !string.IsNullOrWhiteSpace(p.Value.ClientId),
                    HasClientSecret = !string.IsNullOrWhiteSpace(p.Value.ClientSecret),
                    CallbackPath = $"/social/{p.Key.ToLowerInvariant()}/callback",
                    LinkedCount = counts.GetValueOrDefault(p.Key.ToLowerInvariant()),
                })
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
                .ToList();

            return Results.Ok(ApiResponse<SocialConfigStatusDto>.Ok(new SocialConfigStatusDto
            {
                Enabled = opts.Enabled,
                AutoApprove = opts.AutoApprove,
                LinkByVerifiedEmail = opts.LinkByVerifiedEmail,

                // 서버가 센 값을 준다 — UsableProviders() 와 같은 판정을 써야 단추 수와 어긋나지 않는다.
                UsableCount = social.UsableProviders().Count,
                LinkedCount = counts.Values.Sum(),
                Providers = providers,
            }));
        })
        .RequireAuthorization()
        .WithName("GetSocialConfigStatus");

        // redirectUri 와 state 는 셸이 정해서 넘긴다 — 공급자 콘솔 등록값과 글자 하나까지 같아야 한다.
        group.MapPost("/authorize", (
            [FromBody] SocialAuthorizeRequestDto request, SocialLoginService social) =>
        {
            if (string.IsNullOrWhiteSpace(request.RedirectUri)
                || string.IsNullOrWhiteSpace(request.State))
            {
                return Results.Json(
                    ApiResponse<object>.Fail("요청 정보가 모자랍니다.", "400"), statusCode: 400);
            }

            var url = social.BuildAuthorizeUrl(request.Provider, request.RedirectUri, request.State);

            return url is null
                ? Results.Json(
                    ApiResponse<object>.Fail("지금은 쓸 수 없는 로그인 방식입니다.", "404"), statusCode: 404)
                : Results.Ok(ApiResponse<SocialAuthorizeDto>.Ok(new SocialAuthorizeDto(url)));
        })
        .AllowAnonymous()
        .WithName("CreateSocialAuthorizeUrl");

        // ── 로그인 (익명) ────────────────────────────────────────
        group.MapPost("/login", async (
            [FromBody] SocialLoginRequestDto request, HttpContext http,
            SocialLoginService social, LoginCompletion completion,
            ILoginLogService loginLog, ILogger<AccountSocialLogin> logger,
            CancellationToken ct) =>
        {
            var outcome = await social.SignInAsync(request, ct);

            if (outcome.Status == SocialLoginStatus.Failed)
            {
                // 아이디를 모르는 길이라 접속 기록에 적을 이름이 없다 — 그래도 실패를 남겨야 누가 두드리는지 보인다.
                await loginLog.WriteAsync(
                    accountId: null,
                    loginId: $"social:{request.Provider}",
                    success: false,
                    LoginFailReason.NotFound,
                    LoginCompletion.ResolveClientIp(http),
                    http.Request.Headers.UserAgent.ToString());

                return Results.Json(
                    ApiResponse<object>.Fail(outcome.Message ?? "소셜 로그인에 실패했습니다.", "401"),
                    statusCode: 401);
            }

            if (outcome.Status == SocialLoginStatus.Pending)
            {
                // 202 는 성공도 실패도 아니다 — 계정은 만들어졌고 승인을 기다린다. 셸이 「신청이 접수됐다」를 말할 수 있게 갈래를 나눈다.
                logger.LogInformation(
                    "소셜 가입 신청 접수: {LoginId} ({Provider})",
                    outcome.Pending!.LoginId, request.Provider);

                return Results.Json(
                    ApiResponse<SocialSignupPendingDto>.Ok(
                        outcome.Pending,
                        "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다."),
                    statusCode: 202);
            }

            // 상태(승인 대기·정지) 판정은 신원이 밝혀진 다음에 본다 — 먼저 보면 계정이 있는지가 새어 나간다.
            if (await completion.RejectIfNotActiveAsync(http, outcome.Account!, ct) is { } rejected)
            {
                return rejected;
            }

            // 결합된 소셜 계정으로 로그인한 경우 기존 계정과 결합 계정으로 결합 알림(앱알림 및 이메일) 발송
            await social.NotifyMergedLoginAsync(outcome.Account!, request.Provider, LoginCompletion.ResolveClientIp(http), ct);

            return await completion.CompleteAsync(http, outcome.Account!, ct);
        })
        .AllowAnonymous()
        .WithName("SocialLogin");

        // ── 내 연결 목록 (로그인한 사람) ─────────────────────────
        group.MapGet("/links", async (
            UserContext? user, AppDbContext db, SocialLoginService social, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var rows = await (
                from l in db.AccountSocialLogins
                join a in db.Accounts on l.AccountId equals a.Id
                where a.UserId == user.UserId && !l.IsDeleted
                orderby l.CreatedAt
                select l).ToListAsync(ct);

            return Results.Ok(ApiResponse<List<SocialLinkDto>>.Ok(
                [.. rows.Select(r => SocialLoginService.ToDto(r, social.DisplayNameOf(r.Provider)))]));
        })
        .RequireAuthorization()
        .WithName("GetMySocialLinks");

        // 내 계정에 붙이기 — 이 길이 없으면 이미 계정이 있는 사람이 소셜로 들어왔을 때 두 번째 계정이 생긴다.
        group.MapPost("/links", async (
            UserContext? user, [FromBody] SocialLoginRequestDto request,
            AppDbContext db, SocialLoginService social, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.UserId, ct);
            if (account is null)
            {
                return Results.Json(ApiResponse<object>.Fail("사용자를 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var (ok, error, link) = await social.LinkAsync(account, request, ct);

            return ok
                ? Results.Ok(ApiResponse<SocialLinkDto>.Ok(link!, "소셜 계정을 연결했습니다."))
                : Results.Json(
                    ApiResponse<object>.Fail(error ?? "연결하지 못했습니다.", "400"), statusCode: 400);
        })
        .RequireAuthorization()
        .WithName("LinkSocialAccount");

        // ── 끊기 (로그인한 사람) ─────────────────────────────────
        group.MapDelete("/links/{id}", async (
            string id, UserContext? user, AppDbContext db,
            ILogger<AccountSocialLogin> logger, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var link = await (
                from l in db.AccountSocialLogins
                join a in db.Accounts on l.AccountId equals a.Id
                where a.UserId == user.UserId && l.Id == id
                select l).FirstOrDefaultAsync(ct);

            if (link is null)
            {
                return Results.Json(
                    ApiResponse<object>.Fail("연결된 소셜 계정이 아닙니다.", "404"), statusCode: 404);
            }

            // 정말로 지운다 — 표시만 두면 같은 소셜 계정을 다시 붙일 때 고유 색인에 걸린다.
            db.Remove(link);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("소셜 연결을 끊었다: {UserId} ({Provider})", user.UserId, link.Provider);
            return Results.Ok(ApiResponse<bool>.Ok(true, "연결을 끊었습니다."));
        })
        .RequireAuthorization()
        .WithName("UnlinkSocialAccount");
    }
}
