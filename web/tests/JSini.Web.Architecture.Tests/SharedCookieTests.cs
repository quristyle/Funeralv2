using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <b>별개 인스턴스가 같은 인증 쿠키를 풀 수 있는가.</b>
///
/// [이 테스트가 지키는 것]
///
/// 셸이 로그인 쿠키를 굽고, 업무 화면이 그 쿠키에서 게이트웨이 토큰을 꺼내
/// 쓴다(TokenStore). 앱이 각자 프로세스이던 시절에는 그 프로세스들이 같은
/// 쿠키를 풀어야 해서 이것이 필수였는데, 단일 셸이 된 지금도 근거는 그대로
/// 둘이다 — Data Protection 키 링을 폴더에 두는 것과 응용프로그램 이름을
/// 못박아 두는 것.
///
/// 어느 한쪽이 어긋나면 증상은 이렇다: 로그인은 되는데 <b>서버를 다시 띄우면
/// 전원 로그아웃</b>(옛 키로 구운 쿠키를 아무도 못 푼다). 설정만 보면 정상으로
/// 보이고 기동 직후에는 재현되지도 않는다. 그래서 테스트로 못박는다.
///
/// 브라우저·서버를 띄우지 않고 <b>암호화 기제 자체</b>를 확인한다 —
/// 로그인 E2E 는 게이트웨이와 DB 가 있어야 하지만, 깨지는 지점은 여기다.
/// </summary>
public sealed class SharedCookieTests
{
    /// <summary>
    /// JSiniWebApp 이 쓰는 것과 같은 값. 여기가 프로덕션 코드와 어긋나면
    /// 이 테스트는 통과하면서 실제로는 안 되는 상태가 되므로,
    /// <see cref="응용프로그램_이름이_프로덕션과_같다"/> 가 그것을 막는다.
    /// </summary>
    private const string ApplicationName = "JSini.Portal";

    private static IDataProtectionProvider BuildProvider(string keyRing, string applicationName)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRing))
            .SetApplicationName(applicationName);

        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    /// <summary>
    /// 키 링과 응용프로그램 이름이 같으면, 한 인스턴스가 암호화한 것을 다른
    /// 인스턴스가 푼다. 이것이 <b>다시 띄운 셸이 옛 로그인 쿠키를 읽는</b> 근거고,
    /// 앱이 각자 프로세스이던 시절에는 업무 앱이 셸의 쿠키를 읽는 근거였다.
    /// </summary>
    [Fact]
    public void 키링과_이름이_같으면_다른_앱이_쿠키를_푼다()
    {
        var keyRing = Path.Combine(Path.GetTempPath(), "jsini-test-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyRing);

        try
        {
            // 셸이 굽는다.
            var shell = BuildProvider(keyRing, ApplicationName)
                .CreateProtector("Microsoft.AspNetCore.Authentication.Cookies");

            // 다시 띄운 셸이 읽는다 — 키 링 말고는 아무것도 물려받지 않은 별개 인스턴스다.
            var funeral = BuildProvider(keyRing, ApplicationName)
                .CreateProtector("Microsoft.AspNetCore.Authentication.Cookies");

            const string token = "eyJhbGciOiJIUzI1NiJ9.게이트웨이-액세스-토큰";

            Assert.Equal(token, funeral.Unprotect(shell.Protect(token)));
        }
        finally
        {
            Directory.Delete(keyRing, recursive: true);
        }
    }

    /// <summary>
    /// 응용프로그램 이름이 다르면 <b>못 푼다.</b>
    ///
    /// 이 테스트는 위 테스트가 진짜인지 확인한다 — 이름을 달리해도 통과한다면
    /// 위 테스트는 아무것도 지키지 않는 셈이다. 기본값이 어셈블리 이름이라
    /// SetApplicationName 을 빼먹으면 정확히 이 상황이 된다.
    /// </summary>
    [Fact]
    public void 응용프로그램_이름이_다르면_못_푼다()
    {
        var keyRing = Path.Combine(Path.GetTempPath(), "jsini-test-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyRing);

        try
        {
            var shell = BuildProvider(keyRing, ApplicationName)
                .CreateProtector("Microsoft.AspNetCore.Authentication.Cookies");

            // SetApplicationName 을 빼먹었을 때의 모습 — 어셈블리 이름이 들어간다.
            var funeral = BuildProvider(keyRing, "JSini.Web.Funeral")
                .CreateProtector("Microsoft.AspNetCore.Authentication.Cookies");

            var payload = shell.Protect("토큰");

            Assert.ThrowsAny<Exception>(() => funeral.Unprotect(payload));
        }
        finally
        {
            Directory.Delete(keyRing, recursive: true);
        }
    }

    /// <summary>
    /// 이 테스트가 쓰는 이름이 프로덕션 코드와 같은가.
    ///
    /// 위 두 테스트는 자기들끼리만 맞으면 통과한다. 프로덕션의
    /// <c>JSiniWebApp</c> 이 다른 이름을 쓰기 시작하면 테스트는 계속 통과하면서
    /// 실제로는 로그인이 앱마다 풀린다. 리플렉션으로 실제 값을 확인한다.
    /// </summary>
    [Fact]
    public void 응용프로그램_이름이_프로덕션과_같다()
    {
        var field = typeof(JSini.Web.Components.JSiniWebApp)
            .GetField("DataProtectionAppName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(ApplicationName, field!.GetRawConstantValue());
    }
}
