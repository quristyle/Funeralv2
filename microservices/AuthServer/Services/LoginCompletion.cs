using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 「이 사람이 맞다」가 정해진 뒤에 따라붙는 일 한 벌.
/// </summary>
/// <remarks>
/// <para>
/// 들어오는 길이 둘이 됐다 — 비밀번호(<c>AuthEndpoints</c>)와
/// 패스키(<c>WebAuthnEndpoints</c>). 두 길이 갈리는 것은 <b>신원을 확인하는
/// 방법</b>뿐이고, 그 뒤에 할 일은 완전히 같다.
/// </para>
///
/// <list type="number">
///   <item>계정 상태를 본다(승인 대기 · 정지).</item>
///   <item>마지막 접속 시각·아이피를 적는다.</item>
///   <item>접속 기록을 한 줄 쌓는다.</item>
///   <item>비밀번호 사용 기간을 계산한다.</item>
///   <item>토큰 한 벌을 발급한다.</item>
///   <item>갱신 쿠키와 파일 읽기용 쿠키를 굽는다.</item>
/// </list>
///
/// <para>
/// 여섯을 길마다 한 벌씩 적으면 <b>반드시 한쪽만 고치는 날이 온다.</b> 그때
/// 증상은 원인과 멀다 — 「지문으로 들어가면 사진이 안 보인다」(파일 쿠키를
/// 한쪽에만 심었다)나 「지문 로그인은 접속 기록에 안 남는다」 같은 것이다.
/// </para>
/// </remarks>
public sealed class LoginCompletion(
    AppDbContext db,
    IConfiguration config,
    IHostEnvironment env,
    AccessTokenFactory tokenFactory,
    ILoginLogService loginLog,
    ILogger<LoginCompletion> logger)
{
    /// <summary>
    /// 계정을 쓸 수 있는 상태인가. 쓸 수 없으면 <b>그대로 돌려줄 응답</b>이 나오고,
    /// 쓸 수 있으면 <c>null</c> 이다.
    /// </summary>
    /// <remarks>
    /// <b>신원 확인이 끝난 다음에 부른다.</b> 먼저 부르면 아이디만 넣어 보고
    /// 「승인 대기 중」이라는 답을 받을 수 있어, 그 아이디가 있다는 것이 새어
    /// 나간다 — 로그인 실패 문구를 뭉뚱그려 둔 뜻이 사라진다.
    ///
    /// <para>
    /// 상태는 계정 표의 칸이 아니라 <c>account_profile_details</c> 의 Status 다
    /// (계정 관리가 예전부터 그 자리에 넣어 왔다). 값이 아예 없는 옛 계정은
    /// ACTIVE 로 본다 — 없다는 이유로 전원을 막을 수는 없다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 로그인 성공 뒤처리를 하고 <c>/auth/login</c> 과 <b>똑같은 모양</b>의 응답을 만든다.
    /// </summary>
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
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "접속 기록 저장 실패: {Username}", account.UserId);
        }

        // 마지막 값과 별도로 한 줄씩 쌓는다. 계정 정보 화면이 '지난번 접속' ·
        // '접속 기록' 을 보여 주려면 이력이 있어야 한다(마지막 값만으로는 안 된다).
        await loginLog.WriteAsync(
            account.Id, account.UserId, success: true,
            failReason: null, clientIp, http.Request.Headers.UserAgent.ToString());

        // 비밀번호 사용 기간. 만료되어도 토큰은 정상 발급한다 — 비밀번호를
        // 바꾸려면 로그인이 되어야 하기 때문이다.
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

    /// <summary>
    /// 파일 읽기용 쿠키를 굽는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 왜 토큰을 쿠키로 한 번 더 내려보내는가. 화면은 사진을
    /// <c>&lt;img src="/api/file/thumbnail/{id}"&gt;</c> 로 그리는데, 브라우저는
    /// 그런 태그에 <c>Authorization</c> 헤더를 붙여 주지 않는다. 그래서
    /// <b>로그인한 사람이 사진을 보는 요청조차 FileServer 쪽에서는 익명과
    /// 구별되지 않았고</b>, 결국 파일 아이디만 알면 누구나 남의 첨부를 받아 갔다.
    /// </para>
    ///
    /// <para>안전장치 셋을 함께 건다.</para>
    /// <list type="bullet">
    ///   <item><c>Path=/api/file</c> — 파일 경로에만 실려 나간다.</item>
    ///   <item><c>SameSite=Lax</c> — 남의 사이트가 우리 주소로 <c>&lt;img&gt;</c> 를
    ///         걸어도 실리지 않는다(같은 출처에서 오는 것에는 실린다).</item>
    ///   <item><c>HttpOnly</c> — 스크립트가 읽을 수 없다.</item>
    /// </list>
    ///
    /// <para>
    /// 게이트웨이는 이 쿠키를 <b>파일 읽기 경로에서만</b> 신원의 근거로 받는다.
    /// 업로드·삭제에는 쓰지 않는다 — 쓰면 CSRF 로 남이 파일을 지울 수 있다.
    /// <b>쿠키 이름을 바꾸려면 게이트웨이와 함께 바꿔야 한다.</b>
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 요청을 보낸 실제 클라이언트 IP.
    /// </summary>
    /// <remarks>
    /// AuthServer 는 게이트웨이 뒤에 있다. 그래서 <c>RemoteIpAddress</c> 를 그대로 쓰면
    /// 모든 계정의 접속 IP 가 게이트웨이 주소로 똑같이 남는다.
    /// YARP 가 붙여 주는 <c>X-Forwarded-For</c> 의 <b>첫 값</b>이 원래 클라이언트다.
    ///
    /// <para>
    /// 이 값은 <b>클라이언트가 보낸 헤더라 위조할 수 있다.</b> 그래서 <b>참고용
    /// 기록으로만</b> 쓰고 권한 판단에는 절대 쓰지 않는다.
    /// </para>
    /// </remarks>
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
