using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace NotificationServer.Services;

/// <summary>
/// 앱알림 아이콘 <b>한 장을 여는 열쇠</b>를 만든다.
/// </summary>
public interface IAvatarIconTokenFactory
{
    /// <summary>
    /// 그 파일 하나만, 잠깐 동안 읽을 수 있는 토큰. 만들 수 없으면 <c>null</c>.
    /// </summary>
    /// <param name="fileId">프로필 사진 파일 아이디</param>
    string? Create(string fileId);
}

/// <inheritdoc cref="IAvatarIconTokenFactory" />
/// <remarks>
/// <para>
/// <b>왜 열쇠가 따로 필요한가.</b> 알림 아이콘을 실제로 받아 오는 것은 화면이
/// 아니라 <b>브라우저 자신</b>이다 — 서비스워커가 <c>showNotification</c> 에
/// 적어 준 주소를 브라우저가 알림을 띄우는 그 순간에 직접 부르고, 그 요청은
/// 서비스워커의 <c>fetch</c> 처리기도 거치지 않는다. 그래서 그 요청에 실리는
/// 신원은 <b>브라우저가 스스로 보내는 것</b>, 즉 쿠키뿐이다.
/// </para>
///
/// <para>
/// 그런데 알림은 <b>포털에 로그인해 있지 않은 기기에도 도착한다.</b> 구독은
/// 서비스워커 등록에 붙어 오래 살아남는 반면 포털의 인증 쿠키
/// (<c>jsini.portal</c>)는 브라우저를 닫으면 사라지는 세션 쿠키다. 그 기기에서
/// 아이콘 주소는 <b>신원 없이</b> 불리고, 프로필 사진은 익명에게 열리지 않으므로
/// (<c>FileServer/Endpoints/PublicFileAccessFilter.cs</c> — <c>profile/</c> 는
/// 익명 열람 목록에 없다) 아이콘이 늘 그림자로 떨어졌다.
/// </para>
///
/// <para>
/// <b>그래서 「이 파일 한 장」만 여는 열쇠를 주소에 같이 싣는다.</b> 남의 얼굴
/// 사진을 아이디만 알면 열 수 있게 푸는 것(=<c>profile/</c> 를 익명에 여는 것)은
/// 그 판정을 만든 이유를 되돌리는 일이라 하지 않는다. 대신 알림을 보내는 우리가
/// <b>그 사람에게 가는 그 알림에만</b> 열쇠를 실어 준다.
/// </para>
///
/// <para>
/// 열쇠는 <b>다른 데 쓸 수 없다.</b> 주인 이름이 <see cref="SubjectPrefix"/> 로
/// 시작하므로
/// <list type="bullet">
///   <item>게이트웨이가 <b>파일 읽기 경로 밖에서는 아예 거절</b>하고
///         (<c>ApiGateway/Program.cs</c>),</item>
///   <item>FileServer 는 <b>이름에 적힌 그 파일만</b> 내준다
///         (<c>PublicFileAccessFilter</c>).</item>
/// </list>
/// 거기에 수명이 짧다. 잃어버려도 남의 사진 한 장이 잠깐 열릴 뿐이다.
/// </para>
/// </remarks>
public sealed class AvatarIconTokenFactory(IConfiguration config, ILogger<AvatarIconTokenFactory> logger)
    : IAvatarIconTokenFactory
{
    /// <summary>
    /// 이 열쇠의 <b>주인 이름 앞머리</b>. 게이트웨이가 <c>X-User-Id</c> 로 넘겨 주고
    /// FileServer 가 그것을 보고 「사진 한 장짜리 열쇠」임을 안다.
    /// </summary>
    /// <remarks>
    /// <b>세 서비스에 같은 글자가 적혀 있다.</b> 서로를 참조하지 않으므로 한쪽만
    /// 고쳐도 빌드는 통과하고, 어긋나면 아이콘만 조용히 그림자가 된다.
    /// <c>web/tests</c> 의 <c>AvatarIconTests</c> 가 셋을 맞춰 본다.
    /// </remarks>
    public const string SubjectPrefix = "push-icon:";

    /// <summary>열쇠 수명(분). 기본 한 시간.</summary>
    /// <remarks>
    /// 알림 아이콘은 <b>알림이 뜨는 그 순간</b> 받아 가므로 사실 몇 분이면 된다.
    /// 그래도 한 시간을 주는 것은, 기기가 꺼져 있다가 나중에 받는 알림
    /// (푸시 서비스가 들고 있다가 배달한다)에서도 얼굴이 뜨게 하기 위해서다.
    /// 그보다 더 늦게 배달된 알림은 그림자로 뜬다 — 지금과 같다.
    /// </remarks>
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
            // 기동할 때 JwtKeyGuard 가 막으므로 여기 올 일이 없다. 그래도
            // **아이콘 하나 때문에 알림을 멈추지는 않는다** — 열쇠 없는 주소를
            // 주면 로그인해 있는 기기에서는 지금까지처럼 사진이 뜬다.
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
