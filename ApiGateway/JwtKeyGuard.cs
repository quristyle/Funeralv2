// 네임스페이스를 두지 않는다. Program.cs 가 최상위 문(top-level statements)이라
// 전역 네임스페이스에서 도는데, 여기에 네임스페이스를 두면 using 을 따로 붙여야 한다.

/// <summary>
/// JWT 서명 키를 설정에서 읽고, 쓸 수 없는 값이면 기동을 막는다 (결정 D1-B).
/// </summary>
/// <remarks>
/// <b>같은 클래스가 <c>JSini.Shared.Infrastructure</c> 에도 있다.</b> 일부러 복사해 두었다 —
/// 게이트웨이는 지금 공용 프로젝트를 하나도 참조하지 않는다(의존성을 일부러 얇게 둔 것으로
/// 보인다). 키 검사 열 줄 때문에 그 구조를 바꾸는 것보다 복사가 낫다고 보았다.
/// <b>한쪽을 고치면 다른 쪽도 고친다.</b>
///
/// <para>
/// 예전에는 이랬다.
/// </para>
///
/// <code>
/// var jwtKey = config["Jwt:Key"] ?? "a-very-secret-key-that-is-long-enough-for-security";
/// </code>
///
/// <para>
/// 키를 저장소에서 빼내도 이 폴백이 남아 있으면 아무 의미가 없다. 설정이 비어 있을 때
/// <b>조용히 잘 알려진 키로 돌기</b> 때문이다. 조용히 취약하게 도는 것보다 뜨지 않는 편이 낫다.
/// </para>
/// </remarks>
public static class JwtKeyGuard
{
    /// <summary>설정에 자리표시자만 있으면 아직 키를 넣지 않은 것이다.</summary>
    public const string Placeholder = "__SET_IN_appsettings.Local.json__";

    /// <summary>
    /// 예전에 저장소에 평문으로 있던 값들. 커밋 이력에 남아 있으므로 다시 쓰이면 안 된다.
    /// </summary>
    private static readonly string[] Retired =
    {
        "a-very-secret-key-that-is-long-enough-for-security",
        "quristyle_blabbbbbla_secret_key_1234567890!@#$",
        "DefaultSecretKeyForDevelopmentOnly!"
    };

    /// <summary>HS256 서명에 쓸 수 있는 최소 길이(바이트).</summary>
    private const int MinLength = 32;

    /// <summary>
    /// 키를 읽어 돌려준다. 없거나 쓸 수 없는 값이면 <see cref="InvalidOperationException"/>.
    /// </summary>
    public static string Require(IConfiguration configuration, string path, string serviceName)
    {
        var value = configuration[path];

        if (string.IsNullOrWhiteSpace(value)) throw Fail(serviceName, path, "설정에 없습니다");
        if (value == Placeholder) throw Fail(serviceName, path, "자리표시자 그대로입니다");

        if (Retired.Contains(value))
        {
            throw Fail(serviceName, path,
                "예전에 저장소에 평문으로 있던 값입니다. 커밋 이력에 남아 있어 더 쓸 수 없습니다");
        }

        if (value.Length < MinLength)
        {
            throw Fail(serviceName, path, $"너무 짧습니다 ({value.Length}자, 최소 {MinLength}자)");
        }

        return value;
    }

    private static InvalidOperationException Fail(string serviceName, string path, string why) =>
        new(
            $"[{serviceName}] JWT 서명 키를 쓸 수 없습니다 — {path} 가 {why}.\n" +
            $"  {serviceName} 의 appsettings.Local.json 에 키를 넣으세요 (이 파일은 git 에 올라가지 않습니다).\n" +
            "  새 키 만들기:  openssl rand -base64 48\n" +
            "  6개 서비스가 **같은 값**을 가져야 합니다. 다르면 토큰이 검증되지 않습니다.\n" +
            "  자세한 것은 docs/analysis/12-decisions-pending.md 의 D1 을 보세요.");
}
