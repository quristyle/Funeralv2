using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Funeralv2.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/"); // /auth 접두사 제거

        group.MapPost("/login", async (LoginRequestDto request, AppDbContext db, IConfiguration config, ILogger<Account> logger) =>
        {
            logger.LogInformation("로그인 시도: {Username}", request.Username);

            // 1. 사용자 조회
            var account = await db.Accounts
                .FirstOrDefaultAsync(a => a.UserId == request.Username);

            // 2. 계정 검증
            if (account == null || account.Password != request.Password)
            {
                logger.LogWarning("로그인 실패: {Username}", request.Username);
                return Results.Json(ApiResponse<object>.Fail("아이디 또는 비밀번호가 잘못되었습니다.", "401"), statusCode: 401);
            }

            // 3. 토큰 발급
            var jwtSettings = config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "a-very-secret-key-that-is-long-enough-for-security";
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secretKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, account.UserId),
                new Claim(ClaimTypes.Name, account.UserName ?? string.Empty),
                new Claim("Id", account.Id)
            };

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
