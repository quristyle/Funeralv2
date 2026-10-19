namespace AuthServer.Services;

/// <summary>
/// 비밀번호 사용 기간 정책.
/// </summary>
/// 90일마다 비밀번호를 바꾸도록 요구한다. 일수는 설정 Auth:PasswordExpiryDays 로 바꾼다. 0 이나 음수는 정책을 끄는 길이다(사고 때 설정 한 줄로 되돌린다).
/// 기준 시각이 null 이면 만료로 보지 않는다 — 모르는 것과 오래된 것은 다르다. 실제 차단은 게이트웨이가 토큰의 PwdChangedAt 으로 매 요청마다 다시 계산한다.
public static class PasswordPolicy
{
    /// <summary>기본 만료 일수.</summary>
    public const int DefaultExpiryDays = 90;

    /// <summary>설정 키.</summary>
    public const string ConfigKey = "Auth:PasswordExpiryDays";

    /// <summary>
    /// 설정에서 만료 일수를 읽는다. 값이 없으면 <see cref="DefaultExpiryDays"/> 를 쓴다.
    /// </summary>
    public static int ExpiryDays(IConfiguration config) =>
        config.GetValue<int?>(ConfigKey) ?? DefaultExpiryDays;

    /// <summary>정책이 켜져 있는지.</summary>
    public static bool IsEnabled(int expiryDays) => expiryDays > 0;

    /// <summary>
    /// 비밀번호가 만료되었는지. 정책이 꺼져 있거나 기준 시각을 모르면 false 다.
    /// </summary>
    public static bool IsExpired(DateTime? passwordChangedAt, int expiryDays, DateTime utcNow)
    {
        if (!IsEnabled(expiryDays)) return false;
        if (passwordChangedAt is null) return false;

        return utcNow >= ExpiresAt(passwordChangedAt.Value, expiryDays);
    }

    /// <summary>만료 시각.</summary>
    public static DateTime ExpiresAt(DateTime passwordChangedAt, int expiryDays) =>
        DateTime.SpecifyKind(passwordChangedAt, DateTimeKind.Utc).AddDays(expiryDays);

    /// <summary>
    /// <b>이미 만료된 것으로 치는</b> 기준 시각. 새로 만든 계정이 첫 로그인에서
    /// 곧바로 비밀번호를 바꾸도록 이 값을 넣는다.
    /// 첫 로그인 변경은 새 칸 없이 기존 90일 정책으로 한다 — 발급 비밀번호는 사람 손을 거치므로 만료된 것으로 두는 편이 사실에 가깝다.
    /// </summary>
    public static DateTime AlreadyExpiredAt(int expiryDays, DateTime utcNow) =>
        utcNow.AddDays(-(IsEnabled(expiryDays) ? expiryDays : DefaultExpiryDays) - 1);

    /// <summary>
    /// 만료까지 남은 일수. 정책이 꺼져 있거나 기준 시각을 모르면 null 이다.
    /// 이미 지났으면 0 을 준다(음수를 내려보내면 화면에서 다루기 번거롭다).
    /// </summary>
    public static int? DaysRemaining(DateTime? passwordChangedAt, int expiryDays, DateTime utcNow)
    {
        if (!IsEnabled(expiryDays)) return null;
        if (passwordChangedAt is null) return null;

        var remaining = (ExpiresAt(passwordChangedAt.Value, expiryDays) - utcNow).TotalDays;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
    }
}
