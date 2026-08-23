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
            IHostEnvironment env, ILogger<Account> logger) =>
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

            // 3. 토큰 발급
            var jwtSettings = config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "a-very-secret-key-that-is-long-enough-for-security";
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
            var roleIds = await db.RoleAccounts
                .Where(ra => ra.AccountId == account.Id)
                .Join(db.Roles.Where(r => r.Status == 1), ra => ra.RoleId, r => r.Id, (ra, r) => r.Id)
                .Distinct()
                .ToListAsync();

            var email = await db.AccountProfileDetails
                .Where(p => p.AccountId == account.Id && p.DetailType == "Email")
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
            
            // 결과 데이터를 DTO에 담기
            var loginResult = new LoginResponseDto 
            { 
                AccessToken = tokenHandler.WriteToken(token) 
            };

            // [중요] ApiResponse.Ok로 감싸서 반환
            return Results.Ok(ApiResponse<LoginResponseDto>.Ok(loginResult));
        });

        group.MapPost("/logout", () =>
        {
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
}
