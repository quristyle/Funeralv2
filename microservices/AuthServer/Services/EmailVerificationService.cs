using System.Security.Cryptography;
using AuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuthServer.Services;

/// <summary>
/// 이메일 변경 시 본인 확인용 이메일 인증 서비스
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>인증번호 6자리를 생성하고 이메일로 발송한다.</summary>
    Task<(bool Success, string Message, string? DevCode)> SendCodeAsync(string accountId, string email, CancellationToken ct = default);

    /// <summary>사용자가 입력한 인증번호를 검증하고 인증 토큰을 발급한다.</summary>
    Task<(bool Success, string Message, string? VerificationToken)> VerifyCodeAsync(string accountId, string email, string code, CancellationToken ct = default);

    /// <summary>해당 계정 및 이메일의 인증 완료 여부를 확인한다.</summary>
    bool IsVerified(string accountId, string email, string? token = null);

    /// <summary>인증 완료 내역을 캐시에서 정리한다.</summary>
    void ClearVerification(string accountId, string email);
}

/// <inheritdoc cref="IEmailVerificationService"/>
public class EmailVerificationService : IEmailVerificationService
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly AccountMailClient _mail;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IMemoryCache cache,
        AppDbContext db,
        AccountMailClient mail,
        ILogger<EmailVerificationService> logger)
    {
        _cache = cache;
        _db = db;
        _mail = mail;
        _logger = logger;
    }

    /// <summary>번호 하나로 틀릴 수 있는 횟수. 넘으면 번호를 버린다 — 6자리를 5분 안에 다 맞춰 보는 길을 끊는다.</summary>
    public const int MaxWrongAttempts = 5;

    /// <summary>계정 하나가 한 시간에 보낼 수 있는 번호 수. 회사 발신자로 아무 주소에 메일을 쏘는 길을 좁힌다.</summary>
    public const int MaxSendsPerHour = 5;

    /// <summary>다시 보내기까지 기다리는 시간.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>캐시에 두는 번호. 틀린 횟수를 함께 들고 있어야 번호마다 셀 수 있다.</summary>
    private sealed class PendingCode
    {
        public required string Code { get; init; }
        public int WrongAttempts;
    }

    /// <summary>계정의 보내기 기록(한 시간 창).</summary>
    private sealed class SendWindow
    {
        public readonly List<DateTime> SentAt = [];
    }

    private static string SendKey(string accountId) => $"EmailVerifySend:{accountId}";

    private static string CodeKey(string accountId, string email) => $"EmailVerifyCode:{accountId}:{email.Trim().ToLowerInvariant()}";
    private static string VerifiedKey(string accountId, string email) => $"EmailVerified:{accountId}:{email.Trim().ToLowerInvariant()}";

    public async Task<(bool Success, string Message, string? DevCode)> SendCodeAsync(string accountId, string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            return (false, "올바른 이메일 주소를 입력해 주십시오.", null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 다른 사용자가 사용 중인지 중복 검사
        var taken = await _db.AccountProfileDetails.AnyAsync(p =>
            p.DetailType == "Email" &&
            p.AccountId != accountId &&
            p.Content != null &&
            p.Content.ToLower() == normalizedEmail, ct);

        if (taken)
        {
            return (false, $"이미 다른 사용자가 쓰고 있는 이메일입니다: {email}", null);
        }

        // 보내기 제한은 계정 단위로 센다 — 주소를 바꿔 가며 보내도 같은 통이다.
        var now = DateTime.UtcNow;
        var window = _cache.GetOrCreate(SendKey(accountId), e =>
        {
            e.SlidingExpiration = TimeSpan.FromHours(1);
            return new SendWindow();
        })!;

        lock (window)
        {
            window.SentAt.RemoveAll(t => t <= now.AddHours(-1));

            if (window.SentAt.Count > 0 && now - window.SentAt[^1] < ResendCooldown)
            {
                var wait = (int)Math.Ceiling((ResendCooldown - (now - window.SentAt[^1])).TotalSeconds);
                return (false, $"인증번호를 방금 보냈습니다. {wait}초 뒤에 다시 보내 주십시오.", null);
            }

            if (window.SentAt.Count >= MaxSendsPerHour)
            {
                _logger.LogWarning("이메일 인증번호 보내기 제한에 걸렸습니다: account={AccountId}", accountId);
                return (false, "인증번호를 너무 여러 번 요청했습니다. 한 시간 뒤에 다시 시도해 주십시오.", null);
            }

            window.SentAt.Add(now);
        }

        // 6자리 난수 생성
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // 캐시에 5분간 저장. 새로 보내면 앞 번호와 틀린 횟수는 함께 버려진다.
        _cache.Set(CodeKey(accountId, normalizedEmail), new PendingCode { Code = code }, TimeSpan.FromMinutes(5));

        // 메일 발송
        var subject = "[JSini] 이메일 변경 인증번호 안내";
        var body = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 520px; margin: 0 auto; padding: 24px; border: 1px solid #e5e5ea; border-radius: 12px; background-color: #ffffff;">
              <h2 style="color: #1c1c1e; font-size: 20px; font-weight: 600; margin-top: 0; margin-bottom: 12px;">이메일 변경 인증번호</h2>
              <p style="color: #636366; font-size: 14px; line-height: 1.6; margin-bottom: 24px;">
                안녕하세요. 포털 내 정보(/admin/profile)에서 이메일 변경을 요청하셨습니다.<br/>
                아래의 6자리 인증번호를 <strong>5분 이내</strong>에 인증창에 입력해 주십시오.
              </p>
              <div style="background-color: #f2f2f7; border-radius: 8px; padding: 18px; text-align: center; margin-bottom: 24px;">
                <span style="font-size: 32px; font-weight: 700; letter-spacing: 6px; color: #007aff; font-family: monospace;">{code}</span>
              </div>
              <p style="color: #8e8e93; font-size: 12px; line-height: 1.5; margin-bottom: 0;">
                본인이 요청하지 않았거나 계정 정보 변경을 시도하지 않은 경우 이 메일을 무시하셔도 안전합니다.
              </p>
            </div>
            """;

        try
        {
            var (ok, reason) = await _mail.SendToAddressAsync(email.Trim(), subject, body, "AUTH_EMAIL_VERIFY", ct: ct);
            if (!ok)
            {
                _logger.LogWarning("이메일 인증번호 메일 발송 실패 ({Email}): {Reason}. 개발 및 테스트 편의를 위해 발송 코드는 유지됩니다.", email, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "이메일 인증번호 메일 발송 중 예외 발생 ({Email})", email);
        }

        return (true, "인증번호를 발송했습니다. 메일함을 확인해 주십시오.", code);
    }

    public Task<(bool Success, string Message, string? VerificationToken)> VerifyCodeAsync(string accountId, string email, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<(bool, string, string?)>((false, "이메일과 인증번호를 모두 입력해 주십시오.", null));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = CodeKey(accountId, normalizedEmail);

        if (!_cache.TryGetValue<PendingCode>(key, out var pending) || pending is null)
        {
            return Task.FromResult<(bool, string, string?)>((false, "인증번호가 만료되었거나 발송 내역이 없습니다. 다시 발송해 주십시오.", null));
        }

        var matched = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(pending.Code),
            System.Text.Encoding.ASCII.GetBytes(code.Trim()));

        if (!matched)
        {
            var wrong = Interlocked.Increment(ref pending.WrongAttempts);
            if (wrong >= MaxWrongAttempts)
            {
                // 번호를 버린다. 새 번호는 보내기 제한(한 시간 5번)을 다시 지나야 한다.
                _cache.Remove(key);
                _logger.LogWarning("이메일 인증번호를 {Count}번 틀려 폐기했습니다: account={AccountId}", wrong, accountId);
                return Task.FromResult<(bool, string, string?)>((false, $"인증번호를 {MaxWrongAttempts}번 틀려 그 번호를 없앴습니다. 다시 발송해 주십시오.", null));
            }

            return Task.FromResult<(bool, string, string?)>((false, $"인증번호가 일치하지 않습니다. ({MaxWrongAttempts - wrong}번 남음)", null));
        }

        // 일치 시 사용한 인증코드 삭제
        _cache.Remove(key);

        // 30분 유효한 인증 완료 토큰 발급 및 저장
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _cache.Set(VerifiedKey(accountId, normalizedEmail), token, TimeSpan.FromMinutes(30));

        return Task.FromResult<(bool, string, string?)>((true, "이메일 인증이 완료되었습니다.", token));
    }

    public bool IsVerified(string accountId, string email, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = VerifiedKey(accountId, normalizedEmail);

        if (_cache.TryGetValue<string>(key, out var cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            // 인증 토큰이 있어야 한다. 비워 보내도 통과시키면 인증 화면을 거친 다른 창의 결과를 가로챈다.
            if (!string.IsNullOrEmpty(token) && string.Equals(cachedToken, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearVerification(string accountId, string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        _cache.Remove(VerifiedKey(accountId, normalizedEmail));
    }
}
