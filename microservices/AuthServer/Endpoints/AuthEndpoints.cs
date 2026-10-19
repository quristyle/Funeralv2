using System.Security.Claims;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Services;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/"); // /auth 접두사 제거

        group.MapPost("/login", async (LoginRequestDto request, AppDbContext db, IConfiguration config,
            IHostEnvironment env, ILogger<Account> logger,
            ILoginLogService loginLog, LoginCompletion completion,
            HttpContext http) =>
        {
            logger.LogInformation("로그인 시도: {Username}", request.Username);

            var account = await db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == request.Username);

            // 개발용 비밀번호 검사 생략 — 조건이 두 가지 모두 맞을 때만 아이디만으로 통과한다(Development 환경이 그중 하나).
            var skipPasswordCheck =
                env.IsDevelopment() && config.GetValue<bool>("Auth:SkipPasswordCheck");

            if (skipPasswordCheck && account is not null)
            {
                logger.LogWarning(
                    "[개발 전용] 비밀번호 검사를 생략하고 로그인했습니다: {Username}. " +
                    "운영 배포 전에 Auth:SkipPasswordCheck 를 끄거나 해당 코드를 제거하세요.",
                    request.Username);
            }
            else if (account == null || !PasswordHasher.Verify(account.Password, request.Password))
            {
                // 저장값이 아직 평문인 계정도 그대로 로그인된다 — PasswordHasher 참고.
                logger.LogWarning("로그인 실패: {Username}", request.Username);

                // 실패도 기록에 남긴다 — 응답 메시지는 그대로 둔다. 아이디가 있는지 없는지 알려 주지 않는다.
                await loginLog.WriteAsync(
                    account?.Id, request.Username, success: false,
                    account is null ? LoginFailReason.NotFound : LoginFailReason.BadPassword,
                    ResolveClientIp(http), http.Request.Headers.UserAgent.ToString());

                return Results.Json(ApiResponse<object>.Fail("아이디 또는 비밀번호가 잘못되었습니다.", "401"), statusCode: 401);
            }

            // 계정 상태는 비밀번호가 맞은 다음에 본다 — 먼저 보면 아이디만으로 「승인 대기」를 받아 계정 존재가 새어 나간다. 판정은 패스키 로그인과 한 벌을 쓴다(LoginCompletion).
            if (await completion.RejectIfNotActiveAsync(http, account) is { } rejected)
            {
                return rejected;
            }

            // 평문이거나 옛 기준으로 해시된 값이면 성공한 이 기회에 다시 해시해 저장한다 — 저장이 실패해도 로그인은 막지 않는다. 검사를 생략했으면 입력값이 실제 비밀번호가 아니므로 건드리지 않는다.
            if (!skipPasswordCheck && PasswordHasher.NeedsUpgrade(account.Password))
            {
                try
                {
                    account.Password = PasswordHasher.Hash(request.Password);
                    await db.SaveChangesAsync();
                    logger.LogInformation("비밀번호를 해시로 저장했습니다: {Username}", request.Username);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "비밀번호 해시 승격 실패: {Username}", request.Username);
                }
            }

            // 접속 기록·토큰 발급·갱신 쿠키·파일 쿠키·만료 판정이 전부 LoginCompletion 에 있다 — 패스키 로그인과 같은 코드이므로 한쪽에만 손대면 안 된다.
            return await completion.CompleteAsync(http, account);
        });

        // 토큰 갱신 — 프런트(AuthTokenHandler)가 401 을 받으면 여기로 갱신하러 온다. 갱신 실패는 세션 거절이라 401 로 답한다.
        group.MapPost("/refresh", async (
            HttpContext http, IHostEnvironment env,
            AccessTokenFactory tokenFactory, ILogger<Account> logger) =>
        {
            if (!tokenFactory.RefreshEnabled)
            {
                // 설정으로 꺼 둔 상태는 세션 거절이 아니므로 401 이 아니다 — 프런트는 401·403 만 세션 거절로 읽는다.
                return Results.Json(
                    ApiResponse<object>.Fail("토큰 갱신이 꺼져 있습니다.", "404"), statusCode: 404);
            }

            var cookie = http.Request.Cookies[AccessTokenFactory.RefreshCookieName];
            var account = await tokenFactory.ValidateRefreshTokenAsync(cookie);

            if (account is null)
            {
                // 쿠키가 없거나 기간이 지났거나 비밀번호가 바뀌었으면 셋 다 다시 로그인하라는 같은 결론이라 401 로 합치고, 남은 쿠키는 지운다.
                AccessTokenFactory.DeleteRefreshCookie(http.Response, !env.IsDevelopment());

                logger.LogInformation("토큰 갱신 거절 (쿠키 {Has})", cookie is null ? "없음" : "있음");
                return Results.Json(
                    ApiResponse<object>.Fail("다시 로그인해 주세요.", "401"), statusCode: 401);
            }

            // 역할·이메일은 다시 읽어 새 토큰에 싣는다 — 갱신 토큰에 얼려 두면 역할을 바꿔도 옛 권한이 만료일까지 따라다닌다.
            var issued = await tokenFactory.IssueAsync(account, DateTime.UtcNow);

            // 갱신 쿠키도 새로 굽는다(회전) — 계속 쓰는 사람은 다시 로그인할 일이 없고, 쓰지 않으면 30일 뒤 만료된다.
            AccessTokenFactory.AppendRefreshCookie(
                http.Response, issued.RefreshToken, issued.RefreshExpiresAt, !env.IsDevelopment());

            // 파일 읽기용 쿠키도 함께 갱신한다 — 안 하면 사진만 옛 토큰으로 나가다 만료된다. 심는 옵션은 로그인과 한 글자도 다르면 안 된다.
            LoginCompletion.AppendFileCookie(
                http.Response, issued.AccessToken, issued.AccessExpiresAt, !env.IsDevelopment());

            // 봉투 data 에 토큰 문자열을 그대로 싣는다 — 프런트는 data 와 data.result[0] 을 둘 다 받지만 문자열 모양이 정본이다.
            return Results.Ok(ApiResponse<string>.Ok(issued.AccessToken));
        });

        group.MapPost("/logout", (HttpContext http, IHostEnvironment env) =>
        {
            // 갱신 쿠키를 지운다 — 갱신 토큰을 DB 에 두지 않으므로 이 방식에서 세션을 즉시 끊는 유일한 수단이다.
            AccessTokenFactory.DeleteRefreshCookie(http.Response, !env.IsDevelopment());

            // 파일 읽기용 쿠키를 지운다 — 심을 때와 옵션(특히 Path)이 같아야 브라우저가 같은 쿠키로 알아본다.
            http.Response.Cookies.Delete(LoginCompletion.FileCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/file"
            });

            // 게이트웨이 미들웨어가 심는 지문 쿠키도 지운다.
            http.Response.Cookies.Delete("jsini_file_at_chk", new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            return Results.Ok(ApiResponse<bool>.Ok(true, "로그아웃 성공"));
        }).RequireAuthorization();
    

        // --- 인증 (Auth) 엔드포인트 ---
        group.MapGet("/codes", (ClaimsPrincipal user) =>
        {
            var codes = new List<string> { "*" };
            return Results.Ok(ApiResponse<List<string>>.Ok(codes));
        })
        .WithName("GetAccessCodes")
        .RequireAuthorization();


    }

    /// <summary>요청을 보낸 실제 클라이언트 IP. 본체는 <see cref="LoginCompletion.ResolveClientIp"/> 하나다 — 여기 한 벌을 더 두면 프록시 헤더를 읽는 규칙이 두 곳으로 갈린다.</summary>
    private static string? ResolveClientIp(HttpContext http) =>
        LoginCompletion.ResolveClientIp(http);
}
