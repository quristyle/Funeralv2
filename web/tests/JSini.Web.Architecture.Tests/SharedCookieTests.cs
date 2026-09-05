using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 앱 일곱 개가 <b>같은 인증 쿠키를 읽을 수 있는가.</b>
///
/// [이 테스트가 지키는 것]
///
/// 셸이 로그인 쿠키를 굽고, 업무 앱들이 그 쿠키에서 게이트웨이 토큰을 꺼내
/// 쓴다(TokenStore). 프로세스가 갈라진 구조에서 이게 성립하는 유일한 근거는
/// Data Protection 키 링과 응용프로그램 이름이 같다는 것뿐이다.
///
/// 어느 한쪽이 어긋나면 증상은 이렇다: 로그인은 되는데 업무 화면을 누르면
/// 다시 로그인 화면. 각 앱의 설정만 보면 둘 다 정상으로 보이고, 일곱 개를
/// 나란히 놓고 비교해야만 보인다. 그래서 테스트로 못박는다.
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
    /// 키 링과 응용프로그램 이름이 같으면, 한 앱이 암호화한 것을 다른 앱이 푼다.
    /// 이것이 셸의 로그인 쿠키를 업무 앱이 읽을 수 있는 근거다.
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

            // 장례식장 앱이 읽는다. 완전히 다른 프로세스라고 가정한 별개 인스턴스다.
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
