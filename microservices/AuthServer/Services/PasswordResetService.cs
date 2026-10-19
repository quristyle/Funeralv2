using System.Security.Cryptography;
using System.Text;
using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>비밀번호 재설정 링크를 발급하고 받아 준다.</summary>
public interface IPasswordResetService
{
    /// <summary>재설정 링크를 메일로 보낸다. 돌려주는 값이 없다 — 아이디·이메일 유무가 새면 아이디 확인 도구가 된다.</summary>
    Task RequestAsync(string loginId, string email, string? requestIp, CancellationToken ct = default);

    /// <summary>링크를 받아 비밀번호를 바꾼다.</summary>
    Task<PasswordResetResult> ResetAsync(string token, string newPassword, CancellationToken ct = default);
}

/// <summary>재설정 결과. 실패 이유를 나눠 두는 까닭은 화면이 할 말이 다르기 때문이다.</summary>
public enum PasswordResetResult
{
    /// <summary>바꿨다.</summary>
    Success,

    /// <summary>그런 링크가 없다. 주소가 잘렸거나 손으로 고쳐졌다.</summary>
    InvalidToken,

    /// <summary>이미 쓴 링크다. 한 번 쓰면 끝난다.</summary>
    AlreadyUsed,

    /// <summary>시간이 지난 링크다. 다시 요청하면 된다.</summary>
    Expired,

    /// <summary>새 비밀번호가 비어 있다.</summary>
    NewPasswordEmpty,

    /// <summary>지금 쓰는 것과 같은 값이다.</summary>
    SameAsCurrent,
}

/// <inheritdoc cref="IPasswordResetService"/>
public class PasswordResetService(
    AppDbContext db,
    AccountMailClient mail,
    IConfiguration configuration,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    /// <summary>메일에 적어 보내는 보낸 이 이름. 로그에서 이 기능을 가리키는 이름이기도 하다.</summary>
    private const string Sender = "AUTH_PASSWORD_RESET";

    /// <summary>링크 수명(분). 메일이 늦게 도착하는 곳도 있어 기본 30분이다.</summary>
    private int LifetimeMinutes =>
        configuration.GetValue("Auth:PasswordReset:LifetimeMinutes", 30);

    /// <summary>링크에 적을 포털 주소. 운영에서는 반드시 설정한다 — 아니면 메일에 개발 주소가 나간다.</summary>
    private string PortalBaseUrl =>
        (configuration["Portal:BaseUrl"] ?? "http://localhost:5557").TrimEnd('/');

    /// <summary>재설정 링크를 만들어 메일로 보낸다. 아이디·이메일 유무와 무관하게 응답은 같다.</summary>
    /// 이유는 로그에만 남긴다. 새로 보낼 때 앞서 보낸 살아 있는 링크는 모두 죽인다 — 옛 메일로 비밀번호를 바꾸는 길을 막는다.
    public async Task RequestAsync(
        string loginId, string email, string? requestIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(email))
        {
            logger.LogInformation("비밀번호 재설정 요청에 아이디나 이메일이 비어 있다.");
            return;
        }

        var account = await db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == loginId && !a.IsDeleted, ct);

        if (account is null)
        {
            logger.LogInformation("비밀번호 재설정: 없는 아이디 ({LoginId}, {Ip})", loginId, requestIp);
            return;
        }

        // ── 계정에 살아 있는 이메일들 ────────────────────────
        // 계정 이메일은 여럿일 수 있다(지움 표시된 옛 줄 포함). 하나만 집으면 엉뚱한 주소로 가므로 살아 있는 것 전부를 보고 대표를 앞세운다.
        var stored = account.ProfileDetails?
            .Where(p => p.DetailType == "Email" && !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Content))
            .OrderByDescending(p => p.IsPrimary)
            .Select(p => p.Content.Trim())
            .ToList() ?? [];

        if (stored.Count == 0)
        {
            logger.LogWarning(
                "비밀번호 재설정: 계정에 이메일이 없다 ({LoginId}). 관리자가 계정 관리에서 넣어 줘야 한다.",
                loginId);
            return;
        }

        // 적어 낸 주소가 등록된 것 중 하나와 맞으면 된다 — 대표만 인정하면 덧붙인 주소로는 막힌다.
        var target = stored.FirstOrDefault(
            s => string.Equals(s, email.Trim(), StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            // 등록된 주소를 가려서 로그에 남긴다. 화면은 「보냈습니다」만 띄우므로 물어본 사람에게 답할 수 있는 것은 이 로그 한 줄뿐이다.
            logger.LogInformation(
                "비밀번호 재설정: 이메일이 계정과 다르다 ({LoginId}, 등록 {Count}건 {Stored}, {Ip})",
                loginId, stored.Count, string.Join(" · ", stored.Select(Mask)), requestIp);
            return;
        }

        // 앞서 보낸 살아 있는 링크를 모두 죽인다.
        var now = DateTime.UtcNow;
        var live = await db.PasswordResetTokens
            .Where(t => t.AccountId == account.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var old in live)
        {
            old.UsedAt = now;
        }

        // 256비트 난수. base64url 이라 메일 클라이언트가 줄을 접어도 안 깨진다.
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            AccountId = account.Id,
            TokenHash = HashToken(raw),
            ExpiresAt = now.AddMinutes(LifetimeMinutes),
            RequestIp = requestIp,
        });

        await db.SaveChangesAsync(ct);

        var link = $"{PortalBaseUrl}/password/reset?token={Uri.EscapeDataString(raw)}";
        var who = string.IsNullOrWhiteSpace(account.RealName) ? account.UserId : account.RealName;

        // 본문 HTML 은 여기서 조립하지 않는다 — 틀은 AccountEmailTemplates 가 갖는다.
        var body = AccountEmailTemplates.PasswordReset(who, link, LifetimeMinutes);

        // 알림관리가 막지 않는 이벤트(governed=false)지만, 코드를 적어 두어야 그 화면 목록에 나타난다.
        var sent = await mail.SendAsync(
            target, AccountEmailTemplates.PasswordResetSubject, body, Sender,
            NotificationEvents.AccountMail, ct);

        if (sent)
        {
            // 어느 주소로 갔는지까지 남긴다 — 「보냈다」만으로는 다른 주소로
            // 나간 것을 못 가린다(한 계정에 주소가 여럿일 수 있다).
            logger.LogInformation(
                "비밀번호 재설정 링크를 보냈다 ({LoginId} → {To}, {Ip})",
                loginId, Mask(target), requestIp);
        }
        // 실패는 AccountMailClient 가 이미 오류로 남겼다. 화면에는 어차피
        // 같은 문구가 나가므로 여기서 더 할 수 있는 일이 없다.
    }

    /// <inheritdoc />
    public async Task<PasswordResetResult> ResetAsync(
        string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return PasswordResetResult.InvalidToken;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            // 토큰을 쓰기 전에 본다. 빈 값으로 눌렀다고 링크를 태워 버리면
            // 사용자는 메일을 다시 받아야 한다.
            return PasswordResetResult.NewPasswordEmpty;
        }

        var hash = HashToken(token);

        var entry = await db.PasswordResetTokens
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (entry?.Account is null)
        {
            return PasswordResetResult.InvalidToken;
        }

        if (entry.UsedAt is not null)
        {
            return PasswordResetResult.AlreadyUsed;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            return PasswordResetResult.Expired;
        }

        if (PasswordHasher.Verify(entry.Account.Password, newPassword))
        {
            // 링크는 살려 둔다. 다른 값으로 다시 넣으면 된다.
            return PasswordResetResult.SameAsCurrent;
        }

        entry.Account.Password = PasswordHasher.Hash(newPassword);

        // 90일 정책의 기준이 이 값이다. 여기서 안 맞추면 재설정한 사람이
        // 곧바로 「기간이 지났다」로 막힌다.
        entry.Account.PasswordChangedAt = DateTime.UtcNow;
        entry.UsedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("비밀번호를 재설정했다 ({LoginId})", entry.Account.UserId);
        return PasswordResetResult.Success;
    }

    /// <summary>토큰 원문 → 저장용 해시. 왜 PBKDF2 가 아닌지는 엔티티 주석에 있다.</summary>
    private static string HashToken(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>메일 주소를 로그용으로 가린다(quristyle@gmail.com → q***e@gmail.com). 도메인은 남기고 아이디는 양 끝 한 글자만 남긴다.</summary>
    private static string Mask(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "(없음)";
        }

        var at = address.IndexOf('@');

        // 주소 꼴이 아니면 가릴 자리를 정할 수 없다. 길이만 남긴다.
        if (at <= 0)
        {
            return $"(주소 꼴이 아님, {address.Trim().Length}자)";
        }

        var local = address[..at];
        var domain = address[at..];

        return local.Length <= 2
            ? $"{new string('*', local.Length)}{domain}"
            : $"{local[0]}***{local[^1]}{domain}";
    }

    /// <summary>주소에 그대로 실을 수 있는 base64.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
