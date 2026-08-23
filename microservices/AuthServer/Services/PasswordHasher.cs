using System.Security.Cryptography;

namespace AuthServer.Services;

/// <summary>
/// 계정 비밀번호 해시 도우미
/// </summary>
/// <remarks>
/// PBKDF2(HMAC-SHA256) 를 쓴다. .NET 표준 라이브러리만으로 되고 외부 패키지가 필요 없다.
///
/// 저장 형식은 한 줄 문자열이다.
///   pbkdf2$sha256${반복횟수}${salt(base64)}${hash(base64)}
///
/// **기존 평문 비밀번호와 함께 쓸 수 있게 만들었다.**
/// 저장된 값이 이 형식이 아니면 평문으로 보고 그대로 비교한다.
/// 그래서 도입하는 순간 로그인이 막히는 일이 없고,
/// 사용자가 다음에 로그인할 때 조용히 해시로 바뀐다(<see cref="NeedsUpgrade"/>).
/// </remarks>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2$sha256$";

    /// <summary>
    /// 반복 횟수. OWASP 가 PBKDF2-HMAC-SHA256 에 권고하는 하한(2023 기준 600,000)을 따른다.
    /// 올리면 검증이 느려지는 대신 무차별 대입이 비싸진다.
    /// </summary>
    private const int Iterations = 600_000;

    private const int SaltSize = 16;   // 128비트
    private const int HashSize = 32;   // 256비트

    /// <summary>비밀번호를 해시 문자열로 만든다.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 저장된 값과 입력 비밀번호가 맞는지 확인한다.
    /// 저장된 값이 해시 형식이 아니면 평문으로 보고 비교한다(기존 계정 호환).
    /// </summary>
    public static bool Verify(string? stored, string? password)
    {
        if (string.IsNullOrEmpty(stored) || password is null)
        {
            return false;
        }

        if (!IsHashed(stored))
        {
            // 아직 해시로 바뀌지 않은 계정. 길이가 달라도 같은 시간이 걸리도록 비교한다.
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(stored),
                System.Text.Encoding.UTF8.GetBytes(password));
        }

        // pbkdf2$sha256$반복$salt$hash
        var parts = stored.Split('$');
        if (parts.Length != 5 || !int.TryParse(parts[2], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            // 저장된 값이 깨진 경우. 로그인 실패로 다룬다.
            return false;
        }
    }

    /// <summary>이미 해시 형식인지</summary>
    public static bool IsHashed(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// 다시 해시해서 저장해야 하는지.
    /// 평문이거나 반복 횟수가 지금 기준보다 낮으면 참이다.
    /// 로그인에 성공한 직후에만 부르면 된다 — 그때는 평문 비밀번호를 알고 있다.
    /// </summary>
    public static bool NeedsUpgrade(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        if (!IsHashed(stored)) return true;

        var parts = stored.Split('$');
        return parts.Length != 5
            || !int.TryParse(parts[2], out var iterations)
            || iterations < Iterations;
    }
}
