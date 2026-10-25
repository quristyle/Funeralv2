using System.Security.Cryptography;
using System.Text;

namespace JSini.Shared.Internal;

/// <summary>
/// 서버 대 서버 내부 호출의 머리글 자격을 검사한다.
/// </summary>
public static class InternalCallGuard
{
    /// <summary>
    /// 요청 머리글 값이 설정된 비밀값과 일치하는지 본다.
    /// 설정값이 비어 있으면(막아 둔 것으로 보고) 항상 거부한다.
    /// 길이 누출을 줄이려 <see cref="CryptographicOperations.FixedTimeEquals"/> 로 비교한다.
    /// </summary>
    public static bool IsAllowed(string? header, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return false;

        if (string.IsNullOrEmpty(header))
            return false;

        var headerBytes = Encoding.UTF8.GetBytes(header);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);

        return CryptographicOperations.FixedTimeEquals(headerBytes, configuredBytes);
    }
}
