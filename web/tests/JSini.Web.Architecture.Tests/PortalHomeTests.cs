using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 로그인한 뒤 처음 열리는 화면(<see cref="PortalHome"/>).
///
/// <para>
/// 여기서 틀리면 <b>로그인한 사람 전부가 엉뚱한 자리에 떨어진다.</b> 그리고
/// 그 사람이 스스로 고칠 길이 없다 — 고르는 화면으로 가려면 메뉴를 열어야
/// 하는데, 애초에 그 수고를 덜려고 만든 기능이다. 그래서 「못 찾겠으면
/// 환경설정」·「환경설정도 없으면 그 자리에」 두 갈래를 못으로 박아 둔다.
/// </para>
/// </summary>
public sealed class PortalHomeTests
{
    /// <summary>
    /// 운영 메뉴 트리를 줄인 것. 묶음 하나(설정) 밑에 환경설정이 있고,
    /// 옛 Vue 경로를 그대로 쓰는 메뉴(<c>/room_status</c>)가 하나 있다 —
    /// 그 둘의 <c>Path</c> 와 <c>LinkTarget</c> 이 갈라진다는 것이 요점이다.
    /// </summary>
    private static readonly IReadOnlyList<MenuNode> Menus =
    [
        new()
        {
            Path = "/room_status",
            Href = "/funeral/room-status",
            RouteKey = "funeral.room-status",
            Title = "빈소 현황",
        },
        new()
        {
            Path = "/workspace",
            Title = "Workspace",
        },
        new()
        {
            Path = "/setting",
            Title = "설정",
            IsCatalog = true,
            Children =
            [
                new()
                {
                    Path = "/setting/environment",
                    Href = "/funeral/setting/environment",
                    RouteKey = PortalHome.SettingsRouteKey,
                    Title = "환경설정",
                },
            ],
        },
        new()
        {
            Path = "/외부",
            Link = "https://example.test",
            Title = "바깥 링크",
        },
    ];

    /// <summary>
    /// 계정마다 <c>HomePath</c> 가 하나씩 있는데 그 값이 <b>전원
    /// <c>/workspace</c></b> 다 — 사람이 고른 것이 아니라 계정을 만들 때
    /// 서버가 박아 넣은 값이다. 그것을 「골랐다」로 읽으면 이 기능이
    /// 아무에게도 켜지지 않는다.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("/workspace")]
    [InlineData("/analytics")]
    public void 이_값들은_아직_안_고른_것이다(string? saved) =>
        Assert.True(PortalHome.IsUnset(saved));

    [Fact]
    public void 고른_것이_있으면_그_화면이다() =>
        Assert.Equal("/funeral/room-status", PortalHome.Resolve("/room_status", Menus));

    /// <summary>
    /// 담기는 것은 DB 의 메뉴 경로이고 브라우저가 갈 곳은 링크 주소다.
    /// 둘을 안 갈라 두면 옛 경로를 쓰는 69건이 전부 「준비 중」으로 열린다.
    /// </summary>
    [Fact]
    public void 링크_주소로_적어_두었어도_받아_준다() =>
        Assert.Equal("/funeral/room-status", PortalHome.Resolve("/funeral/room-status", Menus));

    [Fact]
    public void 안_골랐으면_환경설정이다() =>
        Assert.Equal("/funeral/setting/environment", PortalHome.Resolve(null, Menus));

    /// <summary>
    /// 고른 뒤에 권한이 끊겼거나 메뉴가 없어진 경우. 그대로 보내면
    /// 「권한 없음」이 뜨고 그 화면에는 다시 고를 길이 없다.
    /// </summary>
    [Fact]
    public void 고른_메뉴가_사라졌으면_환경설정으로_떨어진다() =>
        Assert.Equal("/funeral/setting/environment", PortalHome.Resolve("/없는/화면", Menus));

    /// <summary>
    /// 환경설정 메뉴의 권한조차 없는 계정. <b>못 여는 화면으로 보내느니</b>
    /// 있던 자리(빈 홈)에 둔다.
    /// </summary>
    [Fact]
    public void 환경설정도_못_보면_아무_데도_안_보낸다()
    {
        IReadOnlyList<MenuNode> onlyRoom = [Menus[0]];

        Assert.Null(PortalHome.Resolve(null, onlyRoom));
        Assert.Null(PortalHome.Resolve("/없는/화면", onlyRoom));
    }

    /// <summary>
    /// 메뉴를 하나도 못 읽었을 때(부트스트랩 실패). 빈 트리로 판정하면
    /// 안 되는 자리가 아니라, <b>보낼 곳이 없다</b>가 맞는 답이다.
    /// </summary>
    [Fact]
    public void 메뉴가_비었으면_아무_데도_안_보낸다() =>
        Assert.Null(PortalHome.Resolve("/room_status", []));

    [Fact]
    public void 고르개_맨_윗줄은_지정_안_함이다() =>
        Assert.Equal(PortalHome.NoneValue, PortalHome.Choices(Menus)[0].Path);

    /// <summary>
    /// 묶음은 제 화면이 없어서 골라 봐야 "준비 중" 이 뜨고, 바깥 링크는
    /// 새 창으로 나가는 주소라 첫 화면이 될 수 없다. <c>/workspace</c> 는
    /// 「안 고름」의 표시라 고를 수 있는 값이 되면 안 된다.
    /// </summary>
    [Fact]
    public void 묶음과_바깥_링크와_빈_홈은_고르개에_없다()
    {
        var paths = PortalHome.Choices(Menus).Select(c => c.Path).ToArray();

        Assert.DoesNotContain("/setting", paths);
        Assert.DoesNotContain("/외부", paths);
        Assert.DoesNotContain("/workspace", paths);
        Assert.Contains("/room_status", paths);
        Assert.Contains("/setting/environment", paths);
    }

    /// <summary>
    /// 이름에 줄기를 붙인다 — 「목록」·「현황」 같은 이름이 업무마다 있어서
    /// 붙이지 않으면 어느 것을 고르는지 알 수 없다.
    /// </summary>
    [Fact]
    public void 이름에_줄기를_붙인다() =>
        Assert.Contains(
            PortalHome.Choices(Menus),
            c => c.Label == "설정 › 환경설정");

    /// <summary>
    /// 고르개가 켤 값과 서버에 담을 값은 다르다 — 「지정 안 함」은 목록에서
    /// 자리를 차지해야 하지만(<see cref="PortalHome.NoneValue"/>) 서버에는
    /// 빈 글자로 가야 줄이 지워진다.
    /// </summary>
    [Fact]
    public void 지정_안_함은_서버로_빈_값이_간다()
    {
        Assert.Null(PortalHome.ToStored(PortalHome.NoneValue));
        Assert.Null(PortalHome.ToStored(null));
        Assert.Equal("/setting/environment", PortalHome.ToStored("/setting/environment"));
    }

    [Theory]
    [InlineData(null, PortalHome.NoneValue)]
    [InlineData("/workspace", PortalHome.NoneValue)]
    [InlineData("/setting/environment", "/setting/environment")]
    public void 담긴_값을_고르개의_줄로_옮긴다(string? saved, string expected) =>
        Assert.Equal(expected, PortalHome.ToChoice(saved));
}
