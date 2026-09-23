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

            // 1. 사용자 조회
            var account = await db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == request.Username);

            // ── 개발용 비밀번호 검사 생략 ──────────────────────────
            //
            // 개발·테스트 중에 화면을 확인하려면 매번 로그인해야 하는데,
            // 그때마다 비밀번호를 넣는 것이 번거로워 아이디만으로 통과시키는 길을 둔다.
            //
            // 켜지는 조건이 **두 가지 모두** 맞아야 한다.
            //   1) ASPNETCORE_ENVIRONMENT 가 Development 일 것
            //   2) 설정 Auth:SkipPasswordCheck 가 true 일 것
            //
            // 운영(Production)에서는 설정을 true 로 두어도 절대 켜지지 않는다.
            // 기본값은 false 이므로, 명시적으로 켜지 않는 한 평소와 똑같이 동작한다.
            //
            // **프로젝트가 마무리되면 이 블록을 통째로 지우면 된다.**
            // 지울 때 appsettings.Development.json 의 Auth 섹션도 함께 지운다.
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
                // 2. 계정 검증
                //    저장값이 아직 평문인 계정도 그대로 로그인된다(PasswordHasher 참고).
                logger.LogWarning("로그인 실패: {Username}", request.Username);

                // 실패도 기록에 남긴다. 남기지 않으면 계정 화면에서
                // "누가 내 아이디를 두드리고 있다" 를 볼 방법이 없다.
                // 응답 메시지는 그대로 둔다 — 아이디가 있는지 없는지 알려 주지 않는다.
                await loginLog.WriteAsync(
                    account?.Id, request.Username, success: false,
                    account is null ? LoginFailReason.NotFound : LoginFailReason.BadPassword,
                    ResolveClientIp(http), http.Request.Headers.UserAgent.ToString());

                return Results.Json(ApiResponse<object>.Fail("아이디 또는 비밀번호가 잘못되었습니다.", "401"), statusCode: 401);
            }

            // ── 2-0. 계정 상태 ────────────────────────────────────
            //
            // **비밀번호가 맞은 다음에 본다.** 먼저 보면 아이디만 넣어 보고
            // "승인 대기 중" 이라는 답을 받을 수 있어, 그 아이디가 있다는 것이
            // 새어 나간다. 로그인 실패 문구를 뭉뚱그려 둔 뜻이 사라진다.
            //
            // 판정은 패스키 로그인과 한 벌을 쓴다(LoginCompletion) — 갈라 두면
            // 한쪽으로만 정지 계정이 들어오는 날이 온다.
            if (await completion.RejectIfNotActiveAsync(http, account) is { } rejected)
            {
                return rejected;
            }

            // 2-1. 평문이거나 옛 기준으로 해시된 값이면 이 기회에 다시 해시해 저장한다.
            //      로그인에 성공한 지금이 평문 비밀번호를 아는 유일한 시점이다.
            //      저장에 실패해도 로그인 자체는 막지 않는다.
            //      검사를 생략한 경우에는 입력값이 실제 비밀번호가 아니므로 건드리지 않는다.
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

            // ── 로그인 성공 뒤처리 ────────────────────────────────
            //
            // 접속 기록 · 토큰 발급 · 갱신 쿠키 · 파일 쿠키 · 비밀번호 만료 판정이
            // 전부 저 안에 있다. **패스키 로그인과 같은 코드다**(LoginCompletion) —
            // 한쪽에만 손대면 증상이 원인과 멀어진다(그 클래스 머리말 참고).
            return await completion.CompleteAsync(http, account);
        });

        // ── 토큰 갱신 ────────────────────────────────────────────
        //
        // **왜 있어야 하나** — 프런트(`AuthTokenHandler`)는 401 을 받으면 여기로
        // 갱신하러 온다. 그런데 이 경로가 없어서 늘 404 였다. access token 수명이
        // 7일이라 드러나지 않았을 뿐이고, 「갱신 실패 = 세션이 죽었다」로 읽는
        // 코드와 만나면 스쳐 지나갈 401 하나가 화면 전체를 로그아웃시킨다.
        //
        // **인증을 걸지 않는다.** 여기 오는 사람은 이미 access token 이 만료된
        // 상태다. 인증을 걸면 갱신하러 올 수 있는 사람은 갱신이 필요 없는 사람뿐이다.
        // 신원의 근거는 헤더가 아니라 **갱신 쿠키**이고, 그 검증은
        // AccessTokenFactory 가 한다.
        //
        // 속도 제한을 따로 걸지 않았다(게이트웨이의 `auth-attempts` 는
        // `/api/auth/login` 에만 붙는다). 갱신 쿠키는 서명된 토큰이라 찍어
        // 맞힐 수 있는 값이 아니고, 정상적으로도 며칠에 한 번만 오는 경로다.
        group.MapPost("/refresh", async (
            HttpContext http, IHostEnvironment env,
            AccessTokenFactory tokenFactory, ILogger<Account> logger) =>
        {
            if (!tokenFactory.RefreshEnabled)
            {
                // 설정으로 꺼 둔 상태다. 「세션이 죽었다」가 아니므로 401 이 아니다 —
                // 프런트는 401·403 만 세션 거절로 읽고 나머지는 토큰을 그대로 둔다.
                return Results.Json(
                    ApiResponse<object>.Fail("토큰 갱신이 꺼져 있습니다.", "404"), statusCode: 404);
            }

            var cookie = http.Request.Cookies[AccessTokenFactory.RefreshCookieName];
            var account = await tokenFactory.ValidateRefreshTokenAsync(cookie);

            if (account is null)
            {
                // 쿠키가 없거나·기간이 지났거나·비밀번호가 바뀌었다.
                // 셋 다 「다시 로그인해야 한다」는 같은 결론이라 401 로 합친다.
                // 남은 쿠키가 계속 401 을 부르지 않게 지워 준다.
                AccessTokenFactory.DeleteRefreshCookie(http.Response, !env.IsDevelopment());

                logger.LogInformation("토큰 갱신 거절 (쿠키 {Has})", cookie is null ? "없음" : "있음");
                return Results.Json(
                    ApiResponse<object>.Fail("다시 로그인해 주세요.", "401"), statusCode: 401);
            }

            // 역할·이메일을 **다시 읽어** 새 토큰에 싣는다. 갱신 토큰에 얼려 두면
            // 역할을 바꿔도 옛 권한이 만료일까지 따라다닌다.
            var issued = await tokenFactory.IssueAsync(account, DateTime.UtcNow);

            // 갱신 쿠키도 새로 굽는다(회전). 이렇게 해야 계속 쓰는 사람은
            // 다시 로그인할 일이 없고, 쓰지 않으면 30일 뒤 저절로 만료된다.
            AccessTokenFactory.AppendRefreshCookie(
                http.Response, issued.RefreshToken, issued.RefreshExpiresAt, !env.IsDevelopment());

            // 파일 읽기용 쿠키도 함께 갱신한다. 안 하면 갱신 뒤에도 사진만
            // 옛 토큰으로 나가다가 만료되어 **글은 보이는데 사진만 안 나온다.**
            // 심는 코드는 로그인과 한 벌이다 — 옵션이 한 글자라도 달라지면
            // 브라우저가 다른 쿠키로 알아보고, 옛것이 남아 계속 실려 나간다.
            LoginCompletion.AppendFileCookie(
                http.Response, issued.AccessToken, issued.AccessExpiresAt, !env.IsDevelopment());

            // 봉투의 `data` 에 토큰 문자열을 그대로 싣는다 —
            // 프런트(`AuthTokenHandler.ReadTokenAsync`)가 `data` 와
            // `data.result[0]` 둘 다 받지만, 옛 Vue 의 `refreshTokenApi` 와 같은
            // 모양(문자열)이 정본이다.
            return Results.Ok(ApiResponse<string>.Ok(issued.AccessToken));
        });

        group.MapPost("/logout", (HttpContext http, IHostEnvironment env) =>
        {
            // 갱신 쿠키를 지운다. **이 방식에서 세션을 즉시 끊는 유일한 수단이다** —
            // 갱신 토큰을 DB 에 두지 않아 서버가 따로 무효화할 곳이 없다
            // (그 판단의 근거는 AccessTokenFactory 머리말에 있다).
            AccessTokenFactory.DeleteRefreshCookie(http.Response, !env.IsDevelopment());

            // 파일 읽기용 쿠키를 지운다. 지우지 않으면 로그아웃한 뒤에도
            // 브라우저에 남은 쿠키로 사진을 계속 볼 수 있다.
            // 심을 때와 옵션이 같아야 브라우저가 같은 쿠키로 알아본다(특히 Path).
            http.Response.Cookies.Delete(LoginCompletion.FileCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/file"
            });

            // 게이트웨이의 자가 치유 미들웨어(ApiGateway/Program.cs)가 함께 심는 지문 쿠키도 지운다.
            // 남겨 두면 다음 로그인 때 지문이 어긋나 어차피 다시 심지만, 지우는 쪽이 깔끔하다.
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

    /// <summary>
    /// 요청을 보낸 실제 클라이언트 IP.
    /// <b>본체는 <see cref="LoginCompletion.ResolveClientIp"/> 하나다</b> —
    /// 여기 한 벌을 더 두면 프록시 헤더를 읽는 규칙이 두 곳으로 갈린다.
    /// </summary>
    private static string? ResolveClientIp(HttpContext http) =>
        LoginCompletion.ResolveClientIp(http);
}
