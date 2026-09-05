using JSini.Web.Components.Menu;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// DB 메뉴의 옛 경로를 Blazor 라우트로 옮기는 표(<see cref="RouteAliases"/>)를 검사한다.
///
/// 이 표가 틀리면 증상이 조용하다 — 그 메뉴만 안 열린다. 179개 메뉴 중
/// 하나가 그러면 아무도 한동안 모른다. 그래서 표 자체의 성질을 못박는다.
/// </summary>
public sealed class RouteAliasTests
{
    /// <summary>
    /// 옮긴 결과는 <b>반드시 어떤 모듈의 접두사 아래</b>여야 한다.
    ///
    /// 여기서 새면 그 메뉴는 어느 모듈도 소유하지 않는 주소를 가리키게 되고,
    /// 포괄 라우트조차 잡지 못해 빈 404 로 끝난다.
    /// </summary>
    [Theory]
    [InlineData("/room_status")]
    [InlineData("/building/info")]
    [InlineData("/device/background")]
    [InlineData("/system/player-download")]
    [InlineData("/status/deceased-status")]
    [InlineData("/portal/notice")]
    [InlineData("/system/role-map")]
    [InlineData("/system/server-status/jin114")]
    [InlineData("/profile")]
    [InlineData("/ai/chat")]
    [InlineData("/company/site-inquiries")]
    public void 옮긴_결과는_어떤_모듈의_접두사_아래다(string oldPath)
    {
        var resolved = RouteAliases.Resolve(oldPath);

        Assert.True(
            PortalApps.Descriptors.Any(d =>
                resolved.Equals(d.RoutePrefix, StringComparison.OrdinalIgnoreCase)
                || resolved.StartsWith(d.RoutePrefix + "/", StringComparison.OrdinalIgnoreCase)),
            $"'{oldPath}' → '{resolved}' 는 어느 모듈의 접두사에도 속하지 않는다.");
    }

    /// <summary>
    /// 표에 없는 경로는 그대로 지나간다.
    ///
    /// 컷오버 SQL 을 돌려 DB 가 새 경로로 바뀐 뒤에도 이 표를 그대로 둘 수
    /// 있어야 한다(멱등). 그래야 배포와 SQL 의 순서를 신경 쓰지 않아도 된다.
    /// </summary>
    [Theory]
    [InlineData("/helpdesk/dashboard")]
    [InlineData("/projmng/proj/wbs")]
    [InlineData("/life/weather/dashboard")]
    [InlineData("/funeral/room-status")]
    [InlineData("/admin/notice")]
    [InlineData("/site/ai/chat")]
    public void 이미_새_경로면_그대로_둔다(string newPath)
        => Assert.Equal(newPath, RouteAliases.Resolve(newPath));

    /// <summary>
    /// vben 의 매개변수 표기(<c>:id</c>)를 Blazor 표기(<c>{id}</c>)로 바꾼다.
    ///
    /// 링크로 쓰는 값은 아니지만, 안 바꾸면 헬프데스크 상세·수정 두 화면이
    /// 늘 "메뉴에는 있는데 화면이 없다" 로 잘못 보고된다.
    /// </summary>
    [Theory]
    [InlineData("/helpdesk/request/detail/:id", "/helpdesk/request/detail/{id}")]
    [InlineData("/helpdesk/request/edit/:id", "/helpdesk/request/edit/{id}")]
    public void 매개변수_표기를_바꾼다(string dbPath, string expected)
        => Assert.Equal(expected, RouteAliases.Resolve(dbPath));

    /// <summary>빈 값에도 죽지 않는다. 묶음(CATALOG) 메뉴는 경로가 없을 수 있다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 빈_경로는_빈_값이_된다(string? path)
        => Assert.Equal(string.Empty, RouteAliases.Resolve(path));
}
