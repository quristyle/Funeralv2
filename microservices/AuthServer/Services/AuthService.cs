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

    /// <summary>
    /// AuthService 생성자
    /// </summary>
    /// <param name="db">DB 컨텍스트</param>
    /// <param name="config">구성 설정</param>
    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
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
        if (account == null || account.Password != request.Password)
        {
            return null;
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
        var jwtSettings = _config.GetSection("Jwt");
        var secretKey = jwtSettings["Key"] ?? "DefaultSecretKeyForDevelopmentOnly!";
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
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(securityToken);
    }
}
