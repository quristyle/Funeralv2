using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Services;

/// <summary>메일 본문에 사진 한 장을 싣는 사진 한 장짜리 열쇠.</summary>
/// 메일 프로그램은 로그인 없이 그림을 받아 가므로 열쇠를 붙인다. 알림 아이콘 열쇠와 글자 하나 다르지 않게 만들고, 열리는 것은 그 신청자 사진 한 장뿐이다.
public sealed class MailPhotoToken(IConfiguration config, ILogger<MailPhotoToken> logger)
{
    /// <summary>주인 이름 앞머리. 알림 서비스 · 게이트웨이 · FileServer 와 같은 글이다.</summary>
    public const string SubjectPrefix = "push-icon:";

    private static readonly JwtSecurityTokenHandler Handler = new();

    private int Days => Math.Clamp(config.GetValue<int?>("Auth:Signup:MailPhotoDays") ?? 30, 1, 90);

    /// <summary>파일 하나를 여는 열쇠. 만들지 못하면 <c>null</c>(그때는 사진 없이 보낸다).</summary>
    public string? Create(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        try
        {
            var key = Encoding.ASCII.GetBytes(
                JSini.Shared.Infrastructure.JwtKeyGuard.Require(config, "JwtSettings:SecretKey", "AuthServer"));

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SubjectPrefix + fileId)]),
                Expires = DateTime.UtcNow.AddDays(Days),
                Issuer = AccessTokenFactory.Issuer,
                Audience = AccessTokenFactory.AccessAudience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            };

            return Handler.WriteToken(Handler.CreateToken(descriptor));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "메일 사진 열쇠를 만들지 못했다 — 사진 없이 보낸다.");
            return null;
        }
    }
}
