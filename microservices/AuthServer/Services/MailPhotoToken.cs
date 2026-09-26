using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Services;

/// <summary>
/// 메일 본문에 사진 한 장을 싣는 <b>사진 한 장짜리 열쇠</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 열쇠가 필요한가]
/// </para>
/// <para>
/// 메일 프로그램(네이버 메일 · Gmail · Outlook)은 로그인하지 않은 채로 그림을
/// 받아 간다. 프로필 사진은 익명에게 열리지 않으므로(<c>profile/</c> 는 공개
/// 경로가 아니다) 열쇠 없이 주소만 적으면 늘 깨진 그림이 된다.
/// </para>
/// <para>
/// 앱알림 아이콘이 이미 같은 문제를 같은 방법으로 풀었다
/// (<c>NotificationServer/Services/AvatarIconToken.cs</c>). 여기는 그 열쇠를
/// <b>글자 하나 다르지 않게</b> 만든다 — 주인 이름 <c>push-icon:{파일}</c> ·
/// 같은 서명 · 같은 발급자 · 같은 대상. 그래서 게이트웨이가 파일 읽기 경로로만
/// 묶고, FileServer 가 그 파일 하나만 연다(<c>PublicFileAccessFilter</c>).
/// 새 규칙을 만들지 않는다.
/// </para>
/// <para>
/// [수명만 다르다]
/// </para>
/// <para>
/// 알림은 뜨는 순간 받아 가므로 한 시간이면 되지만, 메일은 며칠 뒤에 열어
/// 보기도 한다. 기본 30일(<c>Auth:Signup:MailPhotoDays</c>). 이 열쇠로 열리는
/// 것은 <b>그 신청자의 사진 한 장뿐</b>이고 받는 사람은 관리자다.
/// </para>
/// </remarks>
public sealed class MailPhotoToken(IConfiguration config, ILogger<MailPhotoToken> logger)
{
    /// <summary>
    /// 주인 이름 앞머리. 알림 서비스 · 게이트웨이 · FileServer 와 같은 글자다
    /// (<c>web/tests</c> 의 <c>AvatarIconTests</c> 가 맞춰 본다).
    /// </summary>
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
