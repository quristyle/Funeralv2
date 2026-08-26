using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Services;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/"); // /auth 접두사 제거

        group.MapPost("/login", async (LoginRequestDto request, AppDbContext db, IConfiguration config,
            IHostEnvironment env, ILogger<Account> logger,
            IRoleAssignmentService roleAssignmentService, ILoginLogService loginLog,
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

            // 2-2. 접속 기록을 남긴다.
            //      /profile 화면의 '최근 로그인 시간 · 접속 아이피' 가 이 값을 읽는다.
            //      기록에 실패해도 로그인은 막지 않는다 — 기록은 로그인의 부수 효과일 뿐이다.
            var loginAt = DateTime.UtcNow;
            var clientIp = ResolveClientIp(http);
            try
            {
                account.LastLoginAt = loginAt;
                account.LastLoginIp = clientIp;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "접속 기록 저장 실패: {Username}", request.Username);
            }

            // 마지막 값과 별도로 한 줄씩 쌓는다. 계정 정보 화면이 '지난번 접속' ·
            // '접속 기록' 을 보여 주려면 이력이 있어야 한다(마지막 값만으로는 안 된다).
            await loginLog.WriteAsync(
                account.Id, request.Username, success: true,
                failReason: null, clientIp, http.Request.Headers.UserAgent.ToString());

            // 2-3. 비밀번호 사용 기간.
            //      만료되어도 토큰은 정상 발급한다. 비밀번호를 바꾸려면 로그인이 되어야 하기 때문이다.
            //      대신 토큰에 기준 시각을 실어, 게이트웨이가 비밀번호 변경 외의 요청을 막는다.
            var expiryDays = PasswordPolicy.ExpiryDays(config);
            var passwordExpired = PasswordPolicy.IsExpired(account.PasswordChangedAt, expiryDays, loginAt);
            var daysRemaining = PasswordPolicy.DaysRemaining(account.PasswordChangedAt, expiryDays, loginAt);

            if (passwordExpired)
            {
                logger.LogInformation(
                    "비밀번호 사용 기간이 지났습니다({Days}일). 변경 전까지 다른 요청은 게이트웨이가 막습니다: {Username}",
                    expiryDays, request.Username);
            }

            // 3. 토큰 발급
            // 토큰을 **발급하는** 자리다. 여기서 폴백 키를 쓰면 게이트웨이가 검증하지 못하는
            // 토큰이 나가거나, 더 나쁘게는 잘 알려진 키로 서명된 토큰이 나간다 (결정 D1-B).
            var secretKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
                config, "JwtSettings:SecretKey", "AuthServer");
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secretKey);
            
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
            // 예전에는 세 가지가 모두 없어서, 게이트웨이가 늘 X-User-Role: User 만 보냈고
            // 헬프데스크의 이메일 대조는 한 번도 동작하지 못했다.
            // 역할은 세 단계로 걸 수 있다 — 회사 · 부서 · 사람. **셋을 모두 합친다.**
            // 예전에는 사람에게 직접 걸린 것만 봤다. 그러면 회사·부서에 걸어 둔 역할이
            // 토큰에 실리지 않아, 화면에서는 역할이 보이는데 실제 권한은 없는 상태가 된다.
            var effective = await roleAssignmentService.ResolveEffectiveRolesAsync(account.Id);
            var roleIds = effective.RoleIds;

            var email = await db.AccountProfileDetails
                .Where(p => p.AccountId == account.Id && p.DetailType == "Email")
                .OrderByDescending(p => p.IsPrimary)
                .Select(p => p.Content)
                .FirstOrDefaultAsync();

            // 이 계정이 어느 MSA 레코드에서 왔는지. 형식은 `<서비스>:<테이블>:<원본키>` 다.
            //   helpdesk:admin:4      → jsini.admin.id = 4
            //   projmng:dev_user:jskim → projmng.dev_user.user_id = 'jskim'
            //
            // 이관 스크립트(docs/sql/msa_user_import.sql)가 계정을 만들 때 남긴 값이라
            // **추정이 아니라 확정된 대응 관계**다. 이관 시 아이디 충돌을 피하려고 접두어를
            // 붙였기 때문에(`jskim` → `pm_jskim`) 로그인 아이디만으로는 원본을 찾을 수 없다.
            // 그래서 이 값을 신원과 함께 내려보내, 각 서비스가 자기 체계의 사용자를 찾을 수 있게 한다.
            var msaSource = await db.AccountProfileDetails
                .Where(p => p.AccountId == account.Id && p.DetailType == "MsaSource")
                .OrderByDescending(p => p.IsPrimary)
                .Select(p => p.Content)
                .FirstOrDefaultAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, account.UserId),
                new Claim(ClaimTypes.Name, account.UserName ?? string.Empty),
                new Claim("Id", account.Id),
                new Claim("RealName", account.RealName ?? account.UserName ?? string.Empty),
                new Claim("CompanyId", account.CompanyId ?? string.Empty)
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
            // 만료 여부(불린)가 아니라 시각을 싣는 이유: 토큰 수명이 7일이라
            // 불린을 실으면 토큰을 받은 뒤 만료되는 구간을 놓친다.
            if (account.PasswordChangedAt is not null)
            {
                claims.Add(new Claim(
                    "PwdChangedAt",
                    DateTime.SpecifyKind(account.PasswordChangedAt.Value, DateTimeKind.Utc)
                        .ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
            }

            foreach (var roleId in roleIds)
            {
                claims.Add(new Claim(ClaimTypes.Role, roleId));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = "funeralv2-auth",
                Audience = "funeralv2-services",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(token);

            // ── 파일 읽기용 쿠키 ──────────────────────────────────
            //
            // 왜 토큰을 쿠키로 한 번 더 내려보내는가.
            //
            // 화면은 사진을 `<img src="/api/file/thumbnail/{id}">` 로 그린다. 브라우저는 그런
            // 태그에 `Authorization` 헤더를 붙여 주지 않는다. 그래서 **로그인한 사람이 포털에서
            // 사진을 보는 요청조차 FileServer 쪽에서는 익명과 구별되지 않았다.**
            // 그 때문에 파일 읽기 라우트를 익명으로 열어 둘 수밖에 없었고,
            // 결국 파일 아이디만 알면 누구나 남의 첨부를 내려받을 수 있었다.
            //
            // 브라우저가 스스로 보내는 인증 수단이 있으면 그 전제가 사라진다. 그래서 쿠키다.
            //
            // 안전장치 셋을 함께 건다.
            //   Path=/api/file  파일 경로에만 실려 나간다. 다른 API 로는 아예 가지 않는다.
            //   SameSite=Lax    남의 사이트가 우리 주소로 `<img>` 를 걸어도 쿠키가 실리지 않는다.
            //                   (같은 출처에서 오는 `<img>` 에는 실린다 — 우리에게 필요한 그 경우다)
            //   HttpOnly        스크립트가 읽을 수 없다.
            //
            // 게이트웨이는 이 쿠키를 **파일 읽기 경로에서만** 신원의 근거로 받는다.
            // 업로드·삭제에는 쓰지 않는다 — 쓰면 CSRF 로 남이 파일을 지울 수 있다.
            // 짝이 되는 코드는 ApiGateway/Program.cs 의 `OnMessageReceived` 다.
            // **쿠키 이름을 바꾸려면 두 곳을 함께 바꿔야 한다.**
            http.Response.Cookies.Append("jsini_file_at", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/file",
                Expires = new DateTimeOffset(tokenDescriptor.Expires!.Value, TimeSpan.Zero)
            });

            // 결과 데이터를 DTO에 담기
            var loginResult = new LoginResponseDto
            {
                AccessToken = accessToken,
                PasswordExpired = passwordExpired,
                PasswordExpiryDays = PasswordPolicy.IsEnabled(expiryDays) ? expiryDays : null,
                PasswordDaysRemaining = daysRemaining
            };

            // [중요] ApiResponse.Ok로 감싸서 반환
            return Results.Ok(ApiResponse<LoginResponseDto>.Ok(loginResult));
        });

        group.MapPost("/logout", (HttpContext http, IHostEnvironment env) =>
        {
            // 파일 읽기용 쿠키를 지운다. 지우지 않으면 로그아웃한 뒤에도
            // 브라우저에 남은 쿠키로 사진을 계속 볼 수 있다.
            // 심을 때와 옵션이 같아야 브라우저가 같은 쿠키로 알아본다(특히 Path).
            http.Response.Cookies.Delete("jsini_file_at", new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/file"
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
        .WithOpenApi()
        .RequireAuthorization();


    }

    /// <summary>
    /// 요청을 보낸 실제 클라이언트 IP.
    /// </summary>
    /// <remarks>
    /// AuthServer 는 게이트웨이 뒤에 있다. 그래서 <c>RemoteIpAddress</c> 를 그대로 쓰면
    /// 모든 계정의 접속 IP 가 게이트웨이 주소로 똑같이 남는다.
    /// YARP 가 붙여 주는 <c>X-Forwarded-For</c> 의 <b>첫 값</b>이 원래 클라이언트다
    /// (뒤로 갈수록 중간 프록시다).
    ///
    /// <para>
    /// 이 값은 <b>클라이언트가 보낸 헤더라 위조할 수 있다.</b> 게이트웨이가 덧붙이는 방식이라
    /// 앞에 임의의 값을 심어 둘 수 있다. 그래서 이 값은 <b>참고용 기록으로만</b> 쓰고
    /// 권한 판단에는 절대 쓰지 않는다.
    /// </para>
    /// </remarks>
    private static string? ResolveClientIp(HttpContext http)
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
