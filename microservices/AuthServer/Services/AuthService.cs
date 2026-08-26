using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Services;

/// <summary>
/// 인증 서비스 구현체
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService>? _logger;

    /// <summary>
    /// AuthService 생성자
    /// </summary>
    /// <param name="db">DB 컨텍스트</param>
    /// <param name="config">구성 설정</param>
    /// <param name="logger">로거 (선택)</param>
    public AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService>? logger = null)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 로그인 처리 로직
    /// </summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
    {
        // 1. 사용자 아이디로 계정 조회
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == request.Username);

        // 2. 계정이 없거나 비밀번호가 틀린 경우 null 반환
        //    저장값이 아직 평문인 계정도 그대로 로그인된다(PasswordHasher 참고).
        if (account == null || !PasswordHasher.Verify(account.Password, request.Password))
        {
            return null;
        }

        // 2-1. 평문이거나 옛 기준으로 해시된 값이면 이 기회에 다시 해시해 저장한다.
        //      로그인에 성공한 지금이 평문 비밀번호를 아는 유일한 시점이다.
        //      저장에 실패해도 로그인 자체는 막지 않는다.
        if (PasswordHasher.NeedsUpgrade(account.Password))
        {
            try
            {
                account.Password = PasswordHasher.Hash(request.Password);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "비밀번호 해시 승격 실패. userId={UserId}", account.UserId);
            }
        }

        // 3. JWT 토큰 발급
        var token = GenerateJwtToken(account);

        return new LoginResponseDto
        {
            AccessToken = token
        };
    }

    /// <summary>
    /// 로그아웃 처리 로직 (서버측 추가 작업)
    /// </summary>
    public Task LogoutAsync()
    {
        // JWT 방식은 클라이언트에서 토큰을 폐기하므로 서버는 별도 작업 없이 완료 보고
        return Task.CompletedTask;
    }

    /// <summary>
    /// JWT 토큰 생성 내부 메서드
    /// </summary>
    private string GenerateJwtToken(Account account)
    {
        // ── 여기에 버그가 있었다 (D1-B 작업 중 발견) ──────────
        //
        // `Jwt:Key` 를 읽고 있었는데 **AuthServer 설정에는 Jwt 섹션이 없다**
        // (이 서비스는 JwtSettings:SecretKey 를 쓴다). 그래서 항상 폴백
        // "DefaultSecretKeyForDevelopmentOnly!" 로 서명하고 있었다.
        //
        // 게이트웨이는 공용 키로 검증하므로 그 토큰을 받아 주지 않는다. 실제 로그인은
        // AuthEndpoints 가 처리하고 있어 눈에 띄지 않았던 것으로 보인다.
        // 이제 다른 곳과 같은 키를 쓴다.
        var secretKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
            _config, "JwtSettings:SecretKey", "AuthServer");
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));

        // 토큰에 담길 클레임 정보 설정
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, account.UserId),
            new Claim(ClaimTypes.Name, account.UserName ?? string.Empty),
            new Claim("Id", account.Id),
            new Claim("CompanyId", account.CompanyId ?? string.Empty)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // 7일 유효
            // 이 둘도 없는 섹션(Jwt)에서 읽어 **null 이었다.** 게이트웨이는
            // 발급자·수신자를 검증하므로(ValidateIssuer·ValidateAudience) 그 토큰은
            // 애초에 통과할 수 없었다. 다른 발급 경로(AuthEndpoints)와 같은 값으로 맞춘다.
            Issuer = _config["JwtSettings:Issuer"] ?? "funeralv2-auth",
            Audience = _config["JwtSettings:Audience"] ?? "funeralv2-services",
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(securityToken);
    }
}
