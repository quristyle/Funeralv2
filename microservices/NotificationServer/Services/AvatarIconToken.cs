using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace NotificationServer.Services;

/// <summary>앱알림 아이콘 한 장을 여는 열쇠를 만든다.</summary>
public interface IAvatarIconTokenFactory
{
    /// <summary>그 파일 하나만 잠깐 읽을 수 있는 토큰. 만들 수 없으면 null.</summary>
    /// <param name="fileId">프로필 사진 파일 아이디</param>
    string? Create(string fileId);
}

/// <inheritdoc cref="IAvatarIconTokenFactory" />
/// 열쇠는 그 파일 한 장만 열고 주인 이름이 SubjectPrefix 로 시작해 다른 데 쓸 수 없다. 알림은 로그인하지 않은 기기에도 도착하므로 쿠키 대신 주소에 싣는다.
public sealed class AvatarIconTokenFactory(IConfiguration config, ILogger<AvatarIconTokenFactory> logger)
    : IAvatarIconTokenFactory
{
    /// <summary>열쇠 주인 이름 앞머리. 게이트웨이가 X-User-Id 로 넘기고 FileServer 가 사진 한 장짜리 열쇠임을 안다. 세 서비스에 같은 글자가 적혀 있어 web/tests 의 AvatarIconTests 가 맞춰 본다.</summary>
    public const string SubjectPrefix = "push-icon:";

    /// <summary>열쇠 수명. 기본 한 시간 — 기기가 꺼져 있다가 나중에 배달되는 알림에서도 얼굴이 뜨게 하려고 길게 잡는다.</summary>
    private int Minutes => config.GetValue<int?>("Push:IconTokenMinutes") ?? 60;

    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>파일 하나에 대응하는 주인 이름.</summary>
    public static string SubjectFor(string fileId) => SubjectPrefix + fileId;

    /// <inheritdoc />
    public string? Create(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        var key = config["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            // 기동할 때 JwtKeyGuard 가 막으므로 여기 올 일은 없다. 그래도 알림을 멈추지 않게 열쇠 없는 주소를 준다.
            logger.LogWarning("Jwt:Key 가 없어 알림 아이콘 열쇠를 만들지 못했습니다.");
            return null;
        }

        try
        {
            var now = DateTime.UtcNow;

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SubjectFor(fileId))]),
                Expires = now.AddMinutes(Math.Clamp(Minutes, 1, 24 * 60)),
                Issuer = config["Jwt:Issuer"] ?? "funeralv2-auth",
                Audience = config["Jwt:Audience"] ?? "funeralv2-services",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
                    SecurityAlgorithms.HmacSha256Signature),
            };

            return Handler.WriteToken(Handler.CreateToken(descriptor));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "알림 아이콘 열쇠를 만들지 못했습니다.");
            return null;
        }
    }
}
