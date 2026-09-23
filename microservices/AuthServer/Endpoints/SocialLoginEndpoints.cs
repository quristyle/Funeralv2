using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>
/// 소셜 로그인(구글 · 네이버 · 카카오) — 공급자 목록 · 인가 주소 · 로그인 · 연결 · 끊기.
/// </summary>
/// <remarks>
/// <para>
/// [경로가 둘로 갈린다 — 패스키와 같은 규칙]
/// </para>
///
/// <list type="table">
///   <item>
///     <term><c>/social/providers</c> · <c>/social/authorize</c> · <c>/social/login</c></term>
///     <description><b>익명이다.</b> 로그인하려는 사람에게 토큰이 있을 리 없다.
///     대신 게이트웨이가 <c>auth-attempts</c>(IP 당 분당 10회)로 조인다 —
///     비밀번호 로그인과 같은 통을 쓰지 않으면 이 경로가 그 제한의 우회로가 된다.</description>
///   </item>
///   <item>
///     <term><c>/social/links*</c></term>
///     <description>로그인한 사람만. <b>내 계정에만</b> 붙이고 뗀다 — 계정 아이디를
///     요청에서 받지 않고 신원 헤더에서만 읽는 이유가 그것이다.</description>
///   </item>
/// </list>
///
/// <para>
/// [로그인 성공 처리를 비밀번호·패스키와 한 벌로 쓴다]
/// </para>
///
/// <para>
/// 토큰 발급 · 갱신 쿠키 · 파일 쿠키 · 접속 기록 · 비밀번호 만료 판정까지
/// <see cref="LoginCompletion"/> 한 곳이다. 여기 한 벌을 더 적으면 <b>반드시
/// 한쪽만 고치는 날이 온다</b> — 그때 증상은 「구글로 들어가면 사진이 안
/// 보인다」처럼 원인과 멀다.
/// </para>
///
/// <para>
/// [승인 대기를 401 로 답하지 않는다]
/// </para>
///
/// <para>
/// 소셜로 처음 들어온 사람에게는 계정이 <b>만들어진다.</b> 그것을 로그인 실패와
/// 같은 401 로 답하면 셸이 「인증에 실패했습니다」라고 말하게 되고, 사용자는
/// 자기 신청이 접수됐다는 것을 영영 모른 채 단추를 계속 누른다. 그래서
/// <c>202 Accepted</c> 로 <b>갈래를 나눠</b> 돌려준다.
/// </para>
/// </remarks>
public static class SocialLoginEndpoints
{
    public static void MapSocialLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/social");

        // ── 쓸 수 있는 공급자 (익명) ─────────────────────────────
        //
        // 로그인 화면이 단추를 그리려고 부른다. **설정이 안 된 공급자는 아예
        // 나오지 않는다** — 없는 길로 가는 단추를 그려 두면 눌러 본 사람이
        // 고장으로 신고한다.
        //
        // 나가는 값에 비밀이 없다(열쇠와 표시 이름뿐). ClientId 조차 내보내지
        // 않는 이유는 인가 주소를 **서버가 지어 주기** 때문이다 — 브라우저는
        // 그 값을 알 이유가 없다.
        group.MapGet("/providers", (SocialLoginService social) =>
            Results.Ok(ApiResponse<IReadOnlyList<SocialProviderDto>>.Ok(social.UsableProviders())))
        .AllowAnonymous()
        .WithName("GetSocialProviders");

        // ── 인가 화면 주소 (익명) ────────────────────────────────
        //
        // `redirectUri` 와 `state` 는 **셸이 정해서 넘긴다.** 여기서 짓지 않는
        // 까닭은 서비스 머리말에 있다 — AuthServer 는 게이트웨이 뒤라 포털의
        // 바깥 주소를 모르고, 그 값이 공급자 콘솔에 등록한 것과 글자 하나까지
        // 같아야 한다.
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
                // 아이디를 모르는 길이라 접속 기록에 적을 이름이 없다. 그래도
                // 실패를 남겨 두어야 「누가 이 길을 두드리고 있다」를 볼 수 있다.
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
                // **성공도 실패도 아니다.** 계정은 만들어졌고 승인을 기다린다.
                // 202 로 갈래를 나눠 두면 셸이 「신청이 접수됐다」를 말할 수 있다.
                logger.LogInformation(
                    "소셜 가입 신청 접수: {LoginId} ({Provider})",
                    outcome.Pending!.LoginId, request.Provider);

                return Results.Json(
                    ApiResponse<SocialSignupPendingDto>.Ok(
                        outcome.Pending,
                        "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다."),
                    statusCode: 202);
            }

            // 상태(승인 대기 · 정지) 판정은 비밀번호·패스키 로그인과 한 벌을 쓴다.
            // **신원이 밝혀진 다음에 본다** — 먼저 보면 계정이 있는지가 새어 나간다.
            if (await completion.RejectIfNotActiveAsync(http, outcome.Account!, ct) is { } rejected)
            {
                return rejected;
            }

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

        // ── 내 계정에 붙이기 (로그인한 사람) ─────────────────────
        //
        // 이 길이 없으면 이미 계정이 있는 사람이 소셜로 들어왔을 때 **두 번째
        // 계정**이 생긴다. 승인하는 사람은 그것이 같은 사람인지 알 길이 없다.
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

            // **정말로 지운다.** 표시만 해 두면 같은 소셜 계정을 다시 붙일 때
            // (공급자, 사용자 번호) 고유 색인에 걸린다. 패스키를 지울 때와 같다.
            db.Remove(link);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("소셜 연결을 끊었다: {UserId} ({Provider})", user.UserId, link.Provider);
            return Results.Ok(ApiResponse<bool>.Ok(true, "연결을 끊었습니다."));
        })
        .RequireAuthorization()
        .WithName("UnlinkSocialAccount");
    }
}
