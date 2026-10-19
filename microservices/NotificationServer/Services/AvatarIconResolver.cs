using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;

namespace NotificationServer.Services;

/// <summary>앱알림에 띄울 <b>사람 얼굴</b>을 고른다. 프로필 사진이 있으면 그것, 없으면 사람 형상 그림자.</summary>
public interface IAvatarIconResolver
{
    /// <summary>그 사람의 알림 아이콘 주소. 언제나 값을 주며, 사진이 없으면 FallbackIcon 이다.</summary>
    Task<string> ResolveAsync(string loginId, CancellationToken ct = default);
}

/// <inheritdoc cref="IAvatarIconResolver" />
/// 주소는 셸 중계 경로(/files/…)로 준다(아이콘을 받아 가는 것은 포털 오리진의 서비스워커다).
public sealed partial class AvatarIconResolver(
    AppDbContext db,
    IAvatarIconTokenFactory tokens,
    ILogger<AvatarIconResolver> logger)
    : IAvatarIconResolver
{
    /// <summary>사진이 없을 때 쓰는 사람 얼굴 그림자. 앱 기본 아이콘은 쓰지 않는다(사진 있는 사람과 없는 사람의 그림이 갈리면 누가 시킨 일인지 읽기 어렵다).</summary>
    public const string FallbackIcon = "/avatar-fallback.png";

    /// <summary>사진 주소를 덧입혀 주는 셸 경로. 사진을 못 받으면 그림자로 갈아 준다(서비스워커는 그림을 못 받았는지 알 방법이 없다).
    /// 열쇠(t)는 로그인하지 않은 기기에서도 신원을 대신하려고 싣는다. 못 만들면 t 없이 보낸다.</summary>
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
            // 대표 사진이 여럿일 수 있다 — 이메일을 풀 때와 같은 규칙으로 대표를 먼저 고른다.
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

            // 바깥 기본 이미지 주소(UserService 가 채운 것)는 우리 파일이 아니므로 사진 없음으로 본다.
            var fileId = ExtractFileId(avatar);

            if (fileId is null)
            {
                // 그림자로 뜬 까닭은 둘 중 하나다 — 사진이 없거나, 있는데 그 기기가 못 받았거나. 이 로그가 있어야 어느 쪽인지 안다.
                logger.LogInformation(
                    "{LoginId} 에게 쓸 프로필 사진이 없습니다(적힌 값: {Avatar}). 그림자로 보냅니다.",
                    id, avatar ?? "(없음)");

                return FallbackIcon;
            }

            return IconUrl(fileId);
        }
        catch (Exception ex)
        {
            // 아이콘 하나 때문에 알림을 멈추지 않는다 — 그림자로 보낸다.
            logger.LogWarning(ex, "{LoginId} 의 프로필 사진을 찾지 못했습니다. 기본 아이콘으로 보냅니다.", id);
            return FallbackIcon;
        }
    }

    /// <summary>사진 한 장의 알림 아이콘 주소. 열쇠가 있으면 함께 싣는다.</summary>
    private string IconUrl(string fileId) =>
        tokens.Create(fileId) is { Length: > 0 } token
            ? string.Format(AvatarPathFormat, fileId, Uri.EscapeDataString(token))
            : string.Format(AvatarPathWithoutTokenFormat, fileId);

    /// <summary>계정에 적힌 사진 주소에서 파일 아이디(GUID)를 꺼낸다. 우리 파일이 아니면 null.</summary>
    internal static string? ExtractFileId(string? avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar))
        {
            return null;
        }

        var match = FileUrl().Match(avatar);
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>파일 읽기 주소. 백엔드 주소(/api/file/…)와 셸 중계 경로(/files/…)를 둘 다 받는다 — 저장된 값이 시절마다 다르다.</summary>
    [GeneratedRegex(
        @"/(?:api/file/(?:download|thumbnail|medium|large)|files(?:/thumbnail|/avatar)?)/(?:id/)?(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.IgnoreCase)]
    private static partial Regex FileUrl();
}
