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
            else if (!PasswordHasher.Verify(account?.Password, request.Password) || account is null)
            {
                // 계정이 없어도 Verify 가 해시 한 번만큼 시간을 쓴다 — 응답 시간으로 아이디가 갈리지 않게 순서를 이렇게 둔다.
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

            // 옛 기준(반복 횟수)으로 해시된 값이면 성공한 이 기회에 다시 해시해 저장한다 — 저장이 실패해도 로그인은 막지 않는다. 검사를 생략했으면 입력값이 실제 비밀번호가 아니므로 건드리지 않는다.
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

        // 관리자 대리 로그인 — 로그인 화면 로고를 두 번 눌러 여는 숨은 창이 보낸다.
        // 시스템관리자의 아이디·비밀번호를 함께 받아 본인이 시스템관리자일 때만, 적어 낸 대상 사용자로 로그인시킨다.
        // 게이트웨이의 auth-route 가 /api/auth/** 를 익명으로 넘기므로(로그인 전 화면이다) 신원 확인은 여기서 전부 한다.
        group.MapPost("/impersonate", async (ImpersonateRequestDto request, AppDbContext db,
            ILogger<Account> logger, ILoginLogService loginLog, LoginCompletion completion,
            IRoleAssignmentService roleService, HttpContext http) =>
        {
            logger.LogWarning("대리 로그인 시도: admin={Admin} → target={Target}",
                request.AdminUsername, request.TargetUsername);

            // 1) 승인하는 시스템관리자의 신원부터 확인한다 — 아이디·비밀번호가 맞아야 한다.
            //    대리 로그인은 개발용 비밀번호 생략(Auth:SkipPasswordCheck)을 적용하지 않는다 — 언제나 실제 비밀번호를 확인한다.
            var admin = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == request.AdminUsername);
            if (!PasswordHasher.Verify(admin?.Password, request.AdminPassword) || admin is null)
            {
                logger.LogWarning("대리 로그인 거부 — 관리자 인증 실패: {Admin}", request.AdminUsername);

                await loginLog.WriteAsync(
                    admin?.Id, request.AdminUsername, success: false,
                    admin is null ? LoginFailReason.NotFound : LoginFailReason.BadPassword,
                    ResolveClientIp(http), http.Request.Headers.UserAgent.ToString());

                return Results.Json(
                    ApiResponse<object>.Fail("관리자 아이디 또는 비밀번호가 올바르지 않습니다.", "401"), statusCode: 401);
            }

            // 2) 그 관리자가 실제로 시스템관리자 역할을 가졌는가 — 토큰과 같은 방식으로 적용 역할을 셈한다.
            var adminRoles = await roleService.ResolveEffectiveRolesAsync(admin.Id);
            if (!adminRoles.RoleIds.Contains("SYSTEM_ADMINISTRATOR", StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("대리 로그인 거부 — 시스템관리자가 아님: {Admin} (roles={Roles})",
                    admin.UserId, string.Join(',', adminRoles.RoleIds));

                // 「비밀번호는 맞았다」를 알려 주지 않는다 — 다르게 답하면 이 익명 창이 아무 계정의 비밀번호를 맞춰 보는 길이 된다.
                return Results.Json(
                    ApiResponse<object>.Fail("관리자 아이디 또는 비밀번호가 올바르지 않습니다.", "401"), statusCode: 401);
            }

            // 3) 대상 사용자를 찾는다.
            var target = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == request.TargetUsername);
            if (target is null)
            {
                return Results.Json(
                    ApiResponse<object>.Fail("대상 사용자를 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            // 대리 로그인은 누가·누구로 들어갔는지 반드시 남긴다 — 평소 로그인과 구분되는 흔적이다.
            logger.LogWarning("대리 로그인 성공: admin={Admin} → target={Target} ({Ip})",
                admin.UserId, target.UserId, ResolveClientIp(http));

            // 대상 계정으로 토큰을 발급하고 쿠키를 굽는다 — 뒤처리는 보통 로그인과 한 벌(LoginCompletion).
            return await completion.CompleteAsync(http, target);
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
