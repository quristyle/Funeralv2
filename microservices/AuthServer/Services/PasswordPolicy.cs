namespace AuthServer.Services;

/// <summary>
/// 비밀번호 사용 기간 정책.
/// </summary>
/// <remarks>
/// 90일마다 비밀번호를 바꾸도록 요구한다. 일수는 설정으로 바꿀 수 있다.
///
/// <code>
/// "Auth": { "PasswordExpiryDays": 90 }
/// </code>
///
/// <para>
/// <b>0 이나 음수를 넣으면 정책이 꺼진다.</b> 사고가 났을 때 코드를 고치지 않고
/// 설정 한 줄로 되돌릴 수 있어야 하므로 끄는 길을 남겨 둔다.
/// </para>
///
/// <para>
/// 기준 시각(<c>password_changed_at</c>)이 null 이면 <b>만료로 보지 않는다.</b>
/// 기준을 모르는 것과 오래된 것은 다르다. 모른다는 이유로 사용자를 잠그면,
/// 칸을 새로 만든 직후처럼 데이터가 아직 없는 상황에서 전원이 갇힌다.
/// </para>
///
/// <para>
/// 판정은 두 곳에서 한다. 이곳(AuthServer)은 화면에 보여 줄 값을 만들고,
/// 실제 차단은 게이트웨이가 한다(ApiGateway/Program.cs 의 비밀번호 만료 차단).
/// 게이트웨이는 토큰의 <c>PwdChangedAt</c> 클레임으로 매 요청마다 다시 계산하므로,
/// 토큰을 받은 뒤에 만료되는 경우(토큰 수명 7일 &gt; 남은 기간)도 놓치지 않는다.
/// </para>
/// </remarks>
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
