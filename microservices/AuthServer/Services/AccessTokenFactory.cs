using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Services;

/// <summary>발급한 토큰 한 벌.</summary>
/// <param name="AccessToken">게이트웨이가 검증하는 access token</param>
/// <param name="AccessExpiresAt">access token 만료 시각 (UTC)</param>
/// <param name="RefreshToken">갱신용 토큰. 쿠키로만 나간다</param>
/// <param name="RefreshExpiresAt">refresh token 만료 시각 (UTC)</param>
public readonly record struct IssuedTokens(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt);

/// <summary>
/// access token 과 refresh token 을 만든다.
///
/// <para>
/// <b>왜 따로 뺐나</b> — 예전에는 이 코드가 <c>/login</c> 안에만 있었다.
/// <c>/refresh</c> 가 생기면서 <b>똑같은 토큰</b>을 두 자리에서 만들어야 하는데,
/// 복사해 두면 한쪽에만 클레임을 더하는 날이 반드시 온다. 그때 증상은
/// 「로그인 직후에는 되는데 일주일 뒤부터 권한이 없다」 가 되고, 원인이
/// 토큰이라는 것을 알아채기가 아주 어렵다.
/// </para>
///
/// <para>
/// <b>refresh token 을 DB 에 두지 않는다</b> — 표를 새로 만들면 EF 마이그레이션이
/// 필요하고, 이 저장소는 개발 DB 가 곧 운영 DB 다. 대신 access token 과 같은
/// 열쇠로 서명하되 <b>대상(audience)을 다르게</b> 해서 서로 바꿔 쓸 수 없게 한다.
/// 게이트웨이는 <c>funeralv2-services</c> 만 받으므로 refresh token 을 헤더에
/// 실어 보내도 통하지 않는다.
/// </para>
///
/// <para>
/// <b>그래서 즉시 무효화는 못 한다.</b> 대신 두 가지로 좁혀 둔다 — 로그아웃이
/// 쿠키를 지우고, 비밀번호를 바꾸면 그 이전에 나간 refresh token 이 전부
/// 거부된다(<see cref="ValidateRefreshTokenAsync"/> 의 시각 대조).
/// 계정을 즉시 끊어야 하는 요구가 생기면 그때 표를 만든다.
/// </para>
/// </summary>
public sealed class AccessTokenFactory(
    AppDbContext db,
    IConfiguration config,
    IRoleAssignmentService roleAssignmentService)
{
    /// <summary>토큰을 발급하는 쪽. 게이트웨이·각 서비스의 <c>ValidIssuer</c> 와 같아야 한다.</summary>
    public const string Issuer = "funeralv2-auth";

    /// <summary>업무 API 를 부를 때 쓰는 토큰의 대상.</summary>
    public const string AccessAudience = "funeralv2-services";

    /// <summary>
    /// 갱신에만 쓰는 토큰의 대상. <b>업무 API 는 이 대상을 받지 않는다</b> —
    /// 게이트웨이가 <c>ValidateAudience</c> 를 켜 두었기 때문이다.
    /// </summary>
    public const string RefreshAudience = "funeralv2-refresh";

    /// <summary>브라우저가 들고 다니는 갱신 쿠키 이름.</summary>
    public const string RefreshCookieName = "jsini_rt";

    /// <summary>
    /// 갱신 쿠키를 실어 보내는 경로. <b>게이트웨이 기준</b>이다 —
    /// 브라우저는 <c>/api/auth/...</c> 로 부르지 AuthServer 를 직접 부르지 않는다.
    /// 좁혀 두면 다른 API 요청에는 아예 실리지 않는다.
    /// </summary>
    public const string RefreshCookiePath = "/api/auth";

    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>access token 수명(일). 옛 값 그대로 7일이 기본이다.</summary>
    private int AccessTokenDays => config.GetValue<int?>("Auth:AccessTokenDays") ?? 7;

    /// <summary>
    /// refresh token 수명(일). 기본 30일 — access token 이 만료된 뒤에도
    /// 한동안은 다시 로그인하지 않게 하려는 값이다. 0 이하로 두면 갱신을 끈다.
    /// </summary>
    public int RefreshTokenDays => config.GetValue<int?>("Auth:RefreshTokenDays") ?? 30;

    /// <summary>갱신을 쓰는가. 설정으로 끌 수 있어야 되돌리기가 쉽다.</summary>
    public bool RefreshEnabled => RefreshTokenDays > 0;

    /// <summary>
    /// 이 계정으로 토큰 한 벌을 만든다.
    /// </summary>
    /// <param name="account">토큰의 주인</param>
    /// <param name="issuedAt">발급 기준 시각 (UTC)</param>
    public async Task<IssuedTokens> IssueAsync(Account account, DateTime issuedAt)
    {
        // 토큰을 **발급하는** 자리다. 여기서 폴백 키를 쓰면 게이트웨이가 검증하지 못하는
        // 토큰이 나가거나, 더 나쁘게는 잘 알려진 키로 서명된 토큰이 나간다 (결정 D1-B).
        var key = SigningKey();

        // ── 토큰에 담는 신원 ──────────────────────────────────
        //
        // 헬프데스크·프로젝트관리처럼 이식해 온 서비스는 "지금 요청한 사람이 누구인가" 를
        // 게이트웨이가 붙여 주는 헤더나 토큰 클레임으로만 알 수 있다. 그 서비스들이
        // 자기 사용자 테이블 대신 JSini 계정을 쓰게 하려면 아래 값이 토큰에 있어야 한다.
        //
        //   - 역할(Role)  : 프로젝트관리의 직접 쿼리 실행 권한 확인 등
        //   - 이메일      : 헬프데스크 계정 연결의 이메일 대조
        //   - 회사        : 고객 범위 제한
        //
        // 역할은 세 단계로 걸 수 있다 — 회사 · 부서 · 사람. **셋을 모두 합친다.**
        // 사람에게 직접 걸린 것만 보면, 회사·부서에 걸어 둔 역할이 토큰에 실리지 않아
        // 화면에서는 역할이 보이는데 실제 권한은 없는 상태가 된다.
        var effective = await roleAssignmentService.ResolveEffectiveRolesAsync(account.Id);

        var email = await db.AccountProfileDetails
            .Where(p => p.AccountId == account.Id && p.DetailType == "Email")
            .OrderByDescending(p => p.IsPrimary)
            .Select(p => p.Content)
            .FirstOrDefaultAsync();

        // 이 계정이 어느 MSA 레코드에서 왔는지. 형식은 `<서비스>:<테이블>:<원본키>` 다.
        //   helpdesk:admin:4      → jsini.admin.id = 4
        //   projmng:dev_user:jskim → projmng.dev_user.user_id = 'jskim'
        var msaSource = await db.AccountProfileDetails
            .Where(p => p.AccountId == account.Id && p.DetailType == "MsaSource")
            .OrderByDescending(p => p.IsPrimary)
            .Select(p => p.Content)
            .FirstOrDefaultAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserId),
            new(ClaimTypes.Name, account.UserName ?? string.Empty),
            new("Id", account.Id),
            new("RealName", account.RealName ?? account.UserName ?? string.Empty),
            new("CompanyId", account.CompanyId ?? string.Empty),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (!string.IsNullOrWhiteSpace(msaSource))
        {
            claims.Add(new Claim("MsaSource", msaSource));
        }

        // 비밀번호를 마지막으로 바꾼 시각. 게이트웨이가 매 요청마다 여기서 만료를 다시 계산한다.
        // 만료 여부(불린)가 아니라 시각을 싣는 이유: 토큰 수명이 길어서
        // 불린을 실으면 토큰을 받은 뒤 만료되는 구간을 놓친다.
        if (PasswordStamp(account) is { } stamp)
        {
            claims.Add(new Claim("PwdChangedAt", stamp));
        }

        foreach (var roleId in effective.RoleIds)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleId));
        }

        var accessExpires = issuedAt.AddDays(AccessTokenDays);
        var accessToken = Write(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessExpires,
            Issuer = Issuer,
            Audience = AccessAudience,
            SigningCredentials = Credentials(key),
        });

        // ── 갱신 토큰 ────────────────────────────────────────
        //
        // 담는 것을 최소로 둔다. 역할·이메일 같은 값은 갱신할 때 **다시 읽어**
        // 새 access token 에 싣는다. 갱신 토큰에 실어 두면 그 값이 발급 시점에
        // 얼어붙어, 역할을 바꿔도 30일 동안 옛 권한이 따라다닌다.
        var refreshExpires = issuedAt.AddDays(Math.Max(RefreshTokenDays, 1));
        var refreshClaims = new List<Claim>
        {
            new("Id", account.Id),
            new(ClaimTypes.NameIdentifier, account.UserId),
        };

        // 비밀번호를 바꾸면 그 이전에 나간 갱신 토큰을 전부 거절하기 위한 기준값이다.
        if (PasswordStamp(account) is { } refreshStamp)
        {
            refreshClaims.Add(new Claim("PwdChangedAt", refreshStamp));
        }

        var refreshToken = Write(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(refreshClaims),
            Expires = refreshExpires,
            Issuer = Issuer,
            Audience = RefreshAudience,
            SigningCredentials = Credentials(key),
        });

        return new IssuedTokens(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    /// <summary>
    /// 갱신 토큰을 검증하고 그 주인을 돌려준다. 못 믿을 값이면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>거절하는 경우 넷</b> — 서명·기간·대상이 안 맞을 때, 계정이 사라졌을 때,
    /// 그리고 <b>토큰을 받은 뒤 비밀번호가 바뀌었을 때</b>다. 마지막 것이 이
    /// 방식에서 유일한 무효화 수단이라 빠뜨리면 안 된다 — 비밀번호를 바꿨는데
    /// 옛 세션이 30일 더 사는 것은 사고다.
    /// </remarks>
    public async Task<Account?> ValidateRefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || !RefreshEnabled)
        {
            return null;
        }

        ClaimsPrincipal principal;
        try
        {
            principal = Handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(SigningKey()),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                // **여기가 요점이다.** 업무용 토큰을 갱신 토큰으로 쓰지 못하게 막는다.
                ValidAudience = RefreshAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            }, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }

        var accountId = principal.FindFirst("Id")?.Value;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null)
        {
            return null;
        }

        // 비밀번호가 바뀌었으면 그 이전 토큰은 전부 거절한다.
        // 값이 없다가 생긴 경우(칸이 비어 있던 계정이 비밀번호를 바꾼 경우)도
        // 「바뀌었다」로 읽어야 한다 — 문자열 비교가 그것까지 잡는다.
        if (!string.Equals(principal.FindFirst("PwdChangedAt")?.Value, PasswordStamp(account),
                StringComparison.Ordinal))
        {
            return null;
        }

        return account;
    }

    /// <summary>갱신 쿠키를 굽는다. 로그인과 갱신이 같은 옵션을 써야 브라우저가 같은 쿠키로 알아본다.</summary>
    public static void AppendRefreshCookie(
        HttpResponse response, string refreshToken, DateTime expiresAt, bool secure) =>
        response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            // Lax 로 둔다. 남의 사이트에서 우리 주소로 POST 를 걸어도 실리지 않는다.
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
        });

    /// <summary>갱신 쿠키를 지운다. <b>심을 때와 옵션이 같아야 지워진다</b>(특히 Path).</summary>
    public static void DeleteRefreshCookie(HttpResponse response, bool secure) =>
        response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
        });

    /// <summary>
    /// 비밀번호 변경 시각을 토큰에 싣는 문자열로. 값이 없으면 <c>null</c> 이고,
    /// 그때는 클레임 자체를 넣지 않는다(게이트웨이가 「모른다」로 읽어 잠그지 않는다).
    /// </summary>
    private static string? PasswordStamp(Account account) =>
        account.PasswordChangedAt is null
            ? null
            : DateTime.SpecifyKind(account.PasswordChangedAt.Value, DateTimeKind.Utc)
                .ToString("o", System.Globalization.CultureInfo.InvariantCulture);

    private byte[] SigningKey() => Encoding.ASCII.GetBytes(
        JSini.Shared.Infrastructure.JwtKeyGuard.Require(
            config, "JwtSettings:SecretKey", "AuthServer"));

    private static SigningCredentials Credentials(byte[] key) =>
        new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);

    private static string Write(SecurityTokenDescriptor descriptor) =>
        Handler.WriteToken(Handler.CreateToken(descriptor));
}
