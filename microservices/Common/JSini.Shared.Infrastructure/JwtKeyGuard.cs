using Microsoft.Extensions.Configuration;

namespace JSini.Shared.Infrastructure;

/// <summary>
/// JWT 서명 키를 설정에서 읽고, 쓸 수 없는 값이면 기동을 막는다. 폴백 키를 두지 않는다 — 조용히 알려진 키로 도는 것보다 뜨지 않는 편이 낫다.
/// 키는 appsettings.Local.json(git 제외)에만 둔다.
/// </summary>
public static class JwtKeyGuard
{
    /// <summary>설정에 자리표시자만 있으면 아직 키를 넣지 않은 것이다.</summary>
    public const string Placeholder = "__SET_IN_appsettings.Local.json__";

/// <summary>커밋 이력에 남은 옛 평문 키. 다시 쓰이면 안 되므로 여기서 거부한다.</summary>
    private static readonly string[] Retired =
    {
        "a-very-secret-key-that-is-long-enough-for-security",
        "quristyle_blabbbbbla_secret_key_1234567890!@#$",
        "DefaultSecretKeyForDevelopmentOnly!"
    };

    /// <summary>HS256 서명에 쓸 수 있는 최소 길이(바이트). 32바이트 미만이면 거부한다.</summary>
    private const int MinLength = 32;

    /// <summary>
    /// 키를 읽어 돌려준다. 없거나 쓸 수 없는 값이면 <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="configuration">설정</param>
    /// <param name="path">설정 경로 (예: <c>Jwt:Key</c>)</param>
    /// <param name="serviceName">오류 메시지에 넣을 서비스 이름</param>
    public static string Require(IConfiguration configuration, string path, string serviceName)
    {
        var value = configuration[path];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw Fail(serviceName, path, "설정에 없습니다");
        }

        if (value == Placeholder)
        {
            throw Fail(serviceName, path, "자리표시자 그대로입니다");
        }

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
