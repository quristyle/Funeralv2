using System.Text.RegularExpressions;
using JSini.Web.Components.Menu;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 메뉴 아이콘 이름 → CSS 클래스.
///
/// <para>
/// 값의 출처가 <b>사람이 고치는 DB 칸</b>이라(메뉴 관리 화면의 아이콘)
/// 무엇이 들어와도 클래스 자리가 깨지지 않아야 한다. 깨지면 그 줄만 이상한
/// 것이 아니라 <b>사이드바 전체</b>가 이상해진다 — 클래스 하나가 엉뚱하게
/// 켜지는 쪽으로 틀리기 때문이다.
/// </para>
///
/// <para>
/// 이름 목록은 여기서 검사하지 않는다. CSS 에 없는 이름은 동그라미로
/// 떨어지는 것이 정상이고, 그 판정은 CSS 가 한다(<c>MenuIcons</c> 머리말).
/// </para>
/// </summary>
public sealed class MenuIconsTests
{
    /// <summary>클래스 이름에 쓸 수 있는 글자만 남았는가.</summary>
    private static readonly Regex Safe = new("^jsini-mi( jsini-mi--[a-z0-9-]+)?$");

    [Theory]
    [InlineData("lucide:calendar-days", "jsini-mi jsini-mi--lucide-calendar-days")]
    [InlineData("carbon:building", "jsini-mi jsini-mi--carbon-building")]
    [InlineData("material-symbols:language", "jsini-mi jsini-mi--material-symbols-language")]
    [InlineData("  LUCIDE:Bell  ", "jsini-mi jsini-mi--lucide-bell")]
    public void 아이콘_이름이_클래스가_된다(string icon, string expected) =>
        Assert.Equal(expected, MenuIcons.CssClass(icon));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 값이_없으면_기본_클래스만_붙는다(string? icon) =>
        Assert.Equal(MenuIcons.BaseClass, MenuIcons.CssClass(icon));

    /// <summary>
    /// 이름 꼴이 아닌 값은 <b>통째로 버린다.</b> 붙임표와 콜론 말고는 받지
    /// 않으므로, 공백으로 클래스를 하나 더 켜거나 따옴표로 속성을 벗어나는
    /// 값이 클래스 자리에 닿지 못한다.
    /// </summary>
    [Theory]
    [InlineData("lucide:bell jsini-sidebar__tab")]
    [InlineData("lucide:bell\" onload=\"x")]
    [InlineData("lucide:bell;color:red")]
    [InlineData("lucide:종")]
    [InlineData("../../etc/passwd")]
    public void 이름_꼴이_아니면_기본_클래스만_붙는다(string icon) =>
        Assert.Equal(MenuIcons.BaseClass, MenuIcons.CssClass(icon));

    [Theory]
    [InlineData("lucide:bell")]
    [InlineData("lucide:bell jsini-sidebar__tab")]
    [InlineData("")]
    [InlineData("mdi:account-group")]
    public void 어떤_값이_와도_클래스_꼴을_지킨다(string icon) =>
        Assert.Matches(Safe, MenuIcons.CssClass(icon));
}
