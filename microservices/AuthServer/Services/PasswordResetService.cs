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
            .FirstOrDefaultAsync(a => a.UserId == loginId && !a.IsDeleted, ct);

        if (account is null)
        {
            logger.LogInformation("비밀번호 재설정: 없는 아이디 ({LoginId}, {Ip})", loginId, requestIp);
            return;
        }

        // ── 계정에 살아 있는 이메일들 ────────────────────────
        //
        // **지운 것과 여러 개를 함께 다뤄야 한다.** 프로필 상세(scom)는 값을
        // 고칠 때 옛 줄을 지움 표시만 하고 새 줄을 더하는 자리라, 한 계정에
        // Email 이 여럿 남는 것이 예사다. 예전에는 `FirstOrDefault` 하나로
        // 집어서 **지워진 옛 주소나 두 번째 주소를 골라 놓고** 사용자가 적은
        // 지금 주소와 다르다며 조용히 끝났다 — 화면에는 「보냈습니다」가 뜨므로
        // 아무도 모른다. NotificationServer 의 수신자 해석(EmailEndpoints)은
        // 처음부터 IsDeleted 를 걸러내고 대표(IsPrimary)를 앞세웠는데,
        // 이쪽만 그러지 않고 있었다.
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

        // 적어 낸 주소가 **등록된 것 중 하나와** 맞으면 된다. 대표 주소만
        // 인정하면 회사 메일로 가입해 개인 메일을 덧붙인 사람이 자기 주소를
        // 적고도 막힌다 — 어느 쪽이든 본인만 열 수 있는 메일함이다.
        var target = stored.FirstOrDefault(
            s => string.Equals(s, email.Trim(), StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            // **등록된 주소를 가려서 함께 남긴다.** 이 자리가 「메일이 안 온다」의
            // 가장 흔한 끝이고, 화면은 아이디 노출을 막으려고 여기서도
            // 「보냈습니다」를 띄우므로 물어본 사람에게 답할 수 있는 것은 이
            // 로그 한 줄뿐이다. 가린 꼴이면 관리자가 「등록된 주소는 q***e@gmail.com
            // 입니다」라고 짚어 줄 수 있으면서 로그를 본 사람이 주소를 통째로
            // 집어 가지는 못한다.
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

        var sent = await mail.SendAsync(
            target, AccountEmailTemplates.PasswordResetSubject, body, Sender, ct);

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

    /// <summary>
    /// 메일 주소를 로그에 남길 수 있게 가린다 (<c>quristyle@gmail.com</c> →
    /// <c>q***e@gmail.com</c>).
    /// </summary>
    /// <remarks>
    /// <b>도메인은 남긴다.</b> 「회사 메일인지 개인 메일인지」가 문의에 답할 때
    /// 가장 쓸모 있는 조각이고, 도메인만으로는 사람을 짚을 수 없다.
    /// 아이디 쪽은 양 끝 한 글자만 남긴다 — 두 글자 이하면 통째로 가린다.
    /// </remarks>
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
