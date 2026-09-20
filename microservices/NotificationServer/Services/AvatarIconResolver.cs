using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;

namespace NotificationServer.Services;

/// <summary>
/// 앱알림에 띄울 <b>사람 얼굴</b>을 고른다 — 프로필 사진이 있으면 그것, 없으면 사람 형상 그림자.
/// </summary>
public interface IAvatarIconResolver
{
    /// <summary>
    /// 그 사람의 알림 아이콘 주소. <b>언제나 값을 준다</b> — 사진이 없으면
    /// <see cref="AvatarIconResolver.FallbackIcon"/> 이다.
    /// </summary>
    /// <param name="loginId">포털 로그인 아이디 (<c>scom.accounts.user_id</c>).</param>
    /// <param name="ct">취소 토큰</param>
    Task<string> ResolveAsync(string loginId, CancellationToken ct = default);
}

/// <inheritdoc cref="IAvatarIconResolver" />
/// <remarks>
/// <para>
/// <b>왜 알림 서비스가 이것을 푸는가.</b> 아이콘을 원하는 쪽(프로젝트관리의 AI 작업
/// 알림 · 헬프데스크)은 자기 DB 만 본다 — 「이 작업을 지시한 사람」의 아이디까지는
/// 알아도 그 사람의 사진이 어디 있는지는 모른다. 이메일 주소를 <c>toUser</c> 로
/// 풀어 주는 것(<c>EmailEndpoints.ResolveUserEmailsAsync</c>)과 같은 이유이고,
/// <c>scom</c> 은 이 서비스가 원래 접속하는 DB 라 서비스 경계를 넘지 않는다.
/// </para>
///
/// <para>
/// <b>주소는 셸 중계 경로(<c>/files/…</c>)로 준다.</b> 계정에 적혀 있는 값은
/// <c>/api/file/download/id/{guid}</c> — 게이트웨이와 같은 오리진에서 보던 Vue
/// 시절의 주소다. 이 아이콘을 실제로 받아 오는 것은 <b>포털(:5557)에 등록된
/// 서비스워커</b>라, 그 오리진에 있는 주소여야 한다
/// (<c>web/src/Shared/JSini.Web.Components/Data/FileDownload.cs</c> 머리말).
/// </para>
/// </remarks>
public sealed partial class AvatarIconResolver(
    AppDbContext db,
    IAvatarIconTokenFactory tokens,
    ILogger<AvatarIconResolver> logger)
    : IAvatarIconResolver
{
    /// <summary>
    /// 사진이 없을 때 쓰는 <b>사람 얼굴 형상 그림자</b>. 셸이 정적 파일로 들고 있다
    /// (<c>web/src/Shell/JSini.Web.Shell/wwwroot/avatar-fallback.png</c>).
    /// </summary>
    /// <remarks>
    /// 비워 두고 서비스워커의 기본값(앱 아이콘)에 맡기지 않는다. 그러면 사진이 있는
    /// 사람과 없는 사람의 알림이 <b>다른 종류의 그림</b>(얼굴 ↔ 회사 로고)으로 갈려
    /// 「누가 시킨 일인가」를 아이콘으로 읽던 눈이 멈춘다.
    /// </remarks>
    public const string FallbackIcon = "/avatar-fallback.png";

    /// <summary>
    /// 사진 주소를 <b>덧입혀 주는</b> 셸 경로. 사진을 못 받으면 이쪽이 그림자로
    /// 갈아 준다 — 서비스워커에는 「그림을 못 받았다」를 알 방법이 없어서,
    /// 여기서 막지 않으면 아이콘 없는 알림이 된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>t</c> 에 그 사진 한 장을 여는 열쇠를 싣는다</b>
    /// (<see cref="IAvatarIconTokenFactory"/>). 이 주소를 부르는 것은 화면이
    /// 아니라 <b>브라우저 자신</b>이고, 알림은 포털에 로그인해 있지 않은
    /// 기기에도 도착한다 — 열쇠가 없으면 그런 기기에서는 신원 없는 요청이 되어
    /// 언제나 그림자로 떨어졌다. 셸은 이 값을 게이트웨이로 그대로 넘긴다
    /// (<c>web/.../Data/FileDownload.cs</c> 의 <c>HandleAvatarAsync</c>).
    /// </para>
    /// <para>
    /// 열쇠를 못 만들면 <c>?t=</c> 없이 보낸다. 그때는 로그인해 있는 기기에서만
    /// 사진이 뜬다 — 열쇠가 생기기 전과 같다.
    /// </para>
    /// </remarks>
    private const string AvatarPathFormat = "/files/avatar/{0}?t={1}";

    /// <summary>열쇠를 싣지 못했을 때의 주소.</summary>
    private const string AvatarPathWithoutTokenFormat = "/files/avatar/{0}";

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string loginId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(loginId))
        {
            return FallbackIcon;
        }

        var id = loginId.Trim();

        try
        {
            // 대표 사진이 여럿일 수 있다(is_primary 가 유일하지 않다). 이메일을 풀 때와
            // 같은 규칙으로 대표를 먼저 고른다.
            var rows = await (
                from a in db.Accounts
                where a.UserId == id && !a.IsDeleted
                join d in db.AccountProfileDetails on a.Id equals d.AccountId
                where d.DetailType == "Avatar" && !d.IsDeleted && d.Content != ""
                select new { d.Content, d.IsPrimary })
                .ToListAsync(ct);

            var avatar = rows
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.Content)
                .FirstOrDefault();

            // 사진을 한 번도 올리지 않은 계정에는 서버가 **바깥 기본 이미지 주소**를
            // 채워 둔 적이 있다(alipayobjects.com — UserService). 그것은 우리 파일이
            // 아니므로 「사진 없음」으로 본다. 그대로 실어 보내면 알림을 띄울 때마다
            // 바깥으로 요청이 나간다.
            var fileId = ExtractFileId(avatar);

            if (fileId is null)
            {
                // **한 줄 남긴다.** 그림자로 뜬 아이콘의 까닭이 둘이다 —
                // 사진이 아예 없거나(여기), 있는데 그 기기가 못 받았거나
                // (셸의 `/files/avatar` 가 302 한다). 밖에서 보면 같은 그림이라
                // 이 줄이 없으면 어느 쪽인지 알 길이 없다.
                logger.LogInformation(
                    "{LoginId} 에게 쓸 프로필 사진이 없습니다(적힌 값: {Avatar}). 그림자로 보냅니다.",
                    id, avatar ?? "(없음)");

                return FallbackIcon;
            }

            return IconUrl(fileId);
        }
        catch (Exception ex)
        {
            // **아이콘 하나 때문에 알림을 멈추지 않는다.** 그림자로 보낸다.
            logger.LogWarning(ex, "{LoginId} 의 프로필 사진을 찾지 못했습니다. 기본 아이콘으로 보냅니다.", id);
            return FallbackIcon;
        }
    }

    /// <summary>사진 한 장의 알림 아이콘 주소. 열쇠가 있으면 함께 싣는다.</summary>
    private string IconUrl(string fileId) =>
        tokens.Create(fileId) is { Length: > 0 } token
            ? string.Format(AvatarPathFormat, fileId, Uri.EscapeDataString(token))
            : string.Format(AvatarPathWithoutTokenFormat, fileId);

    /// <summary>
    /// 계정에 적힌 사진 주소에서 파일 아이디(GUID)를 꺼낸다. 우리 파일이 아니면 <c>null</c>.
    /// </summary>
    internal static string? ExtractFileId(string? avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar))
        {
            return null;
        }

        var match = FileUrl().Match(avatar);
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>
    /// 파일 읽기 주소. 백엔드 주소(<c>/api/file/…</c>)와 셸 중계 경로(<c>/files/…</c>)를
    /// 둘 다 받는다 — 저장된 값이 어느 쪽인지 시절마다 다르다.
    /// </summary>
    [GeneratedRegex(
        @"/(?:api/file/(?:download|thumbnail|medium|large)|files(?:/thumbnail|/avatar)?)/(?:id/)?(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.IgnoreCase)]
    private static partial Regex FileUrl();
}
