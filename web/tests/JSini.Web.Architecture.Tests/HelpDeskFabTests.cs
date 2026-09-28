using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 떠다니는 <b>요청 등록</b> 단추(`.jsini-shell__fab--help`)가 조용히
/// 어긋나지 않게 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 기능이 틀리는 세 갈래가 <b>전부 화면이 멀쩡한 채로</b> 일어난다.
/// 빌드도 통과하고 예외도 안 나고 로그도 안 남는다 —
/// </para>
///
/// <list type="bullet">
///   <item>메뉴 단추를 감췄더니 <b>이 단추까지 함께 사라진다</b>(감추기 규칙이
///   `.jsini-shell__fab` 전부를 잡을 때)</item>
///   <item>같은 귀퉁이를 골랐더니 <b>동그라미 둘이 포개진다</b>(미는 규칙이
///   빠지거나, 자리 칸을 다시 써서 `!important` 에 진다)</item>
///   <item><b>권한 없는 사람에게 단추가 보인다</b> — 눌러야 「권한이 없습니다」가
///   뜬다</item>
/// </list>
///
/// <para>
/// 게다가 어긋날 자리가 <b>파일 넷에 흩어져</b> 있다(열쇠는 `PortalBoot`,
/// 표시는 `MainLayout.ShellCss`, 감추기와 밀기는 `app.css`, 설정 줄은
/// 장례식장 환경설정). 한쪽만 고치는 날을 위한 검사다.
/// </para>
/// </summary>
public sealed class HelpDeskFabTests
{
    /// <summary>요청 등록 단추가 여는 화면. 권한을 묻는 열쇠도 이 경로다.</summary>
    private const string NewPath = "/helpdesk/request/new";

    // ── 저장소 열쇠 ─────────────────────────────────────────────

    /// <summary>
    /// 열쇠가 <b>메뉴 단추의 것과 다르다.</b>
    /// </summary>
    /// <remarks>
    /// 같은 글자를 쓰면 한쪽을 감추는 순간 다른 쪽도 감춰지고, 자리를 옮기면
    /// 둘이 함께 옮겨 간다 — <b>고르개 둘이 늘 같은 값을 말하는</b> 것으로
    /// 보여서 「설정이 안 먹는다」가 아니라 「설정이 하나로 붙어 있다」로
    /// 나타난다.
    /// </remarks>
    [Fact]
    public void 열쇠가_메뉴_단추의_것과_다르다()
    {
        Assert.NotEqual(KeyOf("FabPositionKey"), KeyOf("HelpDeskFabPositionKey"));
        Assert.NotEqual(KeyOf("FabHiddenKey"), KeyOf("HelpDeskFabHiddenKey"));
    }

    /// <summary>
    /// 열쇠 둘이 <b>읽어 오는 목록에 들어 있다.</b>
    /// </summary>
    /// <remarks>
    /// <see cref="PortalBoot"/> 의 공용 왕복은 <c>LocalKeys</c> 에 적힌 것만
    /// 읽어 온다. 빠뜨리면 <b>저장은 되는데 되살아나지 않는다</b> — 고른
    /// 자리가 새로고침마다 기본값으로 돌아가고, 감춘 단추가 다시 나온다.
    /// </remarks>
    [Fact]
    public void 열쇠_둘이_읽어_오는_목록에_있다()
    {
        var list = Between(PortalBoot(), "private static readonly string[] LocalKeys", "];");

        Assert.Contains("HelpDeskFabPositionKey", list, StringComparison.Ordinal);
        Assert.Contains("HelpDeskFabHiddenKey", list, StringComparison.Ordinal);
    }

    /// <summary>
    /// 기본 자리가 메뉴 단추와 <b>다른 귀퉁이</b>다.
    /// </summary>
    /// <remarks>
    /// 한 번도 고치지 않은 사람에게 동그라미 둘이 포개져 나오지 않게 한다.
    /// 미는 규칙이 있긴 하지만 그것은 <b>같은 귀퉁이를 일부러 고른 사람</b>을
    /// 위한 것이다.
    /// </remarks>
    [Fact]
    public void 기본_자리가_메뉴_단추와_다르다()
    {
        var menu = Fallback("NormalizeFabPosition");
        var help = Fallback("NormalizeHelpDeskFabPosition");

        Assert.NotEqual(menu, help);
    }

    // ── 감추기 ──────────────────────────────────────────────────

    /// <summary>
    /// 메뉴 단추를 감추는 규칙이 <b>메뉴 단추만</b> 잡는다.
    /// </summary>
    /// <remarks>
    /// <c>.jsini-shell--fab-hidden .jsini-shell__fab</c> 로 두면 그 표시가
    /// <b>요청 등록 단추까지 함께</b> 감춘다. 둘은 따로 켜고 끄는 것이다.
    /// </remarks>
    [Fact]
    public void 메뉴_단추_감추기가_그_단추만_잡는다()
    {
        Assert.Matches(
            new Regex(@"\.jsini-shell--fab-hidden \.jsini-shell__fab--menu\s*\{"),
            AppCss());

        Assert.DoesNotMatch(
            new Regex(@"\.jsini-shell--fab-hidden \.jsini-shell__fab\s*\{"),
            AppCss());
    }

    /// <summary>요청 등록 단추를 감추는 규칙이 따로 있다.</summary>
    [Fact]
    public void 요청_등록_단추를_감추는_규칙이_따로_있다() =>
        Assert.Matches(
            new Regex(@"\.jsini-shell--hdfab-hidden \.jsini-shell__fab--help\s*\{"),
            AppCss());

    /// <summary>
    /// 그 표시를 붙이는 쪽이 <see cref="MainLayout"/> 의 <c>ShellCss</c> 다.
    /// </summary>
    [Fact]
    public void 셸이_그_표시를_붙인다() =>
        Assert.Contains("jsini-shell--hdfab-hidden", MainLayout(), StringComparison.Ordinal);

    // ── 겹치지 않게 밀기 ────────────────────────────────────────

    /// <summary>
    /// 같은 귀퉁이에 설 때 미는 규칙이 <b>네 귀퉁이에 다 있다.</b>
    /// </summary>
    /// <remarks>
    /// 하나만 빠져도 그 귀퉁이를 고른 사람에게만 단추가 포개진다 — 그리고
    /// 포개지면 위엣것만 눌려서 <b>아래엣것은 있는 줄도 모른다.</b>
    /// </remarks>
    [Theory]
    [InlineData("bottom-left")]
    [InlineData("bottom-right")]
    [InlineData("top-left")]
    [InlineData("top-right")]
    public void 네_귀퉁이_모두_미는_규칙이_있다(string corner) =>
        Assert.Contains(
            $".jsini-shell__fab--stacked.jsini-shell__fab--{corner}",
            AppCss(),
            StringComparison.Ordinal);

    /// <summary>
    /// 미는 일을 <b><c>margin</c> 으로</b> 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 자리를 정하는 <c>top</c>·<c>bottom</c> 은 <b>다른 규칙이
    /// <c>!important</c> 로 덮어쓰는 칸</b>이다(아래 띠를 쓰는 사람에게
    /// 단추를 76px 로 올리는 규칙). 거기에 다시 쓰면 둘 중 하나가 조용히
    /// 지고, <b>어느 쪽이 이기는지가 띠를 쓰는지에 따라 갈린다</b> —
    /// 「띠를 껐더니 단추 둘이 포개진다」가 된다.
    /// </para>
    /// <para>
    /// <c>margin</c> 은 그 계산에 더해지므로 어느 조합에서도 한 칸 민다.
    /// </para>
    /// </remarks>
    [Fact]
    public void 미는_일은_margin_으로_한다()
    {
        foreach (var rule in StackedRules())
        {
            Assert.Matches(new Regex(@"margin-(top|bottom):"), rule);
            Assert.DoesNotMatch(new Regex(@"(?<!margin-)(top|bottom):"), rule);
        }
    }

    /// <summary>
    /// 미는 것은 <b>메뉴 단추가 서 있을 때뿐</b>이다.
    /// </summary>
    /// <remarks>
    /// 메뉴 단추를 감춰 둔 사람에게까지 밀면 귀퉁이가 비었는데 단추만 한 칸
    /// 떠 있는 그림이 된다.
    /// </remarks>
    [Fact]
    public void 메뉴_단추가_감춰져_있으면_밀지_않는다()
    {
        var stacked = Member("private bool HelpDeskFabStacked");

        Assert.Contains("!_fabHidden", stacked, StringComparison.Ordinal);
        Assert.Contains("_hdFabPosition == _fabPosition", stacked, StringComparison.Ordinal);
    }

    // ── 권한 ────────────────────────────────────────────────────

    /// <summary>
    /// 단추를 그릴지가 <c>/helpdesk/request/new</c> 권한으로 갈린다.
    /// </summary>
    /// <remarks>
    /// <b>권한표를 받기 전에는 그리지 않는다</b>(<c>IsLoaded</c>). 빠뜨리면
    /// 없던 단추가 떴다가 사라진다.
    /// </remarks>
    [Fact]
    public void 셸이_권한으로_단추를_가린다()
    {
        var allowed = Member("private bool HelpDeskFabAllowed");

        Assert.Contains("Permissions.IsLoaded", allowed, StringComparison.Ordinal);
        Assert.Contains("Permissions.CanView", allowed, StringComparison.Ordinal);
        Assert.Contains(NewPath, MainLayout(), StringComparison.Ordinal);
        Assert.Contains("@if (HelpDeskFabAllowed)", MainLayout(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 설정 화면도 <b>같은 판정</b>을 쓴다.
    /// </summary>
    /// <remarks>
    /// 갈라 두면 「설정은 있는데 단추가 안 나온다」거나 그 반대가 생기고,
    /// 둘 다 사람 눈에는 <b>설정이 안 먹는 것</b>으로 보인다.
    /// </remarks>
    [Fact]
    public void 설정_줄도_같은_권한으로_갈린다()
    {
        var page = EnvironmentPage();

        Assert.Contains("@if (CanSetHelpDeskFab)", page, StringComparison.Ordinal);
        Assert.Contains("Permissions.IsLoaded", page, StringComparison.Ordinal);
        Assert.Contains($"Permissions.CanView(HelpDeskNewPath)", page, StringComparison.Ordinal);
        Assert.Contains(NewPath, page, StringComparison.Ordinal);
    }

    /// <summary>
    /// 감춰 둔 동안은 <b>위치 고르개를 잠근다.</b>
    /// </summary>
    /// <remarks>
    /// 고를 수 있게 두면 아무 일도 일어나지 않는 조작이 하나 생기고, 그
    /// 자리에서 사람은 「위치가 안 먹는다」고 읽는다 — 바로 위 모바일 메뉴
    /// 단추 줄과 같은 규칙이다.
    /// </remarks>
    [Fact]
    public void 감춰_둔_동안은_위치_고르개를_잠근다() =>
        Assert.Contains(
            @"Enabled=""@(!_helpDeskFabHidden)""",
            EnvironmentPage(),
            StringComparison.Ordinal);

    // ── 읽는 자리들 ─────────────────────────────────────────────

    /// <summary><c>NormalizeXxx</c> 가 모르는 값에 돌려주는 기본 자리.</summary>
    private static string Fallback(string method)
    {
        var match = Regex.Match(PortalBoot(),
            method + @"\(string\? position\) =>\s*NormalizeCorner\(position, ""(?<pos>[^""]+)""\)");

        Assert.True(match.Success, $"`PortalBoot` 에서 `{method}` 의 기본 자리를 찾지 못했다.");
        return match.Groups["pos"].Value;
    }

    private static string KeyOf(string name)
    {
        var match = Regex.Match(PortalBoot(), name + @"\s*=\s*""(?<key>[^""]+)""");

        Assert.True(match.Success, $"`PortalBoot` 에 `{name}` 이 없다.");
        return match.Groups["key"].Value;
    }

    /// <summary><c>--stacked</c> 규칙들의 선언 부분.</summary>
    private static IReadOnlyList<string> StackedRules()
    {
        var rules = Regex.Matches(AppCss(), @"\.jsini-shell__fab--stacked[^{]*\{(?<body>[^}]*)\}")
            .Select(m => m.Groups["body"].Value)
            .ToList();

        Assert.True(rules.Count > 0, "app.css 에 `--stacked` 규칙이 하나도 없다.");
        return rules;
    }

    /// <summary>
    /// <see cref="MainLayout"/> 의 멤버 하나. 다음 멤버 선언까지를 몸통으로 본다
    /// (<c>BottomNavMenuToggleTests</c> 와 같은 수법 — 중괄호를 세지 않는다).
    /// </summary>
    private static string Member(string declaration)
    {
        var source = MainLayout();
        var start = source.IndexOf(declaration, StringComparison.Ordinal);

        Assert.True(start >= 0, $"`MainLayout` 에 `{declaration}` 이 없다.");

        var next = Regex.Match(source[start..], @"\n\n");
        return next.Success && next.Index > 0 ? source[start..(start + next.Index)] : source[start..];
    }

    private static string Between(string source, string from, string to)
    {
        var start = source.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"`{from}` 을 찾지 못했다.");

        var end = source.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"`{from}` 뒤에서 `{to}` 를 찾지 못했다.");

        return source[start..end];
    }

    private static string PortalBoot() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "PortalBoot.cs"));

    private static string MainLayout() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

    private static string AppCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

    private static string EnvironmentPage() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "Components", "Pages",
        "EnvironmentSettingPage.razor"));

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
