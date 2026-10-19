using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>「이 사람이 맞다」가 정해진 뒤에 따라붙는 일 한 벌 — 비밀번호 로그인과 패스키 로그인이 같은 뒤처리를 쓴다. 신원을 확인하는 방법만 다르고 그 뒤는 여기서 한다.</summary>
public sealed class LoginCompletion(
    AppDbContext db,
    IConfiguration config,
    IHostEnvironment env,
    AccessTokenFactory tokenFactory,
    ILoginLogService loginLog,
    ILogger<LoginCompletion> logger)
{
    /// <summary>계정을 쓸 수 있는 상태인가. 쓸 수 없으면 그대로 돌려줄 응답을, 쓸 수 있으면 null 을 준다. 신원 확인이 끝난 다음에 부른다 — 먼저 부르면 계정 존재가 새어 나간다.</summary>
    public async Task<IResult?> RejectIfNotActiveAsync(
        HttpContext http, Account account, CancellationToken cancellationToken = default)
    {
        var status = await db.AccountProfileDetails
            .Where(d => d.AccountId == account.Id && d.DetailType == SignupService.StatusDetail)
            .Select(d => d.Content)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(status)
            || string.Equals(status, SignupService.StatusActive, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        logger.LogWarning("로그인 거절 — 계정 상태 {Status}: {Username}", status, account.UserId);

        await loginLog.WriteAsync(
            account.Id, account.UserId, success: false, LoginFailReason.NotActive,
            ResolveClientIp(http), http.Request.Headers.UserAgent.ToString());

        var message = string.Equals(status, SignupService.StatusPending, StringComparison.OrdinalIgnoreCase)
            ? "가입 승인을 기다리는 계정입니다. 승인되면 알려 드립니다."
            : "지금은 사용할 수 없는 계정입니다. 관리자에게 문의해 주십시오.";

        return Results.Json(ApiResponse<object>.Fail(message, "403"), statusCode: 403);
    }

    /// <summary>로그인 성공 뒤처리를 하고 <c>/auth/login</c> 과 같은 모양의 응답을 만든다.</summary>
    public async Task<IResult> CompleteAsync(
        HttpContext http, Account account, CancellationToken cancellationToken = default)
    {
        var loginAt = DateTime.UtcNow;
        var clientIp = ResolveClientIp(http);

        // 접속 기록. 실패해도 로그인은 막지 않는다 — 기록은 로그인의 부수 효과일 뿐이다.
        try
        {
            account.LastLoginAt = loginAt;
            account.LastLoginIp = clientIp;

            // 마지막으로 쓴 때도 함께 민다 — 로그인만 하고 화면을 안 옮긴 사람이 한 번도 안 쓴 사람으로 보이지 않게 한다.
            account.LastSeenAt = loginAt;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "접속 기록 저장 실패: {Username}", account.UserId);
        }

        // 마지막 값과 별도로 한 줄씩 쌓는다 — 「지난번 접속」·「접속 기록」 화면에는 이력이 있어야 한다.
        await loginLog.WriteAsync(
            account.Id, account.UserId, success: true,
            failReason: null, clientIp, http.Request.Headers.UserAgent.ToString());

        // 비밀번호 사용 기간이 지나도 토큰은 정상 발급한다 — 비밀번호를 바꾸려면 로그인이 되어야 하기 때문이다.
        var expiryDays = PasswordPolicy.ExpiryDays(config);
        var passwordExpired = PasswordPolicy.IsExpired(account.PasswordChangedAt, expiryDays, loginAt);
        var daysRemaining = PasswordPolicy.DaysRemaining(account.PasswordChangedAt, expiryDays, loginAt);

        if (passwordExpired)
        {
            logger.LogInformation(
                "비밀번호 사용 기간이 지났습니다({Days}일). 변경 전까지 다른 요청은 게이트웨이가 막습니다: {Username}",
                expiryDays, account.UserId);
        }

        var issued = await tokenFactory.IssueAsync(account, loginAt);
        var secure = !env.IsDevelopment();

        if (tokenFactory.RefreshEnabled)
        {
            AccessTokenFactory.AppendRefreshCookie(
                http.Response, issued.RefreshToken, issued.RefreshExpiresAt, secure);
        }

        AppendFileCookie(http.Response, issued.AccessToken, issued.AccessExpiresAt, secure);

        return Results.Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
        {
            AccessToken = issued.AccessToken,
            PasswordExpired = passwordExpired,
            PasswordExpiryDays = PasswordPolicy.IsEnabled(expiryDays) ? expiryDays : null,
            PasswordDaysRemaining = daysRemaining,
        }));
    }

    /// <summary>파일 읽기용 쿠키 이름. 게이트웨이의 <c>OnMessageReceived</c> 와 짝이다.</summary>
    public const string FileCookieName = "jsini_file_at";

    /// <summary>파일 읽기용 쿠키를 굽는다. 사진은 <c>&lt;img&gt;</c> 로 그려져 Authorization 헤더를 못 붙이므로 토큰을 쿠키로 한 번 더 내려보낸다.</summary>
    public static void AppendFileCookie(
        HttpResponse response, string accessToken, DateTime expiresAt, bool secure) =>
        response.Cookies.Append(FileCookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/api/file",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
        });

    /// <summary>요청을 보낸 실제 클라이언트 IP. AuthServer 는 게이트웨이 뒤라 RemoteIpAddress 를 그대로 쓰면 모두 게이트웨이 주소가 된다 — 프록시가 붙인 X-Forwarded-For 를 읽는다.</summary>
    public static string? ResolveClientIp(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0) return Truncate(first);
        }

        return Truncate(http.Connection.RemoteIpAddress?.ToString());
    }

    /// <summary>기록용 칸이므로 비정상적으로 긴 값은 잘라 둔다.</summary>
    private static string? Truncate(string? value) =>
        value is null || value.Length <= 100 ? value : value[..100];
}
