using System.Security.Cryptography;
using System.Text;
using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>비밀번호 재설정 링크를 발급하고 받아 준다.</summary>
public interface IPasswordResetService
{
    /// <summary>
    /// 재설정 링크를 메일로 보낸다.
    /// </summary>
    /// <remarks>
    /// <b>돌려주는 값이 없다.</b> 아이디가 있었는지 · 이메일이 맞았는지 ·
    /// 메일이 나갔는지를 부르는 쪽이 알면 그대로 화면에 새어 나간다.
    /// 사연은 구현 주석에 있다.
    /// </remarks>
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

    /// <summary>
    /// 링크 수명(분). 짧을수록 안전하지만 메일이 늦게 도착하는 곳도 있어
    /// 기본을 30분으로 둔다.
    /// </summary>
    private int LifetimeMinutes =>
        configuration.GetValue("Auth:PasswordReset:LifetimeMinutes", 30);

    /// <summary>
    /// 링크에 적을 포털 주소. 없으면 개발 기본값을 쓴다.
    /// <b>운영에서는 반드시 설정한다</b> — 아니면 메일에 개발 주소가 나간다.
    /// </summary>
    private string PortalBaseUrl =>
        (configuration["Portal:BaseUrl"] ?? "http://localhost:5557").TrimEnd('/');

    /// <summary>
    /// 재설정 링크를 만들어 메일로 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [무슨 일이 있어도 조용히 끝난다]
    /// </para>
    ///
    /// <para>
    /// 아이디가 없든 · 이메일이 다르든 · 계정에 이메일이 없든 아무 말도 하지
    /// 않는다. 이유는 <b>이 경로가 아이디를 확인해 주는 도구가 되기 때문</b>이다.
    /// 「그런 아이디가 없습니다」를 돌려주면 아이디 목록을 긁을 수 있고, 그건
    /// 로그인 화면이 「아이디 또는 비밀번호가 잘못되었습니다」로 뭉뚱그려 막아
    /// 둔 것을 옆문으로 여는 셈이다.
    /// </para>
    ///
    /// <para>
    /// 대신 <b>로그에는 이유를 남긴다.</b> 사용자가 「메일이 안 온다」고 하면
    /// 그 로그가 유일한 단서다.
    /// </para>
    ///
    /// <para>
    /// [앞서 보낸 링크는 죽인다]
    /// </para>
    ///
    /// <para>
    /// 살아 있는 링크가 여럿이면 가장 오래된 것도 그대로 먹힌다. 메일함을
    /// 잠깐 본 사람이 옛 링크로 비밀번호를 바꿀 수 있다는 뜻이라, 새로 보낼
    /// 때 앞엣것을 모두 쓴 것으로 표시한다.
    /// </para>
    /// </remarks>
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
            .FirstOrDefaultAsync(a => a.UserId == loginId, ct);

        if (account is null)
        {
            logger.LogInformation("비밀번호 재설정: 없는 아이디 ({LoginId}, {Ip})", loginId, requestIp);
            return;
        }

        var stored = account.ProfileDetails?
            .FirstOrDefault(p => p.DetailType == "Email")?.Content;

        if (string.IsNullOrWhiteSpace(stored))
        {
            logger.LogWarning(
                "비밀번호 재설정: 계정에 이메일이 없다 ({LoginId}). 관리자가 계정 관리에서 넣어 줘야 한다.",
                loginId);
            return;
        }

        if (!string.Equals(stored.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("비밀번호 재설정: 이메일이 계정과 다르다 ({LoginId}, {Ip})", loginId, requestIp);
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

        var body = $"""
            <p>{System.Net.WebUtility.HtmlEncode(who)} 님,</p>
            <p>JSini 포털 비밀번호를 다시 정하시려면 아래 링크를 눌러 주십시오.</p>
            <p><a href="{link}">비밀번호 다시 정하기</a></p>
            <p>이 링크는 {LifetimeMinutes}분 동안만 쓸 수 있고, 한 번 쓰면 사라집니다.</p>
            <p>요청하신 적이 없다면 이 메일을 버리시면 됩니다. 링크를 누르지 않는 한
            비밀번호는 그대로입니다.</p>
            """;

        var sent = await mail.SendAsync(stored.Trim(), "[JSini 포털] 비밀번호 다시 정하기", body, Sender, ct);

        if (sent)
        {
            logger.LogInformation("비밀번호 재설정 링크를 보냈다 ({LoginId}, {Ip})", loginId, requestIp);
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

    /// <summary>주소에 그대로 실을 수 있는 base64.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
